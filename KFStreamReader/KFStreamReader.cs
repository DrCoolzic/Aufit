/*!
@file KFStream.cs
@brief Structures and Functions to decode KryoFlux Stream files

<div class="jlg">Copyright (C) 2011 Software Preservation Society & Jean 
Louis-Guerin\n\n This file is part of the Atari Universal FD Image Tools 
project.\n\n The Atari Universal FD Image Tools project may be used and 
distributed without restriction provided that this copyright statement is 
not removed from the file and that any derivative work contains the 
original copyright notice and the associated disclaimer.\n\n 

The Atari Universal FD Image Tools project are free software; you can 
redistribute it and/or modify  it under the terms of the GNU General Public 
License as published by the Free Software Foundation; either version 2 of 
the License, or (at your option) any later version.\n\n 
The Atari Universal FD Image Tools project is distributed in the hope that 
it will be useful, but WITHOUT ANY WARRANTY; without even the implied 
warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n See the 
GNU General Public License for more details.\n\n 

You should have received a copy of the GNU General Public License along 
with the Atari Universal FD Image Tools project; if not, write to the Free 
Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  
02110-1301  USA\n</div>

This file contains the definition of the different structures required to 
decode a KryoFlux Stream file.

The main function provided in this library is the readStream() function 
that reads and decodes a specific stream file.
The decoded information can be retrieved from four structures:
- An array of Flux that can be accessed with the getFluxes() and getFluxCount() functions
- An array of Indexes that can be accessed with the getIndexes() and getIndexCount() functions
- A string that can be accessed with getInfoString() function
- A Statistic structure that can be accessed with the getStreamStat() function
.


A Stream file can be decomposed in blocks. Each block has a header that 
defines how to proceed with the block.
- Flux1, Flux2, Flux3 blocks are used to store in a stream file the Sample 
Counter value, corresponding to the number of Sample Clock Cycles (sck) 
between two flux reversals.
- Nop blocks are used to skip data in the buffer, ignoring the affected 
stream area. This makes it possible for the firmware to create data in its 
ring buffer without the need to break up a single code sequence when the 
ring buffer filling wraps.
- Overflow block represent an addition to bit 16 (ie 65536) of the final 
value of the cell being represented. There is no limit on the number of 
overflows present in a stream, thus the counter resolution is virtually 
unlimited, although the decoder is using 32 bits currently.
- OOB block starts an Out of Buffer information sequence, e.g. index 
signal, transfer status, stream information. A repeated OOB code makes it 
possible to detect the end of the read stream while reading from the device 
with a simple check regardless of the current stream alignment.
.

For more information please read the "KryoFlux Stream File Documentation"

@author Jean Louis-Guerin based on code provided by Software Preservation 
Society

*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFStreamNS {

/// <summary>Status codes returned by Stream decoding procedures</summary>
public enum StreamStatus : int {
	/// <summary>Status OK - The file was read and decoded successfully</summary>
	sdsOk = 0,
	/// <summary>Missing Data at end of Stream file</summary>
	sdsMissingData,
	/// <summary>Invalid  header code</summary>
	sdsInvalidCode,
	/// <summary>Stream position must match the encoder position</summary>
	sdsWrongPosition,
	/// <summary>Hardware buffering problem</summary>
	sdsDevBuffer,
	/// <summary>Hardware did not detect FD Index</summary>
	sdsDevIndex,
	/// <summary>Unknown hardware error</summary>
	sdsTransfer,
	/// <summary>Unknown OOB header</summary>
	sdsInvalidOOB,
	/// <summary>Stream missing end</summary>
	sdsMissingEnd,
	/// <summary>No index was found or stream is shorter than the reference position of last index</summary>
	sdsIndexReference,
	/// <summary>An Index is missing</summary>
	sdsMissingIndex,
	/// <summary>Error opening/reading the stream file</summary>
	sdsReadError
}



/// <summary>The Index structure contains the useful Index data extracted from the Stream file</summary>
public struct Index {
	/// <summary>Index in flux array of the flux that contains the index signal </summary>
	public int fluxIndex;
	/// <summary>Time between two indexes. The value is given in number of Sample clock </summary>
	public int indexTime;
	/// <summary>Gives the position of the index inside the flux that contains it. The position is given
	/// as a number of sample clock before the index signal. In most cases this value is equal to the 
	/// <paramref name="sampleCounter"/> value unless there was overflows in the sample counter </summary>
	public int preIndexTime;
	/// <summary>Number of sample clock after the index signal in the flux where index was detected</summary>
	//public int postIndexTime;
}


/// <summary>Statistics data computed after parsing the Stream file</summary>
public struct Statistic {
	/// <summary>Average number of revolutions per minute (RPM)</summary>
	public double avgrpm;
	/// <summary>Maximum value for the number of revolutions per minute</summary>
	public double maxrpm;
	/// <summary>Minimum value for the number of revolutions per minute</summary>
	public double minrpm;
	/// <summary>Average USB transfer rate in bytes per second</summary>
	public double avgbps;
	/// <summary>Average number of flux reversals per revolution</summary>
	public int nbflux;
	/// <summary>Minimum flux value</summary>
	public int minFlux;
	/// <summary>Maximum flux value</summary>
	public int maxFlux;
}

/// <summary>
/// The KFStream Reader Class
/// </summary>
public class KFStream {
	/// <summary>
	/// KryoFlux Information string builder
	/// </summary>
	private StringBuilder info;
 	/// <summary>
 	/// pointer to array of IndexWork structures
 	/// </summary>
	private IndexWork[] idxWrk;
	/// <summary>
	/// pointer to array of Index structures
	/// </summary>
	private Index[] indexArray;
	/// <summary>
	/// count number of index in array
	/// </summary>
	private int indexCount;
	/// <summary>
	/// array - flux transition value in sample clocks
	/// </summary>
	private int[] fluxValues;
	/// <summary>
	/// array - Position in stream buffer of the flux transition
	/// </summary>
	private int[] fluxStrPos;
	/// <summary>
	/// count number of flux transitions in the array
	/// </summary>
	private int fluxCount;
	/// <summary>
	/// Statistic - cbStreamTrans number of data bytes
	/// </summary>
	private int statDataCount;
	/// <summary>
	/// Statistic - cbStreamTrans data transfer time of  in ms
	/// </summary>
	private int statDataTime;
	/// <summary>
	/// Statistic - number of cbStreamTrans
	/// </summary>
	private int statDataTrans;
	/// <summary>
	/// minimum flux value during parse
	/// </summary>
	private int minFlux;
	/// <summary>
	/// maximum flux value
	/// </summary>
	private int maxFlux;
	/// <summary>
	/// Statistic about the Stream
	/// </summary>
	private Statistic stats;
	/// <summary>
	/// Value of the Sample Clock
	/// </summary>
	private double sckValue;
	/// <summary>
	/// Value of the Index Clock
	/// </summary>
	private double ickValue;

	/// <summary>The IndexWork structure contains internal Index values extracted from the Stream file.
	/// Normally user should not be concerned with this structure.</summary>
	private struct IndexWork {
		/// <summary>Position of the next flux inside the original stream buffer when index was detected
		/// (internal use)</summary> 
		public int streamPos;
		/// <summary>Value of the Sample counter when index was detected.
		/// The value is given in number of Sample clock</summary>
		public int sampleCounter;
		/// <summary>Value of the index counter when index was detected. This value can be used to measure
		/// the time between two indexes. The value is given in number of Index clock </summary>
		public int indexCounter;
	}

	/// KryoFlux Hardware status
	private enum HWStatus : byte {
		khsOk = 0,				// no error
		khsBuffer,				// buffering problem; overflow during read, underflow during write
		khsIndex				// no index signal detected during timeout period
	}

	/// Stream block header definition (the type is specified as unsigned 8 bits integer)
	private enum BHeader : byte {
		BHFlux2 = 7,			// Max Value of Header for a Flux2 block (h=0x00-0x07)
		BHNop1,					// Header for a Nop1 block (h=0x08)
		BHNop2,					// Header for a Nop2 block (h=0x09)
		BHNop3,					// Header for a Nop3 block (h=0x0A)
		BHOvl16,				// Header for an overflow16 block (h=0x0B)
		BHFlux3,				// Header for a Flux3 block (h=00xC)
		BHOOB,					// Header for a control block (h=0x0D)
		BHFlux1					// Min Value of Header for a Flux1 block (h=0x0E-0x0FF)
	}

	/// Stream OOB Type definition
	private enum OOBType : byte {
		OOBInvalid = 0,			// Invalid block - Should not happen
		OOBStreamInfo,			// StreamInfo block - Transfer information
		OOBIndex,				// Index Block - Index information
		OOBStreamEnd,			// StreamEnd block - No more data
		OOBInfo,				// Info block - Info from KryoFlux device
		OOBEof = BHeader.BHOOB	// EOF block - end of file
	}


	/// <summary>KFStream Constructor</summary>
	public KFStream() {
		fluxValues = null;
		fluxStrPos = null;
		idxWrk = null;
		info = null;

		// reset other attributes
		statDataCount = 0;
		statDataTime = 0;
		statDataTrans = 0;
		//sckFound = false;
		//ickFound = false;
		sckValue = ((18432000.0 * 73.0) / 14.0) / 4.0;
		ickValue = sckValue / 8.0;
	}

	/// <summary>Get the number of recorded transitions in the Flux array.</summary>
	/// <returns>The number of transitions</returns>
	///
	/// All flux transitions are stored in an array. 
	/// The number of transitions stored in the array can be 
	/// retrieved with this function. To access the flux array
	/// you must use the getFluxes() function.
	public int getFluxCount() {
		return fluxCount;
	}

	/// <summary>Return the flux transitions array </summary>
	/// <returns>The Flux values array</returns>
	///
	/// All flux transitions are stored in an array of integer. Each flux
	/// transition is given relative to the previous transition. The value 
	/// is given in number of sample clocks. The number of transitions
	/// can be retrieved with getFluxCount().
	public int[] getFluxes() {
		return fluxValues;
	}

	/// <summary>Get the number of the Index recorded.</summary>
	/// <returns>The number od index entriesy</returns>
	///
	/// The Floppy disk Indexes information are stored in an array of 
	/// Index structures. The number of entries in the array can be 
	/// retrieved with this function. To access the Index array
	/// you must use the getIndexes() function.
	public int getIndexCount() {
		return indexCount;
	}

	/// <summary>Return the Index array</summary> 
	/// <returns>The Index array</returns>
	///
	/// The Floppy disk Indexes information are stored in an array of 
	/// Index structures. For an interpretation of the data please refer 
	/// to the Index structure. The size of the array 
	/// can be retrieved with getIndexCount()
	public Index[] getIndexes() {
		return indexArray;
	}

	/// <summary>Return the KryoFlux HW Info String</summary>
	/// <returns>The info String if The KF firmware is equal or
	/// above 2.x or null if no HW information transmitted</returns>
	///
	/// Some HW Information is transmitted from the KryoFlux device.
	/// This information is returned as a string. Each information
	/// is stored as a "name=value" pair and is separated from the 
	/// previous one with a coma "," character. 
	public string getInfoString() {
		if (info.Length == 0) return null;
		return info.ToString();
	}

	/// <summary>Return the Statistic structure</summary>
	/// <returns>The Statistic structure</returns>
	///
	/// This structure is filled during decode by calling the internal 
	/// function fillStreamStat()
	/// @see Statistic
	public Statistic getStreamStat() {
		return stats;
	}

	/// <summary> Get the Sample Clock value</summary>
	/// <returns>the value of the Sample Clock</returns>
	///
	/// This function is used to get the Sample Clock value. \n
	/// By default this value is sckValue = ((18432000.0 * 73.0) / 14.0) / 4.0;\n
	/// but with firmware 2.0 and above the sample clock values is transmitted
	/// by the KryoFlux iHW. In that case the value transmitted by the HW is 
	/// returned by this call instead of the default value.
	public double sampleClk() {
		return sckValue;
	}

	/// <summary>Gets the Index Clock value</summary>
	/// <returns>the value of the Index Clock</returns>
	///
	/// This function is used to get the Index Clock value. \n
	/// By default this value is ickValue = sckValue / 8.0;\n
	/// but with firmware 2.0 and above the index clock values is transmitted
	/// by the KryoFlux iHW. In that case the value transmitted by the HW is 
	/// returned by this call instead of the default value.
	public double indexClk() {
		return ickValue;
	}

	/// <summary>Gets the number of complete revolution recorded in the stream file</summary>
	/// <returns>the number of revolutions</returns>
	public int getRevNumber() {
		return indexCount - 1;
	}

	/// <summary>Searches for a specific name in KryoFlux HW information string</summary>
	/// <param name="name">name to search</param>
	/// <returns>A string that contains the corresponding value. If "name" is not found, 
	/// it returns null</returns>
	///
	/// Search "name" in the name=value pairs of the KryoFlux information array.
	/// if name is found returns the associated value.
	/// For example findName("sck") returns the sample clock value in a string
	public string findName(string name) {
		string str = getInfoString();
		if (str == null)
			return null;

		int position = str.IndexOf(name);
		if (position == 0)
			return null;
		int start = position + name.Length + 1;	// skip the search string and the '='
		int stop = str.IndexOf(',', start); // until ','
		if (stop == -1)	// last string does not end with ','
			stop = str.Length;
		string value = str.Substring(start, stop - start);
		return value;
	}


	/// <summary>
	/// Reads and parses the KF Stream file specified
	/// </summary>	
	/// <param name="fileName">Name of the stream file to read and decode</param>
	/// <returns>A status code of type <paramref name="StreamStatus"/> to indicate if the 
	/// file was read and parsed correctly.<see cref="StreamStatus"/></returns>
	/// <remarks>
	/// This is the main function of the library. It parses the stream file passed as an
	/// argument and fills in 4 structures:
	/// - An array of flux value that can be retrieved with getFluxes() and getFluxCount()
	/// - An Index array that can be retrieved with getIndexes() and getIndexCount()
	/// - An Info string that can be retrieved with getInfoString()
	/// - Statistic structure that can be retrieved with getStreamStat()
	/// .
	///
	/// Internally this function calls the private functions parseStream(), 
	/// indexAnalysis(), and fillStreamStat(). The function update the sck 
	/// and ick default clock values if these values were passed in an Info block from KF 
	/// device (FW 2.0+). In that case the sckFound and ickFound indicators are set 
	/// to true. In all cases the sck and ick can be retrieved with sampleClk() and 
	/// indexClk() functions.
	/// </remarks>
	public StreamStatus readStream(string fileName) {
		FileStream fs;
		try {
			fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read); 
		} 
		catch (Exception exc) { 
			Console.WriteLine("Error: {0}", exc.Message);
			return StreamStatus.sdsReadError; 
		}

		// read file into buffer
		byte[] sbuf = new byte[fs.Length];
		int bytes = fs.Read(sbuf, 0, (int)fs.Length);
		Debug.Assert(bytes == fs.Length);	// should be same
		fs.Close();

		// we first parse the file
		StreamStatus status = parseStream(sbuf);
		if (status != StreamStatus.sdsOk)
			return status;

		// now we process the index information
		status = indexAnalysis();
		if (status != StreamStatus.sdsOk)
			return status;

		// we perform some computation
		fillStreamStat();

		// checking for sck & ick
		string val;
		val = findName("sck");
		if (val != null) {
			sckValue = Convert.ToDouble(val, new CultureInfo("en-US"));
			//sckFound = true;
		}
		val = findName("ick");
		if (val != null) {
			ickValue = Convert.ToDouble(val, new CultureInfo("en-US"));
			//ickFound = true;
		}

		return status;
	}


	/// <summary>
	/// The main KryoFlux Stream file parser.
	/// </summary>
	/// <param name="sbuf">The buffer that contains the Stream file read</param>
	/// <returns>A status code of type <paramref name="StreamStatus"/> to indicate if the 
	/// file was read and parsed correctly.<see cref="StreamStatus"/></returns>
	///
	/// This is an internal function called by readStream(). It parses the stream file
	/// and fills the main structures.
	///
	/// Parsing is driven by the Block Header that defines the nature and length of the phases. 
	/// All the phases are decoded in a loop that will scan the complete Stream File until an EOF block 
	/// is found. Each Block is processed in three steps:
	/// - We first compute the length of the Block based on the header type (this information is used 
	/// 	to move the pointer to the next block):
	/// 	- For Flux1, Nop1, and Ovl16 phases the length is one
	/// 	- For Flux2, Nop2 the length is two
	/// 	- For Flux3, Nop3 the length is three
	/// 	- For OOB Block the length is equal to the length of the OOB Header Block (4 bytes) plus 
	/// 	the length of the OOB Data Block given in the Size field (see OOB phases). The only exception
	/// 	is the EOF block where the size is not meaningful.
	/// 	.
	/// - In the second step we compute the actual value of the flux transition when the current block 
	/// is of type Flux1, or Flux2, or Flux3, or Ovl16.
	/// - The final step is to process the block:
	/// 	- If the data block is of type Flux1, Flux2, or Flux3 we create a new entry in the Flux array 
	/// 	and we store the Flux Value and Stream Position.
	/// 	- If the block is a StreamInfo block we use the Stream Position information to check that no 
	/// 	bytes were lost during transmission. We can also use the Transfer Time for statistical analysis 
	/// 	of the transfer speed.
	/// 	- If the block is an Index block. We create a new entry in the Index array and we store the 
	/// 	Stream Position, the Timer and Index Clock values.
	/// 	- If the block is an Info block we copy the information into the Info String.
	/// 	- If the block is a StreamEnd block we use the Stream Position information to check that no 
	/// 	bytes were lost during transmission and we look at the Result Code to check if errors where 
	/// 	found during processing.
	/// 	- If the block is an EOF block we stop the parsing of the file.
	/// 	.
	/// .
	/// When parsing of the stream file is finished we have all the data information in the three arrays 
	/// (Flux, Index, and Info) but we still need to analyze the Index information.
	private StreamStatus parseStream(byte[] sbuf) {
		int lastIndexPos = 0;				// stream position associated with the latest index signal
		int streamPos = 0;					// current stream position
		int fluxValue = 0;					// current flux value
		int lastStreamPos = 0;				// start position of the last data block
		HWStatus hwStatus = HWStatus.khsOk; // hardware status - preset to no decoding errors
		bool eoff = false;					// end of file flag
		int pos = 0;						// current position in stream buffer
		
		
		if (sbuf.Length == 0)	// nothing to do
			return StreamStatus.sdsOk;

		minFlux = int.MaxValue;
		maxFlux = 0;

		// at worst the number of flux equals number of stream bytes+1 
		// In fact it is always less due to CB and encoding
		fluxValues = new int[sbuf.Length];
		fluxStrPos = new int[sbuf.Length];
		fluxCount = 0;

		idxWrk = new IndexWork[32];
		indexArray = new Index[32];
		indexCount = 0;

		info = new System.Text.StringBuilder();

		// process the entire buffer
		while (!eoff && (pos < sbuf.Length)) {
			BHeader bhead = (BHeader)sbuf[pos];		// block header
			int blen = 0;							// block length
			bool isFlux = false;					// set to true when data block is a flux

			// calculate the size of the current bloc
			switch (bhead) {
				case BHeader.BHNop1:
				case BHeader.BHOvl16:
					blen = 1;
					break;
				case BHeader.BHNop2:
					blen = 2;
					break;
				case BHeader.BHNop3:
				case BHeader.BHFlux3:
					blen = 3;
					break;
				case BHeader.BHOOB:
					blen = 4;	// header + type + size;
					if ((OOBType)sbuf[pos+1] != OOBType.OOBEof)
						blen += sbuf[pos+2] + (sbuf[pos+3] << 8);	// we add the size
					break;
				default:
					if (bhead >= BHeader.BHFlux1)
						blen = 1;
					else if (bhead <= BHeader.BHFlux2)
						blen = 2;
					else
						return StreamStatus.sdsInvalidCode;			// not possible!
					break;
			}

			// error if data is incomplete
			if ((sbuf.Length - pos) < blen)
				return StreamStatus.sdsMissingData;


			// compute flux value for a flux block
			switch (bhead) {
				case BHeader.BHOvl16:
					fluxValue += 0x10000;
					break;
				case BHeader.BHFlux3:
					fluxValue += (sbuf[pos + 1] << 8) + sbuf[pos + 2];
					isFlux = true;
					break;
				default:
					if (bhead >= BHeader.BHFlux1) {
						fluxValue += (int)bhead;
						isFlux = true;
					}
					else if (bhead <= BHeader.BHFlux2) {
						fluxValue += ((int)bhead << 8) + sbuf[pos + 1];
						isFlux = true;
					}
					break;
			}

			// now complete the process of the block based on block header
			if (bhead != BHeader.BHOOB) {
				// we have detected a new flux block
				if (isFlux) {
					fluxValues[fluxCount] = fluxValue; // store value
					minFlux = Math.Min(minFlux, fluxValue);
					maxFlux = Math.Max(maxFlux, fluxValue);

					fluxStrPos[fluxCount] = streamPos; // store position
					fluxCount++; // one more flux
					fluxValue = 0; // reset current fluxValue
				}
				// calculate next stream position
				streamPos += blen;
			} // not an OOB

			else {
				// OOB => process control bloc
				OOBType oobType = (OOBType)(sbuf[pos + 1]);
				switch (oobType) {
					case OOBType.OOBStreamInfo:
						// decoded stream position must match the encoder position
						int position = sbuf[pos + 4] + (sbuf[pos + 5] << 8) + (sbuf[pos + 6] << 16) + (sbuf[pos + 7] << 24);
						if (streamPos != position)
							return StreamStatus.sdsWrongPosition;

						// number of stream bytes since last OOBStreamInfo
						int sb = streamPos - lastStreamPos;
						lastStreamPos = streamPos;

						// update data count and time for non consecutive OOBStreamInfo
						if (sb != 0) {
							statDataCount += sb;
							statDataTime += sbuf[pos + 8] + (sbuf[pos + 9] << 8) + (sbuf[pos + 10] << 16) + (sbuf[pos + 11] << 24);
							statDataTrans++;
						}
						break;

					case OOBType.OOBIndex:
						// initialize index info
						idxWrk[indexCount].streamPos = sbuf[pos + 4] + (sbuf[pos + 5] << 8) + (sbuf[pos + 6] << 16) + (sbuf[pos + 7] << 24);
						idxWrk[indexCount].sampleCounter = sbuf[pos + 8] + (sbuf[pos + 9] << 8) + (sbuf[pos + 10] << 16) + (sbuf[pos + 11] << 24);
						idxWrk[indexCount].indexCounter = sbuf[pos + 12] + (sbuf[pos + 13] << 8) + (sbuf[pos + 14] << 16) + (sbuf[pos + 15] << 24);
						indexArray[indexCount].fluxIndex = 0;
						indexArray[indexCount].indexTime = 0;
						indexArray[indexCount].preIndexTime = 0;
						//indexArray[indexCount].postIndexTime = 0;

						// next index position
						indexCount++;

						// store the position of the index in the stream for checking
						lastIndexPos = idxWrk[indexCount].streamPos;
						break;

					case OOBType.OOBStreamEnd:
						// check for errors detected by the Kryoflux hardware
						hwStatus = (HWStatus)(sbuf[pos + 8] + (sbuf[pos + 9] << 8) + (sbuf[pos + 10] << 16) + (sbuf[pos + 11] << 24));

						// if no error found, decoded stream position must match the encoder position
						if ((hwStatus == HWStatus.khsOk) && 
							(streamPos != sbuf[pos + 4] + (sbuf[pos + 5] << 8) + (sbuf[pos + 6] << 16) + (sbuf[pos + 7] << 24)))
							return StreamStatus.sdsWrongPosition;
						break;

					case OOBType.OOBInfo:
						int size = sbuf[pos + 2] + (sbuf[pos + 3] << 8);
						
						if (size != 0) {
							if (info.Length > 0) info.Append(", ");

							for (int i = 0; i < size-1; i++)
								info.Append((char)sbuf[pos + 4 + i]);
						}
						break;

					case OOBType.OOBEof:
						eoff = true;
						break;

					// any other oob type is invalid
					default:
						return StreamStatus.sdsInvalidOOB;
				}
			}

			// jump to next block
			pos += blen;
		}

		// additional empty flux at the end
		fluxValues[fluxCount] = fluxValue;
		fluxStrPos[fluxCount] = streamPos;

		// check Kryoflux hardware error
		switch (hwStatus) {
			case HWStatus.khsOk:
				break;
			case HWStatus.khsBuffer:
				return StreamStatus.sdsDevBuffer;
			case HWStatus.khsIndex:
				return StreamStatus.sdsDevIndex;
			default:
				return StreamStatus.sdsTransfer;
		}

		// check oob end
		if (!eoff)
			return StreamStatus.sdsMissingEnd;

		// error if no index was found or stream is shorter than the reference position of the last index
		if (lastIndexPos != 0 && (streamPos < lastIndexPos))
			return StreamStatus.sdsIndexReference;

		return StreamStatus.sdsOk;
	}



	/// <summary>
	/// analyze and fill information in the Index structure
	/// </summary>
	/// <returns>A status code of type <paramref name="StreamStatus"/> to indicate if the 
	/// file was read and parsed correctly.<see cref="StreamStatus"/></returns>
	///
	/// This is an internal function called by decode(). It finalize the information 
	/// stored in the Index structure.
	private StreamStatus indexAnalysis() {
		// stop if either no index or flux data is available
		if (indexCount == 0 || fluxCount == 0)
			return StreamStatus.sdsOk; // nothing to do!

		int iidx = 0;	// Index index
		int fidx;		// Flux index
		int itime = 0;	// Index time

		int nextstrpos = idxWrk[iidx].streamPos; // next flux stream position for index

		// associate flux transition array offsets with stream offsets
		for (fidx = 0; fidx < fluxCount; fidx++) {
			// sum all flux between index signals
			itime += fluxValues[fidx];

			int nfidx = fidx + 1; // next flux index

			// sum until reach the index next flux position
			if (fluxStrPos[nfidx] < nextstrpos)
				continue;

			// edge case; the very first flux has an index signal
			if ((fidx == 0) && (fluxStrPos[0] >= nextstrpos))
				nfidx = 0;

			if (iidx < indexCount) {
				// set the buffer offset of the flux containing the index signal
				indexArray[iidx].fluxIndex = nfidx;

				// the complete flux time of the flux that includes the index signal
				int iftime = fluxValues[nfidx];

				// timer was sampled at the signal edge, just before the interrupt - the real time is the flux length
				if (idxWrk[iidx].sampleCounter == 0)
					idxWrk[iidx].sampleCounter = iftime & 0xffff;

				// if the last flux is unwritten
				if (nfidx >= fluxCount) {
					// and the next position could have been a new code
					if (fluxStrPos[nfidx] == nextstrpos) {
						// flux time is a sum of all overflows plus the sub-flux size to get the flux size until the index signal
						iftime += idxWrk[iidx].sampleCounter;
						fluxValues[nfidx] = iftime;
					}
				}

				// the total number of overflows in the flux containing the index signal
				// due to the way a flux time is calculated, the upper 16 bits represent the number of overflow codes
				int icoverflowcnt = iftime >> 16;

				// the number of overflows to step back before the index cell was created to reach the real signal point
				// this is always positive due to the condition of entering this code
				int preoverflowcnt = fluxStrPos[nfidx] - nextstrpos;

				// check if more steps need to be taken back from the index cell to signal point than the number of overflows
				// if the condition is true, there is an error in the stream or index encoding
				if (icoverflowcnt < preoverflowcnt)
					return StreamStatus.sdsMissingIndex;

				// the number of overflows before the index signal in flux time
				int preIndexTime = (icoverflowcnt - preoverflowcnt) << 16;

				// add the time counted (sub-cell); this happened before writing the next overflow or final cell code
				preIndexTime += idxWrk[iidx].sampleCounter;

				// set sub-cell time before and after the index signal
				indexArray[iidx].preIndexTime = preIndexTime;
				//indexArray[iidx].postIndexTime = iftime - preIndexTime;

				// itime contains the complete cell time for the previous index signal; it must only contain the postIndexTime
				// act.sum-prev.iftime+prev.postIndexTime, where prev.postIndexTime=prev.iftime-prev.preIndexTime
				// -> act.sum-prev.iftime+prev.iftime-prev.preIndexTime -> act.sum-prev.preIndexTime
				if (iidx != 0)
					itime -= indexArray[iidx - 1].preIndexTime;

				// the revolution time consists of the sum of cell times plus the sub-cell time before the index signal
				// if the first cell has the index signal, the revolution time is the sub-cell time before the index signal
				indexArray[iidx].indexTime = (nfidx != 0 ? itime : 0) + preIndexTime;

				// try next index
				iidx++;

				// next stream position for index
				nextstrpos = (iidx < indexCount) ? idxWrk[iidx].streamPos : 0;

				// restart index timer unless the very first cell has the index signal
				// required as next calculation would expect the index time to be part of the sum and that wouldn't happen
				if (nfidx != 0)
					itime = 0;
			} // valid index
		} // for all flux count

		// error if not all indexes have been found
		if (iidx < indexCount)
			return StreamStatus.sdsMissingIndex;

		// use the additional cell if last index happened, but the base cell was incomplete/unwritten
		if (idxWrk[iidx - 1].streamPos >= fluxCount)
			fluxCount++;

		return StreamStatus.sdsOk;
	}

	/// <summary>
	/// Fill the Statistic structure based on info in stream file
	/// </summary>
	///
	/// This function uses the decoded Stream file info to fill the statistical stream structure
	/// After calling this function it is possible to access the different information in the
	/// Statistic structure.
	private void fillStreamStat() {
		int sum;
		int vmin, vmax;
		stats = new Statistic();

		if (statDataTime != 0)
			stats.avgbps = ((double)statDataCount * 1000.0) / statDataTime;
		else
			stats.avgbps = 0;

		sum = 0;
		vmax = 0;
		vmin = int.MaxValue;

		// skip first
		for (int i = 1; i < indexCount; i++) {
			int x = indexArray[i].indexTime;
			vmin = Math.Min(vmin, x);
			vmax = Math.Max(vmax, x);
			sum += x;

			// checking that the computed indexTime is roughly equal to difference in index counter
			int delta = Math.Abs(indexArray[i].indexTime - (idxWrk[i].indexCounter - idxWrk[i - 1].indexCounter) * 8);
			Debug.Assert(delta < 8);
		}
		if ((indexCount - 1) > 0) {
			stats.avgrpm = (sckValue * ((double)(indexCount - 1) * 60.0)) / (double)sum;
			stats.maxrpm = (sckValue * 60.0) / (double)vmin;
			stats.minrpm = (sckValue * 60.0) / (double)vmax;
		}
		else
			stats.avgrpm = stats.maxrpm = stats.minrpm = 0;

		sum = 0;
		if (indexCount > 2) {
			for (int i = 1; i < indexCount; i++)
				sum += indexArray[2].fluxIndex - indexArray[1].fluxIndex;
			stats.nbflux = sum / (indexCount - 1);
		}
		else
			stats.nbflux = 0;
		stats.minFlux = minFlux;
		stats.maxFlux = maxFlux;
	}


}	// KFStream Class

}	// namespace

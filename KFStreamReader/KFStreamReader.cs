﻿/*!
@file KFStreamReader.cs
<summary>KryoFlux Stream File Reader functions and Structures</summary>
<remarks>
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

@section sr_pres Presentation
This file contains the definition of the different structures and functions
required to decode a KryoFlux Stream file.

The main function provided by this library is the readStream() function 
that reads and decodes a specific stream file.\n
Once decoded the information can be retrieved from four structures:
- An array of FluxValues that can be accessed with the FluxValues and FluxCount properties
- An array of Indexes that can be accessed with the Indexes and IndexCount properties
- A string that can be accessed with InfoString property
- A Statistic structure that can be accessed with the StreamStat property
.

Refer to the different structures for more information.

@section sr_inner_working Short Presentation on the reader inner working
 
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
http://info-coach.fr/atari/software/projects/_kryoflux/kryoflux_stream_protocol.pdf

@author Jean Louis-Guerin based on code provided by the Software Preservation 
Society
</remarks>
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFStreamReaderNS {

/// <summary>Define the Status codes returned by Stream decoding procedures</summary>
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
	/// <summary>Stream missing end of file</summary>
	sdsMissingEnd,
	/// <summary>No index was found or stream is shorter than the reference position of last index</summary>
	sdsIndexReference,
	/// <summary>An Index is missing</summary>
	sdsMissingIndex,
	/// <summary>Error opening/reading the stream file</summary>
	sdsReadError
}


/// <summary>The Index structure contains the useful Index information extracted from the Stream file</summary>
public struct Index {
	/// <summary>Position (index) in the flux array of the flux that contains the index signal</summary>
	public int fluxPosition;
	/// <summary>Gives the exact rotation time (i.e. the time between two indexes) in 
	/// number of Sample clocks. In other word the time between two index pulses.</summary>
	public int rotationTime;
	/// <summary>Gives the position of the index pulse inside the flux that contains it. The position 
	/// is given as a number of sample clocks before the index signal.</summary>
	public int preIndexTime;
}


/// <summary>Statistical information computed after parsing the Stream file</summary>
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

/// <summary>The KFStream Reader Class</summary>
/// <remarks>This class contains the declaration of the structures, the properties, and the
/// functions that you need to know to access the information read from a KryoFlux Stream File.\n
/// <b>Important Note:</b> The KFStream class is fully reentrant and thread safe. This means
/// that you can create and call as many KFStream classes as you want from multi-threaded
/// applications safely. :)
/// </remarks>
public class KFStream {
	#region Attributes and Internal structures

	/// <summary>KryoFlux Information string builder </summary>
	private StringBuilder _info;
 	/// <summary>Array of IndexInternal structure (internal use)</summary>
	private IndexInternal[] _indexIntInfo;
	/// <summary>Array of Index structure</summary>
	private Index[] _indexArray;
	/// <summary>Number of Index structure in the _indexArray</summary>
	private int _indexCount;
	/// <summary>Array of FluxValues (flux transition values) in sample clocks</summary>
	private int[] _fluxValues;
	/// <summary>Array of flux Position in stream buffer (internal use)</summary>
	private int[] _fluxStreamPosition;
	/// <summary>Number of flux transitions recorded in the FluxValue array</summary>
	private int _fluxCount;
	/// <summary>Statistic - cbStreamTrans number of data bytes (internal use)</summary> 
	private int statDataCount;
	/// <summary>Statistic - cbStreamTrans data transfer time of  in ms (internal use)</summary>
	private int statDataTime;
	/// <summary>Statistic - number of cbStreamTrans(internal use)</summary>
	private int statDataTrans;
	/// <summary>minimum flux value found during parsing</summary>
	private int minFlux;
	/// <summary>maximum flux value found during parsing</summary>
	private int maxFlux;
	/// <summary>Statistic about the Stream</summary>
	private Statistic _stats;
	/// <summary> Value of the Sample Clock</summary>
	private double _sckValue;
	/// <summary>Value of the Index Clock</summary>
	private double _ickValue;

	/// <summary>The IndexInternal structure contains internal Index information extracted from the Stream file.
	/// Normal user should not be concerned with this structure.</summary>
	private struct IndexInternal {
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

	/// KryoFlux Hardware status (internal use)
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

	#endregion Attributes and internal structures

	#region Properties
	/// <summary>Access the number of flux transitions recorded in the FluxValues array.</summary>
	/// <returns>The number of transitions</returns>
	///
	/// <remarks> All flux transitions are stored in an array. 
	/// The number of transitions stored in the array can be 
	/// retrieved with this property. To access the flux array
	/// you must use the Flux property.</remarks>
	/// <seealso cref="Flux"/>
	public int FluxCount {
		get { return _fluxCount; }
	}

	/// <summary>Access the flux transitions array </summary>
	/// <returns>The FluxValues transition array</returns>
	///
	/// <remarks>All flux transitions are stored in an array of integer. Each flux
	/// transition is given relative to the previous transition. The value 
	/// is expressed in number of sample clocks. The number of transitions
	/// can be retrieved with the FluxCount property.</remarks> 
	public int[] FluxValues {
		get { return _fluxValues; }
	}

	/// <summary>Access the Number of Index recorded.</summary>
	/// <returns>The number of Index entries</returns>
	///
	/// <remarks>The Floppy disk Indexes information are stored in an array of 
	/// Index structures. The number of entries in the array can be 
	/// retrieved with this function. To access the Index array
	/// you must use the Indexes property.</remarks> 
	public int IndexCount {
		get { return _indexCount; }
	}

	/// <summary>Access the Index array</summary> 
	/// <returns>The Index array</returns>
	///
	/// <remarks> The Floppy disk Indexes information are stored in an array of 
	/// Index structures. For an interpretation of the data in this structure
	/// please refer to the Index structure. The size of the array 
	/// can be retrieved with the IndexCount property</remarks>
	public Index[] Indexes {
		get { return _indexArray; }
	}

	/// <summary>Access the KryoFlux HW Info String(s)</summary>
	/// <returns>The KryoFlux HW info String. Only available if The KF firmware 
	/// used to create the Stream file is equal or above 2.rotTime otherwise an empty 
	/// string is returned</returns>
	///
	/// <remarks> HW Information is transmitted from the KryoFlux device.
	/// This information is returned as a string. Each information
	/// is stored as a "name=value" pair and is separated from the 
	/// previous one with a coma "," character. for more information
	/// plase refer to the 
	/// http://info-coach.fr/atari/software/projects/_kryoflux/kryoflux_stream_protocol.pdf
	/// document </remarks>
	public string InfoString {
		get {
			if (_info.Length == 0) return "";
			return _info.ToString();
		}
	}

	/// <summary>Access the Statistic structure</summary>
	/// <returns>The Statistic structure</returns>
	///
	/// <remarks> This structure is filled during decode by calling the internal 
	/// function fillStreamStat()
	/// @see Statistic</remarks>
	public Statistic StreamStat {
		get { return _stats; }
	}

	/// <summary>Access the Clock value</summary>
	/// <returns>the value of the Sample Clock</returns>
	///
	/// <remarks>This function is used to get the Sample Clock value.\n
	/// By default the value of _sckValue = ((18432000.0 * 73.0) / 14.0) / 4.0;\n
	/// but with firmware 2.0 and above the sample clock values is transmitted
	/// by the KryoFlux HW. In that case the value transmitted by the HW is 
	/// returned by this call instead of the default value.</remarks> 
	public double SampleClock {
		get { return _sckValue; }
	}

	/// <summary>Access the Index Clock value</summary>
	/// <returns>the value of the Index Clock</returns>
	///
	/// <remarks> This function is used to get the Index Clock value.\n
	/// By default the value of _ickValue = _sckValue / 8.0;\n
	/// but with firmware 2.0 and above the index clock values is transmitted
	/// by the KryoFlux HW. In that case the value transmitted by the HW is 
	/// returned by this call instead of the default value.</remarks>
	public double IndexClock {
		get { return _ickValue; }
	}

	/// <summary>Gets the number of complete revolution recorded in the stream file</summary>
	/// <returns>the number of revolutions</returns>
	public int RevolutionCount {
		get { return _indexCount - 1; }
	}

	#endregion properties

	/// <summary>KFStream Constructor. Set default values</summary>
	public KFStream() {
		_fluxValues = null;
		_fluxStreamPosition = null;
		_indexIntInfo = null;
		_info = null;
		statDataCount = 0;
		statDataTime = 0;
		statDataTrans = 0;
		_sckValue = ((18432000.0 * 73.0) / 14.0) / 4.0;	// default value
		_ickValue = _sckValue / 8.0;	// default value
	}


	/// <summary>Searches for a specific "name" in KryoFlux HW information string</summary>
	/// <param name="name">string to search</param>
	/// <returns>A string that contains the "value" corresponding to the "name". 
	/// If the string "name" is not found, it returns an empty string</returns>
	///
	/// <remarks> Search "name" in the "name=value" pairs of the KryoFlux information array.
	/// if name is found returns the associated value.
	/// For example findName("sck") returns the sample clock value in a string</remarks>
	public string findName(string name) {
		string str = InfoString;
		// if no HW string return empty string
		if (str == "") return "";

		int position = str.IndexOf(name);
		// if not found return empty string
		if (position == 0) return "";

		int start = position + name.Length + 1;	// skip the searched string and the '='
		int stop = str.IndexOf(',', start); // until ','
		if (stop == -1)	// last string does not end with ',' (last one)
			stop = str.Length;
		string value = str.Substring(start, stop - start);
		return value;
	}


	/// <summary>Reads and parses the KF Stream file specified</summary>	
	/// <param name="fileName">Name of the stream file to read and decode</param>
	/// <returns>A status code of type <paramref name="StreamStatus"/> to indicate if the 
	/// file was read and parsed correctly.
	/// <see cref="StreamStatus"/>
	/// </returns>
	/// <remarks>
	/// This is the main function of the library. It parses the stream file passed as an
	/// argument and fills in 4 structures:
	/// - An array of flux value that can be retrieved with GetFlux FluxCount properties
	/// - An Index array that can be retrieved with Indexes and IndexCount properties
	/// - An Info string that can be retrieved with InfoString property
	/// - Statistic structure that can be retrieved with StreamStat property
	/// .
	///
	/// Internally this function calls the private functions parseStream(), 
	/// indexAnalysis(), and fillStreamStat(). The function update the sck 
	/// and ick default clock values if these values were passed in an Info block from KF 
	/// device (FW 2.0+). In all cases the sck and ick can be retrieved with 
	/// SampleClock and IndexClock properties.
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

		// we now perform some computation to fill the "statistic" structure
		fillStreamStat();

		// checks for sck & ick in info string and update accordingly
		// note that the HW clock values are passed using en-US syntax
		string value;
		value = findName("sck");
		if (value != "") {
			_sckValue = Convert.ToDouble(value, new CultureInfo("en-US"));
		}
		value = findName("ick");
		if (value != "") {
			_ickValue = Convert.ToDouble(value, new CultureInfo("en-US"));
		}
		return status;
	}


	/// <summary>The KryoFlux Stream file parser.</summary>
	/// <param name="sbuf">The buffer that contains the Stream file read</param>
	/// <returns>A status code of type <paramref name="StreamStatus"/> to indicate if the 
	/// file was read and parsed correctly.
	/// <see cref="StreamStatus"/>
	/// </returns>
	///
	/// <remarks> This is an internal function called by readStream(). It parses the stream file
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
	/// 	- If the data block is of type Flux1, Flux2, or Flux3 we create a new entry in the FluxValues array 
	/// 	and we store the FluxValues Value and Stream Position.
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
	/// (FluxValues, Index, and Info) but we still need to analyze the Index information.</remarks>
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
		_fluxValues = new int[sbuf.Length];
		_fluxStreamPosition = new int[sbuf.Length];
		_fluxCount = 0;

		// we use a size of 128 for indexes that should be more than enough
		_indexIntInfo = new IndexInternal[128];
		_indexArray = new Index[128];
		_indexCount = 0;

		_info = new System.Text.StringBuilder();

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
					_fluxValues[_fluxCount] = fluxValue; // store value
					minFlux = Math.Min(minFlux, fluxValue);
					maxFlux = Math.Max(maxFlux, fluxValue);

					_fluxStreamPosition[_fluxCount] = streamPos; // store position
					_fluxCount++; // one more flux
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
						int position = extractStreamInt(sbuf, pos + 4);
						if (streamPos != position)
							return StreamStatus.sdsWrongPosition;

						// number of stream bytes since last OOBStreamInfo
						int sb = streamPos - lastStreamPos;
						lastStreamPos = streamPos;

						// update data count and time for non consecutive OOBStreamInfo
						if (sb != 0) {
							statDataCount += sb;
							statDataTime += extractStreamInt(sbuf, pos + 8);
							statDataTrans++;
						}
						break;

					case OOBType.OOBIndex:
						// initialize index _info
						_indexIntInfo[_indexCount].streamPos = extractStreamInt(sbuf, pos + 4);
						_indexIntInfo[_indexCount].sampleCounter = extractStreamInt(sbuf, pos + 8);
						_indexIntInfo[_indexCount].indexCounter = extractStreamInt(sbuf, pos + 12);
						_indexArray[_indexCount].fluxPosition = 0;
						_indexArray[_indexCount].rotationTime = 0;
						_indexArray[_indexCount].preIndexTime = 0;
						//_indexArray[_indexCount].postIndexTime = 0;

						// next index position
						_indexCount++;

						// store the position of the index in the stream for checking
						lastIndexPos = _indexIntInfo[_indexCount].streamPos;
						break;

					case OOBType.OOBStreamEnd:
						// check for errors detected by the Kryoflux hardware
						hwStatus = (HWStatus)(extractStreamInt(sbuf, pos + 8));

						// if no error found, decoded stream position must match the encoder position
						if ((hwStatus == HWStatus.khsOk) && 
							(streamPos != extractStreamInt(sbuf, pos + 4)))
							return StreamStatus.sdsWrongPosition;
						break;

					case OOBType.OOBInfo:
						int size = sbuf[pos + 2] + (sbuf[pos + 3] << 8);
						
						if (size != 0) {
							if (_info.Length > 0) _info.Append(", ");

							for (int i = 0; i < size-1; i++)
								_info.Append((char)sbuf[pos + 4 + i]);
						}
						break;

					case OOBType.OOBEof:
						eoff = true;
						break;
					
					default:
						// any other oob type is invalid
						return StreamStatus.sdsInvalidOOB;
				}
			}

			// jump to next block
			pos += blen;
		}

		// additional empty flux at the end
		_fluxValues[_fluxCount] = fluxValue;
		_fluxStreamPosition[_fluxCount] = streamPos;

		// check KryoFlux hardware error
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

		// check OOB end
		if (!eoff)
			return StreamStatus.sdsMissingEnd;

		// error if no index was found or stream is shorter than the reference position of the last index
		if (lastIndexPos != 0 && (streamPos < lastIndexPos))
			return StreamStatus.sdsIndexReference;

		return StreamStatus.sdsOk;
	}


	/// <summary>
	/// Extract the next 4 bytes as a 32bits integer from Stream File
	/// </summary>
	/// <param name="sbuf">buffer that contains the 4 bytes to extract (little-Indian ordering)</param>
	/// <param name="pos">Position of the first byte</param>
	/// <returns>The 32 bits integer</returns>
	private int extractStreamInt(byte[] sbuf, int pos) {
		return sbuf[pos] + (sbuf[pos + 1] << 8) + (sbuf[pos + 2] << 16) + (sbuf[pos + 3] << 24);
	}


	/// <summary>
	/// Analyze and fill information in the Index structure
	/// </summary>
	/// <returns>A status code of type <paramref name="StreamStatus"/> to indicate if the 
	/// file was read and parsed correctly.
	/// <see cref="StreamStatus"/>
	/// </returns>
	/// <remarks>This is an internal function called by decode(). It finalize the information  
	/// stored in the Index structure.</remarks>
	private StreamStatus indexAnalysis() {
		// stop if either no index or flux data is available
		if (_indexCount == 0 || _fluxCount == 0)
			return StreamStatus.sdsOk; // nothing to do!

		int iidx = 0;	// Index index
		int fidx;		// FluxValues index
		int itime = 0;	// Index time

		int nextstrpos = _indexIntInfo[iidx].streamPos; // next flux stream position for index

		// associate flux transition array offsets with stream offsets
		for (fidx = 0; fidx < _fluxCount; fidx++) {
			// sum all flux between index signals
			itime += _fluxValues[fidx];

			int nfidx = fidx + 1; // next flux index

			// sum until reach the index next flux position
			if (_fluxStreamPosition[nfidx] < nextstrpos)
				continue;

			// edge case; the very first flux has an index signal
			if ((fidx == 0) && (_fluxStreamPosition[0] >= nextstrpos))
				nfidx = 0;

			if (iidx < _indexCount) {
				// set the buffer offset of the flux containing the index signal
				_indexArray[iidx].fluxPosition = nfidx;

				// the complete flux time of the flux that includes the index signal
				int iftime = _fluxValues[nfidx];

				// timer was sampled at the signal edge, just before the interrupt - the real time is the flux length
				if (_indexIntInfo[iidx].sampleCounter == 0)
					_indexIntInfo[iidx].sampleCounter = iftime & 0xffff;

				// if the last flux is unwritten
				if (nfidx >= _fluxCount) {
					// and the next position could have been a new code
					if (_fluxStreamPosition[nfidx] == nextstrpos) {
						// flux time is a sum of all overflows plus the sub-flux size to get the flux size until the index signal
						iftime += _indexIntInfo[iidx].sampleCounter;
						_fluxValues[nfidx] = iftime;
					}
				}

				// the total number of overflows in the flux containing the index signal
				// due to the way a flux time is calculated, the upper 16 bits represent the number of overflow codes
				int icoverflowcnt = iftime >> 16;

				// the number of overflows to step back before the index cell was created to reach the real signal point
				// this is always positive due to the condition of entering this code
				int preoverflowcnt = _fluxStreamPosition[nfidx] - nextstrpos;

				// check if more steps need to be taken back from the index cell to signal point than the number of overflows
				// if the condition is true, there is an error in the stream or index encoding
				if (icoverflowcnt < preoverflowcnt)
					return StreamStatus.sdsMissingIndex;

				// the number of overflows before the index signal in flux time
				int preIndexTime = (icoverflowcnt - preoverflowcnt) << 16;

				// add the time counted (sub-cell); this happened before writing the next overflow or final cell code
				preIndexTime += _indexIntInfo[iidx].sampleCounter;

				// set sub-cell time before and after the index signal
				_indexArray[iidx].preIndexTime = preIndexTime;
				//_indexArray[iidx].postIndexTime = iftime - preIndexTime;

				// itime contains the complete cell time for the previous index signal; it must only contain the postIndexTime
				// act.sum-prev.iftime+prev.postIndexTime, where prev.postIndexTime=prev.iftime-prev.preIndexTime
				// -> act.sum-prev.iftime+prev.iftime-prev.preIndexTime -> act.sum-prev.preIndexTime
				if (iidx != 0)
					itime -= _indexArray[iidx - 1].preIndexTime;

				// the revolution time consists of the sum of cell times plus the sub-cell time before the index signal
				// if the first cell has the index signal, the revolution time is the sub-cell time before the index signal
				_indexArray[iidx].rotationTime = (nfidx != 0 ? itime : 0) + preIndexTime;

				// try next index
				iidx++;

				// next stream position for index
				nextstrpos = (iidx < _indexCount) ? _indexIntInfo[iidx].streamPos : 0;

				// restart index timer unless the very first cell has the index signal
				// required as next calculation would expect the index time to be part of the sum and that wouldn't happen
				if (nfidx != 0)
					itime = 0;
			} // valid index
		} // for all flux count

		// error if not all indexes have been found
		if (iidx < _indexCount)
			return StreamStatus.sdsMissingIndex;

		// use the additional cell if last index happened, but the base cell was incomplete/unwritten
		if (_indexIntInfo[iidx - 1].streamPos >= _fluxCount)
			_fluxCount++;

		return StreamStatus.sdsOk;
	}	// end of indexAnalysis()

	/// <summary>Fill the Statistic structure based on _info in stream file</summary>
	/// 
	/// <remarks> This function uses the decoded Stream file _info to fill the statistical stream structure
	/// After calling this function it is possible to access the different information in the
	/// Statistic structure.</remarks>
	private void fillStreamStat() {
		int sum, vmin, vmax;
		_stats = new Statistic();

		if (statDataTime != 0)
			_stats.avgbps = ((double)statDataCount * 1000.0) / statDataTime;
		else
			_stats.avgbps = 0;

		sum = 0;
		vmax = 0;
		vmin = int.MaxValue;

		// skip first index (incomplete revolution)
		for (int i = 1; i < _indexCount; i++) {
			int rotTime = _indexArray[i].rotationTime;
			vmin = Math.Min(vmin, rotTime);
			vmax = Math.Max(vmax, rotTime);
			sum += rotTime;

			// checking that the computed rotationTime is roughly equal to difference in index counter
			int delta = Math.Abs(_indexArray[i].rotationTime - (_indexIntInfo[i].indexCounter - _indexIntInfo[i - 1].indexCounter) * 8);
			//Debug.Assert(delta < 8);	// TODO sometimes up to 38 ???
		}
		if ((_indexCount - 1) > 0) {
			_stats.avgrpm = (_sckValue * ((double)(_indexCount - 1) * 60.0)) / (double)sum;
			_stats.maxrpm = (_sckValue * 60.0) / (double)vmin;
			_stats.minrpm = (_sckValue * 60.0) / (double)vmax;
		}
		else
			_stats.avgrpm = _stats.maxrpm = _stats.minrpm = 0;

		sum = 0;
		if (_indexCount > 2) {
			for (int i = 1; i < _indexCount; i++)
				sum += _indexArray[2].fluxPosition - _indexArray[1].fluxPosition;
			_stats.nbflux = sum / (_indexCount - 1);
		}
		else
			_stats.nbflux = 0;
		_stats.minFlux = minFlux;
		_stats.maxFlux = maxFlux;
	}	// fillStreamStat()

}	// KFStream Class
}	// name space

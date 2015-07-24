/*!
@file SCPReader.cs
 * 
<summary>This file contains the structures and functions to read a SuperCard Pro raw file</summary>

<div class="jlg">Copyright (C) 2013-2015 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tool project.\n\n
The Atari Universal FD Image Tool project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tool project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 3
of the License, or (at your option) any later version.\n\n
The Atari Universal FD Image Tool project is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n
See the GNU General Public License for more details.\n\n
You should have received a copy of the GNU General Public License
along with the Atari Universal FD Image Tool project; if not, write to the Free Software
Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA\n</div>

@author Jean Louis-Guerin
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GlobalPackage;
using Aufit;
using System.Windows.Controls;
using System.Windows;

namespace SCPReaderPackage {
	/// <summary>
	/// This structure contains the information related to the SCP File header
	/// </summary>
	public class FileHeader {
		/// <summary>The SuperCard Pro File Header should be SCP</summary>
		public string identifier = "";
		/// <summary>Contains the version/revision as a byte.  This is encoded as Major in the
		/// upper nibble and Minor in the lower nibble so that 0x39 = version 3.9 of the format</summary>
		public int version;
		/// <summary>Is the disk type and represents the type of disk for the image.</summary>
		public int diskType;
		/// <summary>Contains the number of revolutions, which determines how many revolutions for 
		/// each track is contained in the image.</summary>
		public int revCount;
		/// <summary>The number of the first Track recoded</summary>
		public int firstTrack;
		/// <summary>The number of the last Track recoded</summary>
		public int lastTrack;
		/// <summary>These flags provides information on how the file was produced
		/// The bits are defined as follow:
		/// - Bit 0 - INDEX, cleared if the image did not use the indexStart mark for queuing tracks
        ///           set is the image used the indexStart mark to queue tracks.
		/// - Bit 1 - TPI, cleared if the drive is a 48TPI drive set if the drive is a 96TPI drive
		/// - Bit 2 - RPM, cleared if the drive is a 300 RPM drive set if the drive is a 360 RPM drive
		/// - Bit 3 - TYPE, cleared if the image is a read-only type set if the image is read/write capable
		/// </summary>
		public int imageFlags;
		/// <summary>Contains the value of the bit cell time.  Normally this is always 16 bits, but if the
		/// value is non-zero, then it represents the number of bits for each bit cell entry.  For
		/// example, if this byte was set to 8, then each bit cell entry would be 8 bits wide, not
		/// the normal 16 bits.  This is for future expansion, and may never be actually used.</summary>
		public int bitcellWidth;
		/// <summary>
		/// contains the 32 bit checksum of data starting from offset 0x10 through the end of the image 
		/// file.  Checksum is standard addition, with a wrap beyond 32 bits rolling over.
		/// </summary>
		public int checksum;
		/// <summary> Contains the 32 bit offset (from the start of the file) of the tracks
		/// data headers.  </summary> 
		/// <remarks>There is an offset entry for every track, with up to 166 tracks supported.
		/// This means that disk images of up to 83 tracks with sides 0/1 are possible.  If no
		/// track data for a track is present, then the entry will contain a longword of zeros
		/// (0x00000000).  The 1st track header should start at offset 0x000002A8, but could
		/// technically start anywhere in the file.  Since offsets are used, track data does not
		/// have to be in any order, so you could append track data to the file and change the
		/// header to point to the appended data. For simplicity it is recommended that you follow
		/// the normal image format shown below, with all tracks in sequential order.</remarks>
		public int[] TrackHeader;
		/// <summary>After the last byte of track data there will be a time-stamp.  
		/// This time-stamp is an ASCII string, ie: 10/15/2013 5:52:30 PM </summary>
		public string timeStamp;
	}

	/// <summary>
	/// This structure contains information related to one revolution of the recorded data in an SCP file
	/// </summary>
	public class RevInfo {
		/// <summary>Contains the indexStart time for the 1st revolution.  This is the time from
		/// indexStart pulse to indexStart pulse for the track data.  This will be either ~360RPMs or 
		/// ~300 RPMs depending the drive.  It is important to have this exact time as it is 
		/// necessary when writing an image back to a real disk.  The indexStart time is a value 
		/// in nanoseconds/25ns for one revolution. </summary>
		public int revolutionTime;
		/// <summary>
		/// Contains the number of flux transitions recorded for this track
		/// </summary>
		public int fluxCount;
		/// <summary>
		/// The offset of the first flux data from start of Track Data Header
		/// </summary>
		public int fluxOffset;

		///// <summary>
		///// The position of the indexStart inside the first flux. has to be computed in SCP file :(
		///// </summary>
		//public int indexTimePosition;
	}

	/// <summary>
	/// This structure contains the SCP Track header information (one per track).
	/// </summary>
	public class TrackInfo {
		/// <summary>The SuperCard Pro Track Header should be TRK</summary>
		public string identifier = "";
		/// <summary>
		/// contains the track number (0-165).  For single sided disks, tracks 0-41 (48TPI)
		/// or 0-83 (96TPI) are used.  For double-sided disks, the actual track number is this value
		/// divided by 2.  The remainder (0 or 1) is the head.  0 represents the bottom head and
		/// 1 represents the top head.  For example, 0x0B would be 11/2 = track 5, head 1 (top).
		/// Likewise, 0x50 would be 80/2 = track 40, head 0 (bottom).
		/// </summary>
		public int trackSide;
		/// <summary>
		/// An array of RevInfo structures. One for each revolution
		/// </summary>
		public RevInfo[] revInfo;
		/// <summary>
		/// The array that contains all the flux transitions recorded. When multiple revolutions are used
		/// the flux transitions are stored end to end, like the disk is spinning multiple times.  No break
		/// or skew of any kind results from storage.
		/// </summary>
		public int[] fluxValues;
	}

	/// <summary>
	/// Error code returned when reading and decoding SCP file
	/// </summary>
	public enum SCPStatus : int {
		/// <summary>Status OK - The file was read and decoded successfully</summary>
		stsOK = 0,
		/// <summary>Could not open file</summary>
		stsOpenFailed,
		/// <summary>Invalid  File header</summary>
		stsInvalidFile,
		/// <summary>Invalid Track header</summary>
		stsInvalidTrack,
		/// <summary>File checksum error</summary>
		stsFileChecksum,
		/// <summary>Error in file content</summary>
		stsFileContent,
		/// <summary>Error opening the stream file</summary>
		stsFileOpen
	}	
	
	/// <summary>
	/// This class is used to read an SCVP file and to fill the Global structures
	/// </summary>
	public class SCPReader {
		private static int _fluxMin = Int32.MaxValue;
		private static int _fluxMax = 0;

		/// <summary>Reads and decodes the SCP file specified in the parameter</summary>	
		/// <param name="fileName">Name of the stream file to read and decode</param>
		/// <param name="infobox">TextBox used to display histogram info</param>
		/// <returns>True to indicate that the file was read and parsed correctly, false otherwise</returns>
		/// <see cref="SCPStatus"/>
		/// <remarks></remarks>
		public async static Task<bool> readScpAsync(string fileName, TextBox infobox) {
			FileStream fs;
			try {
				fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch (Exception exc) {
				Console.WriteLine("Error: {0}", exc.Message);
				return false;
			}
			infobox.Text = String.Format("{0}\n", fileName);

			// read file into buffer
			byte[] sbuf = new byte[fs.Length];
			int bytes = fs.Read(sbuf, 0, (int)fs.Length);
			Debug.Assert(bytes == fs.Length, "Error reading stream file", "Incorrect number of bytes read");	// should be same
			fs.Close();

			// we parse the file
			SCPStatus status = await decodeScp(sbuf, infobox);
			return (status == SCPStatus.stsOK) ? true : false;
		}


		/// <summary>
		/// Decodes an SCP file read in a buffer
		/// </summary>
		/// <param name="buffer">Contains the SCP file</param>
		/// <param name="infoBox">TextBox to log information</param>
		/// <returns></returns>
		private async static Task<SCPStatus> decodeScp(byte[] buffer, TextBox infoBox) {
			FileHeader file = new FileHeader();
			int idx = 0;
			SCPStatus status;

			// Read File Header
			file.identifier += readChar(buffer, ref idx).ToString();	// 0x00
			file.identifier += readChar(buffer, ref idx).ToString();	// 0x01
			file.identifier += readChar(buffer, ref idx).ToString();	// 0x02
			if (file.identifier != "SCP") {
				return SCPStatus.stsInvalidFile;
			}
			file.version = readByte(buffer, ref idx);					// 0x03
			file.diskType = readByte(buffer, ref idx);					// 0x04
			file.revCount = readByte(buffer, ref idx);					// 0x05
			file.firstTrack = readByte(buffer, ref idx);				// 0x06
			file.lastTrack = readByte(buffer, ref idx);					// 0x07
			file.imageFlags = readByte(buffer, ref idx);				// 0x08
			file.bitcellWidth = readByte(buffer, ref idx);				// 0x09
			idx++;														// 0x0A reserved
			idx++;														// 0x0B reserved
			file.checksum = readInt32(buffer, ref idx);					// 0x0C-0x0F

			string versionString = ((file.version & 0xF0) >> 4).ToString() + "." + (file.version & 0x0F).ToString();
			infoBox.AppendText(String.Format("V{0} - Type={1:X} - {2} Revolutions - Tracks {3}-{4}\n",
				versionString, file.diskType, file.revCount, file.firstTrack, file.lastTrack));

			//int checksum = 0;
			//for (int index = 0x10; index < buffer.Count(); index++)
			//	checksum += buffer[index];
			//if (checksum == file.checksum) {
			//	infoBox.AppendText(String.Format("SCP File Error: Checksum incorrect."));
			//	return SCPStatus.stsFileChecksum;
			//}

			ButtonMatrix.clearButtons();
			file.TrackHeader = new int[166];
			for (int trackAndSide = 0; trackAndSide < 166; trackAndSide++) {
				file.TrackHeader[trackAndSide] = readInt32(buffer, ref idx);
				if (file.TrackHeader[trackAndSide] != 0) {
					status = await readSCPTrackAsync(buffer, trackAndSide, file.TrackHeader[trackAndSide], file.revCount);
					if (status != SCPStatus.stsOK) {
						switch (status) {											
							case SCPStatus.stsInvalidTrack:
								infoBox.AppendText(String.Format("SCP File Error: invalid track header"));
								break;
							case SCPStatus.stsFileContent:
								infoBox.AppendText(String.Format("SCP File Error: inconsistent number of tracks"));
								break;
						}
						return status;
					}
						
					if ((trackAndSide < file.firstTrack) || (trackAndSide > file.lastTrack)) {
						infoBox.Text = String.Format("SCP File Error: inconsistent number of tracks");
						return SCPStatus.stsFileContent;
					}
					int track = trackAndSide / 2;
					int side = trackAndSide % 2;
					
					//Global.mfmDecode(track, side);
					Global.computeHisto(track, side, 0, infoBox);
					ButtonMatrix.setButtonBG(track, side);
				}
			}

			return SCPStatus.stsOK;
		}


		/// <summary>
		/// Read asynchronously one track of an SCP file
		/// </summary>
		/// <param name="buffer">The read buffer</param>
		/// <param name="trackSide">The Trackand Side parameter</param>
		/// <param name="trackHeaderPosition">Position of the header in the buffer</param>
		/// <param name="revCount">Number of revision used</param>
		/// <param name="infoBox">Text box to display log information</param>
		/// <returns>The status</returns>
		public static Task<SCPStatus> readSCPTrackAsync(byte[] buffer, int trackSide, int trackHeaderPosition, int revCount) {
			return Task<bool>.Run(() => readSCPTrack(buffer, trackSide, trackHeaderPosition, revCount));
		}


		/// <summary>
		/// Reads one Track information from buffer
		/// </summary>
		/// <param name="buffer">Buffer that contains the SCP file content</param>
		/// <param name="trackSide">The track and side value</param>
		/// <param name="trackHeaderPosition">Position of the track header in the buffer</param>
		/// <param name="revCount">Number of revision imaged in the file</param>
		/// <param name="infoBox">TextBox to display log information</param>
		/// <returns></returns>
		private static SCPStatus readSCPTrack(byte[] buffer, int trackSide, int trackHeaderPosition, int revCount) {
			int track = trackSide / 2;
			int side = trackSide % 2;
			_fluxMin = Int32.MaxValue;
			_fluxMax = 0;

			TrackInfo ti = new TrackInfo();
			int idx = trackHeaderPosition;

			ti.identifier += readChar(buffer, ref idx).ToString();	// 0x00
			ti.identifier += readChar(buffer, ref idx).ToString();	// 0x01
			ti.identifier += readChar(buffer, ref idx).ToString();	// 0x02
			if (ti.identifier != "TRK") {
				return SCPStatus.stsInvalidTrack;
			}
			ti.trackSide = readByte(buffer, ref idx);				// 0x03 trackSide

			// check track number
			if (ti.trackSide != trackSide) {
				return SCPStatus.stsFileContent;
			}

			// read rev headers
			ti.revInfo = new RevInfo[revCount];
			int sumAllFlux = 0;	// sum all the flux transitions
			for (int i = 0; i < revCount; i++) {
				ti.revInfo[i] = new RevInfo();
				ti.revInfo[i].revolutionTime = readInt32(buffer, ref idx);
				ti.revInfo[i].fluxCount = readInt32(buffer, ref idx);
				sumAllFlux += ti.revInfo[i].fluxCount;
				ti.revInfo[i].fluxOffset = readInt32(buffer, ref idx);
			}

			// sometimes SCP seems to generate invalid short track at end of disks
			// we just ignore them
			if (sumAllFlux < 1000)
				return SCPStatus.stsOK;

			// create buffer for all flux transitions
			// note this buffer might be bigger that what is needed because of null entries (NFA)
			ti.fluxValues = new int[sumAllFlux];

			int[] fluxOffset = new int[revCount];
			int timeMissingFromFirstRev = 0;

			for (int rev = 0; rev < revCount; rev++) {
				int revFluxSum = 0;
				int overflowCount = 0;

				// set bufferIndex to the offset of the data
				idx = ti.revInfo[rev].fluxOffset + trackHeaderPosition;
				// now set fluxOffset to offset in the "extracted data buffer"
				if (rev > 0) fluxOffset[rev] = fluxOffset[rev - 1] + ti.revInfo[rev - 1].fluxCount;
				else fluxOffset[rev] = 0;
				int p = fluxOffset[rev];

				for (int j = 0; j < ti.revInfo[rev].fluxCount; j++) {
					// read next two bytes
					int val = buffer[idx++] << 8;
					val += buffer[idx++];

					if (val == 0) {
						ti.fluxValues[p] += 65536;
						overflowCount++;
					}	// overflow (probably NFA)
					else {
						ti.fluxValues[p] += val;
						revFluxSum += ti.fluxValues[p++];
					}	// normal
				}	// all flux in one rev

				// if we had overflow we must remove from number of fluxes
				if (overflowCount > 0)
					ti.revInfo[rev].fluxCount = ti.revInfo[rev].fluxCount - overflowCount;

				// SCP starts at first flux AFTER index. In case of NFA we miss the end of NFA
				// located at end of track. In that case the fluxSum != revolution time 
				if (rev == 0) {
					timeMissingFromFirstRev = (ti.revInfo[0].revolutionTime - revFluxSum);
				}	
			}	// for all revolutions

			// Here we compensate the best we can for the missing information from SCP
			//if ((timeMissingFromFirstRev > 10000 / 25) && (revCount > 1)) {
				//int nfa = ti.fluxValues[fluxOffset[1]];
				for (int rev = 0; rev < revCount; rev++) {
					int firstOffset = fluxOffset[rev];
					//int lastOffset = firstOffset + ti.revInfo[rev].fluxCount - 1;
					if (rev == 0) {
						ti.fluxValues[firstOffset] += timeMissingFromFirstRev;
						//MessageBox.Show(String.Format("NFA {0}", timeMissingFromFirstRev * 25));
						//ti.fluxValues[lastOffset] += timeMissingFromFirstRev;
					}
				}	// all rev
			//}

			// now we transfer to our Global Disk structure. Values are in NS
			// with correction done on a revolution basis
			MainWindow.CurrentRev = 0;
			Global.Disk[track, side].fluxData = new FluxData();
			Global.Disk[track, side].fluxData.totalFluxCount = ti.fluxValues.Count();
			Global.Disk[track, side].fluxData.fluxValue = new int[ti.fluxValues.Count()];
			Global.Disk[track, side].fluxDataRev = new FluxDataRev[revCount];
			int pos = 0;

			for (int rev = 0; rev < revCount; rev++) {
				// correction for this rev
				double tick = 8000000.0 / (double)ti.revInfo[rev].revolutionTime * 25.0;
				int offset = fluxOffset[rev];

				Global.Disk[track, side].fluxDataRev[rev].revolutionTime = (int)((double)ti.revInfo[rev].revolutionTime * tick);
				Global.Disk[track, side].fluxDataRev[rev].fluxCount = ti.revInfo[rev].fluxCount;
				Global.Disk[track, side].fluxDataRev[rev].firstFluxIndex = fluxOffset[rev];

				for (int f = 0; f < ti.revInfo[rev].fluxCount; f++) {
					int flux = (int)((double)ti.fluxValues[offset + f] * tick);
					Debug.Assert(flux >= 0, "Invalid negative flux read");
					Global.Disk[track, side].fluxData.fluxValue[pos++] = flux;



					flux = Global.Disk[track, side].fluxData.fluxValue[offset + f];
					if (flux > _fluxMax) _fluxMax = flux;
					if (flux < _fluxMin) _fluxMin = flux;
				}	// all flux of rev
			}	// all rev

			Global.Disk[track, side].fluxData.maxFlux = _fluxMax;
			Global.Disk[track, side].fluxData.minFlux = _fluxMin;

			return SCPStatus.stsOK;
		}


		/// <summary>
		/// Extract the next 4 bytes from the Stream File as a 32bits integer
		/// </summary>
		/// <param name="sbuf">buffer that contains the 4 bytes to extract (little-Indian ordering)</param>
		/// <param name="pos">Position of the first byte</param>
		/// <returns>The 32 bits integer</returns>
		/// <remarks>This is due to the poor casting capabilities of C#</remarks>
		private static int readInt32(byte[] sbuf, ref int pos) {
			int value = sbuf[pos] + (sbuf[pos + 1] << 8) + (sbuf[pos + 2] << 16) + (sbuf[pos + 3] << 24);
			pos += 4;
			return value;
		}

		/// <summary>
		/// Extract one byte from the stream file.
		/// </summary>
		/// <param name="sbuf">input buffer</param>
		/// <param name="pos">position of the byte</param>
		/// <returns>the byte extracted</returns>
		private static byte readByte(byte[] sbuf, ref int pos) {
			return sbuf[pos++];
		}

		/// <summary>
		/// extract one character from the buffer
		/// </summary>
		/// <param name="sbuf">the input buffer</param>
		/// <param name="pos">position of the character</param>
		/// <returns>the extracted character</returns>
		private static char readChar(byte[] sbuf, ref int pos) {
			return (char)(sbuf[pos++]);
		}
	}
}

/*!
@file SCPReader.cs
 * 
<summary>This file contains the structures and functions to read a SuperCard Pro raw file</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
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

using GlobalSpace;
using Aufit;

namespace SCPReaderSpace {
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
		/// - Bit 0 - INDEX, cleared if the image did not use the index mark for queuing tracks
        /// set is the image used the index mark to queue tracks.
		/// - Bit 1 - TPI, cleared if the drive is a 48TPI drive set if the drive is a 96TPI drive
		/// - Bit 2 - RPM, cleared if the drive is a 300 RPM drive set if the drive is a 360 RPM drive
		/// - Bit 3 - TYPE, cleared if the image is a read-only type set if the image is read/write capable
		/// </summary>
		public int imageFlags;
		/// <summary>Contains the width of the bit cell time.  Normally this is always 16 bits, but if the
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
		/// <summary>After the last byte of track data there will be a timestamp.  
		/// This timestamp is an ASCII string, ie: 10/15/2013 5:52:30 PM </summary>
		public string timeStamp;
	}

	/// <summary>
	/// This structure contains information related to one revolution of the recorded data in an SCP file
	/// </summary>
	public class RevInfo {
		/// <summary>Contains the index time for the 1st revolution.  This is the time from
		/// index pulse to index pulse for the track data.  This will be either ~360RPMs or 
		/// ~300 RPMs depending the drive.  It is important to have this exact time as it is 
		/// necessary when writing an image back to a real disk.  The index time is a value 
		/// in nanoseconds/25ns for one revolution. </summary>
		public int indexTime;
		/// <summary>
		/// Contains the number of flux transitions recorded for this track
		/// </summary>
		public int fluxCount;
		/// <summary>
		/// The offset of the first flux data from start of Track Data Header
		/// </summary>
		public int fluxOffset;
		/// <summary>
		/// The position of the index inside the first flux. Currently not available in SCP file :(
		/// </summary>
		public int indexTimePosition;
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
		/// <summary>
		/// The checksum of the Track data Block
		/// </summary>
		public long checksum;
	}

	/// <summary>
	/// Error code returned when reading and decoding SCP file
	/// </summary>
	public enum SCPStatus : int {
		/// <summary>Status OK - The file was read and decoded successfully</summary>
		stsOK = 0,
		/// <summary>Invalid  File header</summary>
		stsInvalidFile,
		/// <summary>Invalid Track header</summary>
		stsInvalidTrack,
		/// <summary>File checksum error</summary>
		stsFileChecksum,
		/// <summary>Track chacksum error</summary>
		stsTackChecksum,
		/// <summary>Error in file content</summary>
		stsFileContent,
		/// <summary>Error opening/reading the stream file</summary>
		stsReadError
	}	
	
	/// <summary>
	/// This class is used to read an SCVP file and to fill the Global structures
	/// </summary>
	public class SCPReader {
		private static double _sckValue = 40000000.0;	//		1 /25 ns
		private static int _fluxMin = Int32.MaxValue;
		private static int _fluxMax = 0;

		/// <summary>
		/// Access the SCP sample clock value given in Hz
		/// </summary>
		public static double SampleClock {
			get { return _sckValue; }
		}



		/// <summary>
		/// This is an asynchronous wrapper of the readSCP() function
		/// </summary>
		/// <param name="name">Name of the stream file to read</param>
		/// <returns>True if file processed without problem; false if error while processing</returns>
		public static Task<bool> readSCPAsync(string name) {
			return Task<bool>.Run(() => readSCP(name));
		}


		/// <summary>Reads and decodes the SCP file specified in the parameter</summary>	
		/// <param name="fileName">Name of the stream file to read and decode</param>
		/// <returns>True to indicate that the file was read and parsed correctly, false otherwise</returns>
		/// <see cref="SCPStatus"/>
		/// <remarks></remarks>
		public static bool readSCP(string fileName) {
			FileStream fs;
			try {
				fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch (Exception exc) {
				Console.WriteLine("Error: {0}", exc.Message);
				return false;
			}

			// read file into buffer
			byte[] sbuf = new byte[fs.Length];
			int bytes = fs.Read(sbuf, 0, (int)fs.Length);
			Debug.Assert(bytes == fs.Length);	// should be same
			fs.Close();

			// we parse the file
			SCPStatus status = decodeScp(sbuf);
			return (status == SCPStatus.stsOK) ? true : false;
		}


		private static SCPStatus decodeScp(byte[] buffer) {
			FileHeader file = new FileHeader();
			int bufferIndex = 0;
			SCPStatus status;

			// Read File Header
			file.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x00
			file.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x01
			file.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x02
			if (file.identifier != "SCP")
				return SCPStatus.stsInvalidFile;
			file.version = buffer[bufferIndex++];							// 0x03
			file.diskType = buffer[bufferIndex++];							// 0x04
			file.revCount = buffer[bufferIndex++];							// 0x05
			file.firstTrack = buffer[bufferIndex++];						// 0x06
			file.lastTrack = buffer[bufferIndex++];							// 0x07
			file.imageFlags = buffer[bufferIndex++];						// 0x08
			file.bitcellWidth = buffer[bufferIndex++];						// 0x09
			bufferIndex++;													// 0x0A reserved
			bufferIndex++;													// 0x0B reserved
			file.checksum = readInt32(buffer, ref bufferIndex);				// 0x0C-0x0F
			
			file.TrackHeader = new int[166];
			for (int tnum = 0; tnum < 165; tnum++) {
				file.TrackHeader[tnum] = readInt32(buffer, ref bufferIndex);
				if (file.TrackHeader[tnum] != 0) {
					status = readSCPTrack(buffer, tnum, file.TrackHeader[tnum], file.revCount);
					if (status != SCPStatus.stsOK) return status;
				}
			}

			// TODO check checksum
			// TODO read timestamp
			
			return SCPStatus.stsOK;
		}


		/// <summary>
		/// Reads information from buffer
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="trackSide"></param>
		/// <param name="trackHeaderPosition"></param>
		/// <returns></returns>
		private static SCPStatus readSCPTrack(byte[] buffer, int trackSide, int trackHeaderPosition, int revCount) {
			int track = trackSide / 2;
			int side = trackSide % 2;
			_fluxMin = Int32.MaxValue;
			_fluxMax = 0;

			TrackInfo ti = new TrackInfo();
			int bufferIndex = trackHeaderPosition;

			ti.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x00
			ti.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x01
			ti.identifier += ((char)buffer[bufferIndex++]).ToString();	// 0x02
			if (ti.identifier != "TRK")
				return SCPStatus.stsInvalidTrack;
			ti.trackSide = buffer[bufferIndex++];						// 0x03

			// read rev headers
			ti.revInfo = new RevInfo[revCount];
			int fluxSize = 0;	// sum all the flux transitions
			for (int i = 0; i < revCount; i++) {
				ti.revInfo[i] = new RevInfo();
				ti.revInfo[i].indexTime = readInt32(buffer, ref bufferIndex);
				ti.revInfo[i].fluxCount = readInt32(buffer, ref bufferIndex);
				fluxSize += ti.revInfo[i].fluxCount;
				ti.revInfo[i].fluxOffset = readInt32(buffer, ref bufferIndex);
				ti.revInfo[i].indexTimePosition = 0;	// not provided currently
			}

			// create buffer for all flux transitions
			// note this buffer might be bigger that what is needed because of null entries (strongbits)
			ti.fluxValues = new int[fluxSize];

			for (int i = 0; i < revCount; i++) {
				if (ti.revInfo[i].indexTime != 0) {
					int overflowCount = 0;
					// set bufferIndex to the offset of the data
					bufferIndex = ti.revInfo[i].fluxOffset + trackHeaderPosition;
					// now set fluxOffset to offset in the "extracted data buffer"
					if (i > 0) ti.revInfo[i].fluxOffset = ti.revInfo[i - 1].fluxOffset + ti.revInfo[i - 1].fluxCount;
					else ti.revInfo[i].fluxOffset = 0;
					//revCount++;
					int p = ti.revInfo[i].fluxOffset;
					for (int j = 0; j < ti.revInfo[i].fluxCount; j++) {
						int val = buffer[bufferIndex++] << 8;
						val += buffer[bufferIndex++];
						if (val == 0) {
							ti.fluxValues[p] += 65536;
							overflowCount++;
						}
						else {
							ti.fluxValues[p++] += val;
							if (val > _fluxMax) _fluxMax = val;
							if (val < _fluxMin) _fluxMin = val;
						}
					}
					ti.revInfo[i].fluxCount -= overflowCount;
				}	// existing revolution
			}	// for all revolutions

			//for (int i = 0; i < revCount; i++) {
			//	if (ti.revInfo[i].indexTime != 0) {
			//		// set bufferIndex to the offset of the data
			//		bufferIndex = ti.revInfo[i].fluxOffset + trackHeaderPosition;
			//		// now set fluxOffset to offset in the "extracted data buffer"
			//		if (i > 0) ti.revInfo[i].fluxOffset = ti.revInfo[i - 1].fluxOffset + ti.revInfo[i - 1].fluxCount;
			//		else ti.revInfo[i].fluxOffset = 0;
			//		for (int j = 0; j < ti.revInfo[i].fluxCount; j++) {
			//			int p = j + ti.revInfo[i].fluxOffset;
			//			ti.fluxValues[p] = buffer[bufferIndex++] << 8;
			//			ti.fluxValues[p] += buffer[bufferIndex++];
			//			if (ti.fluxValues[p] > _fluxMax) _fluxMax = ti.fluxValues[p];
			//			if (ti.fluxValues[p] < _fluxMin) _fluxMin = ti.fluxValues[p];
			//		}
			//	}
			//}

			ti.checksum = readInt32(buffer, ref bufferIndex);

			// TODO checksum

			// now we transfer to the Global Disk structure
			MainWindow.Rev = 1;
			Global.Disk[track, side].fluxData = new FluxData();
			Global.Disk[track, side].fluxData.fluxValue = ti.fluxValues;
			Global.Disk[track, side].fluxData.fluxCount = ti.fluxValues.Count();

			Global.SampleClock = SampleClock;
			Global.Disk[track, side].fluxData.fluxMax = _fluxMax;
			Global.Disk[track, side].fluxData.fluxMin = _fluxMin;

			Global.Disk[track, side].revData = new RevData[revCount];
			for (int i = 0; i < revCount; i++) {
				Global.Disk[track, side].revData[i].revolutionTime = ti.revInfo[i].indexTime;
				Global.Disk[track, side].revData[i].fluxCount = ti.revInfo[i].fluxCount;
				Global.Disk[track, side].revData[i].indexTimePosition = ti.revInfo[i].indexTimePosition;
				Global.Disk[track, side].revData[i].fluxOffset = ti.revInfo[i].fluxOffset;
			}
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

		private static byte readByte(byte[] sbuf, ref int pos) {
			return sbuf[pos++];
		}

	}
}

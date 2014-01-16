/*!
@file PastiStruct.cs
<summary>Functions to write Pasti File</summary>

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
using System.Windows.Controls;
using System.Windows.Media;
using GlobalSpace;
using WD1772Space;

namespace PastiSpace {
	/// <summary>
	/// Pasti Writer Functions
	/// </summary>
	class PastiWriter {
		TextBox _infoBox;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="tb">TextBox used to display write information (debug)</param>
		/// <remarks>TextBox parameter can be ignored (normal case). Setting it is used for debug purpose</remarks>
		public PastiWriter(TextBox tb = null) {
			_infoBox = tb;
		}

		/// <summary>
		/// Status returned when reading Pasti file
		/// </summary>
		public enum FileStatus {
			/// <summary>File read correctly</summary>
			Ok = 0,
			/// <summary>Could not open file for reading or file not found</summary>
			FileNotFound,
			/// <summary>The header in the file is not RSY</summary>
			NotPastiFile,
			/// <summary>Not version 3</summary>
			UnsupportedVersion
		}


		/// <summary>
		/// Read a Pasti file and fills the Floppy structure
		/// </summary>
		/// <param name="fileName">Name of the Pasti file to read</param>
		/// <returns>The status</returns>
		public FileStatus writePasti(string fileName) {
			FileStream fs;

			// encode file into buffer
			List<byte> sbuf = new List<byte>();
			FileStatus status = encodePasti(sbuf);

			try {
				fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read);
			}
			catch (Exception exc) {
				Console.WriteLine("Error: {0}", exc.Message);
				return FileStatus.FileNotFound;
			}
			fs.Write(sbuf.ToArray(), 0, sbuf.Count);
			fs.Close();

			return status;		
		}


		/// <summary>
		/// Encode the information in a Floppy structure into a buffer using Pasti file structure
		/// </summary>
		/// <param name="buffer">Contains the data read from Pasti file</param>
		/// <param name="floppy">The Floppy structure to fill</param>
		/// <returns>The read status</returns>
		private FileStatus encodePasti(List<byte> buffer) {
			int start = 0;
			byte trackCount = 0;
			// skip not imaged and track with no sector probably unformatted
			for (int s = 0; s < 2; s++)
				for (int t = 0; t < 84; t++)
					if (Global.Disk[t, s].trackContent != null) 
						if (Global.Disk[t, s].sectorsContent != null) 
							if (Global.Disk[t, s].sectorsContent.Count != 0)
								trackCount++;

			FileDesc fd = new FileDesc();
			fd.version = 3;
			fd.revision = 2;
			fd.trackCount = trackCount;
			fd.tool = 0x0AA;

			writeByte(buffer, (byte)'R');			// File header
			writeByte(buffer, (byte)'S');
			writeByte(buffer, (byte)'Y');
			writeByte(buffer, 0);

			writeInt16(buffer, fd.version);			// version
			writeInt16(buffer, fd.tool);			// tool
			writeInt16(buffer, 0);					// reserved 1
			writeByte(buffer, fd.trackCount);		// trackCount
			writeByte(buffer, fd.revision);			// revision
			writeInt32(buffer, 0);					// reserved 2

			if (_infoBox != null) _infoBox.Clear();
			if (_infoBox != null) _infoBox.ScrollToHome();
			if (_infoBox != null) _infoBox.FontFamily = new FontFamily("Consolas");
			if (_infoBox != null) _infoBox.FontSize = 14;
			if (_infoBox != null) _infoBox.AppendText(fd.ToString());
			if (_infoBox != null) _infoBox.AppendText(String.Format(" - ({0}-{1})\n", start, buffer.Count-1));
						
			// Write all track records
			for (int track = 0; track < 84; track++) {
				for (int side = 0; side < 2; side++) {
					Global.DiskTrack tdata = Global.Disk[track, side];
					if (tdata.trackContent == null) 
						continue;
					if (tdata.sectorsContent == null)
						continue;
					int sectorCount = tdata.sectorsContent.Count;
					if (sectorCount == 0)
						continue;	// unformated

					int startTrackRecord = buffer.Count;
					TrackDesc tdesc = new TrackDesc();

					tdesc.recordSize = 16;			// initial value = size of TrackDesc
					//track.trackFlags = 0x0010;	// Only bit 5 set to 1 initially
					tdesc.sectorCount = (ushort)sectorCount;

					tdesc.trackLength = (ushort)tdata.trackContent.byteCount;
					tdesc.trackNumber = (byte)track;
					if (side == 1) tdesc.trackNumber |= 0x80;
					tdesc.trackType = 0;

					bool needSectorDesc = false;
					bool needTrackData = false;
					// do we have sector descriptors
					if (trackNeedSectorDesc(tdata)) {
						needSectorDesc = true;
						tdesc.trackFlags |= TrackDesc.TRK_SECT_DESC;
						tdesc.recordSize += (uint)(16 * tdesc.sectorCount);
					}

					// do we need fuzzy record
					for (int sect = 0; sect < sectorCount; sect++)
						if (tdata.sectorsContent[sect].fuzzyMask != null)
							tdesc.fuzzyCount += (uint)tdata.sectorsContent[sect].fuzzyMask.Bytes.Count - 3;
					tdesc.recordSize += tdesc.fuzzyCount;

					bool trkSync = false;
					// do we have track data
					if (trackNeedTrackData(tdata)) {
						needTrackData = true;
						if (trkSync) {
							tdesc.trackFlags |= TrackDesc.TRK_IMAGE | TrackDesc.TRK_SYNC;
							tdesc.recordSize += 4;	// header
						}
						else {
							tdesc.trackFlags |= TrackDesc.TRK_IMAGE;
							tdesc.recordSize += 2;	// header
						}
						tdesc.recordSize +=  (uint)tdata.trackContent.byteCount;
						// we write even number of bytes
						tdesc.recordSize += (uint)(tdata.trackContent.byteCount % 2);
					}

					// do we need timing record
					int timingEntries = 0;
					for (int sect = 0; sect < sectorCount; sect++)
						if (tdata.sectorsContent[sect].sectorIntraVariation)
							timingEntries += (tdata.sectorsContent[sect].dataBlock.byteCount - 3) / 16;

					if (timingEntries > 0)
						tdesc.recordSize += (uint)((2 * timingEntries) + 4);

					// we had up the sector data - 
					// we do not add up if we can take in track
					for (int sect = 0; sect < sectorCount; sect++)
						if (tdata.sectorsContent[sect].dataBlock.byteCount != 0)
							if (needTrackData && (tdata.trackContent.sectors[sect + 1].phases[(int)Phases.p_data].badSync != 0))
								tdesc.recordSize += (uint)tdata.sectorsContent[sect].buffer.Bytes.Count -3;

					// Track Descriptor is now ready => write it
					start = buffer.Count;
					writeInt32(buffer, tdesc.recordSize);
					writeInt32(buffer, tdesc.fuzzyCount);
					writeInt16(buffer, tdesc.sectorCount);
					writeInt16(buffer, tdesc.trackFlags);
					writeInt16(buffer, tdesc.trackLength);
					writeByte(buffer, tdesc.trackNumber);
					writeByte(buffer, tdesc.trackType);

					if (_infoBox != null) _infoBox.AppendText(tdesc.ToString());
					if (_infoBox != null) _infoBox.AppendText(String.Format(" ({0}-{1})\n", start, buffer.Count - 1));

					// write sector descriptor if needed
					if (needSectorDesc) {
						uint startSectorOffset = 0;
						uint trackOffset = 0;
						if (needTrackData)
							startSectorOffset = (uint)(tdata.trackContent.byteCount + (tdata.trackContent.byteCount % 2) + (trkSync ? 4 : 2));
						for (int sect = 0; sect < sectorCount; sect++) {
							SectorDesc sd = new SectorDesc();

							// we have data
							if ((tdata.sectorsContent[sect].dataBlock.byteCount) != 0) {

								// we check to see if we already have the same content in track data
								if (needTrackData && (tdata.trackContent.sectors[sect + 1].phases[(int)Phases.p_data].badSync == 0)) {
									// as some bytes may have been shifted we start from inner-gap till we find the 0xFB
									int firstByte = tdata.trackContent.sectors[sect + 1].phases[(int)Phases.p_inner].startByte;
									int lastByte = firstByte + tdata.trackContent.sectors[sect + 1].phases[(int)Phases.p_inner].byteCount + 10;
									for (int i = firstByte; i < lastByte; i++)
										if (tdata.trackContent.buffer.Bytes[i].dataByte == 0xFB) {
											trackOffset = (uint)i;
											break;
										}
								}
								if (trackOffset > 0) {
									sd.dataOffset = trackOffset + 3;
									trackOffset = 0;
								}
								else {
									sd.dataOffset = startSectorOffset;
									startSectorOffset += (uint)tdata.sectorsContent[sect].dataBlock.byteCount - 3;
								}
							}
							else
								sd.dataOffset = 0;	// dont care

							sd.bitPosition = (ushort)(tdata.sectorsContent[sect].idBlock.startBit / 2);
							sd.readTime = (ushort)(tdata.sectorsContent[sect].dataBlock.timeLength / 1000);
							sd.id = new IDField();
							sd.id.track = tdata.sectorsContent[sect].id.track;
							sd.id.side = tdata.sectorsContent[sect].id.head;
							sd.id.number = tdata.sectorsContent[sect].id.number;
							sd.id.size = tdata.sectorsContent[sect].id.size;
							sd.id.crc = tdata.sectorsContent[sect].id.crc;

							// compute flag
							byte fdcFlag = 0;
							if (tdata.sectorsContent[sect].dataBlock.byteCount == 0)
								fdcFlag |= SectorDesc.SECT_RNF;
							else {
								if (tdata.sectorsContent[sect].buffer.Bytes[0].dataByte != 0xFB)
									fdcFlag |= SectorDesc.REC_TYPE;
								if (tdata.sectorsContent[sect].dataBlock.crc != 0)
									fdcFlag |= SectorDesc.CRC_ERR;
							}	// these two flags are only meaningful if record has bytes
							if (tdata.sectorsContent[sect].fuzzyMask != null)
								fdcFlag |= SectorDesc.FUZZY_BITS;
							if (tdata.sectorsContent[sect].sectorIntraVariation)
								fdcFlag |= SectorDesc.BIT_WIDTH;

							sd.fdcFlags = fdcFlag;
							sd.reserved = 0;

							start = buffer.Count;
							writeInt32(buffer, sd.dataOffset);
							writeInt16(buffer, sd.bitPosition);
							writeInt16(buffer, sd.readTime);

							writeByte(buffer, sd.id.track);
							writeByte(buffer, sd.id.side);
							writeByte(buffer, sd.id.number);
							writeByte(buffer, sd.id.size);
							writeInt16(buffer, sd.id.crc);

							writeByte(buffer, sd.fdcFlags);
							writeByte(buffer, sd.reserved);

							if (_infoBox != null) _infoBox.AppendText(sd.ToString());
							if (_infoBox != null) _infoBox.AppendText(String.Format(" ({0}-{1})\n", start, buffer.Count - 1));
						}
					}

					// write fuzzy mask if needed
					for (int sect = 0; sect < sectorCount; sect++) {
						if (tdata.sectorsContent[sect].fuzzyMask != null) {
							start = buffer.Count;
							for (int i = 0; i < tdata.sectorsContent[sect].fuzzyMask.Bytes.Count - 3; i++)
								writeByte(buffer, (byte)~tdata.sectorsContent[sect].fuzzyMask.Bytes[i+1].dataByte);
							if (_infoBox != null) _infoBox.AppendText(String.Format("   Writing Fuzzy {0} bytes for sector {1} ({2}-{3})\n",
								tdata.sectorsContent[sect].fuzzyMask.Bytes.Count -3, tdata.sectorsContent[sect].id.number, start, buffer.Count - 1));
						}	// fuzzy found
					}	// all sect


					// the track data are located after the sector descriptor records & fuzzy record
					int track_data_start = buffer.Count; // store the position of data in track record
					if (_infoBox != null) _infoBox.AppendText(String.Format("   Start of Track data {0}\n", track_data_start));

					// write track data if needed
					if (trackNeedTrackData(tdata)) {
						start = buffer.Count;
						int firstSyncOffset = (tdata.trackContent.syncs[0] != null) ? tdata.trackContent.syncs[0].startByte : 0;
						if (trkSync)
							writeInt16(buffer, (ushort)(firstSyncOffset));
						writeInt16(buffer, (ushort)tdata.trackContent.byteCount);
						for (int i = 0; i < tdata.trackContent.byteCount; i++)
							writeByte(buffer, tdata.trackContent.buffer.Bytes[i].dataByte);
						// we write even number
						if ((tdata.trackContent.byteCount % 2) != 0)
							writeByte(buffer, 0xFF);

						if (_infoBox != null) _infoBox.AppendText(String.Format("      Writing Track {0}{1} bytes SyncPos={2} ({3}-{4})\n",
							tdata.trackContent.byteCount + ((trkSync) ? 4 : 2), ((tdata.trackContent.byteCount % 2) != 0) ? "(+1)" : "",
							firstSyncOffset, start, buffer.Count - 1));
					}

					// write all sectors data
					for (int sect = 0; sect < sectorCount; sect++) {
						start = buffer.Count;
						if (tdata.sectorsContent[sect].buffer.Bytes.Count != 0) {
							if (needTrackData && (tdata.trackContent.sectors[sect + 1].phases[(int)Phases.p_data].badSync == 0)) {
								if (_infoBox != null) _infoBox.AppendText(String.Format("      Sector {0} {1} bytes already in track data\n",
									tdata.sectorsContent[sect].id.number, tdata.sectorsContent[sect].buffer.Bytes.Count - 3));
							}
							else {
								for (int i = 0; i < tdata.sectorsContent[sect].buffer.Bytes.Count - 3; i++)
									writeByte(buffer, tdata.sectorsContent[sect].buffer.Bytes[i + 1].dataByte);
								if (_infoBox != null) _infoBox.AppendText(String.Format("      Writing Sector {0} {1} bytes ({2}-{3})\n",
									tdata.sectorsContent[sect].id.number, tdata.sectorsContent[sect].buffer.Bytes.Count - 3, start, buffer.Count - 1));
							}
						}
					}

					// write timing if necessary
					if (timingEntries > 0) {

						// write header
						start = buffer.Count;
						writeInt16(buffer, 0x05);
						writeInt16(buffer, (ushort)(2 * timingEntries + 4));
						if (_infoBox != null) _infoBox.AppendText(String.Format("   Writing Timing header Flag=5 size={0} ({1}-{2})\n", 2 * timingEntries + 4, start, buffer.Count - 1));
						// write timing
						for (int sect = 0; sect < sectorCount; sect++) {
							if (tdata.sectorsContent[sect].sectorIntraVariation) {
								int timingSum = 0;
								for (int i = 0; i < (tdata.sectorsContent[sect].dataBlock.byteCount - 3); i++) {
									timingSum += (tdata.sectorsContent[sect].buffer.Bytes[i + 1].density * 16);
									if (((i+1) % 16) == 0) {
										timingSum = timingSum / 4000;
										buffer.Add((byte)(timingSum << 8));
										buffer.Add((byte)timingSum);
										timingSum = 0;
									}
								}
								start = buffer.Count;
								if (_infoBox != null) _infoBox.AppendText(String.Format("   Writing Timing {0} values for sector {1} ({2}-{3})\n",
									tdata.sectorsContent[sect].dataBlock.byteCount - 3, tdata.sectorsContent[sect].id.number, start, buffer.Count - 1));
							}	// timing found
						}	// all sect
					}

					if (buffer.Count % 2 != 0) {
						writeByte(buffer, 0xFF);	// dummy extra byte
					}	// even

					Debug.Assert(buffer.Count == startTrackRecord + tdesc.recordSize, "Invalid Track Size",
						String.Format("Track {0} Current pointer position = {1} next track = {2}", tdesc.trackNumber, buffer.Count, startTrackRecord + tdesc.recordSize));
				} // for all tracks
			}	// for all side

			return FileStatus.Ok;

		}	// encode pasti


		private static void writeInt32(List<byte> buffer, uint val) {
			buffer.Add((byte)(val & 0xFF));
			buffer.Add((byte)((val >> 8) & 0xFF));
			buffer.Add((byte)((val >> 16) & 0xFF));
			buffer.Add((byte)((val >> 24) & 0xFF));
		}


		private static void writeInt16(List<byte> buffer, ushort val) {
			buffer.Add((byte)(val & 0xFF));
			buffer.Add((byte)(val >> 8));
		}

		private static void writeByte(List<byte> buffer, byte val) {
			buffer.Add(val);
		}


		private bool trackNeedSectorDesc(Global.DiskTrack tdata) {
			// here we test for
			// - All the sectors of the track are numbered sequentially from 1 to n
			// - All the sectors use valid sector numbers from 0x00-0xF4
			// - All the sectors of the track have unique sector number
			// - All sectors in the track have standard timing values
			// - invalid track number
			// - Special Flags: No data, CRC, DDAM (cover sector no data)

		//	int secnum = 1;
		//	for (int sect = 0; sect < tdata.sectorCount; sect++) {
		//		// sequential sector number 1..n
		//		if (tdata.sectors[sect].id.number != secnum++) 
		//			tdata.standardSectors = false;

		//		// duplicate sector
		//		for (int j = sect + 1; j < tdata.sectorCount; j++)
		//			if (tdata.sectors[sect].id.number == tdata.sectors[j].id.number)
		//				tdata.standardSectors = false;

		//		// sector with invalid sector number
		//		if ((tdata.sectors[sect].id.number >= 0xF5) && (tdata.sectors[sect].id.number <= 0xF7))
		//			tdata.standardSectors = false;

		//		// sector with invalid track number
		//		if (tdata.sectors[sect].id.track != tdata.number)
		//			tdata.standardSectors = false;

		//		// sector with invalid side/head
		//		if (tdata.sectors[sect].id.side != tdata.side)
		//			tdata.standardSectors = false;

		//		// sector with invalid size
		//		if (tdata.sectors[sect].id.size > 3)
		//			tdata.standardSectors = false;

		//		// short long sector time
		//		if (tdata.sectors[sect].readTime != 0)
		//			tdata.standardSectors = false;

		//		// FDC Flags - Here we read the saved flags

		//		// RNF = sector with no data
		//		//if ((track.sectors[sect].fdcFlags & SectorDesc.SECT_RNF) != 0)
		//		if (tdata.sectors[sect].sectorData == null)
		//			tdata.standardSectors = false;

		//		// CRC error = ID / Data
		//		if ((tdata.sectors[sect].fdcFlags & SectorDesc.CRC_ERR) != 0)
		//			tdata.standardSectors = false;

		//		// Record type = DDAM
		//		if ((tdata.sectors[sect].fdcFlags & SectorDesc.REC_TYPE) != 0)
		//			tdata.standardSectors = false;
				
		//		// FDC - Fuzzy
		//		if (tdata.sectors[sect].sectorFuzzy != null)
		//			tdata.standardSectors = false;

		//		// FDC - Timing
		//		if (tdata.sectors[sect].sectorTiming != null)
		//			tdata.standardSectors = false;
		//	}
		//	if (trackNeedTrackData(tdata))
		//		return true;
		//	return !tdata.standardSectors;
			return true;
		}


		private bool trackNeedTrackData(Global.DiskTrack tdata) {
			// We need Optional Track Data when:
			// - Single Data Segment = ID found but no data
			// - Invalid Data in Gap ?
			// - Shifted Track: Data over Index + Data beyond Index + ID over index
			// - Invalid sync mark sequence
			// - Sector within Sector
			// - No Flux Area
			// - Short long track ??
			// - SWS
			// - no data

			// for now we are cheating by using the TRK_IMAGE flag
			//return !track.standardTrack;
			return true;
		}

	}

}

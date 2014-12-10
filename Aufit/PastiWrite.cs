/*!
@file PastiStruct.cs
<summary>Functions to write Pasti File</summary>

<div class="jlg">Copyright (C) 2013-2014 Jean Louis-Guerin\n\n
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
using GlobalPackage;
using WD1772Package;
using Aufit;

namespace PastiPackage {
	/// <summary>Pasti Writer Class</summary>
	class PastiWriter {
		TextBox _infoBox;

		// when set to true the track data header contains sync position
		readonly bool TRK_SYNC = false;

		// when set to true we try to use track data for sector
		readonly bool OPT_SECT = true;

		// when set to true we simulate fuzzy bits on Rob Northen ?
		readonly bool SIM_FUZZY = false;


		/// <summary>Constructor</summary>
		/// <param name="tb">TextBox used to display write information (debug information)</param>
		/// <remarks>TextBox parameter can be ignored (normal case). Setting it is used for debug purpose</remarks>
		public PastiWriter(TextBox tb = null) {
			_infoBox = tb;
		}


		/// <summary>Write the Pasti file</summary>
		/// <param name="fileName">Name of the Pasti file to write</param>
		/// <param name="forceDs">When set to true this force writing Pasti 
		/// information for a Double sided floppy disk</param>
		/// <returns>True if succeeds; false if fails</returns>
		public bool writePasti(string fileName, bool forceDs) {
			FileStream fs;

			// encode file into buffer
			List<byte> sbuf = new List<byte>();
			bool status = encodePasti(sbuf, forceDs);

			try {
				fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read);
			}
			catch (Exception exc) {
				Console.WriteLine("Error: {0}", exc.Message);
				return false;
			}
			fs.Write(sbuf.ToArray(), 0, sbuf.Count);
			fs.Close();
			return status;		
		}


		/// <summary>
		/// Encode the Floppy content into a buffer using Pasti file structure
		/// </summary>
		/// <param name="buffer">Contains the data to be written to Pasti file</param>
		/// <returns>Always returns true!</returns>
		private bool encodePasti(List<byte> buffer, bool forceDs) {
			int start = 0;
			byte trackCount = 0;
			int sideCount = 1;	// single sided

			// check for double sided - we check that we have some sectors on side 1
			if (forceDs) sideCount = 2;
			else {
				for (int track = 0; track < 84; track++)
					if ((Global.Disk[track, 1].track != null) && (Global.Disk[track, 1].sectors != null) && (Global.Disk[track, 1].sectors.Count != 0)) {
						sideCount = 2;	// found at least one sector
						break;
					}
			}

			// number of track descriptor
			for (int side = 0; side < sideCount; side++)
				for (int track = 0; track < 84; track++)
					if (Global.Disk[track, side].track != null)
						trackCount++;
				
			FileDesc fd = new FileDesc();
			fd.version = 3;
			fd.revision = 2;
			fd.trackCount = trackCount;
			fd.tool = 0x0AA;						// written with Aufit Tool

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
			if (_infoBox != null) _infoBox.FontSize = 12;
			if (_infoBox != null) _infoBox.AppendText(fd.ToString());
			if (_infoBox != null) _infoBox.AppendText(String.Format(" - ({0}-{1})\n", start, buffer.Count-1));
						
			// Write all track records
			for (int trackNum = 0; trackNum < 84; trackNum++) {
				for (int side = 0; side < sideCount; side++) {
					Global.DiskTrack dt = Global.Disk[trackNum, side];
					if (dt.track == null) 
						continue;

					int sectorCount = 0;
					if (dt.sectors != null) 
						sectorCount = dt.sectors.Count;

					int startTrackRecord = buffer.Count;
					TrackDesc tdesc = new TrackDesc();

					tdesc.recordSize = 16;			// initial value = size of TrackDesc
					tdesc.trackFlags = 0x0020;		// Bit 5 has to be set to 1 for new format
					tdesc.sectorCount = (ushort)sectorCount;

					tdesc.trackLength = (ushort)dt.track.info.byteCount;
					tdesc.trackNumber = (byte)trackNum;
					if (side == 1) tdesc.trackNumber |= 0x80;
					tdesc.trackType = 0;

					bool needSectorDesc = false;
					bool needTrackData = false;
					// do we have sector descriptors
					if (trackNeedSectorDesc(dt)) {
						needSectorDesc = true;
						tdesc.trackFlags |= TrackDesc.TRK_SECT_DESC;
						tdesc.recordSize += (uint)(16 * tdesc.sectorCount);
					}

					// do we need fuzzy record
					for (int sect = 0; sect < sectorCount; sect++) {
						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.fuzzy != null))
							tdesc.fuzzyCount += (uint)dt.sectors[sect].data.fuzzy.Count();
						else if (SIM_FUZZY && (dt.sectors[sect].data.crc != 0) && Bit.testBits(dt.sectors[sect].flags, Sector.WIDTH_ABOVE)) {
							// Rob Northen protection we force fuzzy bits if not there
							int dataSize = Global.sectSize(dt.sectors[sect].id.size);
							dt.sectors[sect].data.fuzzy = new byte[dataSize];
							for (int i = 0; i < dataSize; i++) {
								dt.sectors[sect].data.fuzzy[i] = (byte)((i < 30) ? 0x00 : 0xFF);
							}
							tdesc.fuzzyCount += (uint)dataSize;
						}
					}
					tdesc.recordSize += tdesc.fuzzyCount;

					// do we have track data
					if (trackNeedTrackData(dt)) {
						needTrackData = true;
						if (TRK_SYNC) {
							tdesc.trackFlags |= TrackDesc.TRK_IMAGE | TrackDesc.TRK_SYNC;
							tdesc.recordSize += 4;	// header
						}
						else {
							tdesc.trackFlags |= TrackDesc.TRK_IMAGE;
							tdesc.recordSize += 2;	// header
						}
						tdesc.recordSize +=  (uint)dt.track.info.byteCount;
						// we write even number of bytes (padding)
						tdesc.recordSize += (uint)(dt.track.info.byteCount % 2);
					}

					// do we need timing record
					int timingEntries = 0;
					for (int sect = 0; sect < sectorCount; sect++)
						if (Bit.testBits(dt.sectors[sect].flags, Sector.MACRODOS))
							timingEntries += Global.sectSize(dt.sectors[sect].id.size) / 16;

					if (timingEntries > 0)
						tdesc.recordSize += (uint)((2 * timingEntries) + 4);

					// we add up the sector data
					for (int sect = 0; sect < sectorCount; sect++) {
						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.buffer.Bytes.Count != 0)) {
							// we do not add up if we can use the info in track
							if (!OPT_SECT || !needTrackData || ((dt.sectors[sect].flags & Sector.SEC_EQ_TRACK) == 0))
								tdesc.recordSize += (uint)dt.sectors[sect].data.buffer.Bytes.Count;
						}
					}

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
						if (needTrackData)
							startSectorOffset = (uint)(dt.track.info.byteCount + (dt.track.info.byteCount % 2) + (TRK_SYNC ? 4 : 2));
						for (int sect = 0; sect < sectorCount; sect++) {
							SectorDesc sd = new SectorDesc();

							// we have data
							if ((dt.sectors[sect].data != null) && ((dt.sectors[sect].data.buffer.Bytes.Count) != 0)) {
								
								// we check to see if we already have the same content in track data
								if (OPT_SECT && needTrackData && ((dt.sectors[sect].flags & Sector.SEC_EQ_TRACK) != 0))
									// TODO Why +1 ? already done in readTrack?
									sd.dataOffset = (uint)dt.sectors[sect].trackOffset + 1;
								else {
									sd.dataOffset = startSectorOffset;
									startSectorOffset += (uint)dt.sectors[sect].data.buffer.Bytes.Count;
								}	// no optimization
							}	// we have data
							else
								sd.dataOffset = 0;	// don't care

							sd.bitPosition = (ushort)(dt.sectors[sect].id.info.startBit / 2);
							sd.readTime = (ushort)(dt.sectors[sect].data.info.timeLength / 1000);
							sd.id = new IDField();
							sd.id.track = dt.sectors[sect].id.track;
							sd.id.side = dt.sectors[sect].id.head;
							sd.id.number = dt.sectors[sect].id.number;
							sd.id.size = dt.sectors[sect].id.size;
							sd.id.crc = dt.sectors[sect].id.crc;

							// compute flag
							byte fdcFlag = 0;
							if (dt.sectors[sect].data == null) {
								fdcFlag |= SectorDesc.SECT_RNF;
								if (dt.sectors[sect].id.info.crc != 0)
									fdcFlag |= SectorDesc.CRC_ERR;
							}
							else {
								if (dt.sectors[sect].data.iam != 0xFB)
									fdcFlag |= SectorDesc.REC_TYPE;
								if (dt.sectors[sect].data.info.crc != 0)
									fdcFlag |= SectorDesc.CRC_ERR;
							}	// these two flags are only meaningful if record has bytes
							if (dt.sectors[sect].data.fuzzy != null)
								fdcFlag |= SectorDesc.FUZZY_BITS;
							if (Bit.testBits(dt.sectors[sect].flags, Sector.MACRODOS))
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
						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.fuzzy != null)) {
							start = buffer.Count;
							for (int i = 0; i < dt.sectors[sect].data.fuzzy.Count(); i++)
								writeByte(buffer, (byte)~dt.sectors[sect].data.fuzzy[i]);
							if (_infoBox != null) _infoBox.AppendText(String.Format("   Writing Fuzzy {0} bytes for sector {1} ({2}-{3})\n",
								dt.sectors[sect].data.fuzzy.Count(), dt.sectors[sect].id.number, start, buffer.Count - 1));
						}	// fuzzy found
					}	// all sect


					// the track data are located after the sector descriptor records & fuzzy record
					int track_data_start = buffer.Count; // store the position of data in track record
					if (_infoBox != null) _infoBox.AppendText(String.Format("   Start of Track data {0}\n", track_data_start));

					// write track data if needed
					if (needTrackData) {
						start = buffer.Count;
						if (TRK_SYNC)
							writeInt16(buffer, (ushort)(dt.track.firstSyncOffset));
						writeInt16(buffer, (ushort)dt.track.info.byteCount);
						for (int i = 0; i < dt.track.info.byteCount; i++)
							writeByte(buffer, dt.track.buffer.Bytes[i].dataByte);
						// we write even number
						if ((dt.track.info.byteCount % 2) != 0)
							writeByte(buffer, 0xFF);

						if (_infoBox != null) _infoBox.AppendText(String.Format("      Writing Track {0}{1} bytes SyncPos={2} ({3}-{4})\n",
							dt.track.info.byteCount + ((TRK_SYNC) ? 4 : 2), ((dt.track.info.byteCount % 2) != 0) ? "(+1)" : "",
							dt.track.firstSyncOffset, start, buffer.Count - 1));
					}

					// write all sectors data
					for (int sect = 0; sect < sectorCount; sect++) {
						start = buffer.Count;
						if (dt.sectors[sect].data.info.byteCount != 0) {
							if (OPT_SECT && needTrackData && ((dt.sectors[sect].flags & Sector.SEC_EQ_TRACK) !=0)) {
								if (_infoBox != null) _infoBox.AppendText(String.Format("      Sector {0} {1} bytes already in track data @{2}\n",
									dt.sectors[sect].id.number, dt.sectors[sect].data.buffer.Bytes.Count, dt.sectors[sect].trackOffset));
							}
							else {
								for (int i = 0; i < dt.sectors[sect].data.buffer.Bytes.Count; i++)
									writeByte(buffer, dt.sectors[sect].data.buffer.Bytes[i].dataByte);
								if (_infoBox != null) _infoBox.AppendText(String.Format("      Writing Sector {0} {1} bytes ({2}-{3})\n",
									dt.sectors[sect].id.number, dt.sectors[sect].data.buffer.Bytes.Count, start, buffer.Count - 1));
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
							if (Bit.testBits(dt.sectors[sect].flags, Sector.MACRODOS)) {
								int timingSum = 0;
								for (int i = 0; i < (dt.sectors[sect].data.buffer.Bytes.Count); i++) {
									timingSum += (dt.sectors[sect].data.buffer.Bytes[i].bitWidth * 16);
									if (((i+1) % 16) == 0) {
										timingSum = timingSum / 4000;
										buffer.Add((byte)(timingSum << 8));
										buffer.Add((byte)timingSum);
										timingSum = 0;
									}
								}
								start = buffer.Count;
								if (_infoBox != null) _infoBox.AppendText(String.Format("   Writing Timing {0} values for sector {1} ({2}-{3})\n",
									dt.sectors[sect].data.buffer.Bytes.Count, dt.sectors[sect].id.number, start, buffer.Count - 1));
							}	// timing found
						}	// all sect
					}

					Debug.Assert(buffer.Count == startTrackRecord + tdesc.recordSize, "Invalid Track Size",
						String.Format("Track {0} Current pointer position = {1} next track = {2}", tdesc.trackNumber, buffer.Count, startTrackRecord + tdesc.recordSize));
				} // for all tracks
			}	// for all side

			return true;

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
			return true;
		}

	}

}

/*!
@file PastiStruct.cs
<summary>Functions to write Pasti File</summary>

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
using System.Windows.Controls;
using System.Windows.Media;
using GlobalPackage;
using WD1772Package;
using Aufit;

namespace PastiPackage {
	/// <summary>Pasti Writer Class</summary>
	class PastiWriter {
		TextBox _saveImageBox;

		// when set to true the track data header contains sync position
		bool _trkSync = false;

		bool _debug = false;
		bool _forceTrack = false;



		/// <summary>Constructor</summary>
		/// <param name="tb">TextBox used to display write information (debug information)</param>
		/// <remarks>TextBox parameter: where to display info</remarks>
		public PastiWriter(TextBox tb) {
			_saveImageBox = tb;
		}


		/// <summary>Write the Pasti file</summary>
		/// <param name="fileName">Name of the Pasti file to write</param>
		/// <param name="forceDs">When set to true this force writing Pasti 
		/// information for a Double sided floppy disk</param>
		/// <returns>True if succeeds; false if fails</returns>
		public bool writePasti(string fileName, ExportFlags flags) {
			FileStream fs;

			_debug = flags.HasFlag(ExportFlags.DEBUG);
			_trkSync = flags.HasFlag(ExportFlags.SYNC_HEAD);

			// encode file into buffer
			List<byte> sbuf = new List<byte>();
			encodePasti(sbuf, flags);

			try {
				fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read);
			}
			catch (Exception exc) {
				Console.WriteLine("Error: {0}", exc.Message);
				return false;
			}
			fs.Write(sbuf.ToArray(), 0, sbuf.Count);
			fs.Close();
			return true;		
		}


		/// <summary>
		/// Encode the Floppy content into a buffer using Pasti file structure
		/// </summary>
		/// <param name="buffer">Contains the data to be written to Pasti file</param>
		/// <returns>Always returns true!</returns>
		private void encodePasti(List<byte> buffer, ExportFlags  flags) {
			int start = 0;
			byte trackCount = 0;
			int sideCount = 1;  // single sided

			bool forceDs = flags.HasFlag(ExportFlags.FORCE_DS);
			bool optimize = flags.HasFlag(ExportFlags.OPTIMIZE);
			_forceTrack = flags.HasFlag(ExportFlags.FORCE_TRACK);

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
					if ((Global.Disk[track, side].track != null) && 
						(((Global.Disk[track, side].sectors != null) && (Global.Disk[track, side].sectors.Count > 0)) || (track < 80))
						)
						trackCount++;

			_saveImageBox.AppendText(String.Format("{0} Sided Diskette with a total of {1} tracks\n", (sideCount == 1) ? "Single" : "Double", trackCount));
				
			FileDesc fd = new FileDesc();
			fd.version = 3;
			fd.revision = 2;
			fd.trackCount = trackCount;
			fd.tool = 0x0AF;						// written with Aufit Tool

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

			if (_debug) {
				_saveImageBox.AppendText(fd.ToString());
				_saveImageBox.AppendText(String.Format(" - ({0}-{1})\n", start, buffer.Count - 1));
			}
						
			// Write all track records
			for (int track = 0; track < 84; track++) {
				for (int side = 0; side < sideCount; side++) {
					Global.DiskTrack dt = Global.Disk[track, side];
					if (dt.track == null)  continue;

					int sectorCount = 0;
					if (dt.sectors != null) 
						sectorCount = dt.sectors.Count;
					if ((track >= 80) && (sectorCount == 0)) 
						continue;

					int startTrackRecord = buffer.Count;
					TrackDesc tdesc = new TrackDesc();

					tdesc.recordSize = 16;			// initial value = size of TrackDesc
					tdesc.trackFlags = 0x0020;		// Bit 5 has to be set to 1 for new format
					tdesc.sectorCount = (ushort)sectorCount;

					tdesc.trackLength = (ushort)dt.track.buffer.DataByte.Count;
					tdesc.trackNumber = (byte)track;
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
						// if we recover data from other revolution we disable data in track
						if (dt.sectors[sect].dataRecovered != null)
							dt.sectors[sect].flags &= ~SectorFlags.SEC_IN_TRACK;
						// recovered or good CRC => no fuzzy
						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.fuzzy != null) &&
							((dt.sectors[sect].dataRecovered != null) || (dt.sectors[sect].data.info.crc == 0))) {
							dt.sectors[sect].data.fuzzy = null;
							if (_debug) {
								_saveImageBox.AppendText(String.Format("Track {0:D2}.{1:D1} ", track, side));
								_saveImageBox.AppendText(String.Format("Reseting fuzzy for sector {0} - CRC={1} Recover={2}\n", sect,
								(dt.sectors[sect].data.info.crc == 0) ? "Good" : "Bad", (dt.sectors[sect].dataRecovered != null) ? "Yes" : "No"));
							}
						}

						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.fuzzy != null) && (dt.sectors[sect].dataRecovered == null))
							tdesc.fuzzyCount += (uint)dt.sectors[sect].data.fuzzy.Count() - 6;
					}
					tdesc.recordSize += tdesc.fuzzyCount;

					// do we have track data
					if (trackNeedTrackData(track, side)) {
						needTrackData = true;
						if (_trkSync) {
							tdesc.trackFlags |= TrackDesc.TRK_IMAGE | TrackDesc.TRK_SYNC;
							tdesc.recordSize += 4;	// header
						}
						else {
							tdesc.trackFlags |= TrackDesc.TRK_IMAGE;
							tdesc.recordSize += 2;	// header
						}
						tdesc.recordSize +=  (uint)dt.track.buffer.DataByte.Count;
						// we write even number of bytes (padding)
						tdesc.recordSize += (uint)(dt.track.buffer.DataByte.Count % 2);
					}

					// do we need timing record
					int timingEntries = 0;
					for (int sect = 0; sect < sectorCount; sect++)
						if ((dt.sectors[sect].flags.HasFlag(SectorFlags.WIDTH_VARIATION)) && (dt.sectors[sect].data != null))
							timingEntries += Global.sectSize(dt.sectors[sect].id.Length) / 16;

					if (timingEntries > 0)
						tdesc.recordSize += (uint)((2 * timingEntries) + 4);

					// we add up the sector data
					for (int sect = 0; sect < sectorCount; sect++) {
						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.buffer.DataByte.Count != 0)) {
							// we do not add up if we can use the info in track
							if (!optimize || !needTrackData || !dt.sectors[sect].flags.HasFlag(SectorFlags.SEC_IN_TRACK))
								tdesc.recordSize += (uint)dt.sectors[sect].data.buffer.DataByte.Count - 6;
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

					if (_debug) {
						_saveImageBox.AppendText(tdesc.ToString());
						_saveImageBox.AppendText(String.Format(" ({0}-{1})\n", start, buffer.Count - 1));
					}

					// write sector descriptor if needed
					if (needSectorDesc) {
						uint startSectorOffset = 0;
						if (needTrackData)
							startSectorOffset = (uint)(dt.track.buffer.DataByte.Count + (dt.track.buffer.DataByte.Count % 2) + (_trkSync ? 4 : 2));
						for (int sect = 0; sect < sectorCount; sect++) {
							SectorDesc sd = new SectorDesc();

							// we have data
							if ((dt.sectors[sect].data != null) && ((dt.sectors[sect].data.buffer.DataByte.Count) != 0)) {
								
								// we check to see if we already have the same content in track data
								if (optimize && needTrackData && dt.sectors[sect].flags.HasFlag(SectorFlags.SEC_IN_TRACK))
									// TODO Why +1 ? already done in readTrack?
									sd.dataOffset = (uint)dt.sectors[sect].trackOffset + 1;
								else {
									sd.dataOffset = startSectorOffset;
									startSectorOffset += (uint)dt.sectors[sect].data.buffer.DataByte.Count - 6;
								}	// no optimization
							}	// we have data
							else
								sd.dataOffset = 0;	// don't care

							sd.bitPosition = (ushort)(dt.sectors[sect].id.info.startBit / 2);
							sd.readTime = (ushort)((dt.sectors[sect].data != null) ? (dt.sectors[sect].data.info.timeLength / 1000) : 0);
							sd.id = new IDField();
							sd.id.track = dt.sectors[sect].id.Track;
							sd.id.side = dt.sectors[sect].id.Head;
							sd.id.number = dt.sectors[sect].id.Sector;
							sd.id.size = dt.sectors[sect].id.Length;
							sd.id.crc = dt.sectors[sect].id.CRC;

							// compute flag
							byte fdcFlag = 0;
							if (dt.sectors[sect].data == null) {
								fdcFlag |= SectorDesc.SECT_RNF;
								if (dt.sectors[sect].id.info.crc != 0)
									fdcFlag |= SectorDesc.CRC_ERR;
							}
							else {
								if (dt.sectors[sect].data.DAM != 0xFB)
									fdcFlag |= SectorDesc.REC_TYPE;
								if ((dt.sectors[sect].data.info.crc != 0) && (dt.sectors[sect].dataRecovered == null)) 
									fdcFlag |= SectorDesc.CRC_ERR;
							}	// these two flags are only meaningful if record has bytes
							if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.fuzzy != null))
								fdcFlag |= SectorDesc.FUZZY_BITS;
							if (dt.sectors[sect].flags.HasFlag(SectorFlags.WIDTH_VARIATION))
								fdcFlag |= SectorDesc.BIT_WIDTH;

							if (((fdcFlag & SectorDesc.CRC_ERR) != 0) && ((fdcFlag & SectorDesc.FUZZY_BITS) == 0) && ((fdcFlag & SectorDesc.SECT_RNF) != 0))
								_saveImageBox.AppendText(String.Format("T{0:D2}.{1:D1} S{2} CRC error on non fuzzy sector. Check image quality\n",
										track, side, dt.sectors[sect].id.Sector));

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

							if (_debug) {
								_saveImageBox.AppendText(sd.ToString());
								_saveImageBox.AppendText(String.Format(" ({0}-{1})\n", start, buffer.Count - 1));
							}
						}
					}

					// write fuzzy mask if needed
					for (int sect = 0; sect < sectorCount; sect++) {
						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.fuzzy != null) && (dt.sectors[sect].dataRecovered == null) && (dt.sectors[sect].data.info.crc != 0)) {
							start = buffer.Count;
							for (int i = 4; i < dt.sectors[sect].data.fuzzy.Count() - 2; i++)
								writeByte(buffer, (byte)~dt.sectors[sect].data.fuzzy[i]);
							if (_debug) _saveImageBox.AppendText(String.Format("   Writing Fuzzy {0} bytes for sector {1} ({2}-{3})\n",
								dt.sectors[sect].data.fuzzy.Count() - 6, dt.sectors[sect].id.Sector, start, buffer.Count - 1));
							else
								_saveImageBox.AppendText(String.Format("T{0:D2}.{1:D1} found fuzzy sector {2}\n",
										track, side, dt.sectors[sect].id.Sector));
						}	// fuzzy found
					}	// all sect


					// the track data are located after the sector descriptor records & fuzzy record
					int track_data_start = buffer.Count; // store the position of data in track record
					if (_debug) _saveImageBox.AppendText(String.Format("   Start of Track data {0}\n", track_data_start));

					// write track data if needed
					if (needTrackData) {
						start = buffer.Count;
						if (_trkSync)
							writeInt16(buffer, (ushort)(dt.track.firstSyncOffset));
						writeInt16(buffer, (ushort)dt.track.buffer.DataByte.Count);
						for (int i = 0; i < dt.track.buffer.DataByte.Count; i++)
							writeByte(buffer, dt.track.buffer.DataByte[i]);
						// we write even number
						if ((dt.track.buffer.DataByte.Count % 2) != 0)
							writeByte(buffer, 0xFF);

						if (_debug) _saveImageBox.AppendText(String.Format("      Writing Track {0}{1} bytes SyncPos={2} ({3}-{4})\n",
							dt.track.buffer.DataByte.Count + ((_trkSync) ? 4 : 2), ((dt.track.buffer.DataByte.Count % 2) != 0) ? "(+1)" : "",
							dt.track.firstSyncOffset, start, buffer.Count - 1));
					}

					// write all sectors data
					for (int sect = 0; sect < sectorCount; sect++) {
						start = buffer.Count;
						if ((dt.sectors[sect].data != null) && (dt.sectors[sect].data.buffer.DataByte.Count != 0)) {
							if (optimize && needTrackData && dt.sectors[sect].flags.HasFlag(SectorFlags.SEC_IN_TRACK)) {
								if (_debug) _saveImageBox.AppendText(String.Format("      Sector {0} {1} bytes already in track data @{2}\n",
									dt.sectors[sect].id.Sector, dt.sectors[sect].data.buffer.DataByte.Count - 6, dt.sectors[sect].trackOffset));
							}
							else {
								for (int i = 4; i < dt.sectors[sect].data.buffer.DataByte.Count - 2; i++) {
									if (dt.sectors[sect].dataRecovered != null)
										writeByte(buffer, dt.sectors[sect].dataRecovered.buffer.DataByte[i]);
									else
										writeByte(buffer, dt.sectors[sect].data.buffer.DataByte[i]);
								}
								if (_debug) 
									_saveImageBox.AppendText(String.Format("      Writing Sector {0} {1} bytes ({2}-{3}) {4}\n",
									dt.sectors[sect].id.Sector, dt.sectors[sect].data.buffer.DataByte.Count - 6, start, buffer.Count - 1,
									(dt.sectors[sect].dataRecovered != null) ? "recovered" : ""));
								else if (dt.sectors[sect].dataRecovered != null) {
									_saveImageBox.AppendText(String.Format("T{0:D2}.{1:D1} recovered Sector {2}\n", 
										track, side, dt.sectors[sect].id.Sector));
								}
							}
						}
					}

					// write timing if necessary
					if (timingEntries > 0) {

						// write header
						start = buffer.Count;
						writeInt16(buffer, 0x05);
						writeInt16(buffer, (ushort)(2 * timingEntries + 4));
						if (_debug) _saveImageBox.AppendText(String.Format("   Writing Timing header Flag=5 size={0} ({1}-{2})\n", 2 * timingEntries + 4, start, buffer.Count - 1));
						// write timing
						for (int sect = 0; sect < sectorCount; sect++) {
							if ((dt.sectors[sect].flags.HasFlag(SectorFlags.WIDTH_VARIATION)) && (dt.sectors[sect].data != null)) {
								int timingSum = 0;
								for (int i = 4; i < (dt.sectors[sect].data.buffer.DataByte.Count - 2); i++) {
									timingSum += (dt.sectors[sect].data.buffer.BitWidth[i] * 16);
									if (((i+1) % 16) == 0) {
										timingSum = timingSum / 4000;
										buffer.Add((byte)(timingSum << 8));
										buffer.Add((byte)timingSum);
										timingSum = 0;
									}
								}
								start = buffer.Count;
								if (_debug) _saveImageBox.AppendText(String.Format("   Writing Timing {0} values for sector {1} ({2}-{3})\n",
									dt.sectors[sect].data.buffer.DataByte.Count - 6, dt.sectors[sect].id.Sector, start, buffer.Count - 1));
								else
									_saveImageBox.AppendText(String.Format("T{0:D2}.{1:D1} writing timing information for sector {2} \n",
										track, side, dt.sectors[sect].id.Sector));
							}	// timing found
						}	// all sect
					}

					Debug.Assert(buffer.Count == startTrackRecord + tdesc.recordSize, "Invalid Track Size",
						String.Format("Track {0} Current pointer position = {1} next track = {2}", tdesc.trackNumber, buffer.Count, startTrackRecord + tdesc.recordSize));
				} // for all tracks
			}	// for all side

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


		private bool trackNeedTrackData(int track, int side) {
			Global.DiskTrack dt = Global.Disk[track, side];
			bool needTrack = false;
			// We need Optional Track Data when:
			// - Single Data Segment = ID found but no data
			// - Invalid Data in Gap ?
			// - Shifted Track: Data over Index + Data beyond Index + ID over index
			// - Invalid sync mark sequence
			// - Sector within Sector
			// - No Flux Area
			// - SWS
			// - no data

			// CHECKED
			// short/long track
			needTrack = dt.track.flags.HasFlag(TrackFlag.SHORT_TRACK) ||
						dt.track.flags.HasFlag(TrackFlag.LONG_TRACK) ||
						dt.track.flags.HasFlag(TrackFlag.NFA_IN_TRACK) ||
						dt.track.flags.HasFlag(TrackFlag.DATA_TRACK) ||

						dt.track.flags.HasFlag(TrackFlag.BAD_DATA_IN_GAP) ||
						//dt.track.flags.HasFlag(TrackFlag.HIDDEN_DATA) ||
						dt.track.flags.HasFlag(TrackFlag.INVALID_SYNC_SEQ);

			if (_forceTrack) return true;
			return needTrack;
		}

	}

}

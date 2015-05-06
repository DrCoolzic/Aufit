/*!
@file Protections.cs
<summary>Everything related to Protections</summary>

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WD1772Package;
using GlobalPackage;
using System.Windows.Controls;

namespace Aufit {
	/// <summary>
	/// Check for Protection
	/// </summary>
	class Protections {
		private struct PInfo {
			public int track;
			public int side;
			public int sect;
			public TextBox text;
		}

		/// <summary>
		/// Checks and eventually displays all the protections for a track
		/// </summary>
		/// <param name="track">Track number</param>
		/// <param name="side">Side Number</param>
		/// <param name="text">Optional textbox to display protections</param>
		/// <remarks>We check the following \n
		/// TODO UPDATE LIST
		/// Track protections:
		/// - short track
		/// - long track
		/// - Track has No FLux Area (done in readTrack())
		/// - Track has Data Over Index (done in readTrack())
		/// - Track has Data Beyond Index (done in readTrack())
		/// - Track has ID over Index (done in readTrack())		
		/// - Extra / Missing track	
		/// .
		/// Sector Protections
		/// - Sector sequentially numbered (for Pasti)
		/// - sector has Invalid Sector number 
		/// - Duplicate Sectors
		/// - Non standard sector size (for Pasti)
		/// - Sector has invalid Data into Gap
		/// - Sector has invalid Sync Mark Sequence (invalid seq, short seq, long seq)		
		/// - Sector Data has CRC error
		/// - Sector ID has CRC error
		/// - Sector has Fuzzy Bytes
		/// - Long sector
		/// - short sector
		/// - intra-sector bit width variation
		/// - Sector with no Data
		/// - Sector with Invalid track number
		/// - Sector contains Sector (done in readTrack())
		/// .
		/// TODO? IIS / SDS / NonStd DAM & IDAM / data into GAP
		/// </remarks>
		public static void checkProtection(int track, int side, TextBox text = null) {
			Global.DiskTrack dt = Global.Disk[track, side];
			if ((dt.track == null) || (dt.sectors.Count == 0))
				return;

			PInfo info = new PInfo();
			info.track = track;
			info.side = side;
			info.text = text;
			info.sect = -1;


			//////////////////////
			// Track Protections
			//////////////////////
			// normal track is 200000 / 32 = 6250 bytes - short track N-3% = 6062 - long track = N+3% = 6437
			if (dt.track.info.byteCount < 6062) {
				dt.track.flags |= Track.SHORT_TRACK;
				dt.track.hasProtections = true;
				writeInfo(info, String.Format("is a short track with {0} bytes (SHT)", dt.track.info.byteCount));
			}
			if (dt.track.info.byteCount > 6437) {
				dt.track.flags |= Track.LONG_TRACK;
				dt.track.hasProtections = true;
				writeInfo(info, String.Format("is a long track with {0} bytes (LGT)", dt.track.info.byteCount));
			}

			// Bad data byte in GAP
			if ((dt.track.flags & Track.BAD_DATA_GAP) != 0) {
				dt.track.hasProtections = true;
				writeInfo(info, "invalid data in GAP (IDG)");
			}

			if (dt.sectors.Count > 0) {
				Sector lastSector = dt.sectors[dt.sectors.Count - 1];
				if (lastSector.id.info.startByte > dt.track.info.byteCount) {
					dt.track.flags |= Track.ID_AFTER_IDX;
					dt.track.hasProtections = true;
					writeInfo(info, "shifted track: Id Beyond Index (IDI)");
				}
				else if ((lastSector.id.info.startByte + lastSector.id.info.byteCount) > dt.track.info.byteCount) {
					dt.track.flags |= Track.ID_OVER_IDX;
					dt.track.hasProtections = true;
					writeInfo(info, "shifted track: Id Over Index (IOI)");
				}
				else if (lastSector.data != null) {
					if (lastSector.data.info.startByte > dt.track.info.byteCount) {
						dt.track.flags |= Track.DATA_AFTER_IDX;
						dt.track.hasProtections = true;
						writeInfo(info, "shifted track: Data Beyond Index (DBI)");
					}
					else if ((lastSector.data.info.startByte + lastSector.data.info.byteCount) > dt.track.info.byteCount) {
						dt.track.flags |= Track.DATA_OVER_IDX;
						dt.track.hasProtections = true;
						writeInfo(info, "shifted track: Data over Index (DOI)");
					}
				}

				// NFA
				if ((dt.track.flags & Track.NO_FLUX_AREA) != 0) {
					dt.track.hasProtections = true;
					writeInfo(info, "has No Flux Area (NFA)");
				}

				// check "dragonflight protection"
				if (dt.sectors.Count > 0) {
					bool dragonflight = false;
					bool jupiterMasterdrive = false;
					int start = 0;

					for (int i = start; i < dt.sectors[0].id.info.startByte; i++) {
						if ((dt.track.buffer.Bytes[i].dataByte == 0x00) &&
							(dt.track.buffer.Bytes[i + 1].dataByte == 0x1C) &&
							(dt.track.buffer.Bytes[i + 2].dataByte == 0x92) &&
							(dt.track.buffer.Bytes[i + 3].dataByte == 0x10) &&
							(dt.track.buffer.Bytes[i + 4].dataByte == 0x90) &&
							(dt.track.buffer.Bytes[i + 5].dataByte == 0xC2)) {
								jupiterMasterdrive = true;
								start = i + 6;
								break;
						}
					}

					for (int i = start; i < dt.sectors[0].id.info.startByte; i++) {
						if ((dt.track.buffer.Bytes[i].dataByte == 0x0B) &&
							(dt.track.buffer.Bytes[i + 1].dataByte == 0xCD) &&
							(dt.track.buffer.Bytes[i + 2].dataByte == 0xB4) &&
							(dt.track.buffer.Bytes[i + 3].dataByte == 0xF7)) {
								dragonflight = true;
								start = i + 4;
								break;
						}
					}

					if (jupiterMasterdrive && !
						((dt.track.buffer.Bytes[start].dataByte == 0x00) &&
						(dt.track.buffer.Bytes[start + 1].dataByte == 0xDE) &&
						(dt.track.buffer.Bytes[start + 2].dataByte == 0xAD) &&
						(dt.track.buffer.Bytes[start + 3].dataByte == 0xC0) &&
						(dt.track.buffer.Bytes[start + 4].dataByte == 0xDE)))
						jupiterMasterdrive = false;
						

					if (dragonflight || jupiterMasterdrive) {
						dt.track.flags |= Track.HIDDEN_DATA;
						dt.track.hasProtections = true;
						if (jupiterMasterdrive)
							writeInfo(info, "hidden data in GAP (HDG) - Jupiter Masterdrive");
						else
							writeInfo(info, "hidden data in GAP (HDG) - Dragonflight");
					}
				}
			}

			//////////////////////
			// Sector Protections
			//////////////////////		
			for (int secIndex = 0; secIndex < dt.sectors.Count; secIndex++) {

				info.sect = dt.sectors[secIndex].id.number;

				// for ST format the sector must be in range 1-n
				// detected on sector but is a track protection
				if (dt.sectors[secIndex].id.number > dt.sectors.Count) {
					dt.track.flags |= Track.SECTOR_ST;
					//writeInfo(info, String.Format("index not in range 1-{0} (SNR)", dt.sectors[secIndex].id.number));
				}


				// normal sector size is 512
				// detected on sector but is a track protection
				if ((dt.sectors[secIndex].id.size & 0x3) != 2) {
					dt.track.flags |= Track.NOT_STD_SIZ;
					//writeInfo(info, "Sector size is not 512 bytes (SSZ)");
				}


				// Duplicate sector number
				for (int comp = secIndex + 1; comp < dt.sectors.Count; comp++) {
					if (dt.sectors[secIndex].id.number == dt.sectors[comp].id.number) {
						dt.sectors[secIndex].flags |= Sector.DUP_SEC_NUM;
						dt.track.hasProtections = true;
						writeInfo(info, "duplicated sector (DSN)");
					}
				}

				// non standard IDAM
				if (dt.sectors[secIndex].id.iam != 0xFE) {
					dt.sectors[secIndex].flags |= Sector.NO_STD_IDAM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("non standard IDAM {0:X2} (NSI)", dt.sectors[secIndex].id.iam));
				}

				// invalid track number
				if (dt.sectors[secIndex].id.track != track) {
					dt.sectors[secIndex].flags |= Sector.BAD_TRK_NUM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("invalid track number {0} (ITN)", dt.sectors[secIndex].id.track));
				}

				// invalid side value
				if (dt.sectors[secIndex].id.head > 1) {
					dt.sectors[secIndex].flags |= Sector.BAD_HEAD_NUM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("invalid head/side number {0} (IHN)", dt.sectors[secIndex].id.head));
				}

				// invalid sector number
				if (dt.sectors[secIndex].id.number > 0xF4) {
					dt.sectors[secIndex].flags |= Sector.BAD_SEC_NUM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("invalid sector number {0} (ISN)", dt.sectors[secIndex].id.number));
				}

				// invalid size value
				if (dt.sectors[secIndex].id.size > 3) {
					dt.sectors[secIndex].flags |= Sector.BAD_SEC_LEN;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("invalid sector length {0} (ISL)", dt.sectors[secIndex].id.size));
				}

				// ID CRC error
				if (dt.sectors[secIndex].id.info.crc != 0) {
					dt.track.hasProtections = true;
					writeInfo(info, "invalid ID CRC (IIC)");
				}

				// sector within sector
				if ((dt.sectors[secIndex].flags & Sector.SEC_IN_SEC) != 0) {
					dt.track.hasProtections = true;
					writeInfo(info, "sector within sector (SWS)");
				}

				if (dt.sectors[secIndex].data != null) {
					// Data CRC Error
					if (dt.sectors[secIndex].data.info.crc != 0) {
						dt.track.hasProtections = true;
						writeInfo(info, "invalid DATA CRC (IDC)");
					}

					// fuzzy bits
					if (dt.sectors[secIndex].data.fuzzy != null) {
						dt.track.hasProtections = true;
						writeInfo(info, "fuzzy sector (FZS)");
					}

					// non standard DAM
					if (dt.sectors[secIndex].data.iam != 0xFB) {
						dt.sectors[secIndex].flags |= Sector.NO_STD_DAM;
						dt.track.hasProtections = true;
						writeInfo(info, String.Format("non standard DAM {0:X2} (NSD)", dt.sectors[secIndex].data.iam));
					}

					// long sector ( 4000 * 8) + 3%
					if ((dt.sectors[secIndex].data.info.timeLength > (32960 * (Global.sectSize(dt.sectors[secIndex].id.size) + 3))) &&
							((dt.track.flags & Track.SHORT_TRACK) == 0)) {
						dt.sectors[secIndex].flags |= Sector.LONG_SECT;
						dt.track.hasProtections = true;
						writeInfo(info, String.Format("long sector {0:F2} µs (LGS)", (double)dt.sectors[secIndex].data.info.timeLength / 1000.0));
					}

					// short sector ( 4000 * 8) - 3%
					if ((dt.sectors[secIndex].data.info.timeLength < (31040 * (Global.sectSize(dt.sectors[secIndex].id.size) + 3))) &&
							((dt.track.flags & Track.LONG_TRACK) == 0)) {
						dt.sectors[secIndex].flags |= Sector.SHORT_SECT;
						dt.track.hasProtections = true;
						writeInfo(info, String.Format("short sector {0:F2} µs (SHS)", (double)dt.sectors[secIndex].data.info.timeLength / 1000.0));
					}

					// Check intra sector bit width variation
					if ((dt.sectors[secIndex].flags & Sector.WIDTH_INTRA) != 0) {
						dt.track.hasProtections = true;
						writeInfo(info, "sector bit-rate variation (SBV)");
					}

				}	// sector has no data
				else {
					dt.track.hasProtections = true;
					writeInfo(info, "sector with no data (SND)");
				}

			}	// for all sectors

		}


		private static void writeInfo(PInfo info, string message) {
			if (info.text == null) return;
			info.text.AppendText(String.Format("T{0:D2}.{1}", info.track, info.side));
			if (info.sect != -1)
				info.text.AppendText(String.Format(" S{0}", info.sect));
			info.text.AppendText(String.Format(" {0}\n", message));
			info.text.ScrollToEnd();
		}

		//private static void trackProtection(TrackContent tc) {
		//	tc.hasProtection = true;
		//}

	}
}

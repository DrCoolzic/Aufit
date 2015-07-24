/*!
@file Protections.cs
<summary>Everything related to Protections</summary>

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
		/// <remarks>We check the following protections
		/// - Track protections:
		///		- SHT short track
		///		- LGT long track
		///		- NFA Track has No FLux Area (done in readTrack())
		///		- SHT Shifted Track
		///			- DOI Track has Data Over Index (done in readTrack())
		///			- DBI Track has Data Beyond Index (done in readTrack())
		///			- IOI Track has ID over Index (done in readTrack())
		///			- IBI Track has ID beyond Index (done in readTrack())
		///			.
		///		- IDG invalid data in GAP
		///		- HDG Hidden data in GAP => DragonFlight & Jupiter Master drive
		///		- ISS Invalid Sync Sequence
		///		- TODO - Extra / Missing track
		///		.
		///	.
		/// - Sector protections:
		///		- Sector not sequentially numbered (for ST format)
		///		- Non standard sector size (for ST format)	
		///		- DSN Duplicate Sectors
		///		- IIF Invalid ID Field
		///			- NSI Non Standard IDAM
		///			- ITN Invalid Track Number
		///			- IHN Invalid Head Number
		///			- ISN Invalid Sector number
		///			- ISL Invalid Sector Length
		///			- ICE ID CRC Error
		///			.
		///		- SWS Sector within Sector (done in readTrack())		
		///		- DCE Data CRC Error
		///		- FZS Fuzzy Sector
		///		- NSD Non Standard DAM
		///		- LGS Long sector
		///		- SHS short sector
		///		- SBV Sector with Bit-rate Variation
		///		- SND Sector with No Data		
		///		.
		///	.
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
				writeInfo(info, String.Format("SHT Short Track - {0} bytes", dt.track.info.byteCount));
			}
			if (dt.track.info.byteCount > 6437) {
				dt.track.flags |= Track.LONG_TRACK;
				dt.track.hasProtections = true;
				writeInfo(info, String.Format("LGT Long Track - {0} bytes ", dt.track.info.byteCount));
			}

			// Bad data byte in GAP
			if ((dt.track.flags & Track.BAD_DATA_GAP) != 0) {
				dt.track.hasProtections = true;
				writeInfo(info, "IDG Invalid Data into GAP");
			}

			if (dt.sectors.Count > 0) {
				Sector lastSector = dt.sectors[dt.sectors.Count - 1];

				if (lastSector.id.info.startByte > dt.track.info.byteCount) {
					dt.track.flags |= Track.ID_AFTER_IDX;
					dt.track.hasProtections = true;
					writeInfo(info, "SHT-IBI Shifted Track with ID Beyond Index");
				}
				else if ((lastSector.id.info.startByte + lastSector.id.info.byteCount) > dt.track.info.byteCount) {
					dt.track.flags |= Track.ID_OVER_IDX;
					dt.track.hasProtections = true;
					writeInfo(info, "SHT-IOI Shifted Track with ID Over Index");
				}
				else if (lastSector.data != null) {
					if (lastSector.data.info.startByte > dt.track.info.byteCount) {
						dt.track.flags |= Track.DATA_AFTER_IDX;
						dt.track.hasProtections = true;
						writeInfo(info, "SHT-DBI Shifted Track with Data Beyond Index");
					}
					else if ((lastSector.data.info.startByte + lastSector.data.info.byteCount) > dt.track.info.byteCount) {
						dt.track.flags |= Track.DATA_OVER_IDX;
						dt.track.hasProtections = true;
						writeInfo(info, "SHT-DOI Shifted Track with Data over Index");
					}
				}

				// NFA
				if ((dt.track.flags & Track.NO_FLUX_AREA) != 0) {
					dt.track.hasProtections = true;
					writeInfo(info, "NFA track has No Flux Area(s)");
				}

				// check "dragon flight and Jupiter master drive protections"
				bool dragonflight = false;
				bool jupiterMasterdrive = false;
				int start = 0;
				for (int i = start; i < (dt.sectors[0].id.info.startByte); i++) {
					if ((dt.track.buffer.DataByte[i] == 0x00) &&
						(dt.track.buffer.DataByte[i + 1] == 0x1C) &&
						(dt.track.buffer.DataByte[i + 2] == 0x92) &&
						(dt.track.buffer.DataByte[i + 3] == 0x10) &&
						(dt.track.buffer.DataByte[i + 4] == 0x90) &&
						(dt.track.buffer.DataByte[i + 5] == 0xC2)) {
						jupiterMasterdrive = true;
						start = i + 6;
						break;
					}
				}

				for (int i = start; i < dt.sectors[0].id.info.startByte; i++) {
					if ((dt.track.buffer.DataByte[i] == 0x0B) &&
						(dt.track.buffer.DataByte[i + 1] == 0xCD) &&
						(dt.track.buffer.DataByte[i + 2] == 0xB4) &&
						(dt.track.buffer.DataByte[i + 3] == 0xF7)) {
						dragonflight = true;
						start = i + 4;
						break;
					}
				}

				if (jupiterMasterdrive && !
					((dt.track.buffer.DataByte[start] == 0x00) &&
					(dt.track.buffer.DataByte[start + 1] == 0xDE) &&
					(dt.track.buffer.DataByte[start + 2] == 0xAD) &&
					(dt.track.buffer.DataByte[start + 3] == 0xC0) &&
					(dt.track.buffer.DataByte[start + 4] == 0xDE)))
					jupiterMasterdrive = false;


				if (dragonflight || jupiterMasterdrive) {
					dt.track.flags |= Track.HIDDEN_DATA;
					dt.track.hasProtections = true;
					if (jupiterMasterdrive)
						writeInfo(info, "HDG Hidden Data into GAP - type: Jupiter Master drive");
					else
						writeInfo(info, "HDG Hidden Data into GAP - type: Dragon flight");
				}

			}	// number of sectors > 0

			if ((dt.track.flags & Track.INV_SYNC_SEQ) != 0) {
				dt.track.hasProtections = true;
				writeInfo(info, "ISS Invalid Sync-mark Sequence");
			}


			//////////////////////
			// Sector Protections
			//////////////////////		
			for (int secIndex = 0; secIndex < dt.sectors.Count; secIndex++) {

				info.sect = dt.sectors[secIndex].id.Sector;

				// for ST format the sector must be in range 1-n
				// detected on sector but is a track protection
				if (dt.sectors[secIndex].id.Sector > dt.sectors.Count) {
					dt.track.flags |= Track.SECTOR_ST;
					//writeInfo(info, String.Format("Sector not sequential 1-{0} (SNS)", dt.sectors[secIndex].id.number));
				}


				// normal sector size is 512
				// detected on sector but is a track protection
				if ((dt.sectors[secIndex].id.Length & 0x3) != 2) {
					dt.track.flags |= Track.NOT_STD_SIZ;
					//writeInfo(info, "Sector size is not 512 bytes (SSZ)");
				}


				// Duplicate sector number
				for (int comp = secIndex + 1; comp < dt.sectors.Count; comp++) {
					if (dt.sectors[secIndex].id.Sector == dt.sectors[comp].id.Sector) {
						dt.sectors[secIndex].flags |= Sector.DUP_SEC_NUM;
						dt.track.hasProtections = true;
						writeInfo(info, String.Format("DSN Duplicate Sector Number {0}", dt.sectors[secIndex].id.Sector));
					}
				}

				// non standard IDAM
				if (dt.sectors[secIndex].id.IDAM != 0xFE) {
					dt.sectors[secIndex].flags |= Sector.NO_STD_IDAM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("IIF-NSI Invalid ID Field: {0:X2} is Non Standard IDAM", dt.sectors[secIndex].id.IDAM));
				}

				// invalid track number
				if (dt.sectors[secIndex].id.Track != track) {
					dt.sectors[secIndex].flags |= Sector.BAD_TRK_NUM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("IIF-ITN Invalid ID Field: {0} is an Invalid Track Number", dt.sectors[secIndex].id.Track));
				}

				// invalid head/side value
				if (dt.sectors[secIndex].id.Head > 1) {
					dt.sectors[secIndex].flags |= Sector.BAD_HEAD_NUM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("IIF-IHN Invalid ID Field: {0} is an Invalid Head/Side Number", dt.sectors[secIndex].id.Head));
				}

				// invalid sector number
				if (dt.sectors[secIndex].id.Sector > 0xF4) {
					dt.sectors[secIndex].flags |= Sector.BAD_SEC_NUM;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("IIF-ISN Invalid ID Field: {0} is an Invalid Sector Number (ISN)", dt.sectors[secIndex].id.Sector));
				}

				// invalid length value
				if (dt.sectors[secIndex].id.Length > 3) {
					dt.sectors[secIndex].flags |= Sector.BAD_SEC_LEN;
					dt.track.hasProtections = true;
					writeInfo(info, String.Format("IIF-ISL Invalid ID Field: {0} in an Invalid Sector Length (ISL)", dt.sectors[secIndex].id.Length));
				}

				// ID CRC error
				if (dt.sectors[secIndex].id.info.crc != 0) {
					dt.track.hasProtections = true;
					writeInfo(info, "IIF-ICE Invalid ID Field: ID CRC Error (ICE)");
				}

				// sector within sector
				if ((dt.sectors[secIndex].flags & Sector.SEC_IN_SEC) != 0) {
					dt.track.hasProtections = true;
					writeInfo(info, "SWS Sector Within Sector");
				}

				if (dt.sectors[secIndex].data != null) {
					if (dt.sectors[secIndex].data.info.crc != 0) {
						dt.track.hasProtections = true;
						writeInfo(info, "DCE DATA CRC Error");
					}

					// fuzzy bits
					if (dt.sectors[secIndex].data.fuzzy != null) {
						dt.track.hasProtections = true;
						writeInfo(info, "FZS Fuzzy Sector");
					}

					// non standard DAM
					if (dt.sectors[secIndex].data.DAM != 0xFB) {
						dt.sectors[secIndex].flags |= Sector.NO_STD_DAM;
						dt.track.hasProtections = true;
						writeInfo(info, String.Format("NSD {0:X2} is a Non-Standard DAM", dt.sectors[secIndex].data.DAM));
					}

					// long sector ( 4000 * 8) + 3%
					if ((dt.sectors[secIndex].data.info.timeLength > (32960 * (Global.sectSize(dt.sectors[secIndex].id.Length) + 3))) &&
							((dt.track.flags & Track.SHORT_TRACK) == 0)) {
						dt.sectors[secIndex].flags |= Sector.LONG_SECT;
						dt.track.hasProtections = true;
						writeInfo(info, String.Format("LGS Long Sector: {0:F2} µs", (double)dt.sectors[secIndex].data.info.timeLength / 1000.0));
					}

					// short sector ( 4000 * 8) - 3%
					if ((dt.sectors[secIndex].data.info.timeLength < (31040 * (Global.sectSize(dt.sectors[secIndex].id.Length) + 3))) &&
							((dt.track.flags & Track.LONG_TRACK) == 0)) {
						dt.sectors[secIndex].flags |= Sector.SHORT_SECT;
						dt.track.hasProtections = true;
						writeInfo(info, String.Format("SHS Short Sector: {0:F2} µs", (double)dt.sectors[secIndex].data.info.timeLength / 1000.0));
					}

					// intra sector bit rate variation
					if ((dt.sectors[secIndex].flags & Sector.WIDTH_INTRA) != 0) {
						dt.track.hasProtections = true;
						writeInfo(info, "SBV Sector Bit-rate Variation");
					}

				}	// sector has data
				else {
					dt.track.hasProtections = true;
					writeInfo(info, "SND Sector with No Data");
				}

			}	// for all sectors

		}


		private static void writeInfo(PInfo info, string message) {
			if (info.text == null) return;
			info.text.AppendText(String.Format("T{0:D2}.{1}", info.track, info.side));
			if (info.sect != -1)
				info.text.AppendText(String.Format("-S{0}", info.sect));
			info.text.AppendText(String.Format(" {0}\n", message));
			info.text.ScrollToEnd();
		}

		//private static void trackProtection(TrackContent tc) {
		//	tc.hasProtection = true;
		//}

	}
}

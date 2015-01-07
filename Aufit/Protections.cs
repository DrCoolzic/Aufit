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
				writeInfo(info, String.Format("is a short Track of {0} bytes (SHT)", dt.track.info.byteCount));
			}
			if (dt.track.info.byteCount > 6437) {
				dt.track.flags |= Track.LONG_TRACK;
				dt.track.hasProtections = true;
				writeInfo(info, String.Format("is a long Track of {0} bytes (LGT)", dt.track.info.byteCount));
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
					writeInfo(info, "is a shifted track: Id Beyond Index (IDI)");
				}
				else if ((lastSector.id.info.startByte + lastSector.id.info.byteCount) > dt.track.info.byteCount) {
					dt.track.flags |= Track.ID_OVER_IDX;
					dt.track.hasProtections = true;
					writeInfo(info, "Is a shifted track: Id Over Index (IOI)");
				}
				else if (lastSector.data != null) {
					if (lastSector.data.info.startByte > dt.track.info.byteCount) {
						dt.track.flags |= Track.DATA_AFTER_IDX;
						dt.track.hasProtections = true;
						writeInfo(info, "Is a shifted track: Data Beyond Index (DBI)");
					}
					else if ((lastSector.data.info.startByte + lastSector.data.info.byteCount) > dt.track.info.byteCount) {
						dt.track.flags |= Track.DATA_OVER_IDX;
						dt.track.hasProtections = true;
						writeInfo(info, "Is a shifted track: Data over Index (DOI)");
					}
				}

				// NFA
				if ((dt.track.flags & Track.NO_FLUX_AREA) != 0) {
					dt.track.hasProtections = true;
					writeInfo(info, "has No Flux Area (NFA)");
				}

			}

			//////////////////////
			// Sector Protections
			//////////////////////		
			for (int secIndex = 0; secIndex < dt.sectors.Count; secIndex++) {

				info.sect = dt.sectors[secIndex].id.number;

				// sector normal sequence = normally sec num = index + 1
				// detected on sector but is a track protection
				if (dt.sectors[secIndex].id.number != (secIndex + 1)) {
					dt.track.flags |= Track.SECTOR_SEQ;
					//writeInfo(info, "Index non sequentially numbered");
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
					writeInfo(info, "invalid ID field CRC (IIC)");
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
						writeInfo(info, "invalid DATA field CRC (IDC)");
					}

					// fuzzy bits
					if (dt.sectors[secIndex].data.fuzzy != null) {
						dt.track.hasProtections = true;
						writeInfo(info, "has fuzzy bytes");
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
						writeInfo(info, "Sector bit width variation (SBV)");
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
			info.text.AppendText(String.Format("Track {0:D2}.{1}", info.track, info.side));
			if (info.sect != -1)
				info.text.AppendText(String.Format(" Sect {0}", info.sect));
			info.text.AppendText(String.Format(" {0}\n", message));
			info.text.ScrollToEnd();
		}

		//private static void trackProtection(TrackContent tc) {
		//	tc.hasProtection = true;
		//}

	}
}

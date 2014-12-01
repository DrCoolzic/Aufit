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
			TrackContent tc = Global.Disk[track, side].trackContent;
			if (tc == null) return;
			AddrContent ac = Global.Disk[track, side].addrContent;
			if (ac == null) return;
			if (ac.ids.Count == 0) return;
			PInfo info = new PInfo();
			info.track = track;
			info.side = side;
			info.text = text;
			info.sect = -1;
			//////////////////////
			// Track Protections
			//////////////////////
			// normal track is 200000 / 32 = 6250 bytes - short track N-5% = 5940 - long track = N+5% = 6560
			if (tc.byteCount < 6000) {
				tc.shortTrack = true;
				trackProtection(tc);
				writeInfo(info, String.Format("is a short Track of {0} bytes", tc.byteCount));
			} 
			if (tc.byteCount > 6500) {
				tc.longTrack = true;
				trackProtection(tc);
				writeInfo(info, String.Format("is a long Track of {0} bytes", tc.byteCount));
			}

			// NFA
			if (tc.NFA != -1) {
				writeInfo(info, String.Format("has No Flux Area starting at {0}", tc.NFA / 1000));
				trackProtection(tc);
			}

			// Over index
			if (tc.dataOverIdx)
				writeInfo(info, "has data over index");
			if (tc.dataBeyondIdx)
				writeInfo(info, "has data beyond index");
			if (tc.idOverIdx)
				writeInfo(info, "has ID field over index");

			//////////////////////
			// Sector Protections
			//////////////////////
			int secnum = 1;
			for (int sect = 0; sect < ac.ids.Count; sect++) {
				info.sect = ac.ids[sect].number;

				// sector in sequence 1 - n
				if (ac.ids[sect].number != secnum++) {
					tc.sectorInSeq = false;
					//trackProtection(tc);
					//writeInfo(info, "not in sequence");
				}

				// Duplicate sector number
				for (int comp = sect + 1; comp < ac.ids.Count; comp++)
					if (ac.ids[sect].number == ac.ids[comp].number) {
						tc.sectorUnique = false;
						trackProtection(tc);
						writeInfo(info, "duplicated");
					}

				// invalid sector number
				if (ac.ids[sect].number > 0xF4) {
					tc.sectorValidNumber = false;
					trackProtection(tc);
					writeInfo(info, "has an invalid number");
				}

				// Standard Size
				if (ac.ids[sect].size != 2) {
					tc.sectorSandardSize = false;
					//trackProtection(tc);
					//writeInfo(info, "has not a standard size");
				}


				// sector within sector
				if (tc.sectors[sect + 1].sws != -1) {
					trackProtection(tc);
					writeInfo(info, String.Format("is inside sector {0}", tc.sectors[sect + 1].sws));
				}

				SectorContent sc = FDC.findSectorContent(track, side, ac.ids[sect].number, ac.blocs[sect].startTime);
				if (sc == null)
					continue;

				// Invalid Data into GAP
				if ((tc.sectors[sect+1].phases[(int)Phases.p_gap].invDataGap + tc.sectors[sect+1].phases[(int)Phases.p_gap].invDataGap) > 0) {
					sc.sectorBadDataInGap = true;
					trackProtection(tc);
					string message = "has invalid characters: ";
					if (tc.sectors[sect+1].phases[(int)Phases.p_gap].invDataGap > 0)
						message += String.Format(" {0} in intra gap", tc.sectors[sect+1].phases[(int)Phases.p_gap].invDataGap);
					if (tc.sectors[sect+1].phases[(int)Phases.p_gap].invDataGap > 0)
						message += String.Format(" {0} in inter gap", tc.sectors[sect+1].phases[(int)Phases.p_gap].invDataGap);
					writeInfo(info, message);
				}

				//// Bad Sync
				//if ((tc.sectors[sect+1].phases[(int)Phases.p_inner].badSync + tc.sectors[sect+1].phases[(int)Phases.p_outer].badSync) 
				//	// + (tc.sectors[sect+1].phases[(int)Phases.p_data].badSync + tc.sectors[sect+1].phases[(int)Phases.p_id].badSync) 
				//	> 0) {
				//	sc.sectorBadSync = true;
				//	trackProtection(tc);
				//	string message = "has invalid sync seq: ";
				//	if (tc.sectors[sect+1].phases[(int)Phases.p_inner].badSync > 0)
				//		message += String.Format(" {0} in intra gap", tc.sectors[sect + 1].phases[(int)Phases.p_inner].badSync);
				//	if (tc.sectors[sect+1].phases[(int)Phases.p_outer].badSync > 0)
				//		message += String.Format(" {0} in inter gap", tc.sectors[sect + 1].phases[(int)Phases.p_outer].badSync);
				//	//if (tc.sectors[sect+1].phases[(int)Phases.p_data].badSync > 0)
				//	//	message += String.Format(" {0} in data", tc.sectors[sect + 1].phases[(int)Phases.p_data].badSync);
				//	//if (tc.sectors[sect+1].phases[(int)Phases.p_id].badSync > 0)
				//	//	message += String.Format(" {0} in id", tc.sectors[sect + 1].phases[(int)Phases.p_id].badSync);
				//	writeInfo(info, message);
				//}

				// Data CRC Error
				if (sc.dataBlock.crc != 0) {
					trackProtection(tc);
					writeInfo(info, "has CRC error in data field");
				}

				// ID CRC error
				if (sc.idBlock.crc != 0) {
					trackProtection(tc);
					writeInfo(info, "has CRC error in id field");
				}

				// fuzzy bits
				if (sc.fuzzyMask != null) {
					trackProtection(tc);
					writeInfo(info, "has fuzzy bits");
				}

				// sector under clocked
				if ((sc.dataBlock.underClock > (Global.sectSize(sc.id.size) / 2)) && !tc.longTrack) {
					sc.sectorUnderClocked = true;
					trackProtection(tc);
					writeInfo(info, String.Format("Short sector {0} µs ({1} bytes under clocked)", sc.dataBlock.timeLength / 1000, sc.dataBlock.underClock));
				}

				// sector over clocked
				if ((sc.dataBlock.overClock > (Global.sectSize(sc.id.size) / 2)) && !tc.shortTrack) {
					sc.sectorOverClocked = true;
					trackProtection(tc);
					writeInfo(info, String.Format("Long sector {0} µs ({1} bytes over clocked)", sc.dataBlock.timeLength / 1000, sc.dataBlock.overClock));
				}

				// intra bit width variation => normal macrodos if size/4 above and size/4 below
				if (sc.dataBlock.underClock > (Global.sectSize(sc.id.size) / 8) && (sc.dataBlock.overClock > (Global.sectSize(sc.id.size) / 8))) {
					sc.sectorIntraVariation = true;
					trackProtection(tc);
					writeInfo(info, String.Format("intra-sector variation ({0} bytes under {1} bytes over clocked))", sc.dataBlock.underClock, sc.dataBlock.overClock));
				}

				// sector with no data
				if (sc.data.Bytes.Count == 0) {
					trackProtection(tc);
					writeInfo(info, "sector with no data");
				}

				// sector has invalid track number
				if (sc.id.track != track) {
					sc.sectorValidTrack = true;
					trackProtection(tc);
					writeInfo(info, String.Format("has invalid track number {0}", sc.id.track));
				}


			}	// for all sector

		}


		private static void writeInfo(PInfo info, string message) {
			if (info.text == null) return;
			info.text.AppendText(String.Format("Track {0:D2}.{1}", info.track, info.side));
			if (info.sect != -1)
				info.text.AppendText(String.Format(" Sect {0}", info.sect));
			info.text.AppendText(String.Format(" {0}\n", message));
			info.text.ScrollToEnd();
		}

		private static void trackProtection(TrackContent tc) {
			tc.hasProtection = true;
		}

	}
}

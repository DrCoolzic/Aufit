using GlobalNS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WD1772NS;

namespace Aufit {
	/// <summary>
	/// Check for Protection
	/// </summary>
	class Protections {


		public static void checkProtection(int track, int side, int rev) {
			TrackContent tc = FDC.readTrack(track, side, rev);
			AddrContent ac = FDC.readAddr(track, side, rev);

			int prot = 0;

			for (int sect = 0; sect < ac.ids.Count; sect++) {
				for (int comp = sect + 1; comp < ac.ids.Count; comp++)
					if (ac.ids[sect].number == ac.ids[comp].number)
						prot++;
				if (ac.ids[sect].number > 0xF4)
					prot++;

				SectorContent sc = FDC.readSector(track, side, rev, ac.ids[sect].number, ac.blocs[sect].startTime, false);
				// TODO check not found
				// now multiple readSector for fuzzy bits
				for (int j = 1; j < Global.Disk[track, side].revData.Count() + 1; j++) {
					if (j == rev) continue;
					FDC.readSector(track, side, j, ac.ids[sect].number, ac.blocs[sect].startByte, true);
				}
			}
		}

	}
}

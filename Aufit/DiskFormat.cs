/*!
@file DiskFormat.cs
<summary>Everything related to Mastering Disk Format detection</summary>

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

using GlobalPackage;
using IpfPackage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WD1772Package;

namespace Aufit {

	class GapInfo {
		public int spliceTime;
		public int spliceByte;
		public int count4E;
		public int count00;
		public bool badDataFound;

		public void reset() {
			badDataFound = false;
			count00 = 0;
			count4E = 0;
			spliceTime = 0;
			spliceByte = 0;
		}
	}

	public partial class MainWindow : Window {

		// In order to detect the format several operations needs to be performed.
		// - protection detection
		// - layout analysis number of sectors, number of block bytes and gap bytes
		// - detection of track splice and first sector position
		// - Inner GAP analysis and eventually correction
		// - Post GAP analysis and correction
		// - conversion of post gap in list of gap stream elements
		// .
		// we can ignore the following track protections
		// - Short track
		// - Long track
		// - Shifted track
		// ??
		// .
		// We can ignore the following sector protections
		// - duplicate sector number
		// - invalid id field
		// - data crc error
		// - non standard dam
		
		
		public void drawDiskFormat(int track, int side) {
			int blockBytes = 0;
			int gapBytes = 0;
			int maxPostSize = 0;
			int firstBlock = 0;
			int lastBlock = 0;
			Global.DiskTrack dt = Global.Disk[track, side];

			if ((dt.track == null) || (dt.sectors.Count == 0))
				return;
			
			Protections.checkProtection(track, side, tbMasterView);
			tbMasterView.AppendText(String.Format("Track T{0:D2}.{1} has {2} blocks ", track, side, dt.sectors.Count));

			for (int i = 0; i < dt.sectors.Count; i++ ) {
				Sector s = dt.sectors[i];
				// we compute the Block bytes and the Gap bytes
				blockBytes += s.id.buffer.DataByte.Count + 3 + s.innerGap.buffer.DataByte.Count + 
					((s.data != null) ? (s.data.buffer.DataByte.Count + 3) : 0);
				gapBytes += (s.postGap != null) ? s.postGap.buffer.DataByte.Count : 0;

				// we search the last block
				// for now we take the sector that have the largest post gap size
				if ((s.postGap != null) && (s.postGap.buffer.DataByte.Count > maxPostSize)) {
					maxPostSize = s.postGap.buffer.DataByte.Count;
					lastBlock = i;
				}
			}	// for all sectors

			firstBlock = (lastBlock == (dt.sectors.Count - 1)) ? 0 : lastBlock + 1;
			tbMasterView.AppendText(String.Format("Size={0}B ({1}b) = {2}B ({3}b) Data + {4}B ({5}b) Gap ",
				dt.track.buffer.DataByte.Count, (blockBytes + gapBytes) * 16,
				blockBytes, blockBytes * 16,
				gapBytes, gapBytes * 16));
			tbMasterView.AppendText(String.Format("-- First sector {0} start at bit {1}\n", 
				dt.sectors[firstBlock].id.Sector, dt.sectors[firstBlock].id.info.startBit - 4*16));

			GapInfo gap = new GapInfo(); ;
			for (int i = firstBlock; i < (firstBlock + dt.sectors.Count); i++ ) {
				Sector s = dt.sectors[i % dt.sectors.Count];
				bool gapBadData;
				tbMasterView.AppendText(String.Format("Sector {0,-3:D} ", s.id.Sector));

				gapBadData = checkGAP(s.innerGap, gap, false);
				tbMasterView.AppendText(String.Format("Inner gap: {0}x4E - {1}x00", gap.count4E, gap.count00));
				if (gapBadData)
					tbMasterView.AppendText(String.Format(" has bad data @ {0} {1:F2}", gap.spliceByte, (double)gap.spliceTime / 1000000.0));
				tbMasterView.AppendText("\n");

				gapBadData = checkGAP(s.postGap, gap, true);
				tbMasterView.AppendText(String.Format("           Post  gap: {0}x4E - {1}x00", gap.count4E, gap.count00));
				if (gapBadData)
					tbMasterView.AppendText(String.Format(" has bad data @ {0} {1:F2} ms", gap.spliceByte, (double)gap.spliceTime / 1000000.0));
				tbMasterView.AppendText("\n");
			}

			// for post gap we generate stream gap elem
			// TODO

			tbMasterView.ScrollToEnd();

		}

		private bool checkGAP(GapField gap, GapInfo gi, bool skipFirst) {
			bool in4E = true;
			int setAside = 0;
			gi.reset();
			if (gap == null) return false;

			for (int i = skipFirst ? 1 : 0; i < gap.buffer.DataByte.Count; i++) {
				byte b = gap.buffer.DataByte[i];
				if (in4E) {
					if (isShifted00(b)) {
						if (b != 0x00) {
							if (!gi.badDataFound) { 
								gi.spliceTime = gap.buffer.StartTime[i];
								gi.spliceByte = gap.info.startByte + i;
							}
							gi.badDataFound = true;
						}
						setAside++;
						if (setAside > 2) {
							gi.count00 = setAside;
							in4E = false;
						}
					}	// 00
					else {
						gi.count4E += setAside;
						gi.count4E++;
						setAside = 0;
						if (b != 0x4E) {
							if (!gi.badDataFound) {
								gi.spliceTime = gap.buffer.StartTime[i];
								gi.spliceByte = gap.info.startByte + i;
							}
							gi.badDataFound = true;
						}
					}	// not 00
				}	// in4E
				else {
					gi.count00++;
					if (b != 0x00) {
						if (!gi.badDataFound) {
							gi.spliceTime = gap.buffer.StartTime[i];
							gi.spliceByte = gap.info.startByte + i;
						}
						gi.badDataFound = true;
					}
				}	// !in4e
			}
			return gi.badDataFound;
		}


		bool isShifted4E(byte b) {
			return ((b == 0x4E) || (b == 0x12) || (b == 0xC9) || (b == 0x24) || (b == 0x93) || (b == 0x48) || (b == 0x27) || (b == 0x90) ||
				    (b == 0xE4) || (b == 0x21) || (b == 0x9C) || (b == 0x42) || (b == 0x39) || (b == 0x84) || (b == 0x72) || (b == 0x09) );
		}


		bool isShifted00(byte b) {
			return ((b == 0x00) || (b == 0xFF));
		}


	}



}

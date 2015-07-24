/*!
@file DiskFormat.cs
<summary>Everything related to Mastering Disk Format detection</summary>

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

	/// <summary>
	/// Contain information about the format
	/// </summary>
	/// <remarks>
	/// In order to detect the format several operations needs to be performed.
	/// - protection detection
	/// - layout analysis number of sectors, number of block bytes and gap bytes
	/// - detection of track splice and first sector position
	/// - Inner GAP analysis and eventually correction
	/// - Post GAP analysis and correction
	/// - conversion of post gap in list of gap stream elements
	/// .
	/// we can ignore the following track protections
	/// - Short track
	/// - Long track
	/// - Shifted track
	/// ??
	/// .
	/// We can ignore the following sector protections
	/// - duplicate sector number
	/// - invalid id field
	/// - data crc error
	/// - non standard dam
	/// </remarks>
	//public class DiskFormat {

	public partial class MainWindow : Window {


		//List<TrackDescriptor> tracks;

		//public DiskFormat(TreeView trv = null) {
		//	tracks = new List<TrackDescriptor>();
		//}

		public void drawDiskFormat(int track, int side /*, TextBox text = null*/) {
			int blockBytes = 0;
			int gapBytes = 0;
			int maxPost = 0;
			int firstBlock = 0;
			int lastBlock = 0;
			Global.DiskTrack dt = Global.Disk[track, side];

			if ((dt.track == null) || (dt.sectors.Count == 0))
				return;
			Protections.checkProtection(track, side, tbMasterView);

			tbMasterView.AppendText(String.Format("Track T{0:D2}.{1} {2} blocks ", track, side, dt.sectors.Count));

			for (int i = 0; i < dt.sectors.Count; i++ ) {
				Sector s = dt.sectors[i];
				// we compute the Block bytes and the Gap bytes
				blockBytes += s.id.info.byteCount + 3 + s.innerGap.info.byteCount + 
					((s.data != null) ? (s.data.info.byteCount + 6) : 0); 
				gapBytes += s.postGap.info.byteCount;

				// we search the Track write splice
				// for now we take the sector after the largest post gap area
				if (s.postGap.info.byteCount > maxPost) {
					maxPost = s.postGap.info.byteCount;
					lastBlock = i;
				}

			}	// for all sectors

			tbMasterView.AppendText(String.Format("Size={0} ({1}) = {2} ({3}) Block + {4} ({5}) Gap ",
				dt.track.info.byteCount, (blockBytes + gapBytes) * 16,
				blockBytes, blockBytes * 16,
				gapBytes, gapBytes * 16));
			firstBlock = (lastBlock == (dt.sectors.Count - 1)) ? 0 : lastBlock + 1;
			tbMasterView.AppendText(String.Format("-- First sector {0} start at bit {1}\n", 
				dt.sectors[firstBlock].id.number, dt.sectors[firstBlock].id.info.startBit - 4*16));

			

			for (int i = firstBlock; i < (firstBlock + dt.sectors.Count); i++ ) {
				Sector s = dt.sectors[i % dt.sectors.Count];

				bool in4E = true;
				bool innerGapBadData = false;
				bool postGapBadData = false;
				int writeSpliceTime;
				int count4E = 0;
				int count00 = 0;
				int setAside = 0;

				// we check inner gap data
				for (int b = 0; b < s.innerGap.info.byteCount; b++) {
					if ((s.innerGap.buffer.DataByte[b] != 0x4E) && (s.innerGap.buffer.DataByte[b] != 0x00)) {
						innerGapBadData = true;
						tbMasterView.AppendText(String.Format("badIGap={0:X2} ", s.innerGap.buffer.DataByte[b]));
					}
				}
				if (innerGapBadData) tbMasterView.AppendText("\n");

				//bool last = (s == dt.sectors[lastBlock]);
				bool last = (i == lastBlock);
				for (int b = 0; b < s.postGap.info.byteCount; b++) {
					if (in4E) {
						if (isShifted00(s.postGap.buffer.DataByte[b])) {
							if ((!last) && (s.postGap.buffer.DataByte[b] != 0x00))
								postGapBadData = true;
							setAside++;
							if (setAside > 2) {
								count00 = setAside;
								in4E = false;
							}
						}	// 00
						else {
							count4E++;
							count4E += setAside;
							setAside = 0;
							if ((!last) && (s.postGap.buffer.DataByte[b] != 0x4E)) {
								postGapBadData = true;
								writeSpliceTime = s.postGap.buffer.StartTime[b];
							}
						}	// not 00
					}
					else {
						count00++;
						if ((!last) && (s.postGap.buffer.DataByte[b] != 0x00))
							postGapBadData = true;
					}
				}
				tbMasterView.AppendText(String.Format("Sector {0} post gap - {1}x4E - {2}x00 -- {3}\n", s.id.number, count4E, count00, postGapBadData));



			}

			// for post gap we generate stream gap elem
			// TODO

			tbMasterView.ScrollToEnd();

		}

		bool isShifted4E(byte b) {
			return ((b == 0x4E) || (b == 0x12) || (b == 0xC9) || (b == 0x24) || (b == 0x93) || (b == 0x48) || (b == 0x27) || (b == 0x90) ||
				(b == 0xE4) || (b == 0x21) || (b == 0x9C) || (b == 0x42) || (b == 0x39) || (b == 0x84) || (b == 0x72) || (b == 0x09));
		}


		bool isShifted00(byte b) {
			return ((b == 0x00) || (b == 0xFF));
		}


	}

	public class TrackDescriptor {
		public TrackDescriptor() {
			this.Items = new ObservableCollection<BlockDescriptor>();
		}

		public ObservableCollection<BlockDescriptor> Items { get; set; }

		public int Track { get; set; }
		public int Head { get; set; }
		public string BlockName { get; set; }

		public string Content {
			get { return String.Format("T{0:D2}.{1:D1} Block=9 Density=Auto Size=6250 (Data=xxx + Gap=yyy Start=92635 -- {2}", Track, Head, BlockName); }
		}

	}

	public class BlockDescriptor {
		public BlockDescriptor() {
			this.Items = new ObservableCollection<DataDescriptor>();
		}

		public int Sector { get; set; }
		public int Length { get; set; }
		public string DataName { get; set; }
		public ObservableCollection<DataDescriptor> Items { get; set; }
		public string Content {
			get { return String.Format("{0} Sector={1} Length={2}", DataName, Sector, Length); }
		}
	}


	public class DataDescriptor {
		public DataDescriptor() {
			Value = new List<byte>();
		}
		public string Name { get; set; }
		public DataType Type { get; set; }
		public int Count { get; set; }
		public List<byte> Value { get; set; }

		public string Content {
			get {
				string s = String.Format("{0} Type={1} Count={2}{3}", Name, Type, Count, (Value.Count > 0) ? " Value " : "");
				foreach (byte b in Value) s += b.ToString("X2");
				return s;
			}
		}

	}

	public class DataContent {

	}
}

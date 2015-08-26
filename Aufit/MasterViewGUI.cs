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
using System.Windows.Documents;
using System.Windows.Media;
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

	class MasterTrack {

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
		
		
		public void displayDiskFormat(int track, int side) {
			int dataBytes = 0;
			int gapBytes = 0;
			int maxPostSize = 0;
			int firstBlock = 0;
			int lastBlock = 0;
			Global.DiskTrack dt = Global.Disk[track, side];

			if ((dt.track == null) || (dt.sectors.Count == 0))
				return;


			fdMasterView.Document = null;
			FlowDocument doc = new FlowDocument();
			doc.FontFamily = new FontFamily("Consolas");
			doc.FontSize = 15;
			Paragraph p = null;
			List l = null;
			ListItem li = null;

			p = new Paragraph();
			p.LineHeight = 1;
			p.Margin = new Thickness(0);
			p.Inlines.Add(new Run("Checking Protections") { FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
			p.Inlines.Add(new LineBreak());

			Protections.checkProtection(track, side);
			p.Inlines.Add("need protection description here");
			p.Inlines.Add(new LineBreak());
			doc.Blocks.Add(p);


			ImageRecord image = new ImageRecord();
			BlockDescriptor[] blocks = new BlockDescriptor[dt.sectors.Count];

			for (int i = 0; i < dt.sectors.Count; i++ ) {
				Sector s = dt.sectors[i];
				blocks[i].gapOffset = 0;
				blocks[i].cellType = SignalType.cell_2us;
				blocks[i].encoderType = BlockEncoderType.MFM;
				blocks[i].blockFlags = BlockFlags.BwGap | BlockFlags.FwGap;
				blocks[i].gapDefaultValue = 0;
				blocks[i].dataOffset = 0;

				// we compute the Block bytes and the Gap bytes
				int db = s.id.buffer.DataByte.Count + 3 + s.innerGap.buffer.DataByte.Count +
					((s.data != null) ? (s.data.buffer.DataByte.Count + 3) : 0);
				dataBytes += db;
				blocks[i].dataBits = (uint)(16 * db);
				int gb = (s.postGap != null) ? s.postGap.buffer.DataByte.Count : 0;
				gapBytes += gb;
				blocks[i].gapBits = (uint)(16 * gb);

				// we search the last block
				// for now we take the sector that have the largest post gap size
				if (s.postGap != null) {
					if (s.postGap.buffer.DataByte.Count > maxPostSize) {
						maxPostSize = s.postGap.buffer.DataByte.Count;
						lastBlock = i;
					}
				}
				else {
					if (s.innerGap.buffer.DataByte.Count > maxPostSize) {
						maxPostSize = s.innerGap.buffer.DataByte.Count;
						lastBlock = i;
					}
				}
			}	// for all sectors
			firstBlock = (lastBlock == (dt.sectors.Count - 1)) ? 0 : lastBlock + 1;

			image.track = (uint)track;
			image.side = (uint)side;
			image.density = Density.Auto;
			image.signalType = SignalType.cell_2us;
			image.trackBytes = (uint)dt.track.buffer.DataByte.Count;
			image.startBitPos = (uint)(dt.sectors[firstBlock].id.info.startBit - 4 * 16);
			image.startBytePos = image.startBitPos / 8;
			image.dataBits = (uint)(dataBytes * 16);
			image.gapBits = (uint)(gapBytes * 16);
			image.trackBits = image.dataBits + image.gapBits;
			image.blockCount = (uint)dt.sectors.Count;
			image.encoder = 0;
			image.dataKey = 0;
			image.reserved = new uint[4];

			p = new Paragraph();
			p.Margin = new Thickness(0, 0, 0, 0);
			p.LineHeight = 1;
			p.Inlines.Add(new Run(String.Format("Track {0:D2}.{1} Description", track, side)) 
				{ FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
			p.Inlines.Add(new LineBreak());
			//p.Inlines.Add(String.Format("Track Size={0} bytes ({1} bits) = {2} bytes ({3} bits) of Data + {4} bytes ({5} bits) of Gap",
			//	dt.track.buffer.DataByte.Count, (dataBytes + gapBytes) * 16,
			//	dataBytes, dataBytes * 16,
			//	gapBytes, gapBytes * 16));
			//p.Inlines.Add(new LineBreak());
			//p.Inlines.Add(String.Format("First block ID={0} start at bit {1} - ", 
			//	dt.sectors[firstBlock].id.Sector, dt.sectors[firstBlock].id.info.startBit - 4*16));

			//p.Inlines.Add(String.Format("Block Count {0}", dt.sectors.Count));
			//p.Inlines.Add(new LineBreak());

			p.Inlines.Add(image.ToString());
			//p.Inlines.Add(new LineBreak());
			doc.Blocks.Add(p);

			GapInfo gap = new GapInfo();
			for (int i = firstBlock; i < (firstBlock + dt.sectors.Count); i++ ) {
				int secIndex = i % dt.sectors.Count;
				Sector s = dt.sectors[secIndex];
				bool gapBadData;

				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(20, 0, 0, 0);
				p.Inlines.Add(new Run(String.Format("Block {0} Type={1} ID={2} Size={3}", 
					secIndex, "ID_DATA", s.id.Sector, Global.sectSize(s.id.Length)))
					{ FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
				p.Inlines.Add(String.Format(" Data={0} Gap={1} Cell={2} Enc={3} Flags={4}",
					blocks[secIndex].dataBits, blocks[secIndex].gapBits, blocks[secIndex].cellType, 
					blocks[secIndex].encoderType, blocks[secIndex].blockFlags));
				//p.Inlines.Add(new LineBreak());
				doc.Blocks.Add(p);

				// block content
				l = new List();
				l.Margin = new Thickness(0);
				l.LineHeight = 1;

				List<byte> sync = new List<byte>() { 0x44, 0x89, 0x44, 0x89, 0x44, 0x89};
				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(30, 0, 0, 0);
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				p.Inlines.Add("Sync 3 MFM bytes");
				//p.Inlines.Add(new LineBreak());
				drawContent(sync, p, s.id.info.startByte - 3, true);
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(30, 0, 0, 0);
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				p.Inlines.Add("ID 7 bytes");
				//p.Inlines.Add(new LineBreak());
				drawContent(s.id.buffer.DataByte, p, s.id.info.startByte, false);
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(30, 0, 0, 0);				
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				//gapBadData = checkGAP_OLD(s.innerGap, gap, false);
				gapBadData = checkGAP(s.innerGap, gap);
				List<byte> igap = new List<byte>();
				for (int j = 0; j < gap.count4E; j++)
					igap.Add(0x4E);
				for (int j = 0; j < gap.count00; j++)
					igap.Add(0);
				p.Inlines.Add(String.Format("IGAP {0} bytes", igap.Count));
				//	p.Inlines.Add(String.Format("Inner gap: {0}x4E - {1}x00", gap.count4E, gap.count00));
				if (gapBadData)
					p.Inlines.Add(String.Format(" has bad data @ {0} {1:F2} ms", gap.spliceByte, (double)gap.spliceTime / 1000000.0));
				drawContent(igap, p, s.innerGap.info.startByte, false);
 				//p.Inlines.Add(new LineBreak());
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(25, 0, 0, 0);
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				p.Inlines.Add("Sync 3 MFM bytes");
				//p.Inlines.Add(new LineBreak());
				drawContent(sync, p, s.data.info.startByte - 3, true);
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(25, 0, 0, 0);
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				p.Inlines.Add(String.Format("DATA {0} bytes", s.data.buffer.DataByte.Count));
				//p.Inlines.Add(new LineBreak());
				drawContent(s.data.buffer.DataByte, p, s.data.info.startByte, false);
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(25, 0, 0, 0);
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				//gapBadData = checkGAP_OLD(s.postGap, gap, false);
				gapBadData = checkGAP(s.postGap, gap);
				List<byte> pgap = new List<byte>();
				for (int j = 0; j < gap.count4E; j++)
					pgap.Add(0x4E);
				for (int j = 0; j < gap.count00; j++)
					pgap.Add(0);
				p.Inlines.Add(String.Format("PGAP {0} bytes", pgap.Count));
				//	p.Inlines.Add(String.Format("Post gap: {0}x4E - {1}x00", gap.count4E, gap.count00));
				if (gapBadData)
					p.Inlines.Add(String.Format(" has bad data @ {0} {1:F2} ms", gap.spliceByte, (double)gap.spliceTime / 1000000.0));
				drawContent(pgap, p, s.postGap.info.startByte, false);
				//p.Inlines.Add(new LineBreak());
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				doc.Blocks.Add(l);
			}

			// for post gap we generate stream gap elem
			// TODO

			//tbMasterView.ScrollToEnd();

			//doc.Blocks.Add(p);
			fdMasterView.Document = doc;


		}

		private bool checkGAP_OLD(GapField gap, GapInfo gi, bool skipFirst) {
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

		private bool checkGAP(GapField gap, GapInfo gi) {
			gi.reset();
			if (gap == null) return false;
			bool in4E = false;

			for (int i = gap.buffer.DataByte.Count - 1; i >= 0 ; i--) {
				byte b = gap.buffer.DataByte[i];
				if (!in4E && (isShifted00(b) || (i == (gap.buffer.DataByte.Count - 1)))) {
					gi.count00++;
					if (b != 0x00) {
						gi.spliceTime = gap.buffer.StartTime[i];
						gi.spliceByte = gap.info.startByte + i;
						gi.badDataFound = true;
					}
				}	// 00
				else {
					in4E = true;
					gi.count4E++;
					if ((b != 0x4E) && (i != 0)) {
						gi.spliceTime = gap.buffer.StartTime[i];
						gi.spliceByte = gap.info.startByte + i;
						gi.badDataFound = true;
					}
				}	// not 00
			}	// all bytes
			List<byte> foob = new List<byte>();
			List<short> foos;
			drawContent(foob, new Paragraph(), 0, true);

			return gi.badDataFound;
		}

		bool isShifted4E(byte b) {
			return ((b == 0x4E) || (b == 0x12) || (b == 0xC9) || (b == 0x24) || (b == 0x93) || (b == 0x48) || (b == 0x27) || (b == 0x90) ||
				    (b == 0xE4) || (b == 0x21) || (b == 0x9C) || (b == 0x42) || (b == 0x39) || (b == 0x84) || (b == 0x72) || (b == 0x09) );
		}


		bool isShifted00(byte b) {
			return ((b == 0x00) || (b == 0xFF));
		}

		public void drawContent(List<byte> buffer, Paragraph p, int startPos, bool isSync) {
			int bytes = buffer.Count;
			for (int i = 0; i < bytes; i += 16) {
				// display position
				p.Inlines.Add(new LineBreak());				
				p.Inlines.Add(new Run(String.Format("{0:D5}  ", i + startPos)));

				string ascii = "  ";
				for (int j = 0; j < 16; j++) {
					if ((i + j) < bytes) {


						//if (_noColor)
							p.Inlines.Add(String.Format("{0:X2}{1}", buffer[i + j], 
								isSync ? ((j % 2 == 0) ? "" : "  ") : " "));
						//else
						//	p.Inlines.Add(new Run(
						//		String.Format("{0:X2} ", buffer.DataByte[i + j])) {
						//			Background =
						//				((buffer.DataByte[i + j] >= 0xF5) && (buffer.DataByte[i + j] <= 0xF7) &&
						//				 FDC.isGapPhase(buffer.Phase[i + j])) ? Brushes.LightBlue :
						//				((buffer.MFMWord[i + j] == 0x0000) || (buffer.MFMWord[i + j] == 0xFFFF)) ? Brushes.Coral :
						//				((buffer.MFMWord[i + j] == 0x4489) || (buffer.MFMWord[i + j] == 0x5224)) ? Brushes.Yellow :
						//				(buffer.Phase[i + j] == Phases.p_data) ? Brushes.LightGreen :
						//				(buffer.Phase[i + j] == Phases.p_id) ? Brushes.GreenYellow :
						//				Brushes.White,
						//			Foreground =
						//				(isGap && (buffer.DataByte[i + j] != 0x00) && (buffer.DataByte[i + j] != 0x4E)) ? Brushes.DarkOrange :
						//				(buffer.MFMWord[i + j] == 0x4489) ? Brushes.Red :
						//				(buffer.MFMWord[i + j] == 0x5224) ? Brushes.Navy :
						//				(buffer.Phase[i + j] == Phases.p_id) ? Brushes.Blue :
						//				(buffer.Phase[i + j] == Phases.p_data) ? Brushes.Black :
						//				FDC.isGapPhase(buffer.Phase[i + j]) ? Brushes.Black :
						//				Brushes.DarkBlue,
						//			FontWeight =
						//				(buffer.MFMWord[i + j] == 0x4489) || (buffer.MFMWord[i + j] == 0x5224) ||
						//				(buffer.Phase[i + j] == Phases.p_id) ? FontWeights.Bold :
						//				FontWeights.Normal,
						//			//FontStyle =
						//			//(buffer.Bytes[i + j].startTime > 200000000) ? FontStyle = FontStyles.Italic :
						//			//FontStyles.Normal
						//		});
						char ch = Convert.ToChar(buffer[i + j]);
						ascii += Char.IsControl(ch) ? "." : ch.ToString();
					}
					else
						p.Inlines.Add(String.Format("   "));
				}	// one more line
				p.Inlines.Add(ascii);

				//if (cbMFM.IsChecked == true) {
				//	p.Inlines.Add("       ");
				//	for (int j = 0; j < 16; j++) {
				//		if ((i + j) < bytes)
				//			p.Inlines.Add(new Run(
				//				String.Format("{0:X4} ", buffer.MFMWord[i + j])) {
				//					Background = Brushes.LightGray
				//				});
				//	}
				//	p.Inlines.Add(new LineBreak());
				//}
			}	// all lines
		}


	}



}

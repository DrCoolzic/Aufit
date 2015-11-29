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

	/// <summary>
	/// Information about a post GAP (inter sector gap)
	/// </summary>
	class PostGapInfo {
		/// <summary></summary>
		public int spliceTime;

		/// <summary></summary>
		public int spliceByte;

		/// <summary>Number of 4E gap leading char</summary>
		public int count4E;

		/// <summary>Number of 00 gap trailing char</summary>
		public int count00;

		/// <summary>True if char other than 4E/00 found</summary>
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
			int trackDataBytes = 0;
			int trackGapBytes = 0;
			int maxPostSize = 0;
			int firstBlock = 0;
			int lastBlock = 0;
			int[] sectorSizeCount = new int[4] { 0, 0, 0, 0 };
			Global.DiskTrack dt = Global.Disk[track, side];

			// nothing to display
			if ((dt.track == null) || (dt.sectors.Count == 0)) return;

			// prepare document
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
			p.Inlines.Add("TODO -- need protection description here");
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

				// we compute the Block data bytes and the Gap bytes
				int dataBytes = s.id.buffer.DataByte.Count + s.id.postIdGap.buffer.DataByte.Count +
					((s.data != null) ? (s.data.buffer.DataByte.Count) : 0);
				trackDataBytes += dataBytes;
				blocks[i].dataBits = (uint)(16 * dataBytes);
				int gapBytes = (s.data.postDataGap != null) ? s.data.postDataGap.buffer.DataByte.Count : 0;
				trackGapBytes += gapBytes;
				blocks[i].gapBits = (uint)(16 * gapBytes);

				// we search the last block
				// for now we take the sector that have the largest post gap size
				if (s.data.postDataGap != null) {
					if (s.data.postDataGap.buffer.DataByte.Count > maxPostSize) {
						maxPostSize = s.data.postDataGap.buffer.DataByte.Count;
						lastBlock = i;
					}
				}
				else {
					if (s.id.postIdGap.buffer.DataByte.Count > maxPostSize) {
						maxPostSize = s.id.postIdGap.buffer.DataByte.Count;
						lastBlock = i;
					}
				}

				if (s.data != null) sectorSizeCount[(s.id.Length) & 3]++;
			}	// for all sectors
			firstBlock = (lastBlock == (dt.sectors.Count - 1)) ? 0 : lastBlock + 1;

			image.track = (uint)track;
			image.side = (uint)side;
			image.density = Density.Auto;
			image.signalType = SignalType.cell_2us;
			image.trackBytes = (uint)dt.track.buffer.DataByte.Count;
			image.startBitPos = (uint)(dt.sectors[firstBlock].id.info.startBit - 4 * 16);
			image.startBytePos = image.startBitPos / 8;
			image.dataBits = (uint)(trackDataBytes * 16);
			image.gapBits = (uint)(trackGapBytes * 16);
			image.trackBits = image.dataBits + image.gapBits;
			image.blockCount = (uint)dt.sectors.Count;
			image.encoder = 0;
			image.dataKey = 0;
			image.reserved = new uint[4];

			p = new Paragraph();
			p.Margin = new Thickness(0, 0, 0, 0);
			p.LineHeight = 1;

			string name = null;
			for (int i = 0; i < 4; i++) {
				if (sectorSizeCount[i] > 0) {
					if (name == null)
						name = "MFM_";
					else
						name += "_";
					name += sectorSizeCount[i].ToString();
					name += "x";
					name += (128 << i).ToString();
                }
            }
			p.Inlines.Add(new Run(String.Format("Track {0:D2}.{1} {2}", track, side, name)) 
				{ FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(image.ToString());
			//p.Inlines.Add(new LineBreak());
			doc.Blocks.Add(p);

			PostGapInfo gap = new PostGapInfo();
			for (int i = firstBlock; i < (firstBlock + dt.sectors.Count); i++ ) {
				int secIndex = i % dt.sectors.Count;
				Sector s = dt.sectors[secIndex];
				bool gapBadData = false;

				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(20, 0, 0, 0);
				p.Inlines.Add(new Run(String.Format("Block {0} Type={1} ID={2} Size={3}", 
					secIndex, "ID_DATA", s.id.Sector, Global.sectSize(s.id.Length)))
					{ FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
				p.Inlines.Add(new LineBreak());
				p.Inlines.Add(String.Format(" Data={0} Gap={1} Cell={2} Enc={3} Flags={4}",
					blocks[secIndex].dataBits, blocks[secIndex].gapBits, blocks[secIndex].cellType, 
					blocks[secIndex].encoderType, blocks[secIndex].blockFlags));
				//p.Inlines.Add(new LineBreak());
				doc.Blocks.Add(p);

				// block content
				l = new List();
				l.Margin = new Thickness(0);
				l.LineHeight = 1;

				// standard sync mark for MFM block
				List<byte> sync = new List<byte>() { 0x44, 0x89, 0x44, 0x89, 0x44, 0x89};
				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(30, 0, 0, 0);
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				p.Inlines.Add("Sync 3 MFM bytes");
				//p.Inlines.Add(new LineBreak());
				displayContent(sync, p, s.id.info.startByte - 3, true);
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				// ID field
				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(30, 0, 0, 0);
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				p.Inlines.Add("ID 7 bytes");
				//p.Inlines.Add(new LineBreak());
				displayContent(s.id.buffer.DataByte, p, s.id.info.startByte, false);
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				// inner gap
				li = new ListItem();
				li.LineHeight = 1;
				li.Margin = new Thickness(30, 0, 0, 0);				
				p = new Paragraph();
				p.LineHeight = 1;
				p.Margin = new Thickness(5, 0, 0, 0);
				//gapBadData = checkGAP_OLD(s.id.gap, gap, false);
				gapBadData = checkGAP(s.id.postIdGap, gap);
				List<byte> igap = new List<byte>();
				for (int j = 0; j < gap.count4E; j++)
					igap.Add(0x4E);
				for (int j = 0; j < gap.count00; j++)
					igap.Add(0);
				if (s.data != null) p.Inlines.Add(String.Format("IGAP {0} bytes", igap.Count));
				else p.Inlines.Add(String.Format("PGAP {0} bytes", igap.Count));
				//	p.Inlines.Add(String.Format("Inner gap: {0}x4E - {1}x00", gap.count4E, gap.count00));
				if (gapBadData)
					p.Inlines.Add(String.Format(" has bad data @ {0} {1:F2} ms", gap.spliceByte, (double)gap.spliceTime / 1000000.0));
				displayContent(igap, p, s.id.postIdGap.info.startByte, false);
 				//p.Inlines.Add(new LineBreak());
				li.Blocks.Add(p);
				l.ListItems.Add(li);

				// if sector has data field
				if (s.data != null) {
                    // display MFM
					li = new ListItem();
					li.LineHeight = 1;
					li.Margin = new Thickness(25, 0, 0, 0);
					p = new Paragraph();
					p.LineHeight = 1;
					p.Margin = new Thickness(5, 0, 0, 0);
					p.Inlines.Add("Sync 3 MFM bytes");
					//p.Inlines.Add(new LineBreak());
					displayContent(sync, p, s.data.info.startByte - 3, true);
					li.Blocks.Add(p);
					l.ListItems.Add(li);

					// Display data
					li = new ListItem();
					li.LineHeight = 1;
					li.Margin = new Thickness(25, 0, 0, 0);
					p = new Paragraph();
					p.LineHeight = 1;
					p.Margin = new Thickness(5, 0, 0, 0);
					p.Inlines.Add(String.Format("DATA {0} bytes", s.data.buffer.DataByte.Count));
					//p.Inlines.Add(new LineBreak());
					displayContent(s.data.buffer.DataByte, p, s.data.info.startByte, false);
					li.Blocks.Add(p);
					l.ListItems.Add(li);

					// Display post gap
					li = new ListItem();
					li.LineHeight = 1;
					li.Margin = new Thickness(25, 0, 0, 0);
					p = new Paragraph();
					p.LineHeight = 1;
					p.Margin = new Thickness(5, 0, 0, 0);
					//gapBadData = checkGAP_OLD(s.data.gap, gap, false);
					gapBadData = checkGAP(s.data.postDataGap, gap);
					List<byte> pgap = new List<byte>();
					for (int j = 0; j < gap.count4E; j++)
						pgap.Add(0x4E);
					for (int j = 0; j < gap.count00; j++)
						pgap.Add(0);
					p.Inlines.Add(String.Format("PGAP {0} bytes", pgap.Count));
					//	p.Inlines.Add(String.Format("Post gap: {0}x4E - {1}x00", gap.count4E, gap.count00));
					if (gapBadData)
						p.Inlines.Add(String.Format(" has bad data @ {0} {1:F2} ms", gap.spliceByte, (double)gap.spliceTime / 1000000.0));
					if (s.data.postDataGap != null)
						displayContent(pgap, p, s.data.postDataGap.info.startByte, false);
					//p.Inlines.Add(new LineBreak());
					li.Blocks.Add(p);
					l.ListItems.Add(li);
				}   // sector has data

				doc.Blocks.Add(l);
			}	// all sectors

			// for post gap we generate stream gap elem
			// TODO

			fdMasterView.Document = doc;
		}


		private bool checkGAP_OLD(GapField gap, PostGapInfo gi, bool skipFirst) {
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


		/// <summary>
		/// Check a GAP field and fill a PostGapInfo structure
		/// </summary>
		/// <param name="gap">The gap field</param>
		/// <param name="gi">The PostGapInfo structure</param>
		/// <returns>True if bad gap value found</returns>
		private bool checkGAP(GapField gap, PostGapInfo gi) {
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
			return gi.badDataFound;
		}


		/// <summary>
		/// Check if byte b is a possible MFM shifted value of 4E
		/// </summary>
		/// <param name="b">Byte value</param>
		/// <returns>True if byte b is a possible shifted value of 4E</returns>
		bool isShifted4E(byte b) {
			return ((b == 0x4E) || (b == 0x12) || (b == 0xC9) || (b == 0x24) || (b == 0x93) || (b == 0x48) || (b == 0x27) || (b == 0x90) ||
				    (b == 0xE4) || (b == 0x21) || (b == 0x9C) || (b == 0x42) || (b == 0x39) || (b == 0x84) || (b == 0x72) || (b == 0x09) );
		}


		/// <summary>
		/// Check if byte b is a possible MFM shifted value of 00
		/// </summary>
		/// <param name="b">Byte to test</param>
		/// <returns>True if byte b is a possible shifted value of 4E</returns>
		bool isShifted00(byte b) {
			return ((b == 0x00) || (b == 0xFF));
		}


		/// <summary>
		/// Display the content of a buffer in a paragraph
		/// </summary>
		/// <param name="buffer">Buffer to draw</param>
		/// <param name="p">Paragraph</param>
		/// <param name="startPos">The starting position of the buffer</param>
		/// <param name="MFM">When true display MFM 16 bits values otherwise display 8 bits value</param>
		public void displayContent(List<byte> buffer, Paragraph p, int startPos, bool MFM) {
			int bytes = buffer.Count;
			for (int i = 0; i < bytes; i += 16) {
				// display position
				p.Inlines.Add(new LineBreak());				
				p.Inlines.Add(new Run(String.Format("{0:D5}  ", i + startPos)));

				string ascii = "  ";
				for (int j = 0; j < 16; j++) {
					if ((i + j) < bytes) {
						p.Inlines.Add(String.Format("{0:X2}{1}", 
							buffer[i + j], MFM ? ((j % 2 == 0) ? "" : "  ") : " "));

						char ch = Convert.ToChar(buffer[i + j]);
						ascii += Char.IsControl(ch) ? "." : ch.ToString();
					}
					else
						p.Inlines.Add(String.Format("   "));
				}   // one more line
				p.Inlines.Add(ascii);
			}   // all lines
		}	// displayContent

	}	// class MainWindow
}	// namespace Aufit

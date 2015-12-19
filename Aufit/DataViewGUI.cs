/*!
@file BufferWindow.xaml.cs
 
<summary>Open a Window and displays buffer information</summary>

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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
// using System.Windows.Controls.RichTextBox;

using FDCPackage;
using GlobalPackage;
using Microsoft.Win32;
using System.IO;
using System.Windows.Threading;
using System.Threading;

namespace Aufit {

	public partial class MainWindow : Window {
		bool _noColor = false;

		public void drawLayout(int track, int side, int rev) {
			// Force reread
			FDC.resetTrack(track, side);
			FDC.processTrack(track, side, rev);
			Global.DiskTrack dt = Global.Disk[track, side];

			fdDataView.Document = null;
			FlowDocument doc = new FlowDocument();
			doc.FontFamily = new FontFamily("Consolas");
			doc.FontSize = 15;

			Paragraph p = new Paragraph();
			p.Margin = new Thickness(0);
			//p.Inlines.Add(new Run(Aufit.MainWindow.ImageName) { FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
			p.Inlines.Add(Aufit.MainWindow.ImageName);
			p.Inlines.Add(new LineBreak());

			p.Inlines.Add(String.Format("Track {0:D2}.{1} {2} bytes has {3} sectors (rev={4})", track, side, dt.track.buffer.DataByte.Count, dt.sectors.Count, rev + 1));
			doc.Blocks.Add(p);

			Table tab = new Table();
			// define columns
			for (int i = 0; i < 10; i++) {
				tab.Columns.Add(new TableColumn());
				tab.Columns[i].Width = new GridLength(70);
				if (i < 3)
					tab.Columns[i].Background = Brushes.Yellow;
				else if ((i == 3) || (i == 9))
					tab.Columns[i].Background = Brushes.LightGreen;
				else
					tab.Columns[i].Background = Brushes.LightSkyBlue;
			}
			tab.RowGroups.Add(new TableRowGroup());

			// header top row
			tab.RowGroups[0].Rows.Add(new TableRow());
			TableRow currentRow = tab.RowGroups[0].Rows[0];
			currentRow.FontWeight = FontWeights.Bold;
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("ID Field"))));
			currentRow.Cells[0].ColumnSpan = 3;
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("GAP"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Data Field"))));
			currentRow.Cells[2].ColumnSpan = 5;
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("GAP"))));

			// header second row
			tab.RowGroups[0].Rows.Add(new TableRow());
			currentRow = tab.RowGroups[0].Rows[1];
			currentRow.FontWeight = FontWeights.Bold;
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Sector"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Position"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("CRC"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Bytes"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Size"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Position"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Length"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("CRC"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Fuzzy"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Bytes"))));

			int row = 2;
			for (int sect = 0; sect < dt.sectors.Count; sect++ ) {
				tab.RowGroups[0].Rows.Add(new TableRow());
				currentRow = tab.RowGroups[0].Rows[sect+row];

				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", dt.sectors[sect].id.Sector)))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", dt.sectors[sect].id.info.startTime / 1000)))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0}", (dt.sectors[sect].id.info.crc == 0) ? "OK" : "BAD")))));

				if (dt.sectors[sect].id.gap != null)
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0:D}", dt.sectors[sect].id.gap.buffer.DataByte.Count)
						))));
				else
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));

				if (dt.sectors[sect].data != null) {
					string cnt = String.Format("{0:D}", dt.sectors[sect].data.buffer.DataByte.Count);
					if (dt.sectors[sect].data.truncByteCount > 0)
						cnt += String.Format(" {0:D}", dt.sectors[sect].data.truncByteCount);
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(cnt))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0:D}", dt.sectors[sect].data.info.startTime / 1000)))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0:D}", dt.sectors[sect].data.info.timeLength / 1000)))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0}", (dt.sectors[sect].data.info.crc == 0) ? "OK" : "BAD")))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0}", (dt.sectors[sect].data.fuzzy != null) ? "Yes" : "No")))));

					if (dt.sectors[sect].data.gap != null)
						currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
							String.Format("{0:D}", dt.sectors[sect].data.gap.buffer.DataByte.Count)
							))));
					else currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));

				}   // sector data

				if (dt.sectors[sect].dataNoId != null) {
					tab.RowGroups[0].Rows.Add(new TableRow());
					row++;
					currentRow = tab.RowGroups[0].Rows[sect + row];

					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));

					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0:D}", dt.sectors[sect].dataNoId.buffer.DataByte.Count)))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0:D}", dt.sectors[sect].dataNoId.info.startTime / 1000)))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0:D}", dt.sectors[sect].dataNoId.info.timeLength / 1000)))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0}", (dt.sectors[sect].dataNoId.info.crc == 0) ? "OK" : "BAD")))));
					currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
						String.Format("{0}", (dt.sectors[sect].dataNoId.fuzzy != null) ? "Yes" : "No")))));

					if (dt.sectors[sect].dataNoId.gap != null)
						currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
							String.Format("{0:D}", dt.sectors[sect].dataNoId.gap.buffer.DataByte.Count)
							))));
					else
						currentRow.Cells.Add(new TableCell(new Paragraph(new Run("0"))));

				}   // sector extra data

			}   // for all sectors
			doc.Blocks.Add(tab);
			fdDataView.Document = doc;
		}


		/// <summary>
		/// Draw the content of the buffer
		/// </summary>
		public void drawSectorBuffer(int track, int side, int rev) {
			// Force reread
			FDC.resetTrack(track, side);
			FDC.processTrack(track, side, rev);

			List<Sector> sectors = Global.Disk[track, side].sectors;

			fdDataView.Document = null;
			FlowDocument doc = new FlowDocument();
			doc.FontFamily = new FontFamily("Consolas");
			doc.FontSize = 15;

			Paragraph p = new Paragraph();
			p.Margin = new Thickness(0);
			p.Inlines.Add(Aufit.MainWindow.ImageName);
			p.Inlines.Add(new LineBreak());

			p.Inlines.Add(String.Format("Track {0:D2}.{1} has {2} sectors (rev={3})", track, side, sectors.Count, rev + 1));
			if (Global.Disk[track, side].hasIncompleteData)
				p.Inlines.Add(new Run(
					String.Format(" *** Incomplete Data *** ", track, side, sectors.Count)
					) { FontWeight = FontWeights.Bold });

			if (Global.Disk[track, side].hasIncompleteGap)
				p.Inlines.Add(new Run(
					String.Format(" *** Incomplete Gap *** ", track, side, sectors.Count)
					) { FontWeight = FontWeights.Bold });

			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(new LineBreak());

			foreach (Sector sector in sectors) {
				p.Inlines.Add(new Run(String.Format(" --- Sector {0} --- ", sector.id.Sector)) 
					{ Background = Brushes.LightGray, FontWeight = FontWeights.Bold });
				if (sector.flags != SectorFlags.None)
					p.Inlines.Add(String.Format(" {0}", sector.flags));
				p.Inlines.Add(new LineBreak());

				p.Inlines.Add(new Run("ID Field: ") { FontWeight = FontWeights.Bold });
				p.Inlines.Add(String.Format("IAM={0:X2} T={1:X2} H={2:X2} S={3:X2} L={4:X2} CRC={5:X4} {6}", sector.id.IDAM,
					sector.id.Track, sector.id.Head, sector.id.Sector, sector.id.Length, sector.id.CRC, (sector.id.info.crc == 0) ? "Good" : "Bad"));
				p.Inlines.Add(new LineBreak());
				p.Inlines.Add(String.Format("   START b={0}/{1} B={2} T={3:F2} ms", 
					sector.id.info.startBit, sector.id.info.startBitIAM, sector.id.info.startByte, 
					(double)sector.id.info.startTime / 1000000.0));
				p.Inlines.Add(String.Format(" - LENGTH b={0} B={1} T={2:F2}µs",
					sector.id.info.bitCount, sector.id.buffer.DataByte.Count, (double)sector.id.info.timeLength / 1000.0));
				p.Inlines.Add(String.Format(" - FLUX={0}-{1}", sector.id.info.firstIndex, sector.id.info.lastIndex));
				p.Inlines.Add(new LineBreak());
				drawBuffer(sector.id.buffer, p, sector.id.info.startByte, false);

				if (sector.id.gap != null) {
					p.Inlines.Add(new Run("Inner GAP Field:") { FontWeight = FontWeights.Bold });
					foreach (Splice splice in sector.id.gap.splices) {
						if (splice.type == SpliceType.Seq00) continue;
						p.Inlines.Add(String.Format(" {0} Splice @{1:F2}ms bit={2}", splice.type, splice.time / 1000000.0, splice.bit));
					}
					p.Inlines.Add(new LineBreak());
					p.Inlines.Add(String.Format("   START b={0} B={1} T={2:F2}ms",
						sector.id.gap.info.startBit, sector.id.gap.info.startByte, (double)sector.id.gap.info.startTime / 1000000.0));
					p.Inlines.Add(String.Format(" - LENGTH b={0} B={1} T={2:F2}µs",
						sector.id.gap.info.bitCount, sector.id.gap.buffer.DataByte.Count, (double)sector.id.gap.info.timeLength / 1000.0));
					p.Inlines.Add(String.Format(" - FLUX={0}-{1}", sector.id.gap.info.firstIndex, sector.id.gap.info.lastIndex));
					p.Inlines.Add(new LineBreak());
					drawBuffer(sector.id.gap.buffer, p, sector.id.gap.info.startByte, true);
				}

				if (sector.data != null) {
					p.Inlines.Add(new Run("DATA Field: ") { FontWeight = FontWeights.Bold });
					if ((sector.data.buffer.DataByte.Count - 6) != Global.sectSize(sector.id.Length))
						p.Inlines.Add(" *** Incomplete *** ");
					p.Inlines.Add(String.Format("DAM={0:X2} {1} bytes start at {2}µs length {3}µs CRC={4:X4}{5}{6}",
						sector.data.DAM, sector.data.buffer.DataByte.Count, sector.data.info.startTime / 1000, sector.data.info.timeLength / 1000,
						sector.data.CRC, (sector.data.info.crc == 0) ? " Good" : " Bad",
						(sector.data.fuzzy != null) ? " Fuzzy sector" : ""));
					p.Inlines.Add(new LineBreak());
					p.Inlines.Add(String.Format("   START b={0}/{1} B={2} T={3:F2}ms",
						sector.data.info.startBit, sector.data.info.startBitIAM, sector.data.info.startByte, (double)sector.data.info.startTime / 1000000.0));
					//p.Inlines.Add(new LineBreak());
					p.Inlines.Add(String.Format(" - LENGTH b={0} B={1} T={2:F2}µs",
						sector.data.info.bitCount, sector.data.buffer.DataByte.Count, (double)sector.data.info.timeLength / 1000.0));
					p.Inlines.Add(String.Format(" - FLUX={0}-{1}", sector.data.info.firstIndex, sector.data.info.lastIndex));
					if (sector.data.truncByteCount > 0) {
						p.Inlines.Add(new LineBreak());
						p.Inlines.Add(String.Format("   TRUNCATED to {0} Bytes", sector.data.truncByteCount));
					}
					p.Inlines.Add(new LineBreak());
					drawBuffer(sector.data.buffer, p, sector.data.info.startByte, false);

					if (sector.data.fuzzy != null) {
						int byte80 = 0;
						bool onlyByte80 = true;
						p.Inlines.Add(new LineBreak());
						p.Inlines.Add(String.Format("Sector {0} Fuzzy Bytes Mask array", sector.id.Sector));
						p.Inlines.Add(new LineBreak());
						for (int line = 0; line < sector.data.fuzzy.Count() / 16; line++) {
							p.Inlines.Add(String.Format("{0:D4} ", line * 16 + sector.data.info.startByte + 4));
							for (int col = 0; col < 16; col++) {
								byte b = sector.data.fuzzy[(line * 16) + col + 4];
								if (b == 0x80)
									byte80++;
								else
									if (b != 0x00) 
										onlyByte80 = false;
								p.Inlines.Add(new Run(
									String.Format("{0:X2} ", b)) {
										FontWeight = (b == 0) ? FontWeights.Normal : FontWeights.Bold
										// , Background = ((b != 0x80) && (b != 0)) ? Brushes.Yellow : Brushes.Transparent
									});
							}
							p.Inlines.Add(new LineBreak());
						}
						if (onlyByte80) {
							p.Inlines.Add(new Run(
								String.Format("{0} fuzzy bytes of type Dungeon Master / Chaos Strike Back found", byte80)));
							p.Inlines.Add(new LineBreak());
						}
					}   // fuzzy
					if (sector.data.gap != null) {
						p.Inlines.Add(new Run("Post GAP Field: ") { FontWeight = FontWeights.Bold });
						foreach (Splice splice in sector.data.gap.splices) {
							if (splice.type == SpliceType.Seq00) continue;
							p.Inlines.Add(String.Format(" {0} Splice @{1:F2}ms bit={2}", splice.type, splice.time / 1000000.0, splice.bit));
						}
						p.Inlines.Add(new LineBreak());
						p.Inlines.Add(String.Format("   START b={0} B={1} T={2:F2}ms",
							sector.data.gap.info.startBit, sector.data.gap.info.startByte, (double)sector.data.gap.info.startTime / 1000000.0));
						//p.Inlines.Add(new LineBreak());
						p.Inlines.Add(String.Format(" - LENGTH b={0} B={1} T={2:F2}µs",
							sector.data.gap.info.bitCount, sector.data.gap.buffer.DataByte.Count, (double)sector.data.gap.info.timeLength / 1000.0));
						p.Inlines.Add(String.Format(" - FLUX={0}-{1}", sector.data.gap.info.firstIndex, sector.data.gap.info.lastIndex));
						p.Inlines.Add(new LineBreak());
						drawBuffer(sector.data.gap.buffer, p, sector.data.gap.info.startByte, true);
					}
				}   // has data


				if (sector.dataNoId != null) {
					p.Inlines.Add(new Run("EXTRA DATA Field: ") { FontWeight = FontWeights.Bold });
					DataField data = sector.dataNoId;
					if ((data.buffer.DataByte.Count - 6) != Global.sectSize(sector.id.Length))
						p.Inlines.Add(" *** Incomplete *** ");
					p.Inlines.Add(String.Format("DAM={0:X2} {1} bytes start at {2}µs length {3}µs CRC={4:X4}{5}{6}",
						data.DAM, data.buffer.DataByte.Count, data.info.startTime / 1000, data.info.timeLength / 1000,
						data.CRC, (data.info.crc == 0) ? " Good" : " Bad",
						(data.fuzzy != null) ? " Fuzzy sector" : ""));
					p.Inlines.Add(new LineBreak());
					p.Inlines.Add(String.Format("   START b={0}/{1} B={2} T={3:F2}ms",
						data.info.startBit, data.info.startBitIAM, data.info.startByte, (double)data.info.startTime / 1000000.0));
					//p.Inlines.Add(new LineBreak());
					p.Inlines.Add(String.Format(" - LENGTH b={0} B={1} T={2:F2}µs",
						data.info.bitCount, data.buffer.DataByte.Count, (double)data.info.timeLength / 1000.0));
					p.Inlines.Add(String.Format(" - FLUX={0}-{1}", data.info.firstIndex, data.info.lastIndex));
					p.Inlines.Add(new LineBreak());
					drawBuffer(data.buffer, p, data.info.startByte, false);

					if (data.fuzzy != null) {
						int byte80 = 0;
						bool onlyByte80 = true;
						p.Inlines.Add(new LineBreak());
						p.Inlines.Add(String.Format("Sector {0} Fuzzy Bytes Mask array", sector.id.Sector));
						p.Inlines.Add(new LineBreak());
						for (int line = 0; line < data.fuzzy.Count() / 16; line++) {
							p.Inlines.Add(String.Format("{0:D4} ", line * 16 + data.info.startByte + 4));
							for (int col = 0; col < 16; col++) {
								byte b = data.fuzzy[(line * 16) + col + 1];
								if (b == 0x80)
									byte80++;
								else
									if (b != 0x00)
									onlyByte80 = false;
								p.Inlines.Add(new Run(
									String.Format("{0:X2} ", b)) {
									FontWeight = (b == 0) ? FontWeights.Normal : FontWeights.Bold
									// , Background = ((b != 0x80) && (b != 0)) ? Brushes.Yellow : Brushes.Transparent
								});
							}
							p.Inlines.Add(new LineBreak());
						}
						if (onlyByte80) {
							p.Inlines.Add(new Run(
								String.Format("{0} fuzzy bytes of type Dungeon Master / Chaos Strike Back found", byte80)));
							p.Inlines.Add(new LineBreak());
						}
					}   // fuzzy
					if (sector.dataNoId.gap != null) {
						p.Inlines.Add(new Run("EXTRA POST GAP Field: ") { FontWeight = FontWeights.Bold });
						foreach (Splice splice in sector.dataNoId.gap.splices) {
							if (splice.type == SpliceType.Seq00) continue;
							p.Inlines.Add(String.Format(" {0} Splice @{1:F2}ms bit={2}", splice.type, splice.time / 1000000.0, splice.bit));
						}
						p.Inlines.Add(new LineBreak());
						p.Inlines.Add(String.Format("   START b={0} B={1} T={2:F2}ms",
							sector.dataNoId.gap.info.startBit, sector.dataNoId.gap.info.startByte, (double)sector.dataNoId.gap.info.startTime / 1000000.0));
						//p.Inlines.Add(new LineBreak());
						p.Inlines.Add(String.Format(" - LENGTH b={0} B={1} T={2:F2}µs",
							sector.dataNoId.gap.info.bitCount, sector.dataNoId.gap.buffer.DataByte.Count, (double)sector.dataNoId.gap.info.timeLength / 1000.0));
						p.Inlines.Add(String.Format(" - FLUX={0}-{1}", sector.dataNoId.gap.info.firstIndex, sector.dataNoId.gap.info.lastIndex));
						p.Inlines.Add(new LineBreak());
						drawBuffer(sector.dataNoId.gap.buffer, p, sector.dataNoId.gap.info.startByte, true);
					}


				}   // has data

				p.Inlines.Add(new LineBreak());
			}

			doc.Blocks.Add(p);
			fdDataView.Document = doc;
		}


		/// <summary>
		/// Display the content of the Track Information in a TextBlock
		/// </summary>
		/// <param name="track">Specifies the track from the disk</param>
		/// <param name="side">Specifies the side from the disk</param>
		/// <param name="rev">Revolution number</param>
		public void drawTrackBuffer(int track, int side, int rev) {
			// Force reread
			FDC.resetTrack(track, side);
			FDC.processTrack(track, side, rev);
			Track tr = Global.Disk[track, side].track;

			fdDataView.Document = null;
			FlowDocument doc = new FlowDocument();
			//d.FontFamily = new FontFamily("Lucida Console");			
			doc.FontFamily = new FontFamily("Consolas");
			doc.FontSize = 15;

			Paragraph p = new Paragraph(new Run(Aufit.MainWindow.ImageName));
			//p.FontWeight = FontWeights.Bold;
			//p.FontFamily = new FontFamily("Century Gothic, Courier New");
			p.Margin = new Thickness(0);
			doc.Blocks.Add(p);

			p = new Paragraph(new Run(String.Format("Track {0:D2}.{1} {2} bytes (rev={3}) Length {4} µs",
				track, side, tr.buffer.DataByte.Count, rev + 1, tr.info.timeLength / 1000)) { FontWeight = FontWeights.Normal });
			p.Margin = new Thickness(0);
			p.Inlines.Add(new LineBreak());
			doc.Blocks.Add(p);

			p.Inlines.Add(String.Format("   START b={0} B={1} T={2:F2}ms",
				tr.info.startBit, tr.info.startByte, (double)tr.info.startTime / 1000000.0));
			p.Inlines.Add(String.Format(" - LENGTH b={0} B={1} T={2:F2}µs",
				tr.info.bitCount, tr.buffer.DataByte.Count, (double)tr.info.timeLength / 1000.0));
			p.Inlines.Add(String.Format(" - FLUX={0}-{1}", tr.info.firstIndex, tr.info.lastIndex));
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(String.Format("   FLAGS = {0}", tr.flags));
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(new LineBreak());

			drawBuffer(tr.buffer, p, 0, false);
			fdDataView.Document = doc;
		}


		/// <summary>
		/// Display the content of TrackBuffer buffer in the tbBufferContent TextBox
		/// </summary>
		/// <param name="buffer">The buffer to display</param>
		/// <param name="message">an optional message to display before the buffer</param>
		public void drawBuffer(TrackBuffer buffer, Paragraph p, int startPos, bool isGap) {
			int bytes = buffer.DataByte.Count;
			int lineLength = 16;
			if (cbMFM.IsChecked == true)
				lineLength = 8;

			for (int i = 0; i < bytes; i += lineLength) {
				// display position
				p.Inlines.Add(new Run(String.Format("{0:D5}  ", i + startPos)));

				// display bitWidth
				p.Inlines.Add(new Run(
						String.Format("{0:D4} ", buffer.BitWidth[i])) {
							Foreground = (buffer.BitWidth[i] > 2050) ? Brushes.Blue :
							(buffer.BitWidth[i] < 1950) ? Brushes.Red : Brushes.Black,
							FontWeight = ((buffer.BitWidth[i] > 2050) || (buffer.BitWidth[i] < 1950)) ?
											FontWeights.Bold : FontWeights.Normal
						});

				// display start time
				p.Inlines.Add(String.Format("{0:D6}  ", buffer.StartTime[i] / 1000));

				string ascii = "  ";
				for (int j = 0; j < lineLength; j++) {
					if ((i + j) < bytes) {
						if (_noColor)
							p.Inlines.Add(String.Format("{0:X2} ", buffer.DataByte[i + j]));
						else
							p.Inlines.Add(new Run(
								String.Format("{0:X2} ", buffer.DataByte[i + j])) {
									Background =
										((buffer.DataByte[i + j] >= 0xF5) && (buffer.DataByte[i + j] <= 0xF7) &&
										 FDC.isGapPhase(buffer.Phase[i + j])) ? Brushes.LightBlue :
										((buffer.MFMWord[i + j] == 0x0000) || (buffer.MFMWord[i + j] == 0xFFFF)) ? Brushes.Coral :
										((buffer.MFMWord[i + j] == 0x4489) || (buffer.MFMWord[i + j] == 0x5224)) ? Brushes.Yellow :
										(buffer.Phase[i + j] == Phase.Data) ? Brushes.LightGreen :
										(buffer.Phase[i + j] == Phase.Id) ? Brushes.GreenYellow :
										Brushes.White,
									Foreground =
										(isGap && (buffer.DataByte[i + j] != 0x00) && (buffer.DataByte[i + j] != 0x4E)) ? Brushes.DarkBlue :
										(buffer.MFMWord[i + j] == 0x4489) ? Brushes.Red :
										(buffer.MFMWord[i + j] == 0x5224) ? Brushes.Navy :
										(buffer.Phase[i + j] == Phase.Id) ? Brushes.Blue :
										(buffer.Phase[i + j] == Phase.Data) ? Brushes.Black :
										FDC.isGapPhase(buffer.Phase[i + j]) ? Brushes.Black :
										Brushes.DarkBlue,
									FontWeight =
										(buffer.MFMWord[i + j] == 0x4489) || (buffer.MFMWord[i + j] == 0x5224) ||
										(buffer.Phase[i + j] == Phase.Id) ? FontWeights.Bold :
										FontWeights.Normal,
									//FontStyle =
									//(buffer.Bytes[i + j].startTime > 200000000) ? FontStyle = FontStyles.Italic :
									//FontStyles.Normal
								});

							
							char ch = Convert.ToChar(buffer.DataByte[i + j]);
						ascii += Char.IsControl(ch) ? "." : ch.ToString();
					}
					else
						p.Inlines.Add(String.Format("   "));
				}   // one more line

				if (cbMFM.IsChecked == true) {
					p.Inlines.Add("  ");
					for (int j = 0; j < lineLength; j++) {
						if ((i + j) < bytes)
							p.Inlines.Add(new Run(
								String.Format("{0:X4} ", buffer.MFMWord[i + j])) {
								Background =
										((buffer.MFMWord[i + j] == 0x0000) || (buffer.MFMWord[i + j] == 0xFFFF)) ? Brushes.Coral :
										((buffer.MFMWord[i + j] == 0x4489) || (buffer.MFMWord[i + j] == 0x5224)) ? Brushes.Yellow :
										Brushes.Lavender
							});
						else p.Inlines.Add(String.Format("     "));
					}
				}
				p.Inlines.Add(ascii);
				p.Inlines.Add(new LineBreak());
			}   // all lines
		}


		private void dataSaveClick(object sender, RoutedEventArgs e) {
			saveFlowDocumentScrollViewer(fdDataView);
			//SaveFileDialog dlg = new SaveFileDialog();
			//dlg.Filter = "Rich Text Format|*.rtf|All files (*.*)|*.*";
			////dlg.Filter = "Text Format|*.txt|All files (*.*)|*.*";
			//if (dlg.ShowDialog() == true) {
			//	//TextRange range = new TextRange(_flowDoc.ContentStart, _flowDoc.ContentEnd);
			//	TextRange range = new TextRange(fdDataView.Document.ContentStart, fdDataView.Document.ContentEnd);
			//	using (FileStream file = new FileStream(dlg.FileName, FileMode.Create)) {
			//		range.Save(file, System.Windows.DataFormats.Rtf);
			//		//range.Save(file, System.Windows.DataFormats.Text);
			//		file.Close();
			//	}
			//}
		}


		private void btTrackClick(object sender, RoutedEventArgs e) {
			if (Global.Disk[CurrentTrack, CurrentSide].track == null) return;
			drawTrackBuffer(CurrentTrack, CurrentSide, CurrentRev);
		}


		private void btSectorClick(object sender, EventArgs e) {
			if (Global.Disk[CurrentTrack, CurrentSide].sectors == null) return;
			drawSectorBuffer(CurrentTrack, CurrentSide, CurrentRev);
		}


		private void btLayoutClick(object sender, RoutedEventArgs e) {
			if (Global.Disk[CurrentTrack, CurrentSide].track == null) return;
			drawLayout(CurrentTrack, CurrentSide, CurrentRev);
		}

	}
}

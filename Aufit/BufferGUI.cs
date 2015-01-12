/*!
@file BufferWindow.xaml.cs
 
<summary>Open a Window and displays buffer information</summary>

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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
// using System.Windows.Controls.RichTextBox;

using WD1772Package;
using GlobalPackage;
using Microsoft.Win32;
using System.IO;
using System.Windows.Threading;
using System.Threading;

namespace Aufit {

	enum DataView { None, Layout, Track, Sector }

	public partial class MainWindow : Window {
		bool _noColor = false;
		DataView _view = DataView.None;

		public void drawLayout(int track, int side, int rev) {
			// Force reread
			FDC.resetTrack(track, side);
			FDC.fdcRealAll(track, side, rev);
			Global.DiskTrack dt = Global.Disk[track, side];

			fdViewer.Document = null;
			FlowDocument doc = new FlowDocument();
			doc.FontFamily = new FontFamily("Consolas");
			doc.FontSize = 15;
			_view = DataView.Layout;

			Paragraph p = new Paragraph();
			p.Margin = new Thickness(0);
			p.Inlines.Add(new Run(Aufit.MainWindow.ImageName) { FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
			p.Inlines.Add(new LineBreak());

			p.Inlines.Add(new Run(
				String.Format("Track {0:D2}.{1} {2} bytes has {3} sectors", track, side, dt.track.info.byteCount, dt.sectors.Count)
				) { FontWeight = FontWeights.Bold });
			p.Inlines.Add(new LineBreak());
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
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Time"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("CRC"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Fuzzy"))));
			currentRow.Cells.Add(new TableCell(new Paragraph(new Run("Bytes"))));

			int cri = 2;
			foreach (Sector sector in dt.sectors) {
				tab.RowGroups[0].Rows.Add(new TableRow());
				currentRow = tab.RowGroups[0].Rows[cri++];

				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", sector.id.number)))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", sector.id.info.startTime / 1000)))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0}", (sector.id.info.crc == 0) ? "OK" : "BAD")))));

				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", sector.data.info.startByte - (sector.id.info.startByte + sector.id.info.byteCount))))));

				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", sector.data.info.byteCount)))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", sector.data.info.startTime / 1000)))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0:D}", sector.data.info.timeLength / 1000)))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0}", (sector.data.info.crc == 0) ? "OK" : "BAD")))));
				currentRow.Cells.Add(new TableCell(new Paragraph(new Run(
					String.Format("{0}", (sector.data.fuzzy != null) ? "Yes" : "No")))));
			}
			doc.Blocks.Add(tab);
			fdViewer.Document = doc;
		}


		/// <summary>
		/// Draw the content of the buffer
		/// </summary>
		public void drawSectorBuffer(int track, int side, int rev) {
			// Force reread
			FDC.resetTrack(track, side);
			FDC.fdcRealAll(track, side, rev);

			List<Sector> sectors = Global.Disk[track, side].sectors;

			fdViewer.Document = null;
			FlowDocument doc = new FlowDocument();
			doc.FontFamily = new FontFamily("Consolas");
			doc.FontSize = 15;
			_view = DataView.Sector;

			Paragraph p = new Paragraph();
			p.Margin = new Thickness(0);
			p.Inlines.Add(new Run(Aufit.MainWindow.ImageName)
				{ FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Century Gothic, Courier New") });
			p.Inlines.Add(new LineBreak());

			p.Inlines.Add(new Run(
				String.Format("Track {0:D2}.{1} has {2} sectors", track, side, sectors.Count)
				) { FontWeight = FontWeights.Bold });
			p.Inlines.Add(new LineBreak());
			p.Inlines.Add(new LineBreak());

			foreach (Sector sector in sectors) {
				p.Inlines.Add(new Run(String.Format("Sector {0}", sector.id.number)) 
					{ Background = Brushes.LightGray, FontWeight = FontWeights.Bold });
				p.Inlines.Add(new LineBreak());
				p.Inlines.Add(String.Format("ID Field: IDAM={0:X2} - Track={1} Side={2} Sect={3} Size={4} CRC={5:X4} {6}", sector.id.iam,
					sector.id.track, sector.id.head, sector.id.number, sector.id.size, sector.id.crc, (sector.id.info.crc == 0) ? "Good" : "Bad"));
				p.Inlines.Add(new LineBreak());
				if (sector.data != null) {
					p.Inlines.Add("DATA Field: ");
					if (sector.data.info.byteCount != Global.sectSize(sector.id.size))
						p.Inlines.Add(" *** Incomplete *** ");
					p.Inlines.Add(String.Format("IAM={0:X2} {1} bytes start at {2}µs length {3} µs  CRC={4:X4}{5}{6}\n",
						sector.data.iam, sector.data.info.byteCount, sector.data.info.startTime / 1000, sector.data.info.timeLength / 1000,
						sector.data.crc, (sector.data.info.crc == 0) ? " Good" : " Bad", 
						(sector.data.fuzzy != null) ? " Fuzzy sector" : ""));
					drawBuffer(sector.data.buffer, p);

					if (sector.data.fuzzy != null) {
						p.Inlines.Add(String.Format("\nSector {0} Contains Fuzzy bytes\n", sector.id.number));
						for (int line = 0; line < sector.data.fuzzy.Count() / 16; line++) {
							p.Inlines.Add(String.Format("{0:D4} ", line * 16));
							for (int col = 0; col < 16; col++) {
								p.Inlines.Add(new Run(
									String.Format("{0:X2} ", sector.data.fuzzy[(line * 16) + col])) {
										FontWeight = (sector.data.fuzzy[(line * 16) + col] == 0) ? FontWeights.Normal : FontWeights.Bold
									});
							}
							p.Inlines.Add("\n");
						}
					}
				}
				else
					p.Inlines.Add(" *** Sector has no data ***\n");
				p.Inlines.Add("\n");
			}

			doc.Blocks.Add(p);
			fdViewer.Document = doc;
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
			FDC.fdcRealAll(track, side, rev);
			Track tr = Global.Disk[track, side].track;

			fdViewer.Document = null;
			FlowDocument doc = new FlowDocument();
			//d.FontFamily = new FontFamily("Lucida Console");			
			doc.FontFamily = new FontFamily("Consolas");
			doc.FontSize = 15;
			_view = DataView.Track;

			Paragraph p = new Paragraph(new Run(Aufit.MainWindow.ImageName));
			p.FontWeight = FontWeights.Bold;
			p.FontFamily = new FontFamily("Century Gothic, Courier New");
			p.Margin = new Thickness(0);
			doc.Blocks.Add(p);

			p = new Paragraph(new Run(String.Format("Track {0:D2}.{1} {2} bytes Length {3} µs",
				track, side, tr.info.byteCount, tr.info.timeLength / 1000)) { FontWeight = FontWeights.Bold });
			p.Margin = new Thickness(0);
			p.Inlines.Add(new LineBreak());
			doc.Blocks.Add(p);

			TrackBuffer buffer = tr.buffer;
			drawBuffer(buffer, p);
			fdViewer.Document = doc;
		}


		/// <summary>
		/// Display the content of TrackBuffer buffer in the tbBufferContent TextBox
		/// </summary>
		/// <param name="buffer">The buffer to display</param>
		/// <param name="message">an optional message to display before the buffer</param>
		public void drawBuffer(TrackBuffer buffer, Paragraph p) {

			for (int i = 0; i < buffer.Bytes.Count; i += 16) {
				// display position
				p.Inlines.Add(new Run(String.Format("{0:D5}  ", i)));

				// display bitWidth
				p.Inlines.Add(new Run(
						String.Format("{0:D4} ", buffer.Bytes[i].bitWidth)) {
							Foreground = (buffer.Bytes[i].bitWidth > 2050) ? Brushes.Blue :
							(buffer.Bytes[i].bitWidth < 1950) ? Brushes.Red : Brushes.Black,
							FontWeight = ((buffer.Bytes[i].bitWidth > 2050) || (buffer.Bytes[i].bitWidth < 1950)) ?
											FontWeights.Bold : FontWeights.Normal
						});

				// display start time
				p.Inlines.Add(String.Format("{0:D6}  ", buffer.Bytes[i].startTime / 1000));

				string ascii = "  ";
				for (int j = 0; j < 16; j++) {
					if ((i + j) < buffer.Bytes.Count) {
						if (_noColor)
							p.Inlines.Add(String.Format("{0:X2} ", buffer.Bytes[i + j].dataByte));
						else
							p.Inlines.Add(new Run(
								String.Format("{0:X2} ", buffer.Bytes[i + j].dataByte)) {
									Background =
										((buffer.Bytes[i + j].dataByte >= 0xF5) && (buffer.Bytes[i + j].dataByte <= 0xF7) &&
										 (buffer.Bytes[i + j].phase == Phases.p_gap)) ? Brushes.LightBlue :
										((buffer.Bytes[i + j].mfmWord == 0x0000) || (buffer.Bytes[i + j].mfmWord == 0xFFFF)) ? Brushes.Coral :
										((buffer.Bytes[i + j].mfmWord == 0x4489) || (buffer.Bytes[i + j].mfmWord == 0x5224)) ? Brushes.Yellow :
										(buffer.Bytes[i + j].phase == Phases.p_data) ? Brushes.LightGreen :
										(buffer.Bytes[i + j].phase == Phases.p_id) ? Brushes.GreenYellow :
										Brushes.White,
									Foreground =
										(buffer.Bytes[i + j].mfmWord == 0x4489) ? Brushes.Red :
										(buffer.Bytes[i + j].mfmWord == 0x5224) ? Brushes.Green :
										(buffer.Bytes[i + j].phase == Phases.p_id) ? Brushes.Blue :
										(buffer.Bytes[i + j].phase == Phases.p_data) ? Brushes.Black :
										(buffer.Bytes[i + j].phase == Phases.p_gap) ? Brushes.Black :
										Brushes.DarkBlue,
									FontWeight =
										(buffer.Bytes[i + j].mfmWord == 0x4489) || (buffer.Bytes[i + j].mfmWord == 0x5224) ||
										(buffer.Bytes[i + j].phase == Phases.p_id) ? FontWeights.Bold :
										FontWeights.Normal,
									//FontStyle =
									//(buffer.Bytes[i + j].startTime > 200000000) ? FontStyle = FontStyles.Italic :
									//FontStyles.Normal
								});
						char ch = Convert.ToChar(buffer.Bytes[i + j].dataByte);
						ascii += Char.IsControl(ch) ? "." : ch.ToString();
					}
					else
						p.Inlines.Add(String.Format("   "));
				}	// one more line
				p.Inlines.Add(ascii);
				p.Inlines.Add(new LineBreak());

				if (cbMFM.IsChecked == true) {
					p.Inlines.Add("       ");
					for (int j = 0; j < 16; j++) {
						if ((i + j) < buffer.Bytes.Count)
							p.Inlines.Add(new Run(
								String.Format("{0:X4} ", buffer.Bytes[i + j].mfmWord)) { 
							Background = Brushes.LightGray});
					}
					p.Inlines.Add(new LineBreak());
				}
			}	// all lines
		}


		private void bufferSaveClick(object sender, RoutedEventArgs e) {
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.Filter = "Rich Text Format|*.rtf|All files (*.*)|*.*";
			//dlg.Filter = "Text Format|*.txt|All files (*.*)|*.*";
			if (dlg.ShowDialog() == true) {
				//TextRange range = new TextRange(_flowDoc.ContentStart, _flowDoc.ContentEnd);
				TextRange range = new TextRange(fdViewer.Document.ContentStart, fdViewer.Document.ContentEnd);
				using (FileStream file = new FileStream(dlg.FileName, FileMode.Create)) {
					range.Save(file, System.Windows.DataFormats.Rtf);
					//range.Save(file, System.Windows.DataFormats.Text);
					file.Close();
				}
			}

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

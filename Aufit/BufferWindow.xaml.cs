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

using WD1772Package;
using GlobalPackage;

namespace Aufit {
	/// <summary>
	/// Interaction logic for TrackBuffer.xaml
	/// </summary>
	public partial class BufferWindow : Window {
		bool _noColor = false;
		/// <summary>
		/// Create a new BufferWindow object.
		/// </summary>
		public BufferWindow() {
			InitializeComponent();
		}

		/// <summary>
		/// Draw the content of the buffer
		/// </summary>
		public void drawSectorBuffer(int track, int side, int rev) {
			// Force reread
			Global.Disk[track, side].sectors = null;
			Global.Disk[track, side].track = null;
			FDC.fdcRealAll(track, side, rev);

			List<Sector> sectors = Global.Disk[track, side].sectors;

			displayBuffer.FontFamily = new FontFamily("Consolas");
			displayBuffer.FontSize = 16;
			displayBuffer.FontStyle = FontStyles.Normal;
			displayBuffer.FontWeight = FontWeights.Normal;

			displayBuffer.Inlines.Add(new Run(
				String.Format("Track {0:D2}.{1} has {2} sectors\n", track, side, sectors.Count)
				) { FontWeight = FontWeights.Bold });

			displayBuffer.Inlines.Add(String.Format("-------------------+---------------------------\n"));
			displayBuffer.Inlines.Add(String.Format("ID                 | DATA                      \n"));
			displayBuffer.Inlines.Add(String.Format("SCT    POS LEN CRC | BYTE    POS  TIME CRC FUZ \n"));
			displayBuffer.Inlines.Add(String.Format("-------------------+---------------------------\n"));

			foreach (Sector sector in sectors) {
				// ID
				displayBuffer.Inlines.Add(String.Format("{0,3:D} {1,6:D} {2,3:D} {3} |", sector.id.number, sector.id.info.startTime / 1000,
						sector.id.info.timeLength / 1000, (sector.id.info.crc == 0) ? " OK" : "Bad"));
				// DATA
				if (sector.data != null)
					displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,6:D} {2,5:D} {3} {4}",
						sector.data.info.byteCount, sector.data.info.startTime / 1000, sector.data.info.timeLength / 1000, 
						(sector.data.info.crc == 0) ? " OK" : "Bad", (sector.data.fuzzy != null) ? "Yes" : " No"));
				displayBuffer.Inlines.Add("\n");
			}			
			displayBuffer.Inlines.Add(String.Format("-------------------+---------------------------\n\n"));

			foreach (Sector sector in sectors) {
				displayBuffer.Inlines.Add(String.Format("Sector {0}\n", sector.id.number));
				displayBuffer.Inlines.Add(String.Format("ID Field: IDAM={0:X2} - Track={1} Side={2} Sect={3} Size={4} CRC={5:X4} {6}\n", sector.id.iam,
					sector.id.track, sector.id.head, sector.id.number, sector.id.size, sector.id.crc, (sector.id.info.crc == 0) ? " Good CRC" : "Bad CRC"));
				if (sector.data != null) {
					displayBuffer.Inlines.Add("DATA Field: ");
					if (sector.data.info.byteCount != Global.sectSize(sector.id.size))
						displayBuffer.Inlines.Add(" *** Imcomplete *** ");
					displayBuffer.Inlines.Add(String.Format("{0} bytes start at {1}µs length {2} µs IAM={3:X2} {4}={5:X4} {6}\n",
						sector.data.info.byteCount, sector.data.info.startTime / 1000, sector.data.info.timeLength / 1000,
						sector.data.iam, (sector.data.info.crc == 0) ? "Good CRC" : "Bad CRC", sector.data.crc,
						(sector.data.fuzzy != null) ? "Fuzzy sector" : "" ));
					drawBuffer(sector.data.buffer);

					if (sector.data.fuzzy != null) {
						displayBuffer.Inlines.Add(String.Format("\nSector {0} Contains Fuzzy bytes\n", sector.id.number));
						for (int line = 0; line < sector.data.fuzzy.Count() / 16; line++) {
							displayBuffer.Inlines.Add(String.Format("{0:D4} ", line * 16));
							for (int col = 0; col < 16; col++) {
								displayBuffer.Inlines.Add(new Run(
									String.Format("{0:X2} ", sector.data.fuzzy[(line *16) + col])) {
									FontWeight = (sector.data.fuzzy[(line * 16) + col] == 0) ? FontWeights.Normal : FontWeights.Bold
								});
							}
							displayBuffer.Inlines.Add("\n");
						}
					}
				}
				else
					displayBuffer.Inlines.Add(" *** Sector has no data ***\n");
				displayBuffer.Inlines.Add("\n");
			}
		}

		/// <summary>
		/// Display the content of the Track Information in a TextBlock
		/// </summary>
		/// <param name="track">Specifies the track from the disk</param>
		/// <param name="side">Specifies the side from the disk</param>
		/// <param name="rev">Revolution number</param>
		public void drawTrackBuffer(int track, int side, int rev) {
			// Force reread
			Global.Disk[track, side].sectors = null;
			Global.Disk[track, side].track = null;
			FDC.fdcRealAll(track, side, rev);

			Track tr = Global.Disk[track, side].track;

			//displayBuffer.FontFamily = new FontFamily("Lucida Console");
			displayBuffer.FontFamily = new FontFamily("Consolas");
			displayBuffer.FontSize = 16;
			displayBuffer.FontStyle = FontStyles.Normal;
			displayBuffer.FontWeight = FontWeights.Normal;

			displayBuffer.Inlines.Add(new Run(
				String.Format("Track {0:D2}.{1} {2} bytes Length {3} µs\n\n",
					track, side, tr.info.byteCount, tr.info.timeLength / 1000)
				) { FontWeight = FontWeights.Bold });

			TrackBuffer buffer = tr.buffer;
			drawBuffer(buffer);
		}

		/// <summary>
		/// Display the content of TrackBuffer buffer in the displayBuffer TextBox
		/// </summary>
		/// <param name="buffer">The buffer to display</param>
		/// <param name="message">an optional message to display before the buffer</param>
		public void drawBuffer(TrackBuffer buffer, string message = null) {
			// get ready to display MFM
			bool displayMFM = false;

			displayBuffer.FontFamily = new FontFamily("Consolas");
			displayBuffer.FontSize = 16;
			displayBuffer.FontStyle = FontStyles.Normal;
			displayBuffer.FontWeight = FontWeights.Normal;
			if (message != null)
				displayBuffer.Inlines.Add(message);

			for (int i = 0; i < buffer.Bytes.Count; i += 16) {
				// display position
				displayBuffer.Inlines.Add(String.Format("{0:D5}  ", i));
				// display bitWidth
				displayBuffer.Inlines.Add(new Run(
					String.Format("{0:D4} ", buffer.Bytes[i].density)) {
						Foreground = (buffer.Bytes[i].density > 2050) ? Brushes.Blue :
						(buffer.Bytes[i].density < 1950) ? Brushes.Red : Brushes.Black,
						FontWeight = ((buffer.Bytes[i].density > 2050) || (buffer.Bytes[i].density < 1950)) ?
										FontWeights.Bold : FontWeights.Normal
					});
				// display start time
				displayBuffer.Inlines.Add(String.Format("{0:D6}  ", buffer.Bytes[i].startTime / 1000));

				string ascii = "  ";
				for (int j = 0; j < 16; j++) {
					if ((i + j) < buffer.Bytes.Count) {
						if (_noColor)
							displayBuffer.Inlines.Add(String.Format("{0:X2} ", buffer.Bytes[i + j].dataByte));
						else
							displayBuffer.Inlines.Add(new Run(
								String.Format("{0:X2} ", buffer.Bytes[i + j].dataByte)) {
									Background =
										((buffer.Bytes[i + j].dataByte >= 0xF5) && (buffer.Bytes[i + j].dataByte <= 0xF7) &&
										 (buffer.Bytes[i + j].phase == Phases.p_gap)) ? Brushes.LightBlue :
										((buffer.Bytes[i + j].mfmWord == 0x0000) || (buffer.Bytes[i + j].mfmWord == 0xFFFF)) ? Brushes.Coral :
										(((buffer.Bytes[i + j].dataByte == 0xA1) && (buffer.Bytes[i + j].mfmWord == 0x4489)) ||
										 ((buffer.Bytes[i + j].dataByte == 0xC2) && (buffer.Bytes[i + j].mfmWord == 0x5224))) ? Brushes.Yellow :
										(buffer.Bytes[i + j].phase == Phases.p_data) ? Brushes.LightGreen :
										(buffer.Bytes[i + j].phase == Phases.p_id) ? Brushes.GreenYellow :
										Brushes.White,
									Foreground =
										((buffer.Bytes[i + j].dataByte == 0xA1) && (buffer.Bytes[i + j].mfmWord == 0x4489)) ? Brushes.Red :
										(buffer.Bytes[i + j].dataByte == 0xC2) && (buffer.Bytes[i + j].mfmWord == 0x5224) ? Brushes.Green :
										(buffer.Bytes[i + j].phase == Phases.p_id) ? Brushes.Blue :
										(buffer.Bytes[i + j].phase == Phases.p_data) ? Brushes.Black :
										(buffer.Bytes[i + j].phase == Phases.p_gap) ? Brushes.Black :
										Brushes.DarkBlue,
									FontWeight =
										((buffer.Bytes[i + j].dataByte == 0xA1) && (buffer.Bytes[i + j].mfmWord == 0x4489)) ||
										((buffer.Bytes[i + j].dataByte == 0xC2) && (buffer.Bytes[i + j].mfmWord == 0x5224)) ||
										(buffer.Bytes[i + j].phase == Phases.p_id) ? FontWeights.Bold :
										FontWeights.Normal,
									FontStyle =
									(buffer.Bytes[i + j].startTime > 200000000) ? FontStyle = FontStyles.Italic :
									FontStyles.Normal
								});
						char ch = Convert.ToChar(buffer.Bytes[i + j].dataByte);
						ascii += Char.IsControl(ch) ? "." : ch.ToString();
					}
					else
						displayBuffer.Inlines.Add(String.Format("   "));
				}	// one more line
				displayBuffer.Inlines.Add(ascii);
				displayBuffer.Inlines.Add(String.Format("\n"));

				if (displayMFM) {
					displayBuffer.Inlines.Add("       ");
					for (int j = 0; j < 16; j++) {
						if ((i + j) < buffer.Bytes.Count)
							displayBuffer.Inlines.Add(String.Format("{0:X4} ", buffer.Bytes[i + j].mfmWord));
					}
					displayBuffer.Inlines.Add("\n");
				}

			}	// all lines

			// EXAMPLE FOR REFERENCE
			//displayBuffer.Inlines.Add(String.Format("first clock {0}", _buffer.Bytes[0].density));
			//displayBuffer.Inlines.Add(new Run("the TextBlock control ") { FontWeight = FontWeights.Bold });
			//displayBuffer.Inlines.Add("using ");
			//displayBuffer.Inlines.Add(new Run("inline ") { FontStyle = FontStyles.Italic });
			//displayBuffer.Inlines.Add(new Run("text formatting ") { Foreground = Brushes.Blue });
			//displayBuffer.Inlines.Add("from ");
			//displayBuffer.Inlines.Add(new Run("Code-Behind") { TextDecorations = TextDecorations.Underline, Foreground = Brushes.Blue });

		}

	}
}

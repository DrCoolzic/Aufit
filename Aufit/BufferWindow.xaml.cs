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
			// force re read
			Global.Disk[track, side].addrContent = null;
			Global.Disk[track, side].sectorsContent = null;
			Global.Disk[track, side].trackContent = null;

			FDC.fdcAllRead(track, side, rev);
			AddrContent ac = Global.Disk[track, side].addrContent;

			displayBuffer.FontFamily = new FontFamily("Consolas");
			displayBuffer.FontSize = 16;
			displayBuffer.FontStyle = FontStyles.Normal;
			displayBuffer.FontWeight = FontWeights.Normal;

			displayBuffer.Inlines.Add(new Run(
				String.Format("Read Sectors for Track {0:D2}.{1} has {2} sectors\n", track, side, ac.ids.Count)
				) { FontWeight = FontWeights.Bold });

			displayBuffer.Inlines.Add(String.Format("-------------------+---------------------------\n"));
			displayBuffer.Inlines.Add(String.Format("ID                 | DATA                      \n"));
			displayBuffer.Inlines.Add(String.Format("SCT    POS LEN CRC | BYTE    POS   LEN CRC FUZ \n"));
			displayBuffer.Inlines.Add(String.Format("-------------------+---------------------------\n"));

			for (int i = 0; i < ac.ids.Count; i++) {
				// ID
				if (ac.blocs[i].byteCount > 0) {
					displayBuffer.Inlines.Add(String.Format("{0,3:D} {1,6:D} {2,3:D} {3} |", ac.ids[i].number, ac.blocs[i].startTime / 1000,
						ac.blocs[i].timeLength / 1000, (ac.blocs[i].crc == 0) ? " OK" : "Bad"));
				}
				
				// DATA
				SectorContent sc = FDC.findSectorContent(track, side, ac.ids[i].number, ac.blocs[i].startTime);
				if ((sc != null) && (sc.dataBlock.byteCount > 0))
					displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,6:D} {2,5:D} {3} {4}",
						sc.dataBlock.byteCount, sc.dataBlock.startTime / 1000, sc.dataBlock.timeLength / 1000, (sc.dataBlock.crc == 0) ? " OK" : "Bad", (sc.fuzzyMask != null) ? "Yes" : " No"));
				displayBuffer.Inlines.Add("\n");
			}			
			displayBuffer.Inlines.Add(String.Format("-------------------+---------------------------\n\n"));

			for (int i = 0; i < ac.ids.Count; i++) {
				displayBuffer.Inlines.Add(String.Format("Sector {0}", ac.ids[i].number));
				SectorContent sc = FDC.findSectorContent(track, side, ac.ids[i].number, ac.blocs[i].startTime);
				if ((sc != null) && (sc.data.Bytes.Count > 0)) {
					if (sc.data.Bytes.Count != Global.sectSize(ac.ids[i].size) + 3)
						displayBuffer.Inlines.Add(" *** Imcomplete ***");
					displayBuffer.Inlines.Add(String.Format(" {0} bytes start at {1} length {2} {3} {4}\n",
						sc.data.Bytes.Count, sc.dataBlock.startTime / 1000, sc.dataBlock.timeLength / 1000,
						(sc.dataBlock.crc == 0) ? "Good CRC" : "Bad CRC", (sc.fuzzyMask != null) ? "Has Fuzzy bytes" : "" ));
					drawBuffer(sc.data);
					if (sc.fuzzyMask != null) {
						displayBuffer.Inlines.Add(String.Format("\nFuzzy bytes for sector {0}\n", ac.ids[i].number));
						_noColor = true;
						drawBuffer(sc.fuzzyMask);
						_noColor = false;
					}
				}
				else
					displayBuffer.Inlines.Add(" *** Sector has no data ***\n");
				displayBuffer.Inlines.Add("\n");
			}
		}

		/// <summary>
		/// Display the content of the Track Information in a textbox
		/// </summary>
		/// <param name="track">Specifies the track from the disk</param>
		/// <param name="side">Specifies the side from the disk</param>
		/// <param name="rev">Revolution number</param>
		public void drawTrackBuffer(int track, int side, int rev) {
			// force re read
			Global.Disk[track, side].addrContent = null;
			Global.Disk[track, side].sectorsContent = null;
			Global.Disk[track, side].trackContent = null;

			FDC.fdcAllRead(track, side, rev);
			TrackContent tc = Global.Disk[track, side].trackContent;
			AddrContent ac = Global.Disk[track, side].addrContent;

			//displayBuffer.FontFamily = new FontFamily("Lucida Console");
			displayBuffer.FontFamily = new FontFamily("Consolas");
			displayBuffer.FontSize = 16;
			displayBuffer.FontStyle = FontStyles.Normal;
			displayBuffer.FontWeight = FontWeights.Normal;

			displayBuffer.Inlines.Add(new Run(
				String.Format("Read Track for track {0:D2}.{1} {2} bytes Length {3} µs with {4} sectors\n",
					track, side, tc.byteCount, tc.timeLength / 1000, tc.sectors.Count - 1)
				) { FontWeight = FontWeights.Bold });

			displayBuffer.Inlines.Add(String.Format("-------------------+-----------+---------------------------+-----------\n"));
			displayBuffer.Inlines.Add(String.Format("ID                 | INTER-GAP | DATA                      | INTRA-GAP\n"));
			displayBuffer.Inlines.Add(String.Format("SCT    POS LEN CRC | BYTE  LEN | BYTE    POS   LEN CRC FUZ | BYTE  LEN\n"));
			displayBuffer.Inlines.Add(String.Format("-------------------+-----------+---------------------------+-----------\n"));

			for (int i = 0; i < tc.sectors.Count; i++) {
				if (i == 0) {
					displayBuffer.Inlines.Add(String.Format("                   |"));
					displayBuffer.Inlines.Add(String.Format("           |"));
					// DATA
					if (tc.sectors[i].phases[(byte)Phases.p_data].byteCount > 0) {
						// this should be the data from last id ?
						SectorContent sc = FDC.findSectorContent(track, side, ac.ids[tc.sectors.Count - 2].number, ac.blocs[tc.sectors.Count - 2].startTime);
						if (sc != null)
							displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,6:D} {2,5:D} {3} {4} |",
								//sc.dataBlock.byteCount, sc.dataBlock.startTime / 1000, sc.dataBlock.timeLength / 1000, (sc.dataBlock.crc == 0) ? " OK" : "Bad", (sc.fuzzyMask != null) ? "Yes" : " No"));
								tc.sectors[i].phases[(byte)Phases.p_data].byteCount, tc.sectors[i].phases[(byte)Phases.p_data].startTime / 1000,
								tc.sectors[i].phases[(byte)Phases.p_data].timeLength / 1000,
								(sc.dataBlock.crc == 0) ? " OK" : "Bad", (sc.fuzzyMask != null) ? "Yes" : " No"));
						else
							displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,6:D} {2,5:D} {3} {4} |",
								tc.sectors[i].phases[(byte)Phases.p_data].byteCount, tc.sectors[i].phases[(byte)Phases.p_data].startTime / 1000,
								tc.sectors[i].phases[(byte)Phases.p_data].timeLength / 1000,
								(tc.sectors[i].phases[(byte)Phases.p_data].crc == 0) ? " OK" : "Bad", "???"));
					}
					else
						displayBuffer.Inlines.Add(String.Format("                           |"));
					// INTER
					if (tc.sectors[i].phases[(byte)Phases.p_outer].byteCount > 0)
						displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,5:D}\n", tc.sectors[i].phases[(byte)Phases.p_outer].byteCount,
							tc.sectors[i].phases[(byte)Phases.p_outer].timeLength / 1000));
					else
						displayBuffer.Inlines.Add(String.Format("\n"));

				}	// pseudo sector
				else {
					// ID
					if (tc.sectors[i].phases[(byte)Phases.p_id].byteCount > 0) {
						displayBuffer.Inlines.Add(String.Format("{0,3:D} {1,6:D} {2,3:D} {3} |", ac.ids[i - 1].number, ac.blocs[i - 1].startTime / 1000,
							ac.blocs[i - 1].timeLength / 1000, (ac.blocs[i - 1].crc == 0) ? " OK" : "Bad"));
					}
					else
						displayBuffer.Inlines.Add(String.Format("                   |"));
					// INTRA
					if (tc.sectors[i].phases[(byte)Phases.p_inner].byteCount > 0)
						displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,4:D} |", tc.sectors[i].phases[(byte)Phases.p_inner].byteCount,
							tc.sectors[i].phases[(byte)Phases.p_inner].timeLength / 1000));
					else
						displayBuffer.Inlines.Add(String.Format("           |"));
					// DATA
					SectorContent sc = FDC.findSectorContent(track, side, ac.ids[i - 1].number, ac.blocs[i - 1].startTime);
					if ((sc != null) && (sc.dataBlock.byteCount > 0))
						displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,6:D} {2,5:D} {3} {4} |",
							sc.dataBlock.byteCount, sc.dataBlock.startTime / 1000, sc.dataBlock.timeLength / 1000, (sc.dataBlock.crc == 0) ? " OK" : "Bad", (sc.fuzzyMask != null) ? "Yes" : " No"));
					else
						displayBuffer.Inlines.Add(String.Format("                           |"));
					// INTER
					if (tc.sectors[i].phases[(byte)Phases.p_outer].byteCount > 0)
						displayBuffer.Inlines.Add(String.Format(" {0,4:D} {1,5:D}\n", tc.sectors[i].phases[(byte)Phases.p_outer].byteCount,
							tc.sectors[i].phases[(byte)Phases.p_inner].timeLength / 1000));
					else
						displayBuffer.Inlines.Add(String.Format("\n"));
				}	// real sector
			}
			displayBuffer.Inlines.Add(String.Format("-------------------+-----------+---------------------------+-----------\n"));

			TrackBuffer buffer = tc.buffer;
			drawBuffer(buffer);

		}

		/// <summary>
		/// Display the content of	 TrackBuffer buffer in the displayBuffer TextBox
		/// </summary>
		/// <param name="buffer">The buffer to display</param>
		/// <param name="message">an optional message to display before the buffer</param>
		public void drawBuffer(TrackBuffer buffer, string message = null) {
			displayBuffer.FontFamily = new FontFamily("Consolas");
			displayBuffer.FontSize = 16;
			displayBuffer.FontStyle = FontStyles.Normal;
			displayBuffer.FontWeight = FontWeights.Normal;
			if (message != null)
				displayBuffer.Inlines.Add(message);

			for (int i = 0; i < buffer.Bytes.Count; i += 16) {
				displayBuffer.Inlines.Add(String.Format("{0:D5}  ", i));
				displayBuffer.Inlines.Add(new Run(
					String.Format("{0:D4} ", buffer.Bytes[i].density)) {
						Foreground = (buffer.Bytes[i].density > 2050) ? Brushes.Blue :
						(buffer.Bytes[i].density < 1950) ? Brushes.Red : Brushes.Black,
						FontWeight = ((buffer.Bytes[i].density > 2050) || (buffer.Bytes[i].density < 1950)) ?
										FontWeights.Bold : FontWeights.Normal
					});
				displayBuffer.Inlines.Add(String.Format("{0:D6}  ", buffer.Bytes[i].startTime / 1000));

				// TODO if phase==p_inner or p_outer and data in range 245-247 display red/yellow

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
										 ((buffer.Bytes[i + j].phase == Phases.p_inner) || (buffer.Bytes[i + j].phase == Phases.p_outer))) ? Brushes.Red :
										(buffer.Bytes[i + j].mfmWord == 0x00) ? Brushes.Coral :
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
										(buffer.Bytes[i + j].phase == Phases.p_inner) ? Brushes.Black :
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
			}	// all lines

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

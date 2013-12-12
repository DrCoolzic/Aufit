/*!
@file BufferWindow.xaml.cs
 * 
<summary>Open a Window and displays buffer information</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tools project.\n\n
The Atari Universal FD Image Tools project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tools project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.\n\n
The Atari Universal FD Image Tools project is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n
See the GNU General Public License for more details.\n\n
You should have received a copy of the GNU General Public License
along with the Atari Universal FD Image Tools project; if not, write to the Free Software
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

using WD1772NS;

namespace Aufit {
	/// <summary>
	/// Interaction logic for TrackBuffer.xaml
	/// </summary>
	public partial class BufferWindow : Window {

		private TrackBuffer _buffer;

		/// <summary>
		/// Create a new BufferWindow object.
		/// </summary>
		/// <param name="buffer">The buffer to display</param>
		public BufferWindow(TrackBuffer buffer) {
			InitializeComponent();
			_buffer = buffer;
			drawBuffer();
		}

		/// <summary>
		/// Draw the content of the buffer
		/// </summary>
		public void drawBuffer() {
			displayBuffer.FontFamily = new FontFamily("Courier New");
			displayBuffer.FontSize = 15;
			displayBuffer.FontStyle = FontStyles.Normal;
			displayBuffer.FontWeight = FontWeights.Normal;
			displayBuffer.Inlines.Add(String.Format("Number of Bytes {0:D6}\n", _buffer.Bytes.Count));
			for (int i = 0; i < _buffer.Bytes.Count; i += 16) {				
				displayBuffer.Inlines.Add(String.Format("{0:D5}  ", i));
				displayBuffer.Inlines.Add(new Run(
					String.Format("{0:D4} ", _buffer.Bytes[i].density)) {
 					Foreground = (_buffer.Bytes[i].density > 2050) ? Brushes.Blue : 
					(_buffer.Bytes[i].density < 1950) ? Brushes.Red : Brushes.Black,
					FontWeight = ((_buffer.Bytes[i].density > 2050) || (_buffer.Bytes[i].density < 1950)) ?
									FontWeights.Bold : FontWeights.Normal
				});
				displayBuffer.Inlines.Add(String.Format("{0:D6}  ", _buffer.Bytes[i].startTime/1000));
					
				string ascii = "  ";
				for (int j = 0; j < 16; j++) {
					if ((i + j) < _buffer.Bytes.Count) {
						displayBuffer.Inlines.Add(new Run(
							String.Format("{0:X2} ", _buffer.Bytes[i + j].dataByte))   {
								Background =
									(_buffer.Bytes[i + j].mfmWord == 0x00) ? Brushes.Coral :
									((_buffer.Bytes[i + j].dataByte == 0xA1) || (_buffer.Bytes[i + j].dataByte == 0xC2)) && 
									((_buffer.Bytes[i + j].syncInfo & 0x80) == 0x80 ) ? Brushes.Yellow :
									((_buffer.Bytes[i + j].phase == Phases.p_data) ||
									(_buffer.Bytes[i + j].phase == Phases.p_id))  ? Brushes.LemonChiffon :
									Brushes.White,
								Foreground = (_buffer.Bytes[i + j].dataByte == 0xA1) && ((_buffer.Bytes[i + j].syncInfo & 0x80) == 0x80) ? Brushes.Red :
									(_buffer.Bytes[i + j].dataByte == 0xC2) && ((_buffer.Bytes[i + j].syncInfo & 0x80) == 0x80) ? Brushes.Green :	
									(_buffer.Bytes[i+j].phase == Phases.p_id) ? Brushes.Blue :						
									(_buffer.Bytes[i + j].phase == Phases.p_data) ? Brushes.Black : 
									Brushes.Green,
								FontWeight = (((_buffer.Bytes[i + j].dataByte == 0xA1) || (_buffer.Bytes[i + j].dataByte == 0xC2)) && 
									((_buffer.Bytes[i + j].syncInfo & 0x80) == 0x80)) || (_buffer.Bytes[i + j].phase == Phases.p_id) ?
									FontWeights.Bold : FontWeights.Normal,
								FontStyle = (((_buffer.Bytes[i + j].syncInfo & 0x80) == 0x80) && ((_buffer.Bytes[i + j].syncInfo & 0x7F) != 0x00)) ?
								FontStyle = FontStyles.Italic : FontStyles.Normal
							});
						char ch = Convert.ToChar(_buffer.Bytes[i + j].dataByte);
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
			//displayBuffer.Inlines.Add(".");
			////this.Content = tb;

							//			String.Format("{0:X2}{1}", _buffer.Bytes[i + j].dataByte, (((_buffer.Bytes[i + j].syncInf & 0x80) == 0x80) && ((_buffer.Bytes[i + j].syncInf & 0x7F) != 0x00)) ? 
							///* (_buffer.Bytes[i + j].syncInf & 0x7F).ToString("X") */ "+" : " "  ))   {
							//	Background =
							//		(_buffer.Bytes[i + j].mfmWord == 0x00) ? Brushes.Coral :
							//		((_buffer.Bytes[i + j].dataByte == 0xA1) || (_buffer.Bytes[i + j].dataByte == 0xC2)) && 
							//		((_buffer.Bytes[i + j].syncInf & 0x80) == 0x80 ) ? Brushes.Yellow :
							//		((_buffer.Bytes[i + j].phase == WD1772NS.FDC.Phases.p_data) ||
							//		(_buffer.Bytes[i + j].phase == WD1772NS.FDC.Phases.p_id))  ? Brushes.LemonChiffon :
							//		Brushes.White,
							//	Foreground = (_buffer.Bytes[i + j].dataByte == 0xA1) && ((_buffer.Bytes[i + j].syncInf & 0x80) == 0x80) ? Brushes.Red :
							//		(_buffer.Bytes[i + j].dataByte == 0xC2) && ((_buffer.Bytes[i + j].syncInf & 0x80) == 0x80) ? Brushes.Green :	
							//		(_buffer.Bytes[i+j].phase == WD1772NS.FDC.Phases.p_id) ? Brushes.Blue :						
							//		(_buffer.Bytes[i + j].phase == WD1772NS.FDC.Phases.p_data) ? Brushes.Black : 
							//		Brushes.Green,
							//	FontWeight = (((_buffer.Bytes[i + j].dataByte == 0xA1) || (_buffer.Bytes[i + j].dataByte == 0xC2)) && 
							//		((_buffer.Bytes[i + j].syncInf & 0x80) == 0x80)) || (_buffer.Bytes[i + j].phase == WD1772NS.FDC.Phases.p_id) ?
							//		FontWeights.Bold : FontWeights.Normal
							//});


		}



	}
}

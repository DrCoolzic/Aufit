/*!
@file ButtonMatrix.cs
 * 
<summary>This class is used to handle functions related to the button matrix</summary>

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
using System.Windows.Media;

using WD1772Package;
using KFReaderPackage;
using GlobalPackage;

namespace Aufit {
	/// <summary>
	/// The static ButtonMatrix class takes care of the matrices of buttons for the track/side
	/// </summary>
	static class ButtonMatrix {

		private readonly static Thickness thinButtonBorder = new Thickness(1);
		private readonly static Thickness thickButtonBorder = new Thickness(2.4);

		private static List<Tuple<Button, Int32, Int32, Int32>> _buttonList = new List<Tuple<Button, int, int, int>>();
		private static Button[, ,] _buttons = new Button[9, 10, 2];
		private static Button _selectedButton = null;

		/// <summary>
		/// Draw a matrix of 84 buttons + text box on the specified Canvas 
		/// </summary>
		/// <param name="cv">Canvas where to put the buttons</param>
		/// <param name="side">This value is used to distinguish between matrix for side 1 and side 2</param>
		/// /// <param name="btMatrixClicked">Button clicked event handler</param>
		public static void drawButtons(Canvas cv, int side, RoutedEventHandler btMatrixClicked) {
			int left = 0;
			int top = 0;
			int ymax = 11;
			for (int x = 0; x < 10; x++) {
				if (x == 9) ymax = 5;
				for (int y = 0; y < ymax; y++) {
					if ((x == 0) && (y != 0)) {
						TextBox tb = new TextBox();
						tb.Margin = new Thickness(left, top, 0, 0);
						tb.Height = 13;
						tb.Width = 13;
						tb.FontFamily = new FontFamily("Segoe UI");
						tb.BorderThickness = new Thickness(0);
						tb.FontSize = 10;
						tb.Text = (y - 1).ToString();
						cv.Children.Add(tb);
					}
					else {
						if (y == 0) {
							if (x != 0) {
								TextBox tb = new TextBox();
								tb.Margin = new Thickness(left, top, 0, 0);
								tb.Height = 13;
								tb.Width = 13;
								tb.FontFamily = new FontFamily("Segoe UI");
								tb.BorderThickness = new Thickness(0);
								tb.FontSize = 10;
								tb.Text = (x - 1).ToString();
								cv.Children.Add(tb);
							}
						}
						else {
							_buttons[x - 1, y - 1, side - 1] = new Button();
							_buttons[x - 1, y - 1, side - 1].Margin = new Thickness(left, top, 0, 0);
							_buttons[x - 1, y - 1, side - 1].Height = 13;
							_buttons[x - 1, y - 1, side - 1].Width = 13;
							//_buttons[x - 1, y - 1, side - 1].Click += new RoutedEventHandler(btMatrixClicked);
							_buttons[x - 1, y - 1, side - 1].Click += btMatrixClicked;
							_buttons[x - 1, y - 1, side - 1].BorderThickness = thinButtonBorder;
							_buttons[x - 1, y - 1, side - 1].BorderBrush = Brushes.Black;
							_buttonList.Add(new Tuple<Button, int, int, int>(_buttons[x - 1, y - 1, side - 1], x, y, side));

							//ToolTip tt = new ToolTip();
							//tt.Content = String.Format("Track {0}{1}.{2}", x - 1, y - 1, side - 1);
							//_buttons[x-1, y-1, side-1].ToolTip = tt;
							ToolTipService.SetToolTip(_buttons[x - 1, y - 1, side - 1], String.Format("Track {0}{1}.{2}", x - 1, y - 1, side - 1));
							ToolTipService.SetInitialShowDelay(_buttons[x - 1, y - 1, side - 1], 1000);

							cv.Children.Add(_buttons[x - 1, y - 1, side - 1]);
						}
					}
					left += 14;
				}
				left = 0;
				top += 14;
			}
		}


		/// <summary>
		/// Set the background for all buttons to White and clear _selectedButton
		/// </summary>
		public static void clearButtons() {
			deselectButton();
			for (int x = 0; x < 9; x++)
				for (int y = 0; y < 10; y++)
					for (int z = 0; z < 2; z++)
						if (_buttons[x, y, z] != null) {
							_buttons[x, y, z].Background = Brushes.White;
							_buttons[x, y, z].BorderThickness = thinButtonBorder;
						}
		}


		/// <summary>
		/// The selected button grows from 13 to 15, the border is set to thick, move one pixel to compensate
		/// </summary>
		/// <param name="track">Track number used to select x,y</param>
		/// <param name="side">Side used to select matrix</param>
		public static void selectButton(int track, int side) {
			int x = track / 10;
			int y = track % 10;
			int z = side;
			_buttons[x, y, z].BorderThickness = thickButtonBorder;
			_buttons[x, y, z].BorderBrush = Brushes.Black;
			_buttons[x, y, z].Width = 15;
			_buttons[x, y, z].Height = 15;
			_buttons[x, y, z].Margin = new Thickness(_buttons[x, y, z].Margin.Left - 1, _buttons[x, y, z].Margin.Top - 1, 0, 0);
			_selectedButton = _buttons[x, y, z];
		}

		/// <summary>
		/// Reset button size from 15 to 13, set border to thin, move to original, and reset _selected
		/// </summary>
		public static void deselectButton() {
			if (_selectedButton != null) {
				_selectedButton.BorderThickness = thinButtonBorder;
				_selectedButton.BorderBrush = Brushes.Black;
				_selectedButton.Width = 13;
				_selectedButton.Height = 13;
				_selectedButton.Margin = new Thickness(_selectedButton.Margin.Left + 1, _selectedButton.Margin.Top + 1, 0, 0);
				_selectedButton = null;
			}
		}

		/// <summary>
		/// Set background color of button based on track / side
		/// </summary>
		/// <param name="track">Track number</param>
		/// <param name="side">Side Number </param>
		public static void setButtonBG(int track, int side) {
			Brush color = Brushes.LimeGreen;
			Global.DiskTrack td = Global.Disk[track, side];
			TrackContent tc = td.trackContent;
			// track not processed
			if (tc != null) {
				if (tc.sectors.Count == 1) color = Brushes.SlateGray;
				else if (tc.hasProtection) color = Brushes.Orange;
			}
			else if (Global.Disk[track, side].fluxData != null) {
				double entropy = td.histoData.entropy;
				color = (entropy < 2000) ? (entropy < 0) ? Brushes.LightGray : Brushes.Yellow : Brushes.Lime;
			}
			else
				color = Brushes.White;
			_buttons[track / 10, track % 10, side].Background = color;		
		}


		/// <summary>
		/// Called by button click handler. !set the current Track/Side based 
		/// on button clicked and select/unselect buttons accordingly
		/// </summary>
		/// <param name="button"></param>
		/// <returns></returns>
		public static bool buttonClicked(Button button) {
			var tuple = _buttonList.Where(t => t.Item1 == button).FirstOrDefault();

			if (tuple != null) {
				Int32 x = tuple.Item2 - 1;
				Int32 y = tuple.Item3 - 1;
				Int32 z = tuple.Item4 - 1;
				int track = 10 * x + y;
				if (Global.Disk[track, z].fluxData == null) return false;
				deselectButton();
				MainWindow.Track = track;
				MainWindow.Side = z;
				selectButton(track, z);
			}
			return (tuple != null);
		}

	}
}

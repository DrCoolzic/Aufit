/*!
@file ClockDrawing.cs
<summary>Everything related to drawing the Clock chart</summary>

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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;

using KFStreamPackage;
using GlobalPackage;
using WD1772Package;

namespace ChartPackage {
	/// <summary>
	/// This class contains all functions required to draw the Clock chart
	/// </summary>
	class BitWidthDrawing {
		private ChartLayout _bitWidthChartLayout = null;
		private int _minBitWidth;
		private int _maxBitWidth;
		private int _track, _side, _rev;
		private CheckBox _avgCheckBox;

		/// <summary>
		/// Access the minimum value of the clock computed in setBitWidthDefault()
		/// </summary>
		public int MinBitWidth {
			get { return _minBitWidth; }
		}

		/// <summary>
		/// Access the maximum value of the clock computed in setBitWidthDefault()
		/// </summary>
		public int MaxBitWidth {
			get { return _maxBitWidth; }
		}

		/// <summary>
		/// Access the ChartLayout used to draw the clock
		/// </summary>
		public ChartLayout Chart {
			get { return _bitWidthChartLayout; }
		}


		/// <summary>
		/// Create a new ClockDrawing object and set default values
		/// </summary>
		/// <param name="grid">Grid of the clock chart that contains the two canvases</param>
		/// <param name="text">Text canvas of the clock chart</param>
		/// <param name="graphic">Graphic canvas of the clock chart</param>
		public BitWidthDrawing(Grid grid, Canvas text, Canvas graphic, CheckBox avg) {
			_bitWidthChartLayout = new ChartLayout(grid, text, graphic);
			_avgCheckBox = avg;
		}


		/// <summary>
		/// Draw the clock chart
		/// </summary>
		/// <param name="track">bitWidth for this track</param>
		/// <param name="side">bitWidth for this side</param>
		/// <param name="rev">bitWidth for this revolution</param>
		public void drawBitWidth(int track = -1, int side = -1, int rev = -1) {
			if (_bitWidthChartLayout == null) return;
			_bitWidthChartLayout.chartResetCanvas();

			int bitAvgCount = 1;
			if (_avgCheckBox.IsChecked == true)
				bitAvgCount = 300;

			if (track == -1) track = _track;
			else _track = track;
			if (side == -1) side = _side;
			else _side = side;
			if (rev == -1) rev = _rev;
			else _rev = rev;

			if (Global.Disk[track, side].fluxData == null) return;

			_bitWidthChartLayout.drawChartGrids();
			int speedUp = FluxDrawing.SpeedUp;

			Global.DiskTrack dt = Global.Disk[track, side];
			int start = Global.firstIndex(track, side, rev);
			int stop = Global.lastIndex(track, side, rev);

			int currentTime = 0;
			Polyline clockLine = new Polyline();
			clockLine.Stroke = Brushes.Black;
			clockLine.StrokeThickness = 1;

			int lastRev = Global.Disk[track, side].fluxDataRev.Count() - 1;
			int lastFlux = Global.Disk[track, side].fluxDataRev[lastRev].firstFluxIndex + Global.Disk[track, side].fluxDataRev[lastRev].fluxCount - 1;

			// populate
			for (int i = start; i < stop; i++) {
				int flux = dt.fluxData.fluxValue[i];
				currentTime += flux;

				int bitWidth = dt.decodedData.bitWidth[i];
				int bitWidthSum = 0;
				int bitWidthCount = 0;
				for (int j = 0; j < bitAvgCount; j++) {
					int index = i - bitAvgCount / 2 + j;
					if ((index >= 0) && (index <= stop)) {
						bitWidthSum += dt.decodedData.bitWidth[index];
						bitWidthCount++;
					}
				}
				if (bitWidthCount > 0) bitWidth = bitWidthSum / bitWidthCount;

				if (i % speedUp != 0) continue;
				if (!_bitWidthChartLayout.inExtendedViewport((double)currentTime / 1000000.0, (double)bitWidth)) continue;
				clockLine.Points.Add(_bitWidthChartLayout.NormPoint((double)currentTime / 1000000.0, bitWidth));
			}
			_bitWidthChartLayout.GraphicCanvas.Children.Add(clockLine);

			// draw IAM sync
			if (Global.Disk[track, side].iams != null)
				foreach (IAM iam in Global.Disk[track, side].iams[rev]) {
					Line iamLine = new Line();
					switch (iam.Type) {
						case IAMType.Id:
							iamLine.Stroke = Brushes.DarkOrange;
							break;
						case IAMType.Data:
							iamLine.Stroke = Brushes.DarkBlue;
							break;
						default:
							iamLine.Stroke = Brushes.Black;
							break;
					}
					iamLine.StrokeThickness = 1.5;
					if (iam.Error) {
						iamLine.StrokeDashArray = new DoubleCollection(new double[2] { 2, 2 });
						iamLine.Stroke = Brushes.Red;
						iamLine.StrokeThickness = 3;
					}

					iamLine.X1 = _bitWidthChartLayout.NormPoint(new Point((double)iam.info.startTime / 1000000.0, 2)).X;
					iamLine.Y1 = _bitWidthChartLayout.NormY(_bitWidthChartLayout.Ymin);
					iamLine.X2 = _bitWidthChartLayout.NormPoint(new Point((double)iam.info.startTime / 1000000.0, 10)).X;
					iamLine.Y2 = _bitWidthChartLayout.NormY(_bitWidthChartLayout.Ymax);

					//TextBox spliceInfo = new TextBox();
					//spliceInfo.Text = String.Format("Out of band at {0} ms -- flux value={1:F3} µs", (double)currentTime / 1000000.0, flux);
					//ToolTipService.SetToolTip(iamLine, spliceInfo);
					//ToolTipService.SetShowDuration(iamLine, 100000);
					_bitWidthChartLayout.GraphicCanvas.Children.Add(iamLine);

				}

		}


		/// <summary>
		/// Set the clock chart viewport default value
		/// </summary>
		public void setBitWidthDefault(int track, int side, int rev) {
			if (_bitWidthChartLayout == null) return;

			if (track == -1) track = _track;
			else _track = track;
			if (side == -1) side = _side;
			else _side = side;
			if (rev == -1) rev = _rev;
			else _rev = rev;
			if (Global.Disk[track, side].fluxData == null) return;

			int fluxXmin = 0;
			int fluxXmax = 200;

			_minBitWidth = Global.Disk[track, side].decodedData.minBitWidth;
			_maxBitWidth = Global.Disk[track, side].decodedData.maxBitWidth;
			if (_minBitWidth == _maxBitWidth) {
				_minBitWidth -= _minBitWidth / 100;
				_maxBitWidth += _maxBitWidth / 100;
			}   // do not want infinity scale
			_bitWidthChartLayout.setViewport(fluxXmin, fluxXmax, _minBitWidth, _maxBitWidth);
		}

	}
}

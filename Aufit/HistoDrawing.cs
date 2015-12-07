/*!
@file HistoDrawing.cs
<summary>Everything related to drawing the Histogram chart</summary>

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
using System.Windows.Media;
using System.Windows.Shapes;

using KFStreamPackage;
using GlobalPackage;
using Aufit;

namespace ChartPackage {
	/// <summary>
	/// This class contains all functions required to draw the Histogram chart
	/// </summary>
	class HistoDrawing {
		private ChartLayout _histoChartLayout = null;
		private MainWindow _win;

		public ChartLayout Chart {
			get { return _histoChartLayout; }
		}

		/// <summary>
		/// Create a new HistoDrawing object and sets the default values.
		/// </summary>
		/// <param name="histoGrid">The enclosing grid</param>
		/// <param name="cvTextHisto">The text canvas</param>
		/// <param name="cvGraphicHisto">The graphic canvas</param>
		/// <param name="win">Pointer to the MainWindow</param>
		public HistoDrawing(Grid histoGrid, Canvas cvTextHisto, Canvas cvGraphicHisto, MainWindow win) {
			_histoChartLayout = new ChartLayout(histoGrid, cvTextHisto, cvGraphicHisto);
			_win = win;
		}

		/// <summary>
		/// Draw the Histogram of flux transitions chart
		/// </summary>
		public void drawTrackHisto() {
			_histoChartLayout.chartResetCanvas();
			if (_histoChartLayout == null) return;

			int track = MainWindow.CurrentTrack;
			int side = MainWindow.CurrentSide;
			int rev = MainWindow.CurrentRev;
			if (Global.Disk[track, side].fluxData == null) return;

			drawHisto(Global.Disk[track, side].histoData);
		}   // drawHisto()


		public void drawHisto(HistoData h, string message = null) {
			bool fixedValue = true;
			_histoChartLayout.chartResetCanvas();
			if (_histoChartLayout == null) return;
			if ((h.histogram == null) || (h.histogram.Length == 0)) return;

			if (_win.cbHistoLog.IsChecked.Value) _histoChartLayout.HasYGrid = false;
			else _histoChartLayout.HasYGrid = true;

			int xmin = Int32.MaxValue;
			int xmax = h.histogram.Length;
			int ymin = 0;
			int ymax = 0;

			int value;
			for (int i = 0; i < h.histogram.Length; i++) {
				if (_win.cbHistoLog.IsChecked.Value)
					value = (int)Math.Log(h.histogram[i] + 1, 1.2);
				else
					value = h.histogram[i];
				if ((xmin == Int32.MaxValue) && (value != 0)) xmin = i - 1;
				if (value > ymax) ymax = value;
			}

			xmin *= Global.HISTO_SCALE;
			xmax *= Global.HISTO_SCALE;
			if (fixedValue) {
				xmin = 2000;    // fixed value
				if (xmax < 10000) xmax = 10000; // fixed value
			}
			// eventually limit to 10 µS
			if ((xmax > 10000) && _win.cbLimitFlux.IsChecked.Value) xmax = 10000;

			_histoChartLayout.setDefaultViewport((double)xmin / 1000.0, (double)xmax / 1000.0, ymin, ymax);
			_histoChartLayout.drawChartGrids();

			Polygon pg = new Polygon();
			pg.Stroke = Brushes.Transparent;
			pg.StrokeThickness = 0;
			pg.Fill = Brushes.Orange;

			// first dummy point
			pg.Points.Add(_histoChartLayout.NormPoint(0, 0));

			Point[] points = new Point[h.histogram.Length];
			// all pointsInViewport
			double timing;
			for (int i = 0; i < h.histogram.Length - 1; i++) {
				if (_win.cbHistoLog.IsChecked.Value) {
					value = (int)Math.Log(h.histogram[i] + 1, 1.2);
				}
				else {
					value = h.histogram[i];
				}

				timing = ((double)i * (double)Global.HISTO_SCALE) / 1000.0;
				pg.Points.Add(_histoChartLayout.NormPoint(timing, value));
				points[i] = _histoChartLayout.NormPoint(timing, value);
			}
			// force last point to zero
			timing = ((h.histogram.Length - 1) * (double)Global.HISTO_SCALE) / 1000.0;
			pg.Points.Add(_histoChartLayout.NormPoint(timing, 0));
			points[h.histogram.Length - 1] = _histoChartLayout.NormPoint(timing, 0);

			// add polygon to canvas
			_histoChartLayout.GraphicCanvas.Children.Add(pg);

			// compute control pointsInViewport for Bezier curve if formatted	
			if (h.entropy < Global.UNFORMATED_ENTROPY) {
				Point[] cp1, cp2;
				ovp.BezierSpline.GetCurveControlPoints(points, out cp1, out cp2);
				PathSegmentCollection lines = new PathSegmentCollection();
				for (int i = 0; i < cp1.Length; ++i)
					lines.Add(new BezierSegment(cp1[i], cp2[i], points[i + 1], true));
				PathFigure f = new PathFigure(points[0], lines, false);
				PathGeometry g = new PathGeometry(new PathFigure[] { f });
				System.Windows.Shapes.Path path = new System.Windows.Shapes.Path() { Stroke = Brushes.Blue, StrokeThickness = 1, Data = g };
				_histoChartLayout.GraphicCanvas.Children.Add(path);
			}   // formatted

			_win.tbEntropy.Text = String.Format("{0} = {1:F2}", (message == null) ? "Track" : message, h.entropy);

			if (h.peaks != null)
				foreach (Tuple<int, int> peak in h.peaks) {
					Line peakLine = new Line();
					peakLine.Stroke = Brushes.Black;
					peakLine.StrokeThickness = 1.5;
					peakLine.X1 = _histoChartLayout.NormPoint(new Point((double)peak.Item1 / 1000.0, 0)).X;
					peakLine.Y1 = _histoChartLayout.NormY(_histoChartLayout.Ymin);
					peakLine.X2 = _histoChartLayout.NormPoint(new Point((double)peak.Item1 / 1000.0, 0)).X;
					peakLine.Y2 = _histoChartLayout.NormY(_histoChartLayout.Ymax);

					TextBox peakInfo = new TextBox();
					peakInfo.Text = String.Format("Peak at {0} ns Value {1}", peak.Item1, peak.Item2);
					ToolTipService.SetToolTip(peakLine, peakInfo);
					ToolTipService.SetShowDuration(peakLine, 100000);
					_histoChartLayout.GraphicCanvas.Children.Add(peakLine);
				}

		}   // drawHisto()
	}
}

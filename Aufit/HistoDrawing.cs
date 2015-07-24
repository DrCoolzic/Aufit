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

using KFReaderPackage;
using GlobalPackage;
using Aufit;

namespace ChartPackage {
/// <summary>
/// This class contains all functions required to draw the Histogram chart
/// </summary>
class HistoDrawing {
	private ChartLayout _histoChartLayout = null;
	private MainWindow _win;
	private int _track, _side, _rev;

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
	public void drawHisto(int track = -1, int side = -1, int rev = -1) {
		bool fixedValue = true;

		_histoChartLayout.chartResetCanvas();
		if (_histoChartLayout == null) return;

		if (track == -1) track = _track;
		else _track = track;
		if (side == -1) side = _side;
		else _side = side;
		if (rev == -1) rev = _rev;
		else _rev = rev;

		if (Global.Disk[track , side].fluxData == null) return;
		if (Global.Disk[track, side].histoData.histogram == null) return;
		if (Global.Disk[track, side].histoData.histogram.Length == 0) return;

		if (_win.cbHistoLog.IsChecked.Value) _histoChartLayout.HasYGrid = false;
		else _histoChartLayout.HasYGrid = true;

		int xmin = Int32.MaxValue;
		int xmax = Global.Disk[track, side].histoData.histogram.Length;
		int ymin = 0;
		int ymax = 0;

		int value;
		for (int i = 0; i < Global.Disk[track, side].histoData.histogram.Length; i++) {
			if (_win.cbHistoLog.IsChecked.Value)
				value = (int)Math.Log(Global.Disk[track, side].histoData.histogram[i] + 1, 1.2);
			else
				value = Global.Disk[track, side].histoData.histogram[i];
			if ((xmin == Int32.MaxValue) && (value != 0)) xmin = i - 1;
			if (value > ymax) ymax = value;
		}


		//xmin = Global.sampleToTiming(xmin);
		//xmax = Global.sampleToTiming(xmax);
		xmin *= Global.HISTO_SCALE;
		xmax *= Global.HISTO_SCALE;
		if (fixedValue) {
			xmin = 2000;	// fixed value
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

		Point[] points = new Point[Global.Disk[track, side].histoData.histogram.Length];
		// all pointsInViewport
		double timing;
		for (int i = 0; i < Global.Disk[track, side].histoData.histogram.Length - 1; i++) {
			if (_win.cbHistoLog.IsChecked.Value) {
				value = (int)Math.Log(Global.Disk[track, side].histoData.histogram[i] + 1, 1.2);
			}
			else {
				value = Global.Disk[track, side].histoData.histogram[i];
			}

			timing = ((double)i * (double)Global.HISTO_SCALE) / 1000.0;
			pg.Points.Add(_histoChartLayout.NormPoint(timing, value));
			points[i] = _histoChartLayout.NormPoint(timing, value);
		}
		// force last point to zero
		timing = ((Global.Disk[track, side].histoData.histogram.Length - 1) * (double)Global.HISTO_SCALE) / 1000.0;
		pg.Points.Add(_histoChartLayout.NormPoint(timing, 0));
		points[Global.Disk[track, side].histoData.histogram.Length - 1] = _histoChartLayout.NormPoint(timing, 0);

		// add polygon to canvas
		_histoChartLayout.GraphicCanvas.Children.Add(pg);

		// compute control pointsInViewport for Bezier curve if formatted	
		if (Global.Disk[track, side].histoData.entropy < Global.UNFORMATED_ENTROPY) {			
			Point[] cp1, cp2;
			ovp.BezierSpline.GetCurveControlPoints(points, out cp1, out cp2);
			PathSegmentCollection lines = new PathSegmentCollection();
			for (int i = 0; i < cp1.Length; ++i)
				lines.Add(new BezierSegment(cp1[i], cp2[i], points[i + 1], true));
			PathFigure f = new PathFigure(points[0], lines, false);
			PathGeometry g = new PathGeometry(new PathFigure[] { f });
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path() { Stroke = Brushes.Blue, StrokeThickness = 1, Data = g };
			_histoChartLayout.GraphicCanvas.Children.Add(path);
		}	// formatted
	}	// drawHisto()

}
}

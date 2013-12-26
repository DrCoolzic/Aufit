/*!
@file HistoDrawing.cs
<summary>Everything related to drawing the Histogram chart</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tool project.\n\n
The Atari Universal FD Image Tool project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tool project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
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

using KFReaderNS;
using Aufit;

namespace ChartNS {
/// <summary>
/// This class contains all functions required to draw the Histogram chart
/// </summary>
class HistoDrawing {
	private ChartLayout _histoChartLayout = null;

	/// <summary>
	/// Create a new HistoDrawing object and sets the default values.
	/// </summary>
	/// <param name="histoGrid">The enclosing grid</param>
	/// <param name="cvTextHisto">The text canvas</param>
	/// <param name="cvGraphicHisto">The graphic canvas</param>
	public HistoDrawing(Grid histoGrid, Canvas cvTextHisto, Canvas cvGraphicHisto) {
		_histoChartLayout = new ChartLayout(histoGrid, cvTextHisto, cvGraphicHisto);
	}

	/// <summary>
	/// Draw the Histogram of flux transitions chart
	/// </summary>
	public void drawHisto(bool logScale, bool limitFlux) {
		bool fixedValue = true;
		_histoChartLayout.chartResetCanvas();
		if ((_histoChartLayout == null) || (Global.currentTrack().fluxData == null)) return;

		if (logScale) _histoChartLayout.HasYGrid = false;
		else _histoChartLayout.HasYGrid = true;

		int value;
		// compute Xmax and Ymax
		int xmin = Int32.MaxValue;
		//int xmax = Global.Histo.Length;
		int xmax = Global.currentTrack().histoData.histogram.Length;
		int ymin = 0;
		int ymax = 0;

		//for (int i = 0; i < Global.Histo.Length; i++) {
		for (int i = 0; i < Global.currentTrack().histoData.histogram.Length; i++) {
			if (logScale)
				value = (int)Math.Log(Global.currentTrack().histoData.histogram[i] + 1, 1.2);
			else
				value = Global.currentTrack().histoData.histogram[i];
			if ((xmin == Int32.MaxValue) && (value != 0))
				xmin = i - 1;
			if (value > ymax) ymax = value;
		}


		xmin = Global.sampleToTiming(xmin);
		xmax = Global.sampleToTiming(xmax);
		if (fixedValue) {
			//_viewportXmin = _min_flux_def_value;	// fixed value
			//if (_viewportXmax < _max_flux_def_value) _viewportXmax = _max_flux_def_value; // fixed value
			xmin = 2000;	// fixed value
			if (xmax < 10000) xmax = 10000; // fixed value
		}
		// eventually limit to 10 µS
		if ((xmax > 10000) && limitFlux) xmax = 10000;

		_histoChartLayout.setDefaultViewport((double)xmin / 1000.0, (double)xmax / 1000.0, ymin, ymax);
		_histoChartLayout.drawChartGrids();

		Polygon pg = new Polygon();
		pg.Stroke = Brushes.Transparent;
		pg.StrokeThickness = 0;
		pg.Fill = Brushes.Orange;

		// first dummy point
		pg.Points.Add(_histoChartLayout.NormPoint(0, 0));

		Point[] points = new Point[Global.currentTrack().histoData.histogram.Length];
		// all pointsInViewport
		for (int i = 0; i < Global.currentTrack().histoData.histogram.Length - 1; i++) {
			if (logScale) {
				value = (int)Math.Log(Global.currentTrack().histoData.histogram[i] + 1, 1.2);
			}
			else {
				value = Global.currentTrack().histoData.histogram[i];
			}

			pg.Points.Add(_histoChartLayout.NormPoint((double)Global.sampleToTiming(i) / 1000.0, value));
			points[i] = _histoChartLayout.NormPoint((double)Global.sampleToTiming(i) / 1000.0, value);
		}
		// force last point to zero
		pg.Points.Add(_histoChartLayout.NormPoint((double)Global.sampleToTiming(Global.currentTrack().histoData.histogram.Length - 1) / 1000.0, 0));
		points[Global.currentTrack().histoData.histogram.Length - 1] = _histoChartLayout.NormPoint((double)Global.sampleToTiming(Global.currentTrack().histoData.histogram.Length - 1) / 1000.0, 0);

		// add polygon to canvas
		_histoChartLayout.GraphicCanvas.Children.Add(pg);

		if (Global.currentTrack().histoData.entropy > 0) {
			// compute control pointsInViewport for Bezier curve
			Point[] cp1, cp2;
			ovp.BezierSpline.GetCurveControlPoints(points, out cp1, out cp2);
			PathSegmentCollection lines = new PathSegmentCollection();
			for (int i = 0; i < cp1.Length; ++i) {
				lines.Add(new BezierSegment(cp1[i], cp2[i], points[i + 1], true));
			}
			PathFigure f = new PathFigure(points[0], lines, false);
			PathGeometry g = new PathGeometry(new PathFigure[] { f });
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path() { Stroke = Brushes.Blue, StrokeThickness = 1, Data = g };

			_histoChartLayout.GraphicCanvas.Children.Add(path);
		}

		//// draw bands on histogram
		//if (Bands.Count < 12) foreach (double band in Bands) {
		//	Line gridline = new Line();
		//	gridline.Stroke = Brushes.Black;
		//	gridline.StrokeThickness = 1.0;
		//	gridline.X2 = gridline.X1 = _histoChartLayout.NormPoint(new Point(band, _histoChartLayout.Ymin)).X;
		//	gridline.Y1 = _histoChartLayout.NormPoint(new Point(band, _histoChartLayout.Ymin)).Y;
		//	gridline.Y2 = _histoChartLayout.NormPoint(new Point(band, _histoChartLayout.Ymax)).Y;
		//	_histoChartLayout.GraphicCanvas.Children.Add(gridline);
		//}
	}

}
}

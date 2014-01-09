/*!
@file FluxDrawing.cs
<summary>Everything related to drawing the Flux chart</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using WD1772Space;
using KFReaderSpace;
using GlobalSpace;
using Aufit;

namespace ChartSpace {
/// <summary>
/// This class contains all functions required to draw the flux chart
/// </summary>
class FluxDrawing {
	private FluxEnsemble _fluxEnsemble;
	private ChartLayout _fluxChartLayout;

	private double _viewportXmin;
	private double _viewportXmax;
	private MainWindow _win;
	private static int _speedUp;

	private Point _startPoint = new Point();
	private Point _endPoint = new Point();
	private Shape _rubberBand = null;
	private double _xIncrement = 100;
	private double _yIncrement = 10;

	private int _track, _side, _rev;

	/// <summary>
	/// Set to true for slow drawing (i.e. draw ALL points)
	/// </summary>
	public bool SlowDraw { get; set; }

	/// <summary>
	/// When set to true we limit the maximum value of the flux to 10µs
	/// </summary>
//	public bool LimitFlux { get; set; }

	/// <summary>
	/// When set we draw the fluxes with a limited number of points for performance
	/// </summary>
	public bool FastDraw { get; set; }

	/// <summary>
	/// Access _speedUp
	/// </summary>
	/// <remarks>The _speedUp value is computed by drawFlux to optimize the speed drawing.
	/// This value is based on the number of points to display that can vary based on the
	/// zoom factor. You can access this property to get the computed factor.</remarks>
	public static int SpeedUp {
		get { return _speedUp;  }
	}

	/// <summary>
	/// Access the current flux viewport Xmin
	/// </summary>
	public double ViewportXmin {
		get { return _viewportXmin; }
	}

	/// <summary>
	/// Access the current flux viewport Xmax
	/// </summary>
	public double ViewportXmax {
		get { return _viewportXmax; }
	}

	/// <summary>
	/// Access the ChartLayout used to draw the flux
	/// </summary>
	public ChartLayout Chart {
		get { return _fluxChartLayout; }
	}


	/// <summary>
	/// Create a FluxDrawing object and set the default values.
	/// </summary>
	/// <param name="grid">Grid in the ChartLayout</param>
	/// <param name="text">Test canvas in the ChartLayout</param>
	/// <param name="graphic">Graphic canvas in the ChartLayout</param>
	/// <param name="win">Pointer to the MainWindow</param>
	public FluxDrawing(Grid grid, Canvas text, Canvas graphic, MainWindow win) {
		_fluxChartLayout = new ChartLayout(grid, text, graphic);
		_win = win;
		FastDraw = false;
		SlowDraw = false;
		//LimitFlux = true;
	}


	/// <summary>
	/// draw the flux chart
	/// </summary>
	public void drawFlux(int track = -1, int side = -1, int rev = -1) {
		_fluxChartLayout.chartResetCanvas();

		if (track == -1) track = _track;
		else _track = track;
		if (side == -1) side = _side;
		else _side = side;
		if (rev == -1) rev = _rev;
		else _rev = rev;
		if (Global.Disk[track, side].fluxData == null) return;

		_fluxChartLayout.drawChartGrids();

		DSR dsr = new DSR();
		Global.TrackData td = Global.Disk[track, side];
		int start = Global.startOffset(track, side, rev);
		int stop = Global.stopOffset(track, side, rev);
		int indexFirstFluxCorrection = Global.indexTimePosition(track, side, rev);

		int maxFlux = 0;
		int minFlux = Int32.MaxValue;
		int pointsInViewport = 0;
		int currentTime = 0;

		// compute the number of points in the "extended" viewport
		if (_fluxChartLayout.zoomed()) {
			for (int i = start; i < stop; i++) {
				int flux = Global.sampleToTiming(td.fluxData.fluxValue[i]);
				if (i == start)
					flux -= indexFirstFluxCorrection;
				currentTime += flux;
				if (_fluxChartLayout.inExtendedViewport((double)currentTime / 1000000.0, (double)flux / 1000.0))
					pointsInViewport++;
			}	// for all
		}	// zoomed
		else {
			pointsInViewport = stop - start;
		}

		double zoom = _fluxChartLayout.zoom();
		_speedUp = pointsInViewport / 10000 + 1;
		if (SlowDraw) _speedUp = 1;
		if (FastDraw) _speedUp = 20;

		int size = pointsInViewport + 1;
		FluxEnsemble.FluxPoint[] fluxPoints = new FluxEnsemble.FluxPoint[size];

		// populate
		currentTime = 0;
		pointsInViewport = 0;
		int syncCount = 0;


		for (int i = start; i < stop; i++) {

			int flux = Global.sampleToTiming(td.fluxData.fluxValue[i]);
			if (i == start) flux -= indexFirstFluxCorrection;
			if (flux > maxFlux) maxFlux = flux;
			if (flux < minFlux) minFlux = flux;
			currentTime += flux;

			// black line for revolution
			if (rev == 0) {
				for (int j = 1; j < Global.Disk[track, side].revData.Count(); j++) {
					if (Global.Disk[track, side].revData[j].fluxOffset - 1 == i) {
						Line revLine = new Line();
						revLine.Stroke = Brushes.Black;
						revLine.StrokeThickness = 1.5;
						revLine.X1 = _fluxChartLayout.NormPoint(new Point((double)(currentTime + indexFirstFluxCorrection) / 1000000.0, 2)).X;
						revLine.Y1 = _fluxChartLayout.NormY(_fluxChartLayout.Ymin);
						revLine.X2 = _fluxChartLayout.NormPoint(new Point((double)(currentTime + indexFirstFluxCorrection) / 1000000.0, 10)).X;
						revLine.Y2 = _fluxChartLayout.NormY(_fluxChartLayout.Ymax);
						_fluxChartLayout.GraphicCanvas.Children.Add(revLine);
					}
				}
			}

			// line for sync
			int nbWin = td.decodeData.fluxInWin[i];
			int clk = td.decodeData.decodedClock[i];
			dsr.shiftBitsIn(nbWin);	// push data in register
			while (dsr.byteReady()) {
				int bitShift = dsr.searchSynch();	// search sync
				dsr.extract();						// get one mfm
				if (dsr.SyncFound) {
					syncCount++;
					Line syncLine = new Line();
					if ((dsr.Data == 0xA1) && (_win.cbA1.IsChecked.Value)) {
						syncLine.Stroke = Brushes.Red;
						syncLine.StrokeThickness = 1;
						syncLine.X1 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 2)).X;
						syncLine.Y1 = _fluxChartLayout.NormY(_fluxChartLayout.Ymin);
						syncLine.X2 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 10)).X;
						syncLine.Y2 = _fluxChartLayout.NormY(_fluxChartLayout.Ymax);
						_fluxChartLayout.GraphicCanvas.Children.Add(syncLine);
					}
					else if ((dsr.Data == 0xC2) && (_win.cbC2.IsChecked.Value)) {
						syncLine.Stroke = Brushes.Blue;
						syncLine.StrokeThickness = 0.5;
						syncLine.X1 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 2)).X;
						syncLine.Y1 = _fluxChartLayout.NormY(_fluxChartLayout.Ymin);
						syncLine.X2 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 10)).X;
						syncLine.Y2 = _fluxChartLayout.NormY(_fluxChartLayout.Ymax);
						_fluxChartLayout.GraphicCanvas.Children.Add(syncLine);
					}
				}	// sync byte
				else syncCount = 0;
			}

			if (i % SpeedUp != 0) continue;
			if (!_fluxChartLayout.inExtendedViewport((double)currentTime / 1000000.0, (double)flux/1000.0)) continue;

			double r = (zoom > 1) ? (zoom > 25) ? (zoom > 50) ? 2 : 1.6 : 1.2 : 0.7;
			if ((flux > 10000) && (Global.Disk[track, side].histoData.entropy > 0)) {
				r = 2.5;
				fluxPoints[pointsInViewport].colorIndex = 4;
			}

			//if ((flux < 3.85) || ((flux > 4.15) && (flux < 5.85)) || ((flux > 6.15) && (flux < 7.85)) || (flux > 8.15))
			//	fluxPoints[pointsInViewport].colorIndex = 1;
			//else
			fluxPoints[pointsInViewport].colorIndex = 0;
			//if (i % 2 == 0) fluxPoints[pointsInViewport].colorIndex += 2; ;

			
			fluxPoints[pointsInViewport].ellipseGeometry = new EllipseGeometry(_fluxChartLayout.NormPoint((double)currentTime / 1000000.0, (double)flux / 1000.0), r, r);
			pointsInViewport++;
		}

		//add to FluxEnsemble - this will draw flux via onrender method
		_fluxEnsemble = new FluxEnsemble(fluxPoints);
		_fluxChartLayout.GraphicCanvas.Children.Add(_fluxEnsemble);

		_win.tbFluxInfo.Clear();
		_win.tbFluxInfo.AppendText(String.Format("Track {0:D2}.{1} Rev {2}", track, side, rev));
		_win.tbFluxInfo.AppendText(String.Format(" - Flux range: {0:F2} {1:F2} µs", (double)minFlux / 1000.0, (double)maxFlux / 1000.0));
		_win.tbFluxInfo.AppendText(String.Format(" - Track length: {0:F3} ms", (double)(currentTime + indexFirstFluxCorrection) / 1000000.0));
		_win.tbFluxInfo.AppendText(String.Format(" - Debug z={0:F1} s={1} p={2}", _fluxChartLayout.zoom(), SpeedUp, pointsInViewport));
	}


	/// <summary>
	/// Set the default viewport values for the flux graphic canvas
	/// </summary>
	public void setFluxDefault(int track = -1, int side = -1, int rev = -1) {
		if (_fluxChartLayout == null) return; 
		if (track == -1) track = _track;
		else _track = track;
		if (side == -1) side = _side;
		else _side = side;
		if (rev == -1) rev = _rev;
		else _rev = rev;
		if (Global.Disk[track, side].fluxData == null) return;

		Global.TrackData td = Global.Disk[track, side];

		_viewportXmin = 0;
		if (rev > 0)
			_viewportXmax = 200;
		else
			_viewportXmax = 200 * Global.Disk[track, side].revData.Count();

		int max = Global.sampleToTiming(td.fluxData.fluxMax) + 500;
		if (max < 10000) max = 10000;
		if (_win.cbLimitFlux.IsChecked.Value && (max > 10000)) max = 10000;
		int min = Global.sampleToTiming(td.fluxData.fluxMin) - 500;
		if (min < 2000) min = 2000;

		_fluxChartLayout.setDefaultViewport(_viewportXmin, _viewportXmax, min / 1000, max / 1000);
	}

	/// <summary>
	/// Mouse Left Button Up event handler
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public void mouseLBU(object sender, MouseButtonEventArgs e) {
		// BUG FIX ABOVE
		//if (_fluxChartLayout.GraphicCanvas.IsMouseCaptured) {
			_fluxChartLayout.GraphicCanvas.ReleaseMouseCapture();
			_fluxChartLayout.GraphicCanvas.Cursor = Cursors.Arrow;
		//}
		//else
		//	return;		// we receive a mouse up event from Openfiledialog !!! Bug?

		if (_rubberBand != null) {
			_rubberBand = null;
		}

		double x0 = 0;
		double x1 = 1;
		double y0 = 0;
		double y1 = 1;
		_endPoint = e.GetPosition(_fluxChartLayout.GraphicCanvas);

		if (!_fluxChartLayout.zoomed()) {
			// we use the 2 pointsInViewport to zoom
			if (_endPoint.X > _startPoint.X) {
				x0 = _fluxChartLayout.Xmin + (_fluxChartLayout.Xmax - _fluxChartLayout.Xmin) * _startPoint.X / _fluxChartLayout.GraphicCanvas.Width;
				x1 = _fluxChartLayout.Xmin + (_fluxChartLayout.Xmax - _fluxChartLayout.Xmin) * _endPoint.X / _fluxChartLayout.GraphicCanvas.Width;
			}
			else if (_endPoint.X < _startPoint.X) {
				x1 = _fluxChartLayout.Xmin + (_fluxChartLayout.Xmax - _fluxChartLayout.Xmin) * _startPoint.X / _fluxChartLayout.GraphicCanvas.Width;
				x0 = _fluxChartLayout.Xmin + (_fluxChartLayout.Xmax - _fluxChartLayout.Xmin) * _endPoint.X / _fluxChartLayout.GraphicCanvas.Width;
			}

			if (_endPoint.Y < _startPoint.Y) {
				y0 = _fluxChartLayout.Ymin + (_fluxChartLayout.Ymax - _fluxChartLayout.Ymin) * (_fluxChartLayout.GraphicCanvas.Height - _startPoint.Y) / _fluxChartLayout.GraphicCanvas.Height;
				y1 = _fluxChartLayout.Ymin + (_fluxChartLayout.Ymax - _fluxChartLayout.Ymin) * (_fluxChartLayout.GraphicCanvas.Height - _endPoint.Y) / _fluxChartLayout.GraphicCanvas.Height;
			}
			else if (_endPoint.Y > _startPoint.Y) {
				y1 = _fluxChartLayout.Ymin + (_fluxChartLayout.Ymax - _fluxChartLayout.Ymin) * (_fluxChartLayout.GraphicCanvas.Height - _startPoint.Y) / _fluxChartLayout.GraphicCanvas.Height;
				y0 = _fluxChartLayout.Ymin + (_fluxChartLayout.Ymax - _fluxChartLayout.Ymin) * (_fluxChartLayout.GraphicCanvas.Height - _endPoint.Y) / _fluxChartLayout.GraphicCanvas.Height;
			}
		}
		else {
			// we use the 2 pointsInViewport to move
			double dx = (_fluxChartLayout.Xmax - _fluxChartLayout.Xmin) * (_endPoint.X - _startPoint.X) / _fluxChartLayout.GraphicCanvas.Width;
			double dy = (_fluxChartLayout.Ymax - _fluxChartLayout.Ymin) * (_endPoint.Y - _startPoint.Y) / _fluxChartLayout.GraphicCanvas.Height;
			x0 = _fluxChartLayout.Xmin - dx;
			x1 = _fluxChartLayout.Xmax - dx;
			y0 = _fluxChartLayout.Ymin + dy;
			y1 = _fluxChartLayout.Ymax + dy;
			_fluxEnsemble.translateFluxes(0, 0);
		}
		_viewportXmin = x0;
		_viewportXmax = x1;
		_fluxChartLayout.setViewport(x0, x1, y0, y1);
		drawFlux();
	}

	/// <summary>
	/// Mouse Left Button Down event handler
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public void mouseLBD(object sender, MouseButtonEventArgs e) {
		if (!_fluxChartLayout.GraphicCanvas.IsMouseCaptured) {
			_startPoint = e.GetPosition(_fluxChartLayout.GraphicCanvas);
			_fluxChartLayout.GraphicCanvas.CaptureMouse();
		}
	}

	/// <summary>
	/// Mouse move event handler
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public void mouseMove(object sender, MouseEventArgs e) {
		if (_fluxChartLayout.GraphicCanvas.IsMouseCaptured) {
			_endPoint = e.GetPosition(_fluxChartLayout.GraphicCanvas);
			if (!_fluxChartLayout.zoomed()) {
				if (_rubberBand == null) {
					_rubberBand = new Rectangle();
					_rubberBand.Stroke = Brushes.Black;
					_rubberBand.StrokeThickness = 2;
					_fluxChartLayout.GraphicCanvas.Children.Add(_rubberBand);
				}
				_rubberBand.Width = Math.Abs(_startPoint.X - _endPoint.X);
				_rubberBand.Height = Math.Abs(_startPoint.Y - _endPoint.Y);
				double left = Math.Min(_startPoint.X, _endPoint.X);
				double top = Math.Min(_startPoint.Y, _endPoint.Y);
				Canvas.SetLeft(_rubberBand, left);
				Canvas.SetTop(_rubberBand, top);
			}	// not zoomed => zoom area
			else {
				//TranslateTransform tt = new TranslateTransform();
				double x = _endPoint.X - _startPoint.X;
				double y = _endPoint.Y - _startPoint.Y;
				_fluxEnsemble.translateFluxes(x,y);
			}	// zoomed => pan
		}
	}

	/// <summary>
	/// Mouse Right Button Down event handler
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public void mouseRBD(object sender, MouseButtonEventArgs e) {
		_fluxChartLayout.GraphicCanvas.Children.Clear();
		_fluxChartLayout.TextCanvas.Children.RemoveRange(1, _fluxChartLayout.TextCanvas.Children.Count - 1);
		setFluxDefault();
		drawFlux();
	}

	/// <summary>
	/// Mouse wheel event handler
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public void mouseWheel(object sender, MouseWheelEventArgs e) {
		////var x = Math.Pow(2, e.Delta / 3.0 / Mouse.MouseWheelDeltaForOneLine);

		double dx = (e.Delta > 0) ? _xIncrement : -_xIncrement;
		double dy = (e.Delta > 0) ? _yIncrement : -_yIncrement;
		double x0 = _fluxChartLayout.Xmin + (_fluxChartLayout.Xmax - _fluxChartLayout.Xmin) * dx / _fluxChartLayout.GraphicCanvas.Width;
		double x1 = _fluxChartLayout.Xmax - (_fluxChartLayout.Xmax - _fluxChartLayout.Xmin) * dx / _fluxChartLayout.GraphicCanvas.Width;
		double y0 = _fluxChartLayout.Ymin + (_fluxChartLayout.Ymax - _fluxChartLayout.Ymin) * dy / _fluxChartLayout.GraphicCanvas.Height;
		double y1 = _fluxChartLayout.Ymax - (_fluxChartLayout.Ymax - _fluxChartLayout.Ymin) * dy / _fluxChartLayout.GraphicCanvas.Height;
		_viewportXmin = x0;
		_viewportXmax = x1;

		_fluxChartLayout.GraphicCanvas.Children.Clear();
		_fluxChartLayout.TextCanvas.Children.RemoveRange(1, _fluxChartLayout.TextCanvas.Children.Count - 1);
		_fluxChartLayout.setViewport(x0, x1, y0, y1);
		drawFlux();
	}

}
}


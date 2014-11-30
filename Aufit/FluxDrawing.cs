/*!
@file FluxDrawing.cs
<summary>Everything related to drawing the Flux chart</summary>

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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using WD1772Package;
using KFReaderPackage;
using GlobalPackage;
using Aufit;

namespace ChartPackage {
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
		Global.DiskTrack dt = Global.Disk[track, side];

		if (dt.packedFluxData == null) return;

		_fluxChartLayout.drawChartGrids();
		
		int startBit = dt.packedFluxDataRev[rev].firstFluxBit;
		int stopBit = startBit + dt.packedFluxDataRev[rev].bitCount;
		int pointsInViewport = 0;
		int currentTime = 0;

		// compute the number of points in the "extended" viewport
		if (_fluxChartLayout.zoomed()) {
			for (int bitPosition = startBit; bitPosition < stopBit; bitPosition++) {
				int bytePos = bitPosition / 8;
				int testBit = 0x1 << (bitPosition % 8);
				if ((dt.packedFluxData.packedBits[bytePos] & (byte)testBit) == 0) continue;
				int flux = Global.fluxWidth(bitPosition, track, side);
				currentTime += flux;
				if (_fluxChartLayout.inExtendedViewport((double)currentTime / 1000000.0, (double)flux / 1000.0))
					pointsInViewport++;
			}	// for all
		}	// zoomed
		else
			pointsInViewport = dt.packedFluxDataRev[rev].fluxCount;

		double zoom = _fluxChartLayout.zoom();
		_speedUp = pointsInViewport / 10000 + 1;
		if (SlowDraw) _speedUp = 1;
		if (FastDraw) _speedUp = 20;

		int size = pointsInViewport + 1;
		FluxEnsemble.FluxPoint[] fluxPoints = new FluxEnsemble.FluxPoint[size];

		// populate
		currentTime = 0;
		pointsInViewport = 0;
		int fluxCount = 0;

		DSR dsr = new DSR();
		dsr.SyncDetectEnable = true;

		for (int bitPosition = startBit; bitPosition < stopBit; bitPosition++) {
			int currentByte = bitPosition / 8;
			int currentBitInByte = bitPosition % 8;
			byte bits = dt.packedFluxData.packedBits[currentByte];
			int bit = (bits >> currentBitInByte) & 0x1;
			dsr.shiftBitsIn(bit);

			if (bit == 0) continue;
			int flux = Global.fluxWidth(bitPosition, track, side);
			currentTime += flux;
			fluxCount++;

			// add sync bar
			if (dsr.SyncFound) {
				Line syncLine = new Line();
				if ((dsr.A1Found) && (_win.cbA1.IsChecked.Value)) {
					syncLine.Stroke = Brushes.Red;
					syncLine.StrokeThickness = 1;
					syncLine.X1 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 2)).X;
					syncLine.Y1 = _fluxChartLayout.NormY(_fluxChartLayout.Ymin);
					syncLine.X2 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 10)).X;
					syncLine.Y2 = _fluxChartLayout.NormY(_fluxChartLayout.Ymax);
					_fluxChartLayout.GraphicCanvas.Children.Add(syncLine);
				}
				else if ((dsr.C2Found) && (_win.cbC2.IsChecked.Value)) {
					syncLine.Stroke = Brushes.Blue;
					syncLine.StrokeThickness = 0.5;
					syncLine.X1 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 2)).X;
					syncLine.Y1 = _fluxChartLayout.NormY(_fluxChartLayout.Ymin);
					syncLine.X2 = _fluxChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 10)).X;
					syncLine.Y2 = _fluxChartLayout.NormY(_fluxChartLayout.Ymax);
					_fluxChartLayout.GraphicCanvas.Children.Add(syncLine);
				}
			}

			if (fluxCount % SpeedUp != 0) continue;
			if (!_fluxChartLayout.inExtendedViewport((double)currentTime / 1000000.0, (double)flux / 1000.0)) continue;

			double r = (zoom > 1) ? (zoom > 25) ? (zoom > 50) ? 2 : 1.6 : 1.2 : 0.7;
			fluxPoints[pointsInViewport].colorIndex = 0;

			if (_win.cbLimitFlux.IsChecked.Value) {
				// border flux
				if (((flux > 4900) && (flux < 5100)) || ((flux > 6900) && (flux < 7100))) {
					//r *= 2;
					fluxPoints[pointsInViewport].colorIndex = 2;
				}
				// invalid flux
				if ((flux < 3500) || (flux > 8500)) {
					//r = 2.5 * r;
					fluxPoints[pointsInViewport].colorIndex = 4;
				}
				// odd even
				if (bitPosition % 2 == 0) fluxPoints[pointsInViewport].colorIndex++;
			}
			else
				fluxPoints[pointsInViewport].colorIndex = 6;
			
			fluxPoints[pointsInViewport].ellipseGeometry = new EllipseGeometry(_fluxChartLayout.NormPoint((double)currentTime / 1000000.0, (double)flux / 1000.0), r, r);
			pointsInViewport++;
		}

		//add to FluxEnsemble - this will draw flux via onrender method
		_fluxEnsemble = new FluxEnsemble(fluxPoints);
		_fluxChartLayout.GraphicCanvas.Children.Add(_fluxEnsemble);

		_win.tbFluxInfo.Clear();
		_win.tbFluxInfo.AppendText(String.Format("Track {0:D2}.{1} Rev {2} of {3}", track, side, rev + 1, dt.packedFluxDataRev.Count()));
		_win.tbFluxInfo.AppendText(String.Format(" - Flux range: {0:F2}-{1:F2} µs", 
			(double)dt.packedFluxData.minFlux / 1000.0, (double)dt.packedFluxData.maxFlux / 1000.0));
		_win.tbFluxInfo.AppendText(String.Format(" - Track length: {0:F3} ms - Flux count: {1}", (double)currentTime / 1000000.0, fluxCount));
		//_win.tbFluxInfo.AppendText(String.Format(" - Debug z={0:F1} s={1} p={2}", _fluxChartLayout.zoom(), SpeedUp, pointsInViewport));
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
		if (Global.Disk[track, side].packedFluxData == null) return;

		_viewportXmin = 0;
		_viewportXmax = 200;

		int max = Global.Disk[track, side].packedFluxData.maxFlux + 500;
		if (max < 10000) max = 10000;
		if (_win.cbLimitFlux.IsChecked.Value && (max > 10000)) max = 10000;
		int min = Global.Disk[track, side].packedFluxData.minFlux;
		if (min < 2000) min = 2000;

		_fluxChartLayout.setDefaultViewport(_viewportXmin, _viewportXmax, min / 1000, max / 1000);
	}

	/// <summary>
	/// Mouse Left Button Up event handler
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	public void mouseLBU(object sender, MouseButtonEventArgs e) {
		_fluxChartLayout.GraphicCanvas.ReleaseMouseCapture();
		_fluxChartLayout.GraphicCanvas.Cursor = Cursors.Arrow;

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


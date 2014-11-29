/*!
@file ClockDrawing.cs
<summary>Everything related to drawing the Clock chart</summary>

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
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows;

using KFReaderPackage;
using GlobalPackage;

namespace ChartPackage {
/// <summary>
/// This class contains all functions required to draw the Clock chart
/// </summary>
class ClockDrawing {
	private ChartLayout _clockChartLayout = null;
	private int _minBitWidth;
	private int _maxBitWidth;
	private int _track, _side, _rev;
	private const int CLK_AVG_COUNT = 50;

	/// <summary>
	/// Access the minimum value of the clock computed in setClockDefault()
	/// </summary>
	public int MinBitWidth {
		get { return _minBitWidth; }
	}

	/// <summary>
	/// Access the maximum value of the clock computed in setClockDefault()
	/// </summary>
	public int MaxBitWidth {
		get { return _maxBitWidth; }
	}

	/// <summary>
	/// Access the ChartLayout used to draw the clock
	/// </summary>
	public ChartLayout Chart {
		get { return _clockChartLayout; }
	}


	/// <summary>
	/// Create a new ClockDrawing object and set default values
	/// </summary>
	/// <param name="grid">Grid of the clock chart that contains the two canvases</param>
	/// <param name="text">Text canvas of the clock chart</param>
	/// <param name="graphic">Graphic canvas of the clock chart</param>
	public ClockDrawing(Grid grid, Canvas text, Canvas graphic) {
		_clockChartLayout = new ChartLayout(grid, text, graphic);
	}


	/// <summary>
	/// Draw the clock chart
	/// </summary>
	/// <param name="track">Clock for this track</param>
	/// <param name="side">Clock for this side</param>
	/// <param name="rev">Clock for this revolution</param>
	public void drawClock(int track = -1, int side = -1, int rev = -1) {
		if (_clockChartLayout == null) return;
		_clockChartLayout.chartResetCanvas();

		if (track == -1) track = _track;
		else _track = track;
		if (side == -1) side = _side;
		else _side = side;
		if (rev == -1) rev = _rev;
		else _rev = rev;

		Global.DiskTrack td = Global.Disk[track, side];
		if (Global.Disk[track, side].packedFluxData == null) return;

		_clockChartLayout.drawChartGrids();
		int speedUp = FluxDrawing.SpeedUp;

		int startBit = Global.Disk[track, side].packedFluxDataRev[rev].firstFluxBit;
		int stopBit = startBit + Global.Disk[track, side].packedFluxDataRev[rev].bitCount;

		int currentTime = 0;
		Polyline clockLine = new Polyline();
		clockLine.Stroke = Brushes.Black;
		clockLine.StrokeThickness = 1;

		int lastRev = Global.Disk[track, side].fluxDataRev.Count() - 1;
		int lastFlux = Global.Disk[track, side].fluxDataRev[lastRev].firstFluxIndex + Global.Disk[track, side].fluxDataRev[lastRev].fluxCount - 1;

		// populate
		for (int bitPosition = startBit; bitPosition < stopBit; bitPosition++) {
			int currentByte = bitPosition / 8;
			int currentBitInByte = bitPosition % 8;
			byte bits = Global.Disk[track, side].packedFluxData.packedBits[currentByte];
			int bit = (bits >> currentBitInByte) & 0x1;
			if (bit == 0) continue;

			int flux = Global.fluxValue(bitPosition, track, side);
			currentTime += flux;

			int clk = td.packedFluxData.bitWidth[currentByte];
			int avgClock = 0;
			int avgCount = 0;
			for (int j = 0; j < CLK_AVG_COUNT; j++) {
				int index = currentByte - CLK_AVG_COUNT / 2 + j;
				if ((index >= 0) && (index <= (stopBit/8))) {
					avgClock += td.packedFluxData.bitWidth[index];
					avgCount++;
				}
			}
			if (avgCount > 0) clk = avgClock / avgCount;

			if (bitPosition % speedUp != 0) continue;
			if (!_clockChartLayout.inExtendedViewport((double)currentTime / 1000000.0, (double)clk)) continue;
			clockLine.Points.Add(_clockChartLayout.NormPoint((double)currentTime / 1000000.0, clk));
		}
		_clockChartLayout.GraphicCanvas.Children.Add(clockLine);

		// Bezier Spline
		//int nbpoints = 0;
		//currentPosition = 0;
		//for (int indexStart = start; indexStart < stop; indexStart++) {
		//	int flux = StreamProcess.sampleToTiming(track.flux[indexStart]);
		//	if (indexStart == start) flux -= indexFirstFluxCorrection;
		//	currentPosition += flux;

		//	int clk = track.clock[indexStart];
		//	if (indexStart % speedUp != 0) continue;
		//	if (!_clockChartLayout.inExtendedViewport((double)currentPosition / 1000000.0, (double)clk)) continue;
		//	nbpoints++;
		//}
		//Point[] points = new Point[nbpoints+3];

		//nbpoints = 0;
		//currentPosition = 0;
		////points[nbpoints++] = _clockChartLayout.NormPoint(start, 2000);
		//for (int indexStart = start; indexStart < stop; indexStart++) {
		//	int flux = StreamProcess.sampleToTiming(track.flux[indexStart]);
		//	if (indexStart == start) flux -= indexFirstFluxCorrection;
		//	currentPosition += flux;
		//	int clk = track.clock[indexStart];
		//	if (indexStart % speedUp != 0) continue;
		//	if (!_clockChartLayout.inExtendedViewport((double)currentPosition / 1000000.0, (double)clk)) continue;
		//	points[nbpoints++] = _clockChartLayout.NormPoint((double)currentPosition / 1000000.0, clk);
		//}
		////points[nbpoints++] = _clockChartLayout.NormPoint(stop, 2200);

		//Point[] cp1, cp2;
		//ovp.BezierSpline.GetCurveControlPoints(points, out cp1, out cp2);
		//PathSegmentCollection lines = new PathSegmentCollection();
		//for (int indexStart = 0; indexStart < cp1.Length; ++indexStart) {
		//	lines.Add(new BezierSegment(cp1[indexStart], cp2[indexStart], points[indexStart + 1], true));
		//}
		//PathFigure f = new PathFigure(points[0], lines, false);
		//PathGeometry g = new PathGeometry(new PathFigure[] { f });
		//Path path = new Path() { Stroke = Brushes.Blue, StrokeThickness = 1, Data = g };
		//_clockChartLayout.GraphicCanvas.Children.Add(path);

	}


	/// <summary>
	/// Set the clock chart viewport default value
	/// </summary>
	public void setClockDefault(int track, int side, int rev) {
		if (_clockChartLayout == null) return;

		if (track == -1) track = _track;
		else _track = track;
		if (side == -1) side = _side;
		else _side = side;
		if (rev == -1) rev = _rev;
		else _rev = rev;
		if (Global.Disk[track, side].packedFluxData == null) return;

		int fluxXmin = 0;
		int fluxXmax = 200;

		_minBitWidth = Global.Disk[track, side].packedFluxData.minBitWidth;
		_maxBitWidth = Global.Disk[track, side].packedFluxData.maxBitWidth;
		if (_minBitWidth == _maxBitWidth) {
			_minBitWidth -= _minBitWidth / 100;
			_maxBitWidth += _maxBitWidth / 100;
		}	// do not want infinity scale
		_clockChartLayout.setViewport(fluxXmin, fluxXmax, _minBitWidth, _maxBitWidth);
	}

}
}

/*!
@file ChartLayout.cs
<summary>Information and methods related to the Charts Layout</summary>

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
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ChartPackage {
/// <summary>
/// Contains all layout information necessary for drawing the different charts ...
/// </summary>
public class ChartLayout {

	/// <summary>
	/// Enumerate the different grid line possible
	/// </summary>
	public enum GridlinePatternEnum {
		Solid = 1,
		Dash = 2,
		Dot = 3,
		DashDot = 4
	}

	private Grid _chartGrid = null;
	private Canvas _graphicCanvas = null;
	private Canvas _textCanvas = null;

	// default Canvas real world values
	private double _xminCanvas;
	private double _xmaxCanvas;
	private double _yminCanvas;
	private double _ymaxCanvas;

	// current viewport in canvas - real world values
	private double _xmin;
	private double _xmax;
	private double _ymin;
	private double _ymax;
				
	private bool _hasXGrid = true;
	private bool _hasYGrid = true;
	private Brush _gridlineColor = Brushes.LightGray;
	private GridlinePatternEnum _gridlinePattern = ChartLayout.GridlinePatternEnum.Solid;
	private double _optimalXSpacing = 50;
	private double _optimalYSpacing = 30;

	#region properties
	/// <summary>
	/// When set to true the X left and right offset are fixed so we can align several charts
	/// </summary>
	public bool FixOffset { get; set; }

	/// <summary>
	/// Access the Graphic canvas of the chart
	/// </summary>
	public Canvas GraphicCanvas {
		get { return _graphicCanvas; }
		set { _graphicCanvas = value; }
	}

	/// <summary>
	/// Access the Text canvas of the chart
	/// </summary>
	public Canvas TextCanvas {
		get { return _textCanvas; }
		set { _textCanvas = value; }
	}

	/// <summary>
	/// Access optimum X spacing value. By default set to 50
	/// </summary>
	public double OptimalXSpacing {
		get { return _optimalXSpacing; }
		set { _optimalXSpacing = value; }
	}

	/// <summary>
	/// Access optimum Y spacing value. By default set to 30
	/// </summary>
	public double OptimalYSpacing {
		get { return _optimalYSpacing; }
		set { _optimalYSpacing = value; }
	}

	/// <summary>
	/// Access current viewport Xmin value
	/// </summary>
	public double Xmin {
		get { return _xmin; }
		set { _xmin = value; }
	}

	/// <summary>
	/// Access current viewport Xmax value
	/// </summary>
	public double Xmax {
		get { return _xmax; }
		set { _xmax = value; }
	}

	/// <summary>
	/// Access current viewport Ymin value
	/// </summary>
	public double Ymin {
		get { return _ymin; }
		set { _ymin = value; }
	}

	/// <summary>
	/// Access current viewport Ymax value
	/// </summary>
	public double Ymax {
		get { return _ymax; }
		set { _ymax = value; }
	}

	/// <summary>
	/// Access flag to turn on/off the X grid
	/// </summary>
	public bool HasXGrid {
		get { return _hasXGrid; }
		set { _hasXGrid = value; }
	}

	/// <summary>
	/// Access flag to turn on/off the Y grid
	/// </summary>
	public bool HasYGrid {
		get { return _hasYGrid; }
		set { _hasYGrid = value; }
	}

	/// <summary>
	/// Access the pattern used to draw the Grid Lines
	/// </summary>
	public GridlinePatternEnum GridlinePattern {
		get { return _gridlinePattern; }
		set { _gridlinePattern = value; }
	}

	/// <summary>
	/// Access the brush used to draw the grid lines
	/// </summary>
	public Brush GridlineColor {
		get { return _gridlineColor; }
		set { _gridlineColor = value; }
	}

	#endregion properties


	/// <summary>Create a ChartLayout object and initialize the values</summary>
	/// <param name="grid">The Grid that contains the two canvases</param>
	/// <param name="text">The text canvas</param>
	/// <param name="graphics">The graphic canvas</param>
	public ChartLayout(Grid grid, Canvas text, Canvas graphics) {
		_chartGrid = grid;
		_graphicCanvas = graphics;
		_textCanvas = text;
		FixOffset = false;
	}

	/// <summary>
	/// Set the values of the default viewport in world coordinate
	/// </summary>
	/// <param name="xmin">Minimum value of the X coordinate</param>
	/// <param name="xmax">Maximum value of the X coordinate</param>
	/// <param name="ymin">Minimum value of the Y coordinate</param>
	/// <param name="ymax">Maximum value of the Y coordinate</param>
	public void setDefaultViewport(double xmin, double xmax, double ymin, double ymax) {
		_xminCanvas = xmin;
		_xmaxCanvas = xmax;
		_yminCanvas = ymin;
		_ymaxCanvas = ymax;
		resetViewport();
	}

	/// <summary>
	/// Sets the values for the current viewport in world coordinate. This allow to zoom pan in the default viewport.
	/// </summary>
	/// <param name="xmin">Minimum value of the X coordinate</param>
	/// <param name="xmax">Maximum value of the X coordinate</param>
	/// <param name="ymin">Minimum value of the Y coordinate</param>
	/// <param name="ymax">Maximum value of the Y coordinate</param>
	public void setViewport(double xmin, double xmax, double ymin, double ymax) {
		_xmin = xmin;
		_xmax = xmax;
		_ymin = ymin;
		_ymax = ymax;
	}

	/// <summary>
	/// Resets the viewport: i.e. set the current view port to the default
	/// </summary>
	public void resetViewport() {
		_xmin = _xminCanvas;
		_xmax = _xmaxCanvas;
		_ymin = _yminCanvas;
		_ymax = _ymaxCanvas;
	}

	/// <summary>
	/// Returns the current zoom factor as a value
	/// </summary>
	/// <returns>The zoom factor value</returns>
	/// <remarks>Computes the zoom factor on both X and Y directions. Return the biggest 
	/// value</remarks>
	public double zoom() {
		double zoomX = (_xmaxCanvas - _xminCanvas) / (_xmax - _xmin);
		double zoomY = (_ymaxCanvas - _yminCanvas) / (_ymax - _ymin);
		return Math.Max(zoomX, zoomY);
	}

	/// <summary>
	/// Returns the zoom factor on X axis
	/// </summary>
	/// <returns>The X zoom factor</returns>
	public double zoomX() {
		return (_xmaxCanvas - _xminCanvas) / (_xmax - _xmin);
	}

	/// <summary>
	/// Returns the zoom factor on Y axis
	/// </summary>
	/// <returns>The Y zoom factor</returns>
	public double zoomY() {
		return (_ymaxCanvas - _yminCanvas) / (_ymax - _ymin);
	}

	/// <summary>
	/// Indicates if we are currently zoomed.
	/// </summary>
	/// <returns>true if zoomed or false otherwise</returns>
	public bool zoomed() {
		return ( (((_xmaxCanvas - _xminCanvas) / (_xmax - _xmin)) > 1) ||
				 (((_ymaxCanvas - _yminCanvas) / (_ymax - _ymin)) > 1) );
	}

	/// <summary>
	/// Indicates if the point passed by x,y values is inside the current viewport.
	/// </summary>
	/// <param name="x">The x coordinate of the point to test</param>
	/// <param name="y">The y coordinate of the point to test</param>
	/// <returns>true if the point is inside the current viewport, false otherwise</returns>
	public bool inViewport(double x, double y) {
		return ( (x > _xmin) && (x < _xmax) && (y > _ymin) && (y < _ymax) );
	}

	/// <summary>
	/// Indicates if the point passed by x,y values is inside the extended viewport.
	/// </summary>
	/// <param name="x">The x coordinate of the point to test</param>
	/// <param name="y">The y coordinate of the point to test</param>
	/// <returns></returns>
	/// <remarks>The extended viewport is defined as the current viewport doubled in 
	/// each directions (left, right, up, down) ...
	/// </remarks>
	public bool inExtendedViewport(double x, double y) {
		double xmin = _xmin - (_xmax - _xmin);
		double xmax = _xmax + (_xmax - _xmin);
		double ymin = _ymin - (_ymax - _ymin);
		double ymax = _ymax + (_ymax - _ymin);
		return ((x > xmin) && (x < xmax) && (y > ymin) && (y < ymax));
	}


	/// <summary>
	/// Normalize a Point into the canvas custom coordinate system
	/// </summary>
	/// <param name="pt">Point to normalize</param>
	/// <returns>Point in canvas coordinate</returns>
	public Point NormPoint(Point pt) {
		Point result = new Point();
		result.X = (pt.X - _xmin) * _graphicCanvas.Width / (_xmax - _xmin);
		result.Y = _graphicCanvas.Height - ((pt.Y - _ymin) * _graphicCanvas.Height / (_ymax - _ymin));
		return result;
	}

	/// <summary>
	/// Inverse of NormPoint. Return Word coordinate from canvas custom coordinates
	/// </summary>
	/// <param name="pt">Point in Canvas coordinate</param>
	/// <returns>Point in Word coordinate</returns>
	public Point WorldPoint(Point pt) {
		Point result = new Point();
		result.X = pt.X * (_xmax - _xmin) / _graphicCanvas.Width + _xmin;
		result.Y = (_ymax - _ymin) * (_graphicCanvas.Height - pt.Y) / _graphicCanvas.Height + _ymin;
		return result;
	}

	/// <summary>
	/// Create a normalized point from X Y world coordinate to Canvas
	/// </summary>
	/// <param name="x">X World coordinate</param>
	/// <param name="y">Y world coordinate</param>
	/// <returns>Point in canvas coordinate</returns>
	public Point NormPoint(double x, double y) {
		Point result = new Point();
		result.X = (x - _xmin) * _graphicCanvas.Width / (_xmax - _xmin);
		result.Y = _graphicCanvas.Height - ((y - _ymin) * _graphicCanvas.Height / (_ymax - _ymin));
		return result;
	}

	/// <summary>
	/// Normalize X value from world to Canvas
	/// </summary>
	/// <param name="x">X value</param>
	/// <returns>Normalized X value</returns>
	public double NormX(double x) {
		return (x - _xmin) * _graphicCanvas.Width / (_xmax - _xmin);
	}

	/// <summary>
	/// Normalize Y value from world to Canvas
	/// </summary>
	/// <param name="y">Y value</param>
	/// <returns>Normalized Y value</returns>
	public double NormY(double y) {
		return _graphicCanvas.Height - ((y - _ymin) * _graphicCanvas.Height / (_ymax - _ymin));
	}


	/// <summary>
	/// Draw the chart grid, x and y axis with optimized tick marks and labels
	/// </summary>
	public void drawChartGrids() {
		double leftOffset = 20;
		double bottomOffset = 20;
		double rightOffset = 10;

		Line gridline;
		Point pt = new Point();
		Line tick = new Line();
		double offset = 0;
		double dx, dy;
		TextBlock tb = new TextBlock();

		//  determine right offset:
		tb.Text = Math.Round(Xmax, 0).ToString();
		tb.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
		Size size = tb.DesiredSize;
		rightOffset = size.Width / 2 + 5;
		if (FixOffset) rightOffset = 15;

		// Determine left offset:
		double xScale = 0.0, yScale = 0.0;
		double xSpacing = 0.0, ySpacing = 0.0;
		double xTick = 0.0, yTick = 0.0;
		int xStart = 0, xEnd = 1;
		int yStart = 0, yEnd = 1;
		double offset0 = 30;

		if (HasYGrid == true) {
			while (Math.Abs(offset - offset0) > 1) {
				if (Xmin != Xmax)
					xScale = (double)(_textCanvas.Width - offset0 - rightOffset - 5) / (double)(Xmax - Xmin);
				if (Ymin != Ymax)
					yScale = (double)_textCanvas.Height / (double)(Ymax - Ymin);
				xSpacing = _optimalXSpacing / xScale;
				xTick = OptimalSpacing(xSpacing);
				ySpacing = _optimalYSpacing / yScale;
				yTick = OptimalSpacing(ySpacing);
				xStart = (int)Math.Ceiling(Xmin / xTick);
				xEnd = (int)Math.Floor(Xmax / xTick);
				yStart = (int)Math.Ceiling(Ymin / yTick);
				yEnd = (int)Math.Floor(Ymax / yTick);

				for (int i = yStart; i <= yEnd; i++) {
					dy = i * yTick;
					pt = NormPoint(Xmin, dy);
					tb = new TextBlock();
					tb.Text = dy.ToString();
					tb.TextAlignment = TextAlignment.Right;
					tb.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
					size = tb.DesiredSize;
					if (offset < size.Width)
						offset = size.Width;
				}
				if (offset0 > offset)
					offset0 -= 0.5;
				else if (offset0 < offset)
					offset0 += 0.5;
			}
		}
		leftOffset = offset + 5;
		if (FixOffset) leftOffset = 30;

		Canvas.SetLeft(_graphicCanvas, leftOffset);
		Canvas.SetBottom(GraphicCanvas, bottomOffset);
		GraphicCanvas.Width = Math.Abs(_textCanvas.Width - leftOffset - rightOffset);
		GraphicCanvas.Height = Math.Abs(_textCanvas.Height - bottomOffset - size.Height / 2);
		Rectangle chartRect = new Rectangle();
		chartRect.Stroke = Brushes.Black;
		chartRect.Width = GraphicCanvas.Width;
		chartRect.Height = GraphicCanvas.Height;
		GraphicCanvas.Children.Add(chartRect);

		if (Xmin != Xmax)
			xScale = (double)GraphicCanvas.Width / (double)(Xmax - Xmin);
		if (Ymin != Ymax)
			yScale = (double)GraphicCanvas.Height / (double)(Ymax - Ymin);
		xSpacing = _optimalXSpacing / xScale;
		xTick = OptimalSpacing(xSpacing);
		ySpacing = _optimalYSpacing / yScale;
		yTick = OptimalSpacing(ySpacing);
		xStart = (int)Math.Ceiling(Xmin / xTick);
		xEnd = (int)Math.Floor(Xmax / xTick);
		yStart = (int)Math.Ceiling(Ymin / yTick);
		yEnd = (int)Math.Floor(Ymax / yTick);

		// Create vertical grid-lines and x tick marks:
		if (HasXGrid == true) {
			for (int i = xStart; i <= xEnd; i++) {
				gridline = new Line();
				//Canvas.SetZIndex(gridline, 99);
				setLinePattern(gridline);
				dx = i * xTick;
				gridline.X1 = NormPoint(new Point(dx, Ymin)).X;
				gridline.Y1 = NormPoint(new Point(dx, Ymin)).Y;
				gridline.X2 = NormPoint(new Point(dx, Ymax)).X;
				gridline.Y2 = NormPoint(new Point(dx, Ymax)).Y;
				GraphicCanvas.Children.Add(gridline);

				pt = NormPoint(new Point(dx, Ymin));
				tick = new Line();
				Canvas.SetZIndex(tick, 99);
				tick.Stroke = Brushes.Black;
				tick.X1 = pt.X;
				tick.Y1 = pt.Y;
				tick.X2 = pt.X;
				tick.Y2 = pt.Y - 5;
				GraphicCanvas.Children.Add(tick);

				tb = new TextBlock();
				tb.Text = dx.ToString();
				tb.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
				size = tb.DesiredSize;
				_textCanvas.Children.Add(tb);
				Canvas.SetLeft(tb, leftOffset + pt.X - size.Width / 2);
				Canvas.SetTop(tb, pt.Y + 2 + size.Height / 2);
			}
		}

		// Create horizontal grid-lines and y tick marks:
		if (HasYGrid == true) {
			for (int i = yStart; i <= yEnd; i++) {
				gridline = new Line();
				//Canvas.SetZIndex(gridline, 99);
				setLinePattern(gridline);
				dy = i * yTick;
				gridline.X1 = NormPoint(new Point(Xmin, dy)).X;
				gridline.Y1 = NormPoint(new Point(Xmin, dy)).Y;
				gridline.X2 = NormPoint(new Point(Xmax, dy)).X;
				gridline.Y2 = NormPoint(new Point(Xmax, dy)).Y;
				GraphicCanvas.Children.Add(gridline);

				pt = NormPoint(new Point(Xmin, dy));
				tick = new Line();
				Canvas.SetZIndex(tick, 99);
				tick.Stroke = Brushes.Black;
				tick.X1 = pt.X;
				tick.Y1 = pt.Y;
				tick.X2 = pt.X + 5;
				tick.Y2 = pt.Y;
				GraphicCanvas.Children.Add(tick);

				tb = new TextBlock();
				tb.Text = dy.ToString();
				tb.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
				size = tb.DesiredSize;
				_textCanvas.Children.Add(tb);
				// Canvas.SetRight(tb, ChartCanvas.Width + 10);
				Canvas.SetRight(tb, GraphicCanvas.Width + rightOffset + 5);	// JLG
				Canvas.SetTop(tb, pt.Y);
			}
		}
	}


	private void setLinePattern(Line gridline) {
		gridline.Stroke = GridlineColor;
		gridline.StrokeThickness = 1;

		switch (GridlinePattern) {
			case GridlinePatternEnum.Dash:
				gridline.StrokeDashArray = new DoubleCollection(new double[2] { 4, 3 });
				break;
			case GridlinePatternEnum.Dot:
				gridline.StrokeDashArray = new DoubleCollection(new double[2] { 1, 2 });
				break;
			case GridlinePatternEnum.DashDot:
				gridline.StrokeDashArray = new DoubleCollection(new double[4] { 4, 2, 1, 2 });
				break;
		}
	}


	private double OptimalSpacing(double original) {
		double[] da = { 1.0, 2.0, 5.0 };
		double multiplier = Math.Pow(10, Math.Floor(Math.Log(original) / Math.Log(10)));
		double dmin = 100 * multiplier;
		double spacing = 0.0;
		double mn = 100;

		foreach (double d in da) {
			double delta = Math.Abs(original - d * multiplier);
			if (delta < dmin) {
				dmin = delta;
				spacing = d * multiplier;
			}
			if (d < mn) {
				mn = d;
			}
		}
		if (Math.Abs(original - 10 * mn * multiplier) < Math.Abs(original - spacing))
			spacing = 10 * mn * multiplier;
		return spacing;
	}


	/// <summary>
	/// We reset the size of both canvases based on Grid values and we removed all the children
	/// </summary>
	public void chartResetCanvas() {
		GraphicCanvas.Width = TextCanvas.Width = _chartGrid.ActualWidth;
		GraphicCanvas.Height = TextCanvas.Height = _chartGrid.ActualHeight;
		GraphicCanvas.Children.Clear();
		TextCanvas.Children.RemoveRange(1, TextCanvas.Children.Count - 1);
	}

}
}

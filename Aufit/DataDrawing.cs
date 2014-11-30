/*!
@file DataDrawing.cs
<summary>Everything related to drawing the Track Data chart</summary>

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
using System.Windows.Shapes;

using KFReaderPackage;
using WD1772Package;
using GlobalPackage;

namespace ChartPackage {
/// <summary>
/// This class contains all functions required to draw the track data chart
/// </summary>
class DataDrawing {

	private ChartLayout _dataChartLayout = null;
	private int _track, _side, _rev;

	/// <summary>
	/// Get the underlying ChartLayout
	/// </summary>
	public ChartLayout Chart {
		get { return _dataChartLayout; }
	}

	/// <summary>
	/// Creates a DataDrawing object and sets default values.
	/// </summary>
	/// <param name="grid">The name of the Grid that contains the two Canvases</param>
	/// <param name="text">The name of the Text Canvas</param>
	/// <param name="graphic">The name of the Graphic Canvas</param>
	public DataDrawing(Grid grid, Canvas text, Canvas graphic) {
		_dataChartLayout = new ChartLayout(grid, text, graphic);
	}

	/// <summary>
	/// draw the data chart
	/// </summary>
	public void drawTrackData(int track = -1, int side = -1, int rev = -1) {

		if (track == -1) track = _track;
		else _track = track;
		if (side == -1) side = _side;
		else _side = side;
		if (rev == -1) rev = _rev;
		else _rev = rev;

		if (Global.Disk[track, side].packedFluxData == null) return;
		_dataChartLayout.chartResetCanvas();
		_dataChartLayout.drawChartGrids();

		FDC.fdcRealAll(track, side, rev);
		List<Sector> sectors = Global.Disk[track, side].sectors;

		foreach (Sector sector in sectors) {

			Polygon polySect = new Polygon();
			polySect.Stroke = Brushes.DarkBlue;
			polySect.StrokeThickness = 0.5;
			polySect.Fill = Brushes.Transparent;

			StackPanel panel = new StackPanel();
			TextBox snum = new TextBox();
			int endTime;
			if (sector.data != null)
				endTime = sector.data.info.startTime + sector.data.info.timeLength;
			else
				endTime = sector.id.info.startTime + sector.id.info.timeLength;

			snum.Text = String.Format("Sector {0} Start {1:F2} ms - Stop: {2:F2} ms",
				sector.id.number, (double)sector.id.info.startTime / 1000000.0, (double)endTime / 1000000.0);
				
			panel.Children.Add(snum);

			double bstart = 0;
			double bstop = 0;
			double blength;
			
			// draw ID field
			Polygon idPlg = new Polygon();
			idPlg.Stroke = Brushes.Black;
			idPlg.StrokeThickness = 0.5;

			bstart = (double)sector.id.info.startTime / 1000000.0;
			blength = (double)sector.id.info.timeLength / 1000000.0;
			bstop = bstart + blength;

			polySect.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
			polySect.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));

			TextBox tb = new TextBox();
			tb.Text = String.Format("ID Field (T={0} H={1} N={2} S={3} CRC={4:X4}) Bit={5} Time={6:F2} ms",
				sector.id.track, sector.id.head, sector.id.number, sector.id.size, sector.id.crc,
				sector.id.info.startBit, bstart);
			panel.Children.Add(tb);

			idPlg.Fill = Brushes.Maroon;
			idPlg.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
			idPlg.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));
			idPlg.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
			idPlg.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));
			_dataChartLayout.GraphicCanvas.Children.Add(idPlg);

			// draw DATA field
			if (sector.data != null) {
				Polygon dataPlg = new Polygon();
				dataPlg.Stroke = Brushes.Black;
				dataPlg.StrokeThickness = 0.5;

				bstart = (double)sector.data.info.startTime / 1000000.0;
				blength = (double)sector.data.info.timeLength / 1000000.0;
				bstop = bstart + blength;

				tb = new TextBox();
				tb.Text = String.Format("Data Field {0} bytes Bit={1} Time={2:F2} ms Length={3:F2} {4} {5}",
					sector.data.info.byteCount - 3, sector.data.info.startBit, bstart, blength,
					(sector.data.info.crc != 0) ? " Bad CRC" : "",
					(sector.data.fuzzy != null) ? " has fuzzy bytes" : "");
				panel.Children.Add(tb);

				dataPlg.Fill = Brushes.DeepSkyBlue;
				//if (si.phases[p].overClock > Global.sectSize(si.addr.size) / 2)
				//	plg.Fill = Brushes.Blue;
				//if (si.phases[p].underClock > Global.sectSize(si.addr.size) / 2)
				//	plg.Fill = Brushes.Aqua;
				//// macrodos
				//if ((si.phases[p].underClock > Global.sectSize(si.addr.size) / 8) && (si.phases[p].overClock > Global.sectSize(si.addr.size) / 8))
				//	plg.Fill = Brushes.BlueViolet;


				if (sector.data.info.crc != 0) {
					Polygon perr = new Polygon();
					perr.Fill = Brushes.Red;
					perr.StrokeThickness = 0;
					perr.Points.Add(_dataChartLayout.NormPoint(bstart, 0.05));
					perr.Points.Add(_dataChartLayout.NormPoint(bstart, 0.95));
					perr.Points.Add(_dataChartLayout.NormPoint(bstop, 0.95));
					perr.Points.Add(_dataChartLayout.NormPoint(bstop, 0.05));
					_dataChartLayout.GraphicCanvas.Children.Add(perr);
				}
				if (sector.data.fuzzy != null) {
					Polygon perr = new Polygon();
					perr.Fill = Brushes.Orange;
					perr.StrokeThickness = 0;
					perr.Points.Add(_dataChartLayout.NormPoint(bstart, 0.10));
					perr.Points.Add(_dataChartLayout.NormPoint(bstart, 0.9));
					perr.Points.Add(_dataChartLayout.NormPoint(bstop, 0.9));
					perr.Points.Add(_dataChartLayout.NormPoint(bstop, 0.10));
					_dataChartLayout.GraphicCanvas.Children.Add(perr);

					//	int nbFuzzy = 0;
					//	for (int j = 0; j < sc.fuzzyMask.Bytes.Count; j++) {
					//		if (sc.fuzzyMask.Bytes[j].dataByte != 0) nbFuzzy++;
					//	}
					//	if (nbFuzzy > (int)((double)sc.fuzzyMask.Bytes.Count * 0.6)) plg.Fill = Brushes.Gray;
				}

				dataPlg.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
				dataPlg.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));
				dataPlg.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
				dataPlg.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));
				_dataChartLayout.GraphicCanvas.Children.Add(dataPlg);
			}	// sector has data field

			polySect.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
			polySect.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));
			ToolTipService.SetToolTip(polySect, panel);
			ToolTipService.SetShowDuration(polySect, 100000);
			_dataChartLayout.GraphicCanvas.Children.Add(polySect);

		}	// all sectors
	}
		
}
}



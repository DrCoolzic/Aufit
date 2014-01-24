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

		if (Global.Disk[track, side].fluxData == null) return;
		_dataChartLayout.chartResetCanvas();
		_dataChartLayout.drawChartGrids();

		FDC.fdcAllRead(track, side, rev);
		TrackContent tc = Global.Disk[track, side].trackContent;

		for (int i = 0; i < tc.sectors.Count; i++) {
			SectorInfo si = tc.sectors[i];
			IDField id = new IDField();
			SectorContent sc = null;
			if (i > 0) {
				id = Global.Disk[track, side].addrContent.ids[i-1];
				sc = FDC.findSectorContent(track, side, id.number, si.startTime);
			}
			Polygon polySect = new Polygon();
			polySect.Stroke = Brushes.DarkBlue;
			polySect.StrokeThickness = 1.5;
			polySect.Fill = Brushes.Transparent;
			StackPanel panel = new StackPanel();
			TextBox snum = new TextBox();
			if ((i > 0) && (sc != null))
				snum.Text = String.Format("Sector {0} Start - Stop: {1:F2} - {2:F2} ms - {3} {4}",
					id.number, (double)si.startTime / 1000000.0, (double)si.endTime / 1000000.0,
					(sc.dataBlock.crc == 0) ? "CRC OK" : "Bad CRC", (sc.fuzzyMask != null) ? "- Has fuzzy bytes" : "");
			panel.Children.Add(snum);
			bool open = false;
			double bstart = 0;
			double bstop = 0;
			double blength;
			for (byte p = 0; p < 4; p++) {
				if (si.phases[p].byteCount > 0) {
					Polygon plg = new Polygon();
					plg.Stroke = Brushes.Black;
					plg.StrokeThickness = 0.5;
					bstart = (double)si.phases[p].startTime / 1000000.0;
					bstop = (double)(si.phases[p].startTime + si.phases[p].timeLength) / 1000000.0;
					blength = (double)si.phases[p].timeLength / 1000.0;
					TextBox tb = new TextBox();
					switch (p) {
						case (byte)Phases.p_id:
							polySect.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
							polySect.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));
							tb.Text = String.Format("ID Field [T={0} H={1} N={2} S={3}] @ {4} ({5:F2} ms) bit {6}",
 								si.addr.track, si.addr.head, si.addr.number, si.addr.size,
								si.phases[p].startByte, bstart, si.phases[p].startBit);
							panel.Children.Add(tb);
							plg.Fill = Brushes.Yellow;
							//if (sector.phases[p].crc != 0) plg.Opacity = 0.7;
							//if (sector.phases[p].crc != 0) plg.Fill = Brushes.Orange;
							open = true;
							break;

						case (byte)Phases.p_inner:
							tb.Text = String.Format("Intra GAP {0} bytes ({1:F2} µs) @ byte {2} ({3:F2} ms) bit {4} {5}",
								si.phases[p].byteCount, blength, si.phases[p].startByte, bstart, si.phases[p].startBit,
									(si.phases[p].invDataGap > 0) ? "contains invalid data in gap" : "");
							panel.Children.Add(tb);
							plg.Fill = Brushes.LightGreen;
							break;

						case (byte)Phases.p_data:
							tb.Text = String.Format("Data Field {0} bytes ({1:F2} µs) @ byte {2} ({3:F2} ms) bit {4}{5}{6}",
								si.phases[p].byteCount, blength, si.phases[p].startByte, bstart, si.phases[p].startBit,
								(sc == null) ? "" : (sc.dataBlock.crc != 0) ? " Bad CRC" : "", 
								(sc == null) ? "" : ((sc.fuzzyMask == null) ? "" : " has fuzzy bytes"));
							panel.Children.Add(tb);
							plg.Fill = Brushes.DeepSkyBlue;
							if (si.phases[p].overClock > Global.sectSize(si.addr.size) / 2)
								plg.Fill = Brushes.Blue;
							if (si.phases[p].underClock > Global.sectSize(si.addr.size) / 2)
								plg.Fill = Brushes.Aqua;
							// macrodos
							if ((si.phases[p].underClock > Global.sectSize(si.addr.size) / 8) && (si.phases[p].overClock > Global.sectSize(si.addr.size) / 8))
								plg.Fill = Brushes.BlueViolet;

							if ((i > 0) && (sc != null)) {

								if (sc.dataBlock.crc != 0) {
									Polygon perr = new Polygon();
									perr.Fill = Brushes.Red;
									perr.StrokeThickness = 0;
									perr.Points.Add(_dataChartLayout.NormPoint(si.phases[p].startTime / 1000000.0, 0.05));
									perr.Points.Add(_dataChartLayout.NormPoint(si.phases[p].startTime / 1000000.0, 0.95));
									perr.Points.Add(_dataChartLayout.NormPoint((si.phases[p].startTime + si.phases[p].timeLength) / 1000000.0, 0.95));
									perr.Points.Add(_dataChartLayout.NormPoint((si.phases[p].startTime + si.phases[p].timeLength) / 1000000.0, 0.05));
									_dataChartLayout.GraphicCanvas.Children.Add(perr);
								}
								if (sc.fuzzyMask != null) {
									Polygon perr = new Polygon();
									perr.Fill = Brushes.Orange;
									perr.StrokeThickness = 0;
									perr.Points.Add(_dataChartLayout.NormPoint(si.phases[p].startTime / 1000000.0, 0.1));
									perr.Points.Add(_dataChartLayout.NormPoint(si.phases[p].startTime / 1000000.0, 0.9));
									perr.Points.Add(_dataChartLayout.NormPoint((si.phases[p].startTime + si.phases[p].timeLength) / 1000000.0, 0.9));
									perr.Points.Add(_dataChartLayout.NormPoint((si.phases[p].startTime + si.phases[p].timeLength) / 1000000.0, 0.1));
									_dataChartLayout.GraphicCanvas.Children.Add(perr);

									int nbFuzzy = 0;
									for (int j = 0; j < sc.fuzzyMask.Bytes.Count; j++) {
										if (sc.fuzzyMask.Bytes[j].dataByte != 0) nbFuzzy++;
									}
									if (nbFuzzy > (int)((double)sc.fuzzyMask.Bytes.Count * 0.6)) plg.Fill = Brushes.Gray;
								}
							}
							//if (sector.phases[p].crc != 0) plg.Fill = Brushes.Plum;
							break;

						case (byte)Phases.p_outer:
							tb.Text = String.Format("Inter GAP {0} bytes ({1:F2} µs) @ byte {2} ({3:F2} ms) bit {4} {5}", 
								si.phases[p].byteCount, blength, si.phases[p].startByte, bstart, si.phases[p].startBit,
								(si.phases[p].invDataGap > 0) ? "contains invalid data in gap" : "");
							panel.Children.Add(tb);
							plg.Fill = Brushes.LimeGreen;
							if (open) {
								polySect.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
								polySect.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));
							}
							open = false;
							break;

						default:
							plg.Fill = Brushes.Red;
							break;
					}
					plg.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
					plg.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));
					plg.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
					plg.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));
					_dataChartLayout.GraphicCanvas.Children.Add(plg);
				}	// non null phase
			}	// all phases
			if (open) {
				polySect.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
				polySect.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));
				open = false;
			}

			ToolTipService.SetToolTip(polySect, panel);
			ToolTipService.SetShowDuration(polySect, 100000);
			_dataChartLayout.GraphicCanvas.Children.Add(polySect);
		}	// all sectors
	}
		
}
}



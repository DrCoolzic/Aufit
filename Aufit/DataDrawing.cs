/*!
@file DataDrawing.cs
<summary>Everything related to drawing the Track Data chart</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tools project.\n\n
The Atari Universal FD Image Tools project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tools project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.\n\n
The Atari Universal FD Image Tools project is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.\n
See the GNU General Public License for more details.\n\n
You should have received a copy of the GNU General Public License
along with the Atari Universal FD Image Tools project; if not, write to the Free Software
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

using StreamNS;
using WD1772NS;

namespace ChartNS {
/// <summary>
/// This class contains all functions required to draw the track data chart
/// </summary>
class DataDrawing {

	private ChartLayout _dataChartLayout = null;

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
	public void drawTrackData(FDC trackInfo) {
		if (ProcessStream.currentTrack().flux == null) return;
		_dataChartLayout.chartResetCanvas();
		_dataChartLayout.drawChartGrids();

		ReadTrackInfo track = trackInfo.readTrack();

		if (track.sectors.Count > 1)
			foreach (SectorInfo sector in track.sectors) {
				Polygon polySect = new Polygon();
				polySect.Stroke = Brushes.DarkBlue;
				polySect.StrokeThickness = 1.5;
				polySect.Fill = Brushes.Transparent;
				StackPanel panel = new StackPanel();
				TextBox snum = new TextBox();
				snum.Text = String.Format("Sector {0} Start-Stop: {1:F2}-{2:F2} ms", sector.addr.sector, (double)sector.startTime/1000000.0, (double)sector.endTime/1000000.0);
				panel.Children.Add(snum);
				bool open = false;
				double bstart = 0;
				double bstop = 0;
				double blength;
				for (byte p = 0; p < 4; p++) {
					if (sector.phases[p].byteCount > 0) {
						Polygon plg = new Polygon();
						plg.Stroke = Brushes.Black;
						plg.StrokeThickness = 0.5;
						bstart = (double)sector.phases[p].startTime / 1000000.0;
						bstop = (double)(sector.phases[p].startTime + sector.phases[p].timeLength) / 1000000.0;
						blength = (double)sector.phases[p].timeLength / 1000.0;
								TextBox tb = new TextBox();
						switch (p) {
							case (byte)Phases.p_id:
								polySect.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
								polySect.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));
								tb.Text = String.Format("ID Field {0} bytes ({1:F2} µs) @ byte {2} ({3:F2} ms) bit {4}", sector.phases[p].byteCount, blength, sector.phases[p].startByte, bstart, sector.phases[p].startBit);
								panel.Children.Add(tb);
								plg.Fill = Brushes.Yellow;
								//if (sector.phases[p].crc != 0) plg.Opacity = 0.7;
								if (sector.phases[p].crc != 0) plg.Fill = Brushes.Orange;
								open = true;
								break;
							case (byte)Phases.p_inner:
								tb.Text = String.Format("Intra GAP {0} bytes ({1:F2} µs) @ byte {2} ({3:F2} ms) bit {4}", sector.phases[p].byteCount, blength, sector.phases[p].startByte, bstart, sector.phases[p].startBit);
								panel.Children.Add(tb);
								plg.Fill = Brushes.LightGreen;
								break;
							case (byte)Phases.p_data:
								tb.Text = String.Format("Data Field {0} bytes ({1:F2} µs) @ byte {2} ({3:F2} ms) bit {4}", sector.phases[p].byteCount, blength, sector.phases[p].startByte, bstart, sector.phases[p].startBit);
								panel.Children.Add(tb);
								plg.Fill = Brushes.DeepSkyBlue;
								//if (sector.phases[p].crc != 0) plg.Opacity = 0.7;
								if (sector.phases[p].crc != 0) plg.Fill = Brushes.Plum;
								break;
							case (byte)Phases.p_outer:
								tb.Text = String.Format("Inter GAP {0} bytes ({1:F2} µs) @ byte {2} ({3:F2} ms) bit {4}", sector.phases[p].byteCount, blength, sector.phases[p].startByte, bstart, sector.phases[p].startBit);
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



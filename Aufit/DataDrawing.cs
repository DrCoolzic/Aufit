/*!
@file DataDrawing.cs
<summary>Everything related to drawing the Track Data chart</summary>

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

		private enum SecType { id, gap, data, data2 }

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

			FDC.processTrack(track, side, rev);
			List<Sector> sectors = Global.Disk[track, side].sectors;
			double revolutionTime = (double)Global.Disk[track, side].fluxDataRev[rev].revolutionTime / 1000000.0;

			for (int i = 0; i < sectors.Count; i++) {
				Sector sector = sectors[i];

				Polygon sectorBorderPolygon = new Polygon();
				sectorBorderPolygon.Stroke = Brushes.DarkBlue;
				sectorBorderPolygon.StrokeThickness = 0.5;
				sectorBorderPolygon.Fill = Brushes.Transparent;

				StackPanel sectorInfoPanel = new StackPanel();
				TextBox snum = new TextBox();
				int endTime;
				int endByte;
				if (sector.data2 != null) {
					endTime = sector.data2.info.startTime + sector.data2.info.timeLength;
					endByte = sector.data2.info.startByte + sector.data2.buffer.DataByte.Count;
				}
				else if (sector.data != null) {
					endTime = sector.data.info.startTime + sector.data.info.timeLength;
					endByte = sector.data.info.startByte + sector.data.buffer.DataByte.Count;
				}
				else {
					endTime = sector.id.info.startTime + sector.id.info.timeLength;
					endByte = sector.id.info.startByte + sector.id.buffer.DataByte.Count;
				}

					snum.Text = String.Format("Sector {0} --- Start {1} {2:F2}ms - Stop: {3} {4:F2}ms",
					sector.id.Sector, sector.id.info.startByte, (double)sector.id.info.startTime / 1000000.0, endByte, (double)endTime / 1000000.0);
				sectorInfoPanel.Children.Add(snum);

				double bstart = 0;
				double bstop = 0;
				double blength;

				// draw ID field
				bstart = (double)sector.id.info.startTime / 1000000.0;
				blength = (double)sector.id.info.timeLength / 1000000.0;
				bstop = bstart + blength;
				double idbstop = bstop;

				sectorBorderPolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
				sectorBorderPolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));

				TextBox tb = new TextBox();
				tb.Text = String.Format("ID Field (T={0} H={1} S={2} L={3} CRC={4:X4}) - Start @ {5} {6:F2}ms",
					sector.id.Track, sector.id.Head, sector.id.Sector, sector.id.Length, sector.id.CRC,
					sector.id.info.startByte, bstart);
				sectorInfoPanel.Children.Add(tb);

				drawRectangle(sector, bstart, bstop, SecType.id);

				// draw DATA field
				if (sector.data != null) {
					bstart = (double)sector.data.info.startTime / 1000000.0;
					blength = (double)sector.data.info.timeLength / 1000000.0;
					bstop = bstart + blength;

					tb = new TextBox();
					tb.Text = String.Format("IGap Field {0} bytes - Start @ {1} {2:F2}ms",
						sector.innerGap.buffer.DataByte.Count, sector.innerGap.info.startByte, (double)sector.innerGap.info.startTime / 1000000.0);
					sectorInfoPanel.Children.Add(tb);
					//drawRectangle(sector, idbstop, bstart, Phases.p_gap_inner);
					drawRectangle(sector, (double)sector.innerGap.info.startTime / 1000000.0,
						(double)(sector.innerGap.info.startTime + sector.innerGap.info.timeLength) / 1000000.0, SecType.gap);

					tb = new TextBox();
					tb.Text = String.Format("Data Field {0} bytes Length={1:F2}µs - Start @ {2} {3:F2}ms {4} {5}",
						sector.data.buffer.DataByte.Count, blength * 1000, sector.data.info.startByte, bstart,
						(sector.data.info.crc != 0) ? " - Bad CRC" : "",
						(sector.data.fuzzy != null) ? " - Fuzzy sector" : "");
					sectorInfoPanel.Children.Add(tb);

					// if SWS we truncate
					if (((i + 1) < sectors.Count) &&
						(((double)sectors[i + 1].id.info.startTime / 1000000.0) < bstop))
						bstop = (double)sectors[i + 1].id.info.startTime / 1000000.0;

					drawRectangle(sector, bstart, bstop, SecType.data);

					// if shifted we draw an extra wrapped part
					if (bstop > revolutionTime) {
						double shiftedBstart = 0;
						double shiftedBstop = bstop - revolutionTime;
						// if SWS we truncate
						if (((double)sectors[0].id.info.startTime / 1000000.0) < shiftedBstop)
							shiftedBstop = (double)sectors[0].id.info.startTime / 1000000.0;

						drawRectangle(sector, shiftedBstart, shiftedBstop, SecType.data);
					}
				}   // sector has data field

				if (sector.postGap != null) {
					tb = new TextBox();
					tb.Text = String.Format("PGap Field {0} bytes - Start @ {1} {2:F2}ms",
						sector.postGap.buffer.DataByte.Count, sector.postGap.info.startByte, (double)sector.postGap.info.startTime / 1000000.0);
					sectorInfoPanel.Children.Add(tb);
				}

				// draw extra DATA field
				if (sector.data2 != null) {
					bstart = (double)sector.data2.info.startTime / 1000000.0;
					blength = (double)sector.data2.info.timeLength / 1000000.0;
					bstop = bstart + blength;

					tb = new TextBox();
					tb.Text = String.Format("Extra Data Field {0} bytes Length={1:F2}µs - Start @ {2} {3:F2}ms {4} {5}",
						sector.data2.buffer.DataByte.Count, blength * 1000, sector.data2.info.startByte, bstart,
						(sector.data2.info.crc != 0) ? " - Bad CRC" : "",
						(sector.data2.fuzzy != null) ? " - Fuzzy sector" : "");
					sectorInfoPanel.Children.Add(tb);

					// if SWS we truncate
					if (((i + 1) < sectors.Count) &&
						(((double)sectors[i + 1].id.info.startTime / 1000000.0) < bstop))
						bstop = (double)sectors[i + 1].id.info.startTime / 1000000.0;

					drawRectangle(sector, bstart, bstop, SecType.data2);

					// if shifted we draw an extra wrapped part
					if (bstop > revolutionTime) {
						double shiftedBstart = 0;
						double shiftedBstop = bstop - revolutionTime;
						// if SWS we truncate
						if (((double)sectors[0].id.info.startTime / 1000000.0) < shiftedBstop)
							shiftedBstop = (double)sectors[0].id.info.startTime / 1000000.0;

						drawRectangle(sector, shiftedBstart, shiftedBstop, SecType.data2);
					}
					if (sector.postGap2 != null) {
						tb = new TextBox();
						tb.Text = String.Format("Extra PGap Field {0} bytes - Start @ {1} {2:F2}ms",
							sector.postGap2.buffer.DataByte.Count, sector.postGap2.info.startBit, (double)sector.postGap2.info.startTime / 1000000.0);
						sectorInfoPanel.Children.Add(tb);
					}
				}   // sector has extra data field

				sectorBorderPolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
				sectorBorderPolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));

				ToolTipService.SetToolTip(sectorBorderPolygon, sectorInfoPanel);
				ToolTipService.SetShowDuration(sectorBorderPolygon, 100000);
				_dataChartLayout.GraphicCanvas.Children.Add(sectorBorderPolygon);

			}   // all sectors

			// draw out of band
			int OOBCount = 0;
			Global.DiskTrack dt = Global.Disk[track, side];
			if (dt.sectors.Count > 1) {
				int start = Global.firstIndex(track, side, rev);
				int stop = Global.lastIndex(track, side, rev);
				int currentTime = 0;
				PhaseSector ps = new PhaseSector(dt, dt.fluxDataRev[rev].revolutionTime);

				for (int bitPosition = start; bitPosition < stop; bitPosition++) {
					int flux = dt.fluxData.fluxValue[bitPosition];
					currentTime += flux;

					int bitWidth = dt.decodedData.bitSpacing[bitPosition];
					// currentBit += bitWidth;


					ps.computePhaseAndSector(currentTime);
					if (!FDC.isGapPhase(ps.Phase)) continue;

					int percent = 8;
					int upper_high = 4 * bitWidth + ((4 * percent * bitWidth) / 100);
					int upper_low = 4 * bitWidth - ((4 * percent * bitWidth) / 100);
					int middle_high = 3 * bitWidth + ((3 * percent * bitWidth) / 100);
					int middle_low = 3 * bitWidth - ((3 * percent * bitWidth) / 100);
					int lower_high = 2 * bitWidth + ((2 * percent * bitWidth) / 100);
					int lower_low = 2 * bitWidth - ((2 * percent * bitWidth) / 100);

					if (((flux > upper_high) || ((flux < upper_low) && (flux > middle_high)) ||
							(flux < lower_low) || ((flux > lower_high) && (flux < middle_low)))
							&& (dt.sectors.Count > 0) && FDC.isGapPhase(ps.Phase)) {
						if (OOBCount++ > 50) break;
						Line splice = new Line();
						splice.Stroke = Brushes.DarkBlue;
						splice.StrokeThickness = 1.5;
						//DoubleCollection dashPattern = new DoubleCollection(new double[2] { 2, 1 });
						//splice.StrokeDashArray = dashPattern;
						splice.X1 = _dataChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 2)).X;
						splice.Y1 = _dataChartLayout.NormY(_dataChartLayout.Ymin);
						splice.X2 = _dataChartLayout.NormPoint(new Point((double)currentTime / 1000000.0, 10)).X;
						splice.Y2 = _dataChartLayout.NormY(_dataChartLayout.Ymax);

						TextBox spliceInfo = new TextBox();
						spliceInfo.Text = String.Format("Out of band at {0} ms -- flux value={1:F3} µs", (double)currentTime / 1000000.0, flux);
						ToolTipService.SetToolTip(splice, spliceInfo);
						ToolTipService.SetShowDuration(splice, 100000);
						_dataChartLayout.GraphicCanvas.Children.Add(splice);

					}
				}
			}
		}


		private void drawRectangle(Sector sector, double bstart, double bstop, SecType type) {
			Brush IGapBrush = new SolidColorBrush(Global.IGapColor);
			IGapBrush.Freeze();
			Brush ShortSectorBrush = new SolidColorBrush(Global.ShortSectorColor);
			ShortSectorBrush.Freeze();
			Brush BitRateSectorBrush = new SolidColorBrush(Global.BitRateSectorColor);
			BitRateSectorBrush.Freeze();
			Brush FuzzySectorBrush = new SolidColorBrush(Global.FuzzySectorColor);
			FuzzySectorBrush.Freeze();
			Brush LongSectorBrush = new SolidColorBrush(Global.LongSectorColor);
			LongSectorBrush.Freeze();
			Brush NormalSectorBrush = new SolidColorBrush(Global.NormalSectorColor);
			NormalSectorBrush.Freeze();
			Brush IdBrush = new SolidColorBrush(Global.IdColor);
			IdBrush.Freeze();
			Brush NoIdDataBrush = new SolidColorBrush(Global.NoIdDataColor);
			NoIdDataBrush.Freeze();

			Polygon sectorPhasePolygon = new Polygon();
			sectorPhasePolygon.Stroke = Brushes.Black;
			sectorPhasePolygon.StrokeThickness = 0.5;

			if (type == SecType.id)
				sectorPhasePolygon.Fill = IdBrush;
			//else if (phase == Phases.p_gap)
			else if (type == SecType.gap)
				sectorPhasePolygon.Fill = IGapBrush;
			else {
				sectorPhasePolygon.Fill = NormalSectorBrush;
				if ((sector.flags & Sector.WIDTH_ABOVE) != 0)
					//if ((sector.flags & Sector.LONG_SECT) != 0)
					sectorPhasePolygon.Fill = LongSectorBrush;
				if ((sector.flags & Sector.WIDTH_BELOW) != 0)
					//if ((sector.flags & Sector.SHORT_SECT) != 0)
					sectorPhasePolygon.Fill = ShortSectorBrush;
				if ((sector.flags & Sector.WIDTH_INTRA) != 0)
					sectorPhasePolygon.Fill = BitRateSectorBrush;

				DataField data;
				if (type == SecType.data2) {
					sectorPhasePolygon.Fill = NoIdDataBrush;
					data = sector.data2;
				}
				else
					data = sector.data;

				if (data.info.crc != 0) {
					Polygon crcPolygon = new Polygon();
					crcPolygon.Fill = Brushes.Red;
					crcPolygon.StrokeThickness = 0;
					crcPolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.05));
					crcPolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.95));
					crcPolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.95));
					crcPolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.05));
					_dataChartLayout.GraphicCanvas.Children.Add(crcPolygon);
				}
				if (data.fuzzy != null) {
					Polygon fuzzyPolygon = new Polygon();
					fuzzyPolygon.Fill = FuzzySectorBrush;
					fuzzyPolygon.StrokeThickness = 0;
					fuzzyPolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.10));
					fuzzyPolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.9));
					fuzzyPolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.9));
					fuzzyPolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.10));
					_dataChartLayout.GraphicCanvas.Children.Add(fuzzyPolygon);
				}   // fuzzy
			}   // phase p_data

			sectorPhasePolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.2));
			sectorPhasePolygon.Points.Add(_dataChartLayout.NormPoint(bstart, 0.8));
			sectorPhasePolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.8));
			sectorPhasePolygon.Points.Add(_dataChartLayout.NormPoint(bstop, 0.2));
			_dataChartLayout.GraphicCanvas.Children.Add(sectorPhasePolygon);
		}

	}
}



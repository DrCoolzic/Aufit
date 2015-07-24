/*!
@file DiskGUI.cs
<summary>Everything related to drawing the Disk Data</summary>

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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
//using System.Diagnostics;

using WD1772Package;
using GlobalPackage;
using System.ComponentModel;

namespace Aufit {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		const int diskHalfWindowSize = 300;
		const double diskFineLine = 1.7;
		const double diskHeavyLine = 2.7;
		const double rad_max = diskHalfWindowSize - diskHalfWindowSize * 0.03;
		const double rad_start = diskHalfWindowSize / 6;
		const double rad_inc = (rad_max - rad_start) / 84;
		Point center = new Point(diskHalfWindowSize, diskHalfWindowSize);
		int _selectedTrack = -1;
		int _selectedSide = -1;
		Brush disk_background = new SolidColorBrush(Colors.DarkGray);
		Brush disk_color = new SolidColorBrush(Colors.DimGray);
		Brush reference_line = new SolidColorBrush(Colors.MediumBlue);


		private async void btDiskRedraw(object sender, RoutedEventArgs e) {
			diskClear();
			await drawDiskLayout();
		}


		private void diskClear() {
			cvDisk0.Children.Clear();
			cvDisk1.Children.Clear();
		}


		/// <summary>
		/// Display the layout of a FD
		/// </summary>
		private async Task<bool> drawDiskLayout() {
			bool _debugNoAsync = false;
			if (cvDisk0.Children.Count > 0) return false;

			Brush Black = new SolidColorBrush(Colors.Black);
			Black.Freeze();
			Brush DarkSlateGray = new SolidColorBrush(Colors.DarkSlateGray);
			DarkSlateGray.Freeze();
			Brush White = new SolidColorBrush(Colors.White);
			White.Freeze();
			Brush Transparent = new SolidColorBrush(Colors.Transparent);
			Transparent.Freeze();
			Brush LightGray = new SolidColorBrush(Colors.LightGray);
			LightGray.Freeze();

			const double referenceThickness = 0.5;
			double startAngle = -0.0001;
			double stopAngle = Math.PI * 2;
			double radius = diskHalfWindowSize;

			// add filled disk
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
			path.Fill = disk_color;
			PathGeometry pg = new PathGeometry();
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			cvDisk0.Children.Add(path);

			Canvas.SetZIndex(path, -1);
			path = new System.Windows.Shapes.Path();
			path.Fill = disk_color;
			path.Data = pg;
			cvDisk1.Children.Add(path);
			Canvas.SetZIndex(path, -1);

			// add center hole
			radius /= 9;
			path = new System.Windows.Shapes.Path();
			path.Fill = disk_background;
			PathGeometry hole = new PathGeometry();
			addArc(hole, startAngle, stopAngle, center, radius);
			path.Data = hole;
			cvDisk0.Children.Add(path);

			path = new System.Windows.Shapes.Path();
			path.Fill = disk_background;
			path.Data = hole;
			cvDisk1.Children.Add(path);

			// draw side value
			TextBox tb = new TextBox();
			tb.IsReadOnly = true;
			tb.Margin = new Thickness(0, diskHalfWindowSize - 20, 0, 0);
			tb.FontFamily = new FontFamily("Segoe UI");
			tb.BorderThickness = new Thickness(0);
			tb.FontSize = 11;
			tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			tb.Background = Transparent;
			tb.Foreground = White;
			tb.Text = "Side 0";
			cvDisk0.Children.Add(tb);

			tb = new TextBox();
			tb.IsReadOnly = true;
			tb.Margin = new Thickness(0, diskHalfWindowSize - 20, 0, 0);
			tb.FontFamily = new FontFamily("Segoe UI");
			tb.BorderThickness = new Thickness(0);
			tb.FontSize = 11;
			tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			tb.Background = Transparent;
			tb.Foreground = White;
			tb.Text = "Side 1";
			cvDisk1.Children.Add(tb);

			// draw reference line
			//DoubleCollection referenceDashPattern = new DoubleCollection(new double[2] { 10, 2 });
			Line zeroLine = new Line();
			zeroLine.Stroke = reference_line;
			zeroLine.StrokeThickness = referenceThickness;
			//zeroLine.StrokeDashArray = referenceDashPattern;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			cvDisk0.Children.Add(zeroLine);
			zeroLine = new Line();
			zeroLine.Stroke = reference_line;
			zeroLine.StrokeThickness = referenceThickness;
			//zeroLine.StrokeDashArray = referenceDashPattern;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			cvDisk1.Children.Add(zeroLine);

			// display Aufit version (nice for picture)
			tbImageVersion.Text = "Aufit " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			// Display Image name
			tbImageName.Text = Aufit.MainWindow.ImageName;

			// draw all tracks of both side
			for (int track = 0; track < 84; track++) {
				for (int side = 0; side < 2; side++) {
					Grid grid = (side == 0) ? cvDisk0 : cvDisk1;
					if ((Global.Disk[track, side].fluxData == null) &&
						(Global.Disk[track, side].packedFluxData == null)) continue;
					if (_debugNoAsync) {
						Global.decodeAndRead(track, side, 0);
						drawDiskTrackLayout(track, side, 0, cvDisk0);
					}
					else {
						await Global.decodeAndReadAsync(track, side, 0);
						await drawDiskTrackLayoutAsync(track, side, 0, grid);
					}
					// TODO below commented because fails in batch mode for certain files???
					//ButtonMatrix.setButtonBG(track, 0);
				}	// all tracks
			}	// all sides

			return true;
		}


		public void drawDiskSelectedTrack(int track, int side) {
			if (track != -1)
				drawSelected(_selectedTrack, _selectedSide, disk_color);
			drawSelected(track, side, disk_background);
			_selectedTrack = track;
			_selectedSide = side;
		}


		private void drawSelected(int track, int side, Brush color) {
			double radius = rad_max - track * rad_inc;
			Grid grid = (side == 0) ? cvDisk0 : cvDisk1;
			Path path = new Path();
			path.StrokeThickness = diskHeavyLine - 0.3;
			PathGeometry pg = new PathGeometry();
			path.Stroke = color;
			double startAngle = -0.0001;
			double stopAngle = Math.PI * 2;
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			grid.Children.Add(path);
			Grid.SetZIndex(path, -1);
		}


		private Task<bool> drawDiskTrackLayoutAsync(int track, int side, int rev, Grid grid) {
			return Task<bool>.Run(() => drawDiskTrackLayout(track, side, rev, grid));
		}


		/// <summary>
		/// Draw one track of the FD
		/// </summary>
		/// <param name="track">The track to display</param>
		/// <param name="side">The side of the FD</param>
		/// <param name="rev">The revolution index</param>
		private bool drawDiskTrackLayout(int track, int side, int rev, Grid grid) {
			Global.DiskTrack dt = Global.Disk[track, side];
			List<Sector> sectors = dt.sectors;
			if (sectors == null) return false;
			double radius = rad_max - track * rad_inc;
			Brush White = new SolidColorBrush(Colors.White);
			White.Freeze();
			Brush Black = new SolidColorBrush(Colors.Black);
			Black.Freeze();
			Brush Blue = new SolidColorBrush(Colors.Blue);
			Blue.Freeze();
			Brush SlateGray = new SolidColorBrush(Colors.SlateGray);
			SlateGray.Freeze();
			Brush Yellow = new SolidColorBrush(Colors.Yellow);
			Yellow.Freeze();
			Brush Red = new SolidColorBrush(Colors.Red);
			Red.Freeze();

			// http://www.developpez.net/forums/d966615/dotnet/developpement-windows/windows-presentation-foundation/probleme-stathread-wpf/
			this.Dispatcher.Invoke(DispatcherPriority.Background, (ThreadStart)delegate {

				// draw sectors arcs
				if ((sectors.Count > 0) || ((sectors.Count == 1) && (sectors[0].data != null))) {
					for (int i = 0; i < sectors.Count; i++) {
						Sector sector = sectors[i];
						double start = 0;
						double stop = 0;
						double revolutionTime = (double)Global.Disk[track, side].packedFluxDataRev[rev].revolutionTime;

						// draw ID segment
						start = (double)sector.id.info.startTime;
						stop = (double)(sector.id.info.startTime + sector.id.info.timeLength);
						double idStop = stop;
						drawArc(sector, center, radius, timeAngle(start), timeAngle(stop), Phases.p_id, grid);

						// Draw gap and data if there
						if (sector.data != null) {
							start = (double)sector.data.info.startTime;
							stop = (double)(sector.data.info.startTime + sector.data.info.timeLength);
							drawArc(sector, center, radius, timeAngle(idStop), timeAngle(start), Phases.p_gap_inner, grid);

							// if SWS we truncate
							if (((i + 1) < sectors.Count) && ((double)sectors[i + 1].id.info.startTime < stop))
								stop = (double)sectors[i + 1].id.info.startTime;

							// if shifted we check to see if need truncate
							if (stop > revolutionTime) {
								// if SWS we truncate
								double shiftedBstop = stop - revolutionTime;
								if ((double)sectors[0].id.info.startTime < shiftedBstop)
									shiftedBstop = (double)sectors[0].id.info.startTime;
								stop = shiftedBstop + revolutionTime;
							}
							drawArc(sector, center, radius, timeAngle(start), timeAngle(stop), Phases.p_data, grid);
						}	// sector has data field
					}	// all sectors
				}	// one or more sectors
				else {
					Path path = new Path();
					path.StrokeThickness = diskFineLine;
					PathGeometry pg = new PathGeometry();
					//path.Stroke = Black;

					if ((dt.histoData.hasBitsInBands) && (dt.histoData.entropy < Global.UNFORMATED_ENTROPY)) {
						if (dt.track.firstSyncOffset > 0)
							path.Stroke = Blue;
						else
							path.Stroke = Brushes.LightBlue;
					}	// bits in band found
					else
						path.Stroke = Brushes.DarkSlateGray;

					double startAngle = -0.0001;
					double stopAngle = Math.PI * 2;
					addArc(pg, startAngle, stopAngle, center, radius);
					path.Data = pg;
					grid.Children.Add(path);
				}	// no sector track

				DSR dsr = new DSR();
				int startBit = dt.packedFluxDataRev[rev].firstFluxBit;
				int stopBit = startBit + dt.packedFluxDataRev[rev].bitCount;
				int currentTime = 0;
				int flux = 0;
				int nfaStartTime = 0;
				int lastOobStopTime = 0;
				const int minDisplayWidth = 100000;

				for (int currentBit = startBit; (currentBit < stopBit); currentBit++) {
					int currentByte = currentBit / 8;
					int bitInByte = currentBit % 8;
					byte bits = dt.packedFluxData.packedBits[currentByte];
					int bit = (bits >> bitInByte) & 0x1;
					//int bitWidth = dt.packedFluxData.bitWidth[currentByte];
					//currentTime += bitWidth;

					dsr.shiftBitsIn(bit);
					// draw out of band
					if (bit != 0) {
						flux = Global.fluxWidth(currentBit, track, side);
						currentTime += flux;
						if ((dt.histoData.entropy < Global.UNFORMATED_ENTROPY) && (dt.sectors.Count > 0) && (cbOutOfBand.IsChecked == true) &&
								(Global.fluxOutOfBand(flux)) && (currentTime > lastOobStopTime)) {
							lastOobStopTime = currentTime + minDisplayWidth;
							double startAngle = timeAngle(currentTime);
							double stopAngle = timeAngle(lastOobStopTime);
							Path path = new Path();
							path.StrokeThickness = diskHeavyLine;
							PathGeometry pg = new PathGeometry();
							path.Stroke = White;
							addArc(pg, startAngle, stopAngle, center, radius);
							path.Data = pg;
							grid.Children.Add(path);
						}	// out of band
					}	// one more flux

					// draw NFA
					if (dsr.DataReady) {
						if (((dt.histoData.entropy < Global.UNFORMATED_ENTROPY) || (cbDiskNFA.IsChecked == true)) && dsr.DataReady) {
							if ((nfaStartTime == 0) && ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF)))
								nfaStartTime = currentTime;
							if ((dsr.MFM != 0) && (dsr.MFM != 0xFFFF) && (nfaStartTime > 0) && (currentTime > (nfaStartTime + minDisplayWidth))) {
								double startAngle = timeAngle(nfaStartTime);
								double stopAngle = timeAngle(currentTime);
								nfaStartTime = 0;
								System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
								path.StrokeThickness = diskHeavyLine;
								PathGeometry pg = new PathGeometry();
								path.Stroke = Red;
								addArc(pg, startAngle, stopAngle, center, radius);
								path.Data = pg;
								grid.Children.Add(path);
							}
						}
					}

					// add sync bars
					if (dsr.MFM == 0x4489) {
						double startAngle = timeAngle(currentTime);
						double stopAngle = timeAngle(currentTime + minDisplayWidth);
						Path path = new Path();
						path.StrokeThickness = diskHeavyLine;
						PathGeometry pg = new PathGeometry();
						path.Stroke = Brushes.Black;
						addArc(pg, startAngle, stopAngle, center, radius);
						path.Data = pg;
						grid.Children.Add(path);
					}

				}	// all bits

			});	// dispatcher
			return true;
		}	// End of drawDisk()


		private void drawArc(Sector sector, Point center, double radius, double startAngle, double stopAngle, Phases phase, Grid grid) {
			Brush LightGreen = new SolidColorBrush(Colors.LightGreen);
			LightGreen.Freeze();
			Brush Aqua = new SolidColorBrush(Colors.Aqua);
			Aqua.Freeze();
			Brush Purple = new SolidColorBrush(Colors.Purple);
			Purple.Freeze();
			Brush Orange = new SolidColorBrush(Colors.Orange);
			Orange.Freeze();
			Brush Blue = new SolidColorBrush(Colors.Blue);
			Blue.Freeze();
			Brush DeepSkyBlue = new SolidColorBrush(Colors.DeepSkyBlue);
			DeepSkyBlue.Freeze();
			Brush Yellow = new SolidColorBrush(Colors.Yellow);
			Yellow.Freeze();

			System.Windows.Shapes.Path idPath = new System.Windows.Shapes.Path();
			idPath.StrokeThickness = diskFineLine;
			idPath.Stroke = LightGreen;
			if (phase == Phases.p_id)
				idPath.Stroke = Yellow;
			if (phase == Phases.p_data) {
				idPath.Stroke = DeepSkyBlue;
				if ((sector.flags & Sector.WIDTH_ABOVE) != 0)
					idPath.Stroke = Blue;
				if ((sector.flags & Sector.WIDTH_BELOW) != 0)
					idPath.Stroke = Aqua;
				if ((sector.flags & Sector.WIDTH_INTRA) != 0)
					idPath.Stroke = Purple;
				if ((sector.data.info.crc != 0) || (sector.data.fuzzy != null))
					idPath.Stroke = Orange;
			}

			PathGeometry idPg = new PathGeometry();
			addArc(idPg, startAngle, stopAngle, center, radius);
			idPath.Data = idPg;
			grid.Children.Add(idPath);
		}


		/// <summary>
		/// Add an arc to a path geometry
		/// </summary>
		/// <param name="pg">Pointer to the PathGeometry</param>
		/// <param name="start">Start angle</param>
		/// <param name="stop">Stop angle</param>
		/// <param name="center">Center point of the arc</param>
		/// <param name="radius">Radius of the arc</param>
		private void addArc(PathGeometry pg, double start, double stop, Point center, double radius) {
			Point startPoint = center + new Vector(Math.Cos(start), Math.Sin(start)) * radius;
			Point stopPoint = center + new Vector(Math.Cos(stop), Math.Sin(stop)) * radius;
			SweepDirection direction = SweepDirection.Counterclockwise;
			bool isLargeArc = false;

			if ((stop - start) > Math.PI) isLargeArc = true;
			ArcSegment aseg = new ArcSegment(stopPoint, new Size(radius, radius), 0, isLargeArc, direction, true);
			PathFigure pf = new PathFigure();
			pf.IsClosed = false;
			pf.StartPoint = startPoint;
			pf.Segments.Add(aseg);
			pg.Figures.Add(pf);
		}


		/// <summary>
		/// Convert a time from 0 to 200ms into an angle in 0 to 2 PI
		/// </summary>
		/// <param name="time"></param>
		/// <returns></returns>
		private double timeAngle(double time) {
			double angle = 2 * Math.PI - (Math.PI * time / 100000000.0);
			return angle;
		}


		private double byteAngle(int pos, int count) {
			double angle = 2 * Math.PI * (1.0 - (double)pos / (double)count);
			return angle;
		}


		private void saveImageClick(object sender, RoutedEventArgs e) {
			double scale = 0.8;
			if (rbMed.IsChecked == true) scale = 1.2;
			else if (rbHigh.IsChecked == true) scale = 2.0;
			Global.saveImage(gridImageView, scale, ".disk_view.png", true);
		}

	}
}
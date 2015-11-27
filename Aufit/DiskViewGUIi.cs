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
using System.Windows.Input;

namespace Aufit {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		const int diskHalfWindowSize = 150;
		const double diskWindowSize = 2.0 * (double)diskHalfWindowSize;
		const double diskFineLine = (double)diskHalfWindowSize / 200;
		const double diskHeavyLine = (double)diskHalfWindowSize / 140;
		const double rad_max = diskHalfWindowSize - diskHalfWindowSize * 0.03;
		const double rad_start = diskHalfWindowSize / 6;
		const double rad_inc = (rad_max - rad_start) / 84;

		Point center = new Point(diskHalfWindowSize, diskHalfWindowSize);
		Brush DiskHighlightBrush = new SolidColorBrush(Colors.DarkGray);
		Brush DiskBackgoundBrush = new SolidColorBrush(Colors.DimGray);
		Brush ReferenceLineBrush = new SolidColorBrush(Colors.MediumBlue);

		EllipseGeometry clipRegionSide0 = new EllipseGeometry();
		EllipseGeometry clipRegionSide1 = new EllipseGeometry();
		System.Windows.Shapes.Path clipPathSide0;
		System.Windows.Shapes.Path clipPathSide1;
		ScaleTransform scaleTrSide0 = new ScaleTransform();
		ScaleTransform scaleTrSide1 = new ScaleTransform();
		double diskScaleSide0;
		double diskScaleSide1;
		bool diskFreezeClipSide0;
		bool diskFreezeClipSide1;
		const double diskScaleMin = 1.2;
		const double diskScaleDef = 4;

		int _selectedTrack = -1;
		int _selectedSide = -1;


		private void enterDiskSide0(object sender, MouseEventArgs e) {
			// TODO this is insane ?
			for (int i = 0; i < 50; i++)
				moveMagnifierSide0(e);
			clipDiskSide0.Visibility = System.Windows.Visibility.Visible;
		}


		private void enterDiskSide1(object sender, MouseEventArgs e) {
			for (int i = 0; i < 50; i++)
				moveMagnifierSide1(e);
			clipDiskSide1.Visibility = System.Windows.Visibility.Visible;
		}


		private void leaveDiskSide0(object sender, MouseEventArgs e) {
			if (diskFreezeClipSide0) return;
			clipDiskSide0.Visibility = System.Windows.Visibility.Hidden;
		}


		private void leaveDiskSide1(object sender, MouseEventArgs e) {
			if (diskFreezeClipSide1) return;
			clipDiskSide1.Visibility = System.Windows.Visibility.Hidden;
		}


		private void moveDiskSide0(object sender, MouseEventArgs e) {
			moveMagnifierSide0(e);
		}


		private void moveDiskSide1(object sender, MouseEventArgs e) {
			moveMagnifierSide1(e);
		}


		void moveMagnifierSide0(MouseEventArgs e) {
			if (diskFreezeClipSide0) return;

			double X = e.GetPosition(clipDiskSide0).X;
			double Y = e.GetPosition(clipDiskSide0).Y;

			scaleTrSide0.ScaleX = diskScaleSide0;
			scaleTrSide0.ScaleY = diskScaleSide0;
			scaleTrSide0.CenterX = X;
			scaleTrSide0.CenterY = Y;

			clipRegionSide0.Center = new Point(X, Y);
			clipRegionSide0.RadiusX = 100 / diskScaleSide0;
			clipRegionSide0.RadiusY = 100 / diskScaleSide0;
		}


		void moveMagnifierSide1(MouseEventArgs e) {
			if (diskFreezeClipSide1) return;

			double X = e.GetPosition(clipDiskSide1).X;
			double Y = e.GetPosition(clipDiskSide1).Y;

			scaleTrSide1.ScaleX = diskScaleSide1;
			scaleTrSide1.ScaleY = diskScaleSide1;
			scaleTrSide1.CenterX = X;
			scaleTrSide1.CenterY = Y;

			clipRegionSide1.Center = new Point(X, Y);
			clipRegionSide1.RadiusX = 100 / diskScaleSide1;
			clipRegionSide1.RadiusY = 100 / diskScaleSide1;
		}


		private void diskMouseWheelSide0(object sender, MouseWheelEventArgs e) {
			if (e.Delta > 0)
				diskScaleSide0 += 0.5;
			else
				diskScaleSide0 -= 0.5;
			if (diskScaleSide0 < diskScaleMin) diskScaleSide0 = diskScaleMin;
			moveMagnifierSide0(e);

		}


		private void diskMouseWheelSide1(object sender, MouseWheelEventArgs e) {
			if (e.Delta > 0)
				diskScaleSide1 += 0.5;
			else
				diskScaleSide1 -= 0.5;
			if (diskScaleSide1 < diskScaleMin) diskScaleSide1 = diskScaleMin;
			moveMagnifierSide1(e);

		}


		private void diskMLBSide0(object sender, MouseButtonEventArgs e) {
			if (diskFreezeClipSide0) {
				diskFreezeClipSide0 = false;
			}
			else {
				diskFreezeClipSide0 = true;
			}
			e.Handled = true;
		}


		private void diskMRBSide0(object sender, MouseButtonEventArgs e) {
			diskScaleSide0 = diskScaleDef;
			moveMagnifierSide0(e);
		}


		private void diskMLBSide1(object sender, MouseButtonEventArgs e) {
			if (diskFreezeClipSide1) {
				diskFreezeClipSide1 = false;
			}
			else {
				diskFreezeClipSide1 = true;
			}
			e.Handled = true;
		}


		private void diskMRBSide1(object sender, MouseButtonEventArgs e) {
			diskScaleSide1 = diskScaleDef;
			moveMagnifierSide1(e);
		}


		private void resetDiskMagnifier() {
			cbMagnifier.IsChecked = false;

			diskScaleSide0 = diskScaleDef;
			diskScaleSide1 = diskScaleDef;
			diskFreezeClipSide0 = false;
			diskFreezeClipSide1 = false;

			clipPathSide0 = new Path();
			//clipPathSide0.Stroke = Brushes.Red;
			//clipPathSide0.StrokeThickness = 1;
			clipPathSide0.Data = clipRegionSide0;
			clipDiskSide0.Children.Add(clipPathSide0);
			clipDiskSide0.Clip = clipRegionSide0;
			scaleTrSide0.ScaleX = diskScaleSide0;
			scaleTrSide0.ScaleY = diskScaleSide0;
			clipDiskSide0.RenderTransform = scaleTrSide0;
			clipDiskSide0.Visibility = System.Windows.Visibility.Hidden;

			clipPathSide1 = new Path();
			//clipPathSide1.Stroke = Brushes.Red;
			//clipPathSide1.StrokeThickness = 1;
			clipPathSide1.Data = clipRegionSide1;
			clipDiskSide1.Children.Add(clipPathSide1);
			clipDiskSide1.Clip = clipRegionSide1;
			scaleTrSide1.ScaleX = diskScaleSide0;
			scaleTrSide1.ScaleY = diskScaleSide0;
			clipDiskSide1.RenderTransform = scaleTrSide1;
			clipDiskSide1.Visibility = System.Windows.Visibility.Hidden;

			cvDiskSide0.MouseEnter -= enterDiskSide0;
			cvDiskSide1.MouseEnter -= enterDiskSide1;
			clipDiskSide0.MouseMove -= moveDiskSide0;
			clipDiskSide1.MouseMove -= moveDiskSide1;
			clipDiskSide0.MouseLeave -= leaveDiskSide0;
			clipDiskSide1.MouseLeave -= leaveDiskSide1;

		}


		private void magnifierClicked(object sender, RoutedEventArgs e) {
			if (cbMagnifier.IsChecked == true) {
				cvDiskSide0.MouseEnter += enterDiskSide0;
				cvDiskSide1.MouseEnter += enterDiskSide1;
				clipDiskSide0.MouseMove += moveDiskSide0;
				clipDiskSide1.MouseMove += moveDiskSide1;
				clipDiskSide0.MouseLeave += leaveDiskSide0;
				clipDiskSide1.MouseLeave += leaveDiskSide1;
			}
			else {
				resetDiskMagnifier();
			}
		}


		private async void btDiskRedraw(object sender, RoutedEventArgs e) {
			diskClear();
			await drawDiskLayout();
		}


		private void diskClear() {
			resetDiskMagnifier();
			cvDiskSide0.Children.Clear();
			cvDiskSide1.Children.Clear();
			clipDiskSide0.Children.Clear();
			clipDiskSide1.Children.Clear();
		}


		/// <summary>
		/// Display the layout of a FD
		/// </summary>
		private async Task<bool> drawDiskLayout() {
			if (cvDiskSide0.Children.Count > 0) return false;
			resetDiskMagnifier();

			Brush TextColorBrush = new SolidColorBrush(Colors.Black);
			TextColorBrush.Freeze();
			Brush TextBackgroundBrush = new SolidColorBrush(Colors.Transparent);
			TextBackgroundBrush.Freeze();

			const double referenceThickness = 0.5;
			double radius = diskHalfWindowSize;

			//// draw background disk
			Path path;
			CombinedGeometry cb = new CombinedGeometry();
			cb.GeometryCombineMode = GeometryCombineMode.Exclude;
			EllipseGeometry diskBg = new EllipseGeometry(center, radius, radius);
			EllipseGeometry diskHole = new EllipseGeometry(center, radius/9, radius/9);
			cb.Geometry1 = diskBg;
			cb.Geometry2 = diskHole;

			path = new Path();
			path.Fill = DiskBackgoundBrush;
			path.Data = cb;
			cvDiskSide0.Children.Add(path);
			//Canvas.SetZIndex(path, -1);

			path = new Path();
			path.Fill = DiskBackgoundBrush;
			path.Data = cb;
			clipDiskSide0.Children.Add(path);
			
			path = new Path();
			path.Fill = DiskBackgoundBrush;
			path.Data = cb;
			cvDiskSide1.Children.Add(path);

			path = new Path();
			path.Fill = DiskBackgoundBrush;
			path.Data = cb;
			clipDiskSide1.Children.Add(path);

			// draw side value
			TextBox tb = new TextBox();
			tb.IsReadOnly = true;
			tb.Margin = new Thickness(0, diskHalfWindowSize - 11, 0, 0);
			tb.FontFamily = new FontFamily("Segoe UI");
			tb.BorderThickness = new Thickness(0);
			tb.FontSize = 8;
			tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			tb.Background = TextBackgroundBrush;
			tb.Foreground = TextColorBrush;
			tb.FontWeight = FontWeights.Bold;
			tb.Text = "Side 0";
			cvDiskSide0.Children.Add(tb);

			//tb = new TextBox();
			//tb.IsReadOnly = true;
			//tb.Margin = new Thickness(0, diskHalfWindowSize - 11, 0, 0);
			//tb.FontFamily = new FontFamily("Segoe UI");
			//tb.BorderThickness = new Thickness(0);
			//tb.FontSize = 8;
			//tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			//tb.Background = Transparent;
			//tb.Foreground = Black;
			//tb.FontWeight = FontWeights.Bold;
			//tb.Text = "Side 0";
			//clipDiskSide0.Children.Add(tb);

			tb = new TextBox();
			tb.IsReadOnly = true;
			tb.Margin = new Thickness(0, diskHalfWindowSize - 11, 0, 0);
			tb.FontFamily = new FontFamily("Segoe UI");
			tb.BorderThickness = new Thickness(0);
			tb.FontSize = 8;
			tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			tb.Background = TextBackgroundBrush;
			tb.Foreground = TextColorBrush;
			tb.FontWeight = FontWeights.Bold;
			tb.Text = "Side 1";
			cvDiskSide1.Children.Add(tb);

			//tb = new TextBox();
			//tb.IsReadOnly = true;
			//tb.Margin = new Thickness(0, diskHalfWindowSize - 11, 0, 0);
			//tb.FontFamily = new FontFamily("Segoe UI");
			//tb.BorderThickness = new Thickness(0);
			//tb.FontSize = 8;
			//tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			//tb.Background = Transparent;
			//tb.Foreground = Black;
			//tb.FontWeight = FontWeights.Bold;
			//tb.Text = "Side 1";
			//clipDiskSide1.Children.Add(tb);

			// draw reference line
			//DoubleCollection referenceDashPattern = new DoubleCollection(new double[2] { 10, 2 });
			Line zeroLine = new Line();
			zeroLine.Stroke = ReferenceLineBrush;
			zeroLine.StrokeThickness = referenceThickness;
			//zeroLine.StrokeDashArray = referenceDashPattern;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			cvDiskSide0.Children.Add(zeroLine);

			zeroLine = new Line();
			zeroLine.Stroke = ReferenceLineBrush;
			zeroLine.StrokeThickness = referenceThickness;
			//zeroLine.StrokeDashArray = referenceDashPattern;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			clipDiskSide0.Children.Add(zeroLine);

			zeroLine = new Line();
			zeroLine.Stroke = ReferenceLineBrush;
			zeroLine.StrokeThickness = referenceThickness;
			//zeroLine.StrokeDashArray = referenceDashPattern;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			cvDiskSide1.Children.Add(zeroLine);

			zeroLine = new Line();
			zeroLine.Stroke = ReferenceLineBrush;
			zeroLine.StrokeThickness = referenceThickness;
			//zeroLine.StrokeDashArray = referenceDashPattern;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			clipDiskSide1.Children.Add(zeroLine);

			// display Aufit version (nice for picture)
			tbImageVersion.Text = "Aufit " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			// Display Image name
			tbImageName.Text = Aufit.MainWindow.ImageName;

			// draw all tracks of both side
			bool _debugNoAsync = false;
			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {				
					if (Global.Disk[track, side].fluxData == null) continue;
					
					if (_debugNoAsync) {
						if (side == 0) {
							drawDiskTrackLayout(track, side, 0, cvDiskSide0);
							drawDiskTrackLayout(track, side, 0, clipDiskSide0);
						}
						else {
							drawDiskTrackLayout(track, side, 0, cvDiskSide1);
							drawDiskTrackLayout(track, side, 0, clipDiskSide1);
						}
					}
					else {
						if (side == 0) {
							await drawDiskTrackLayoutAsync(track, side, 0, cvDiskSide0);
							if (!BatchMode)
								await drawDiskTrackLayoutAsync(track, side, 0, clipDiskSide0);
						}
						else {
							await drawDiskTrackLayoutAsync(track, side, 0, cvDiskSide1);
							if (!BatchMode)
								await drawDiskTrackLayoutAsync(track, side, 0, clipDiskSide1);
						}
					}
					ButtonMatrix.setButtonBG(track, 0);
				}	// all tracks
			}	// all sides
			return true;
		}


		public void drawDiskSelectedTrack(int track, int side) {
			if (track != -1)
				drawSelected(_selectedTrack, _selectedSide, true);
			drawSelected(track, side, false);
			_selectedTrack = track;
			_selectedSide = side;
		}


		private void drawSelected(int track, int side, bool erase) {
			double radius = rad_max - track * rad_inc;
			Grid grid = (side == 0) ? cvDiskSide0 : cvDiskSide1;
			Path path = new Path();
			if (erase) {
				path.StrokeThickness = diskHeavyLine - 0.2;
				path.Stroke = DiskBackgoundBrush;
			}
			else {
				path.StrokeThickness = diskHeavyLine - 0.3;
				path.Stroke = DiskHighlightBrush;
			}
			PathGeometry pg = new PathGeometry();
			double startAngle = -0.0001;
			double stopAngle = Math.PI * 2;
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			grid.Children.Add(path);
			Grid.SetZIndex(path, 10);
		}


		private Task<bool> drawDiskTrackLayoutAsync(int track, int side, int rev, Grid grid) {
			return Task<bool>.Run(() => drawDiskTrackLayout(track, side, rev, grid));
		}


		private enum SecType { id, gap, data, data2 }

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

			Brush OutOfBandBrush = new SolidColorBrush(Colors.White);
			OutOfBandBrush.Freeze();
			Brush SyncBrush = new SolidColorBrush(Colors.Red);
			SyncBrush.Freeze();
			Brush NFABrush = new SolidColorBrush(Colors.Black);
			NFABrush.Freeze();

			Brush DataOneIdTrackBrush = new SolidColorBrush(Global.DataOneIdTrackColor);
			DataOneIdTrackBrush.Freeze();
			Brush UnformattedTrackBrush = new SolidColorBrush(Global.UnformattedTrackColor);
			UnformattedTrackBrush.Freeze();


			// http://www.developpez.net/forums/d966615/dotnet/developpement-windows/windows-presentation-foundation/probleme-stathread-wpf/
			this.Dispatcher.Invoke(DispatcherPriority.Background, (ThreadStart)delegate {

				// draw sectors arcs
				if ((sectors.Count > 1) || ((sectors.Count == 1) && (sectors[0].data != null))) {
					for (int i = 0; i < sectors.Count; i++) {
						Sector sector = sectors[i];
						double start = 0;
						double stop = 0;
						double revolutionTime = (double)Global.Disk[track, side].fluxDataRev[rev].revolutionTime;

						// draw ID segment
						start = (double)sector.id.info.startTime;
						stop = (double)(sector.id.info.startTime + sector.id.info.timeLength);
						double idStop = stop;
						drawArc(sector, center, radius, timeAngle(start), timeAngle(stop), SecType.id, grid);

						// Draw gap and data if there
						if (sector.data != null) {
							start = (double)sector.data.info.startTime;
							stop = (double)(sector.data.info.startTime + sector.data.info.timeLength);
							drawArc(sector, center, radius, timeAngle(idStop), timeAngle(start), SecType.gap, grid);

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
							drawArc(sector, center, radius, timeAngle(start), timeAngle(stop), SecType.data, grid);
						}   // sector has data field
						// Draw gap and data if there
						if (sector.dataNoId != null) {
							start = (double)sector.dataNoId.info.startTime;
							stop = (double)(sector.dataNoId.info.startTime + sector.data.info.timeLength);
							//drawArc(sector, center, radius, timeAngle(idStop), timeAngle(start), Phases.p_gap_inner, grid);

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
							drawArc(sector, center, radius, timeAngle(start), timeAngle(stop), SecType.data2, grid);
						}   // sector has extra data field

					}   // all sectors
				}	// one or more sectors
				else {
					Path path = new Path();
					path.StrokeThickness = diskFineLine;
					PathGeometry pg = new PathGeometry();

					if ((dt.histoData.hasMFM) /*&& (dt.histoData.entropy < Global.UNFORMATED_ENTROPY)*/) {
						if (dt.track.firstSyncOffset > 0)
							path.Stroke = DataOneIdTrackBrush;
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
					Grid.SetZIndex(path, 100);
				}	// no sector track

				DSR dsr = new DSR();
				int currentTime = 0;
				int nfaStartTime = 0;
				int lastOobStopTime = 0;
				const int minDisplayWidth = 100000;

				for (int i = Global.firstIndex(track, side, rev); (i < Global.lastIndex(track, side, rev)); i++) {
					int flux = dt.fluxData.fluxValue[i];
					currentTime += flux;
					int bitSpacing = dt.decodedData.bitSpacing[i];
					int bitWidth = dt.decodedData.bitWidth[i];

					// draw out of band
					if ((dt.histoData.entropy < Global.UNFORMATED_ENTROPY) && (dt.sectors.Count > 0) && (cbOutOfBand.IsChecked == true) &&
							(Global.fluxOutOfBand(flux)) && (currentTime > lastOobStopTime)) {
						lastOobStopTime = currentTime + minDisplayWidth;
						double startAngle = timeAngle(currentTime);
						double stopAngle = timeAngle(lastOobStopTime);
						Path path = new Path();
						path.StrokeThickness = diskHeavyLine;
						PathGeometry pg = new PathGeometry();
						path.Stroke = OutOfBandBrush;
						addArc(pg, startAngle, stopAngle, center, radius);
						path.Data = pg;
						grid.Children.Add(path);
						Grid.SetZIndex(path, 100);
					}   // out of band

					int runningTime = currentTime - flux;
					while (bitSpacing-- > 0) {
						dsr.shiftIn(bitSpacing > 0 ? 0 : 1);
						runningTime += bitWidth;

						// draw NFA
						if (dsr.DataReady) {
							if (((dt.histoData.entropy < Global.UNFORMATED_ENTROPY) || (cbDiskNFA.IsChecked == true)) && dsr.DataReady) {
								if ((nfaStartTime == 0) && ((dsr.MFM == 0) || (dsr.MFM == 0xFFFF)))
									nfaStartTime = runningTime;
								if ((dsr.MFM != 0) && (dsr.MFM != 0xFFFF) && (nfaStartTime > 0) && (runningTime > (nfaStartTime + minDisplayWidth))) {
									double startAngle = timeAngle(nfaStartTime);
									double stopAngle = timeAngle(runningTime);
									nfaStartTime = 0;
									System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
									path.StrokeThickness = diskHeavyLine;
									PathGeometry pg = new PathGeometry();
									path.Stroke = NFABrush;
									addArc(pg, startAngle, stopAngle, center, radius);
									path.Data = pg;
									grid.Children.Add(path);
									Grid.SetZIndex(path, 100);
								}
							}
						}

						// add sync bars
						if (dsr.MFM == 0x4489) {
							double startAngle = timeAngle(runningTime);
							double stopAngle = timeAngle(runningTime + minDisplayWidth);
							Path path = new Path();
							path.StrokeThickness = diskHeavyLine;
							PathGeometry pg = new PathGeometry();
							path.Stroke = SyncBrush;
							addArc(pg, startAngle, stopAngle, center, radius);
							path.Data = pg;
							grid.Children.Add(path);
							Grid.SetZIndex(path, 100);
						}
					}	// shift bit in
				}	// all bits

			});	// dispatcher
			return true;
		}	// End of drawDisk()


		private void drawArc(Sector sector, Point center, double radius, double startAngle, double stopAngle, SecType type, Grid grid) {
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

			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
			path.StrokeThickness = diskFineLine;
			path.Stroke = IGapBrush;

			if (type == SecType.id)
				path.Stroke = IdBrush;
			else if (type == SecType.data) {
				path.Stroke = NormalSectorBrush;
				if ((sector.flags & Sector.WIDTH_ABOVE) != 0)
				//if ((sector.flags & Sector.LONG_SECT) != 0)
					path.Stroke = LongSectorBrush;
				if ((sector.flags & Sector.WIDTH_BELOW) != 0)
				//if ((sector.flags & Sector.SHORT_SECT) != 0)
					path.Stroke = ShortSectorBrush;
				if ((sector.flags & Sector.WIDTH_INTRA) != 0)
					path.Stroke = BitRateSectorBrush;
				if ((sector.data.info.crc != 0) || (sector.data.fuzzy != null))
					path.Stroke = FuzzySectorBrush;
			}
			else if (type == SecType.data2) {
				path.Stroke = NoIdDataBrush;
				if ((sector.dataNoId.info.crc != 0) || (sector.dataNoId.fuzzy != null))
					path.Stroke = FuzzySectorBrush;
			}

			PathGeometry idPg = new PathGeometry();
			addArc(idPg, startAngle, stopAngle, center, radius);
			path.Data = idPg;
			grid.Children.Add(path);
			Grid.SetZIndex(path, 100);
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
			double scale = 1.0;
			if (rbMed.IsChecked == true) scale = 1.4;
			else if (rbHigh.IsChecked == true) scale = 2.2;
			Global.saveImage(gridImageView, scale, "", true);
		}

	}
}
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

namespace Aufit {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		const int diskHalfWindowSize = 300;
		const double diskFineLine = 1.0;
		const double diskHeavyLine = 1.8;
		const double rad_max = diskHalfWindowSize - diskHalfWindowSize * 0.03;
		const double rad_start = diskHalfWindowSize / 4;
		const double rad_inc = (rad_max - rad_start) / 84;
		Point center = new Point(diskHalfWindowSize, diskHalfWindowSize);
		int _selectedTrack = -1;
		int _selectedSide = -1;

		#region DiskLayout
		private async void btDiskRefresh(object sender, RoutedEventArgs e) {
			await diskClearAndDraw();
		}


		private async Task diskClearAndDraw() {
			cvDisk0.Children.Clear();
			cvDisk1.Children.Clear();
			await drawDiskLayout();
		}


		/// <summary>
		/// Display the layout of a FD
		/// </summary>
		private async Task drawDiskLayout() {
			//MessageBox.Show(String.Format("{0} {1}", Track, Side));
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

			bool _debugNoAsync = false;
			const double referenceThickness = 0.5;

			double startAngle = -0.0001;
			double stopAngle = Math.PI * 2;
			double radius = diskHalfWindowSize;

			// add black filled disk
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
			path.Fill = Black;
			PathGeometry pg = new PathGeometry();
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			cvDisk0.Children.Add(path);
			Canvas.SetZIndex(path, -1);
			path = new System.Windows.Shapes.Path();
			path.Fill = Black;
			path.Data = pg;
			cvDisk1.Children.Add(path);
			Canvas.SetZIndex(path, -1);

			// add center hole
			radius /= 9;
			path = new System.Windows.Shapes.Path();
			path.Fill = DarkSlateGray;
			PathGeometry hole = new PathGeometry();
			addArc(hole, startAngle, stopAngle, center, radius);
			path.Data = hole;
			cvDisk0.Children.Add(path);
			path = new System.Windows.Shapes.Path();
			path.Fill = DarkSlateGray;
			path.Data = hole;
			cvDisk1.Children.Add(path);

			// draw side value
			TextBox tb = new TextBox();
			tb.Margin = new Thickness(0, 240, 0, 0);
			tb.FontFamily = new FontFamily("Segoe UI");
			tb.BorderThickness = new Thickness(0);
			tb.FontSize = 11;
			tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			tb.Background = Transparent;
			tb.Foreground = White;
			tb.Text = "Side 0";
			cvDisk0.Children.Add(tb);
			tb = new TextBox();
			tb.Margin = new Thickness(0, 240, 0, 0);
			tb.FontFamily = new FontFamily("Segoe UI");
			tb.BorderThickness = new Thickness(0);
			tb.FontSize = 11;
			tb.HorizontalContentAlignment = HorizontalAlignment.Center;
			tb.Background = Transparent;
			tb.Foreground = White;
			tb.Text = "Side 1";
			cvDisk1.Children.Add(tb);

			// draw reference line
			Line zeroLine = new Line();
			zeroLine.Stroke = LightGray;
			zeroLine.StrokeThickness = referenceThickness;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			cvDisk0.Children.Add(zeroLine);
			zeroLine = new Line();
			zeroLine.Stroke = LightGray;
			zeroLine.StrokeThickness = referenceThickness;
			zeroLine.X1 = diskHalfWindowSize;
			zeroLine.Y1 = diskHalfWindowSize;
			zeroLine.X2 = 2 * diskHalfWindowSize;
			zeroLine.Y2 = diskHalfWindowSize;
			cvDisk1.Children.Add(zeroLine);

			// draw all tracks of both side
			for (int side = 0; side < 2; side++) {
				Grid grid = (side == 0) ? cvDisk0 : cvDisk1;
				for (int track = 0; track < 84; track++) {
					if ((Global.Disk[track, side].fluxData == null) &&
						(Global.Disk[track, side].packedFluxData == null)) continue;
					if (_debugNoAsync) {
						Global.decodeAndRead(track, side, 0);
						drawDiskTrackLayout(track, side, 0, cvDisk0);
					}
					else {
						await Global.decodeAndReadAsync(track, side, 0);
						await drawDiskTrackAsync(track, side, 0, grid);
					}
					ButtonMatrix.setButtonBG(track, 0);
				}
			}

			// display Aufit version (nice for picture)
			TextBox version = new TextBox();
			version.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
			version.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
			version.Margin = new Thickness(0, 0, 0, 0);
			version.FontFamily = new FontFamily("Segoe UI");
			version.BorderThickness = new Thickness(0);
			version.FontSize = 10;
			version.HorizontalContentAlignment = HorizontalAlignment.Center;
			version.Background = Transparent;
			version.Foreground = White;
			version.Text = "Aufit " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			cvDisk1.Children.Add(version);
		}


		private Task<bool> drawDiskTrackAsync(int track, int side, int rev, Grid grid) {
			return Task<bool>.Run(() => drawDiskTrackLayout(track, side, rev, grid));
		}


		public void drawDiskSelectedTrack(int track, int side) {
			if (track != -1)
				drawSelected(_selectedTrack, _selectedSide, Brushes.Black);
			drawSelected(track, side, Brushes.Gray);
			_selectedTrack = track;
			_selectedSide = side;
		}


		private void drawSelected(int track, int side, Brush color) {
			double radius = rad_max - track * rad_inc;
			Grid grid = (side == 0) ? cvDisk0 : cvDisk1;
			Path path = new Path();
			path.StrokeThickness = diskHeavyLine;
			PathGeometry pg = new PathGeometry();
			path.Stroke = color;
			double startAngle = -0.0001;
			double stopAngle = Math.PI * 2;
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			grid.Children.Add(path);
			Grid.SetZIndex(path, -1);
		}

		/// <summary>
		/// Draw one track of the FD
		/// </summary>
		/// <param name="track">The track to display</param>
		/// <param name="side">The side of the FD</param>
		/// <param name="rev">The revolution index</param>
		private bool drawDiskTrackLayout(int track, int side, int rev, Grid grid) {

			//DoubleCollection referenceDashPattern = new DoubleCollection(new double[2] { 10, 10 });

			Global.DiskTrack dt = Global.Disk[track, side];
			List<Sector> sectors = dt.sectors;
			if (sectors == null) return false;
			double radius = rad_max - track * rad_inc;

			// http://www.developpez.net/forums/d966615/dotnet/developpement-windows/windows-presentation-foundation/probleme-stathread-wpf/
			this.Dispatcher.Invoke(DispatcherPriority.Background, (ThreadStart)delegate {

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

				//_tb.Text = String.Format("Processing track {0} Side {1}", track, side);

				// flux not in range
				if (dt.fluxData != null) {
					int startFluxIndex = dt.fluxDataRev[0].firstFluxIndex;
					int stopFluxIndex = startFluxIndex + dt.fluxDataRev[0].fluxCount;
					int fluxStartTime = 0;
					if ((dt.histoData.entropy < Global.UNFORMATED_ENTROPY) && (cbOutOfBand.IsChecked == true)) {
						int minDisplayWidth = 50000;
						for (int idx = startFluxIndex; idx < stopFluxIndex; idx++) {
							int flux = dt.fluxData.fluxValue[idx];
							if (fluxNotInRange(flux)) {
								double startAngle = timeAngle(fluxStartTime);
								int j = idx;
								int fluxStopTime = fluxStartTime + flux;
								// try to concatenate to reduce number of arc to draw
								while ((j < stopFluxIndex) && (fluxNotInRange(dt.fluxData.fluxValue[j])))
									fluxStopTime += dt.fluxData.fluxValue[j++];
								if ((fluxStartTime - fluxStopTime) < minDisplayWidth)
									fluxStopTime = fluxStartTime + minDisplayWidth;
								double stopAngle = timeAngle(fluxStopTime);
								System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
								path.StrokeThickness = diskHeavyLine;
								PathGeometry pg = new PathGeometry();
								path.Stroke = White;
								addArc(pg, startAngle, stopAngle, center, radius);
								path.Data = pg;
								grid.Children.Add(path);
								idx = j;
							}
							fluxStartTime += flux;
						}
					}
				}

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
							drawArc(sector, center, radius, timeAngle(idStop), timeAngle(start), Phases.p_gap, grid);

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
							path.Stroke = Yellow;
					}	// bits in band found
					else
						path.Stroke = SlateGray;

					double startAngle = -0.0001;
					double stopAngle = Math.PI * 2;
					addArc(pg, startAngle, stopAngle, center, radius);
					path.Data = pg;
					grid.Children.Add(path);
				}	// no sector track

				// draw NFA
				if ((dt.histoData.entropy < Global.UNFORMATED_ENTROPY) && (cbDiskNFA.IsChecked == true)) {
					for (int i = 0; i < dt.track.info.byteCount; i++) {
						if ((dt.track.buffer.Bytes[i].mfmWord == 0) || (dt.track.buffer.Bytes[i].mfmWord == 0xFFFF)) {
							double startAngle = byteAngle(i - 1, dt.track.info.byteCount);
							int j = i;
							while ((j < dt.track.info.byteCount) && ((dt.track.buffer.Bytes[j].mfmWord == 0) || (dt.track.buffer.Bytes[j].mfmWord == 0xFFFF)))
								j++;
							double stopAngle = byteAngle(j, dt.track.info.byteCount);
							System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
							path.StrokeThickness = diskHeavyLine;
							PathGeometry pg = new PathGeometry();
							path.Stroke = Red;
							addArc(pg, startAngle, stopAngle, center, radius);
							path.Data = pg;
							grid.Children.Add(path);
							i = j;
						}
					}
				}
			});	// dispatcher
			return true;
		}	// End of drawDisk()


		private bool fluxNotInRange(int flux) {
			const int limit = 700;
			if (((flux > (4000 - limit)) && (flux < (4000 + limit))) ||
				((flux > (6000 - limit)) && (flux < (6000 + limit))) ||
				((flux > (8000 - limit)) && (flux < (8000 + limit))))
				return false;
			return true;
		}


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
			//if (stop > start) isLargeArc = false;
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
			Global.saveImage(gridDisk, scale);
		}
		#endregion
	}
}
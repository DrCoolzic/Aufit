/*!
@file DiskWindow.xaml.cs
 * 
<summary>Used to display the Layout of a Disk</summary>

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
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using WD1772Package;
using KFReaderPackage;
using GlobalPackage;
using System.Windows.Threading;
using System.Threading;


namespace Aufit {
/// <summary>
/// Interaction logic for DiskWindow.xaml
/// </summary>
public partial class DiskWindow : Window {
	TextBox _tb;
	Grid _gd;
	const int halfWindowSize = 300;
	const double fineLine = 1.0;
	const double heavyLine = 1.8;
	const double referenceThickness = 0.1;
	//DoubleCollection referenceDashPattern = new DoubleCollection(new double[2] { 10, 10 });
	Point center = new Point(halfWindowSize, halfWindowSize);
	const double rad_max = halfWindowSize - halfWindowSize * 0.03;
	const double rad_start = halfWindowSize / 4;
	const double rad_inc = (rad_max - rad_start) / 84;

	bool _debugNoAsync = false;

	/// <summary>
	/// Create a window and call the draw disk function
	/// </summary>
	public DiskWindow() {
		InitializeComponent();
	}


	/// <summary>
	/// Display the layout of a FD
	/// </summary>
	/// <param name="message">Message to display at end of drawing</param>
	public async void drawDiskLayout(string message) {
		Brush Black = new SolidColorBrush(Colors.Black);
		Black.Freeze();
		Brush DarkSlateGray = new SolidColorBrush(Colors.DarkSlateGray);
		DarkSlateGray.Freeze();
		Brush White = new SolidColorBrush(Colors.White);
		White.Freeze();
		Brush Transparent = new SolidColorBrush(Colors.Transparent);
		Transparent.Freeze();

		int side = 0;
		_tb = tbDisk;
		_gd = cvDisk0;

		double startAngle = -0.0001;
		double stopAngle = Math.PI * 2;
		double radius = halfWindowSize; 

		// add black filled disk
		System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
		path.Fill = Black;
		PathGeometry pg = new PathGeometry();
		addArc(pg, startAngle, stopAngle, center, radius);
		path.Data = pg;
		cvDisk0.Children.Add(path);

		path = new System.Windows.Shapes.Path();
		path.Fill = Black;
		path.Data = pg;
		cvDisk1.Children.Add(path);

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

		// add side indication
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

		for (int track = 0; track < 84; track++) {
			if ((Global.Disk[track, side].fluxData == null) &&
				(Global.Disk[track, side].packedFluxData == null)) continue;
			if (_debugNoAsync) {
				Global.decodeAndRead(track, side, 0);
				drawDiskTrackLayout(track, side, 0);
			}
			else {
				await Global.decodeAndReadAsync(track, side, 0);
				await drawDiskTrackAsync(track, side, 0);
			}
			ButtonMatrix.setButtonBG(track, side);
		}

		side = 1;
		_gd = cvDisk1;
		for (int track = 0; track < 84; track++) {
			if ((Global.Disk[track, side].fluxData == null) &&
				(Global.Disk[track, side].packedFluxData == null)) continue;
			if (_debugNoAsync) {
				Global.decodeAndRead(track, side, 0);
				drawDiskTrackLayout(track, side, 0);
			}
			else {
				await Global.decodeAndReadAsync(track, side, 0);
				await drawDiskTrackAsync(track, side, 0);
			}
			ButtonMatrix.setButtonBG(track, side);
		}

		_tb.Text = message;

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


	//void drawAllDiskTrack() {
	//	Task<bool>[] tasks = new Task<bool>[168];
	//	for (int i = 0; i < 84; i++) {
	//		for (int j = 0; j < 2; j++) {
	//			//if ((Global.Disk[i, j].fluxData == null) && (Global.Disk[i, j].packedFluxData == null)) continue;
	//			tasks[i + j * 84] = drawDiskTrackAsync(i, j, 0);
	//		}
	//	}
	//	Task.WaitAll(tasks);
	//}


	private Task<bool> drawDiskTrackAsync(int track, int side, int rev) {
		return Task<bool>.Run(() => drawDiskTrackLayout(track, side, rev));
	}


	/// <summary>
	/// Draw one track of the FD
	/// </summary>
	/// <param name="track">The track to display</param>
	/// <param name="side">The side of the FD</param>
	/// <param name="rev">The revolution index</param>
	private bool drawDiskTrackLayout(int track, int side, int rev) {
		 Global.DiskTrack dt = Global.Disk[track, side];
		List<Sector> sectors = dt.sectors;
		if (sectors == null) return false;

		// http://www.developpez.net/forums/d966615/dotnet/developpement-windows/windows-presentation-foundation/probleme-stathread-wpf/
		this.Dispatcher.Invoke(DispatcherPriority.Background, (ThreadStart)delegate { 

			Brush LightGray = new SolidColorBrush(Colors.LightGray);
			LightGray.Freeze();
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


			double radius = rad_max - track * rad_inc;
			_tb.Text = String.Format("Processing track {0} Side {1}", track, side);
			Line zeroLine = new Line();
			zeroLine.Stroke = LightGray;
			zeroLine.StrokeThickness = referenceThickness;
			//zeroLine.StrokeDashArray = referenceDashPattern;
			zeroLine.X1 = halfWindowSize;
			zeroLine.Y1 = halfWindowSize;
			zeroLine.X2 = 2*halfWindowSize;
			zeroLine.Y2 = halfWindowSize;
			_gd.Children.Add(zeroLine);

			if (dt.fluxData != null) {
				int startFluxIndex = dt.fluxDataRev[0].firstFluxIndex;
				int stopFluxIndex = startFluxIndex + dt.fluxDataRev[0].fluxCount;
				int fluxStartTime = 0;
				if (dt.histoData.entropy < Global.UNFORMATED_ENTROPY) {
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
							path.StrokeThickness = heavyLine;
							PathGeometry pg = new PathGeometry();
							path.Stroke = White;
							addArc(pg, startAngle, stopAngle, center, radius);
							path.Data = pg;
							_gd.Children.Add(path);
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
					drawArc(sector, center, radius, timeAngle(start), timeAngle(stop), Phases.p_id);

					// Draw gap and data if there
					if (sector.data != null) {
						start = (double)sector.data.info.startTime;
						stop = (double)(sector.data.info.startTime + sector.data.info.timeLength);
						drawArc(sector, center, radius, timeAngle(idStop), timeAngle(start), Phases.p_gap);

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
						drawArc(sector, center, radius, timeAngle(start), timeAngle(stop), Phases.p_data);
					}	// sector has data field
				}	// all sectors
			}	// one or more sectors
			else {
				System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
				path.StrokeThickness = fineLine;
				PathGeometry pg = new PathGeometry();
				path.Stroke = Black;

				if ((dt.histoData.hasBitsInBands) && (dt.histoData.entropy < Global.UNFORMATED_ENTROPY)) {
					if (dt.track.firstSyncOffset > 0)
						path.Stroke = Blue;
					else
						path.Stroke = Yellow;
				}	// bits in band found
				else
					path.Stroke = SlateGray;
			
				double startAngle = - 0.0001;
				double stopAngle = Math.PI * 2;
				addArc(pg, startAngle, stopAngle, center, radius);
				path.Data = pg;
				_gd.Children.Add(path);
			}	// no sector track

			for (int i = 0; i < dt.track.info.byteCount; i++) {
				if ((dt.track.buffer.Bytes[i].mfmWord == 0) || (dt.track.buffer.Bytes[i].mfmWord == 0xFFFF)) {
					double startAngle = byteAngle(i - 1, dt.track.info.byteCount);
					int j = i;
					while ((j < dt.track.info.byteCount) && ((dt.track.buffer.Bytes[j].mfmWord == 0) || (dt.track.buffer.Bytes[j].mfmWord == 0xFFFF)))
						j++;
					double stopAngle = byteAngle(j, dt.track.info.byteCount);
					System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
					path.StrokeThickness = heavyLine;
					PathGeometry pg = new PathGeometry();
					path.Stroke = Red;
					addArc(pg, startAngle, stopAngle, center, radius);
					path.Data = pg;
					_gd.Children.Add(path);
					i = j;
				}
			}
		});	// dispatcher
		return true;

	}	// End of drawDisk()


	private bool fluxNotInRange(int flux) {
		const int limit = 700;
		if (((flux > (4000 - limit)) && (flux < (4000 + limit))) ||
			((flux > (6000 - limit)) && (flux < (6000 + limit))) ||
			((flux > (8000 - limit)) && (flux < (8000 + limit)))			)
			return false;
		return true;
	}


	private void drawArc(Sector sector, Point center, double radius, double startAngle, double stopAngle, Phases phase) {
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
		idPath.StrokeThickness = fineLine;
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
		_gd.Children.Add(idPath);
	}


/* --------------- GONE FOR NOW ---------------
	/// <summary>
	/// Display the Data of a FD
	/// </summary>
	/// <param name="message">Message to display at end of drawing</param>
	public async void drawDiskData(string message) {
		int side = 0;
		_tb = tbDisk0;
		_gd = cvDisk0;
		for (int track = 0; track < 84; track++) {
			if (Global.Disk[track, side].fluxData == null) continue;
			await decodeAndReadAsync(track, side, 0);
			drawDiskTrackData(track, side);
			ButtonMatrix.setButtonBG(track, side);
		}
		message += " Side 0";
		_tb.Text = message;
		side = 1;
		_tb = tbDisk1;
		_gd = cvDisk1;
		for (int track = 0; track < 84; track++) {
			if (Global.Disk[track, side].fluxData == null) continue;
			await decodeAndReadAsync(track, side, 0);
			drawDiskTrackData(track, side);
			ButtonMatrix.setButtonBG(track, side);
		}
		message += " Side 1";
		_tb.Text = message;

	}



	enum Segment {
		sync, nfa, data, id, gap
	}


	/// <summary>
	/// Draw data of one track of the FD
	/// </summary>
	/// <param name="track">The track to display</param>
	/// <param name="side">The side of the FD</param>
	public void drawDiskTrackData(int track, int side) {
		Point center = new Point(halfWindowSize, halfWindowSize);
		double rad_max = halfWindowSize - halfWindowSize * 0.03;
		double rad_start = halfWindowSize / 6;
		double rad_inc = (rad_max - rad_start) / 84;
		double radius = rad_max - track * rad_inc;
		_tb.Text = String.Format("Processing track {0}", track);
		Line zeroLine = new Line();
		zeroLine.Stroke = Brushes.White;
		zeroLine.StrokeThickness = referenceThickness;
		zeroLine.StrokeDashArray = referenceDashPattern;
		zeroLine.X1 = halfWindowSize;
		zeroLine.Y1 = halfWindowSize;
		zeroLine.X2 = halfWindowSize * 2;
		zeroLine.Y2 = halfWindowSize;
		_gd.Children.Add(zeroLine);

		TrackContent tc = Global.Disk[track, side].trackContent;
		double startAngle = byteAngle(0, tc.byteCount);
		TrackBuffer.TrackByte lastByte = tc.buffer.Bytes[tc.byteCount - 1];
		Segment lastSegment = getSegment(tc, tc.byteCount - 1);
		int count = 0;

		for (int i = 0; i < tc.byteCount; i++) {
			count++;
			Segment segment = getSegment(tc, i);
			// print from start to stop if different segment or more than n char in gap/id/data or last char of track
			if ((segment != lastSegment) || 
				((segment != Segment.nfa) && (segment != Segment.sync) && (count > 8) && (tc.buffer.Bytes[i].dataByte != lastByte.dataByte)) || 
				(i == tc.byteCount - 1)) {

				double stopAngle = byteAngle(i - 1, tc.byteCount);
				count = 0;
				SolidColorBrush b = new SolidColorBrush();
				Path path = new Path();
				byte data = lastByte.dataByte;

				if (lastByte.mfmWord == 0x4489) {
					path.StrokeThickness = heavyLine;
					b.Color = Color.FromRgb(255, 0, 0);
				}	// sync in red
				else if (lastByte.mfmWord == 0) {
					path.StrokeThickness = heavyLine;
					b.Color = Color.FromRgb(255, 255, 0);
				}	// NFA in yellow
				else if (lastByte.phase == Phases.p_id) {
					b.Color = Color.FromRgb(0, 0, 255);
					path.StrokeThickness = fineLine;
				}	// id in blue
				else if (lastByte.phase == Phases.p_data) {
					b.Color = Color.FromRgb(data, data, data);
					path.StrokeThickness = fineLine;
				}	// data in grey
				else {
					b.Color = Color.FromRgb(0, data, 0);
					path.StrokeThickness = fineLine;
				}	// gap in green

				PathGeometry pg = new PathGeometry();
				path.Stroke = b;
				addArc(pg, startAngle, stopAngle, center, radius);
				pg.Freeze();
				path.Data = pg;
				_gd.Children.Add(path);

				lastByte = tc.buffer.Bytes[i];
				lastSegment = segment;
				// sync line are too small so we add up 3 bytes width
				if (segment == Segment.sync)
					startAngle = byteAngle(i-4, tc.byteCount);
				else
					startAngle = byteAngle(i, tc.byteCount);
			}
		}	// all bytes
	}	// End of drawDiskTrackData()


	private static Segment getSegment(TrackContent tc, int i) {
		Segment segment;
		segment = (tc.buffer.Bytes[i].mfmWord == 0) ? Segment.nfa :
			(tc.buffer.Bytes[i].mfmWord == 0x4489) ? Segment.sync :
			(tc.buffer.Bytes[i].phase == Phases.p_data) ? Segment.data :
			(tc.buffer.Bytes[i].phase == Phases.p_id) ? Segment.id :
			Segment.gap;
		return segment;
	}

	*/


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
		double scale = 0.7;
		if (rbHigh.IsChecked == true) scale = 2.0;
		else if (rbMed.IsChecked == true) scale = 1.2;
		Global.saveImage(gridDisk, scale);
	}

}	// class DiskWindow
}	// name space

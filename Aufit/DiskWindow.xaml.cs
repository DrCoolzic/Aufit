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
using System.IO;


namespace Aufit {

/// <summary>
/// Interaction logic for DiskWindow.xaml
/// </summary>
public partial class DiskWindow : Window {
	TextBox _tb;
	Grid _gd;
	const int halfWindowSize = 300;
	const double fineLine = 1.5;
	const double heavyLine = 2.2;
	const double referenceThickness = 0.4;
	DoubleCollection referenceDashPattern = new DoubleCollection(new double[2] { 10, 5 });

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
		int side = 0;
		_tb = tbDisk0;
		_gd = cvDisk0;

		double startAngle = -0.0001;
		double stopAngle = Math.PI * 2;
		Point center = new Point(halfWindowSize, halfWindowSize);
		double radius = halfWindowSize; 

		System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
		//path.StrokeThickness = fineLine;
		//path.Stroke = Brushes.Black;
		path.Fill = Brushes.Black;

		PathGeometry pg = new PathGeometry();
		addArc(pg, startAngle, stopAngle, center, radius);
		path.Data = pg;
		cvDisk0.Children.Add(path);

		path = new System.Windows.Shapes.Path();
		path.Fill = Brushes.Black;
		path.Data = pg;
		cvDisk1.Children.Add(path);

		radius /= 7;
		path = new System.Windows.Shapes.Path();
		path.Fill = Brushes.White;		
		
		PathGeometry hole = new PathGeometry();		
		addArc(hole, startAngle, stopAngle, center, radius);
		path.Data = hole;
		cvDisk0.Children.Add(path);

		path = new System.Windows.Shapes.Path();
		path.Fill = Brushes.White;
		path.Data = hole;
		cvDisk1.Children.Add(path);


		for (int track = 0; track < 84; track++) {
			if ((Global.Disk[track, side].fluxData == null) &&
				(Global.Disk[track, side].packedFluxData == null)) continue;
			if (_debugNoAsync)
				decodeAndRead(track, side, 0);
			else
				await decodeAndReadAsync(track, side, 0);
			drawDiskTrackLayout(track, side, 0);
			ButtonMatrix.setButtonBG(track, side);
		}
		message += " Side 0";
		_tb.Text = message;
		side = 1;
		_tb = tbDisk1;
		_gd = cvDisk1;
		for (int track = 0; track < 84; track++) {
			if ((Global.Disk[track, side].fluxData == null) &&
				(Global.Disk[track, side].packedFluxData == null)) continue;
			if (_debugNoAsync)
				decodeAndRead(track, side, 0);
			else
				await decodeAndReadAsync(track, side, 0);
			drawDiskTrackLayout(track, side, 0);
			ButtonMatrix.setButtonBG(track, side);
		}
		message += " Side 1";
		_tb.Text = message;
	}



	/// <summary>
	/// This is an asynchronous version of decodeAndRead()
	/// </summary>
	/// <param name="track">The track to read and decode</param>
	/// <param name="side">The side to read and decode</param>
	/// <param name="rev">The revolution to read and decode</param>
	/// <returns>A pointer to the TrackContent structure</returns>
	public static Task<List<Sector>> decodeAndReadAsync(int track, int side, int rev) {
		return Task<List<Sector>>.Run(() => decodeAndRead(track, side, rev));
	}


	/// <summary>
	/// Decode the flux transitions and do all FDC read commands
	/// </summary>
	/// <remarks>To display the layout we must make sure the flux are correctly
	/// decoded and that all the read functions (readTrack(), readAddr(), and 
	/// readSect() functions are done before we display.</remarks>
	/// <param name="track">The track to read and decode</param>
	/// <param name="side">The side to read and decode</param>
	/// <param name="rev">The revolution to read and decode</param>
	/// <returns>A pointer to the TrackContent structure</returns>
	public static List<Sector> decodeAndRead(int track, int side, int rev) {
		if ((Global.Disk[track, side].fluxData == null) &&
			(Global.Disk[track, side].packedFluxData == null)) return null;
		List<Sector> sectors = Global.Disk[track, side].sectors;
		if (sectors != null) return sectors;
		Global.mfmDecode(track, side);
		FDC.fdcRealAll(track, side, rev);
		sectors = Global.Disk[track, side].sectors;
		return sectors;
	}


	/// <summary>
	/// Draw one track of the FD
	/// </summary>
	/// <param name="track">The track to display</param>
	/// <param name="side">The side of the FD</param>
	public void drawDiskTrackLayout(int track, int side, int rev) {
		 Global.DiskTrack dt = Global.Disk[track, side];
		List<Sector> sectors = dt.sectors;
		if (sectors == null) return;

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
		zeroLine.X2 = 2*halfWindowSize;
		zeroLine.Y2 = halfWindowSize;
		_gd.Children.Add(zeroLine);

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
			path.Stroke = Brushes.Black;

			if ((dt.histoData.hasBitsInBands) && (dt.histoData.entropy < Global.UNFORMATED_ENTROPY)) {
				if (dt.track.firstSyncOffset > 0)
					path.Stroke = Brushes.Blue;
				else
					path.Stroke = Brushes.Yellow;
			}	// bits in band found
			else
				path.Stroke = Brushes.SlateGray;
			
			double startAngle = - 0.0001;
			double stopAngle = Math.PI * 2;
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			_gd.Children.Add(path);
		}	// no sector track

		for (int i = 0; i < dt.track.info.byteCount; i++) {
			double startAngle = byteAngle(i - 1, dt.track.info.byteCount);
			double stopAngle = byteAngle(i, dt.track.info.byteCount);
			if ((dt.track.buffer.Bytes[i].mfmWord == 0) || (dt.track.buffer.Bytes[i].mfmWord == 0xFFFF)) {
				System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
				path.StrokeThickness = heavyLine;
				PathGeometry pg = new PathGeometry();
				path.Stroke = Brushes.Red;
				addArc(pg, startAngle, stopAngle, center, radius);
				path.Data = pg;
				_gd.Children.Add(path);
			}
		}

	}	// End of drawDisk()


	private void drawArc(Sector sector, Point center, double radius, double startAngle, double stopAngle, Phases phase) {
		System.Windows.Shapes.Path idPath = new System.Windows.Shapes.Path();
		idPath.StrokeThickness = fineLine;
		idPath.Stroke = Brushes.LightGreen;

		if (phase == Phases.p_id)
			idPath.Stroke = Brushes.Yellow;
		if (phase == Phases.p_data) {
			idPath.Stroke = Brushes.DeepSkyBlue;
			if (Bit.testBits(sector.flags, Sector.WIDTH_ABOVE))
				idPath.Stroke = Brushes.Blue;
			if (Bit.testBits(sector.flags, Sector.WIDTH_BELOW))
				idPath.Stroke = Brushes.Aqua;
			if (Bit.testBits(sector.flags, Sector.MACRODOS))
				idPath.Stroke = Brushes.Purple;
			if ((sector.data.info.crc != 0) || (sector.data.fuzzy != null))
				idPath.Stroke = Brushes.Orange;
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
		//double angle = Math.PI / 2 + Math.PI * time / 100000000.0;
		double angle = 2 * Math.PI - (Math.PI * time / 100000000.0);
		//while (angle > 2 * Math.PI) angle -= 2 * Math.PI;
		return angle;
	}

	private double byteAngle(int pos, int count) {
		//double angle = Math.PI / 2 + 2 * Math.PI * (double)pos / (double)count;
		double angle = 2 * Math.PI * (double)pos / (double)count;
		return angle;
	}

	private void saveImageClick0(object sender, RoutedEventArgs e) {
		CreateBitmapFromVisual(cvDisk0, "U:\\test0.png");
	}

	private void saveImageClick1(object sender, RoutedEventArgs e) {
		CreateBitmapFromVisual(cvDisk1, "U:\\test1.png");
	}

	// http://mcleodsean.wordpress.com/2008/10/07/bitmap-snapshots-of-wpf-visuals/
	void CreateBitmapFromVisual(Visual target, string filename) {
		if (target == null) return;

		Rect bounds = VisualTreeHelper.GetDescendantBounds(target);

		int scale = 3;
		DrawingVisual dv = new DrawingVisual();
		using (DrawingContext dc = dv.RenderOpen()) {
			dv.Transform = new ScaleTransform(scale, scale);
			VisualBrush vb = new VisualBrush(target);
			dc.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
		}

		RenderTargetBitmap rtb = new RenderTargetBitmap(scale * (Int32)bounds.Width, scale * (Int32)bounds.Height, 96, 96, PixelFormats.Pbgra32);
		rtb.Render(dv);

		PngBitmapEncoder png = new PngBitmapEncoder();
		png.Frames.Add(BitmapFrame.Create(rtb));

		using (Stream stm = File.Create(filename)) {
			png.Save(stm);
		}
	}

}	// class DiskWindow
}	// name space

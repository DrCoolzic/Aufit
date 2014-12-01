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

namespace Aufit {

/// <summary>
/// Interaction logic for DiskWindow.xaml
/// </summary>
public partial class DiskWindow : Window {
	TextBox _tb;
	Grid _gd;
	const int halfWindowSize = 300;
	const double fineLine = 1.6;
	const double heavyLine = 2.0;
	const double gridThickness = 0.4;
	DoubleCollection gridDash = new DoubleCollection(new double[2] { 10, 5 });

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
		for (int track = 0; track < 84; track++) {
			if (Global.Disk[track, side].fluxData == null) continue;
			await decodeAndReadAsync(track, side, 1);
			drawDiskTrackLayout(track, side);
			ButtonMatrix.setButtonBG(track, side);
		}
		message += " Side 0";
		_tb.Text = message;
		side = 1;
		_tb = tbDisk1;
		_gd = cvDisk1;
		for (int track = 0; track < 84; track++) {
			if (Global.Disk[track, side].fluxData == null) continue;
			await decodeAndReadAsync(track, side, 1);
			drawDiskTrackLayout(track, side);
			ButtonMatrix.setButtonBG(track, side);
		}
		message += " Side 1";
		_tb.Text = message;
	}


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
			await decodeAndReadAsync(track, side, 1);
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
			await decodeAndReadAsync(track, side, 1);
			drawDiskTrackData(track, side);
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
	public static Task<TrackContent> decodeAndReadAsync(int track, int side, int rev) {
		return Task<TrackContent>.Run(() => decodeAndRead(track, side, rev));
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
	public static TrackContent decodeAndRead(int track, int side, int rev) {
		if (Global.Disk[track, side].fluxData == null) return null;
		TrackContent trackInfo = Global.Disk[track, side].trackContent;
		if (trackInfo != null) return trackInfo;
		Global.mfmDecode(track, side);
		FDC.fdcRealAll(track, side, rev);
		trackInfo = Global.Disk[track, side].trackContent;
		return trackInfo;
	}


	/// <summary>
	/// Draw one track of the FD
	/// </summary>
	/// <param name="track">The track to display</param>
	/// <param name="side">The side of the FD</param>
	public void drawDiskTrackLayout(int track, int side) {
		TrackContent tc = Global.Disk[track, side].trackContent;
		Point center = new Point(halfWindowSize, halfWindowSize);
		double rad_max = halfWindowSize - halfWindowSize * 0.03;
		double rad_start = halfWindowSize / 6;
		double rad_inc = (rad_max - rad_start) / 84;
		double radius = rad_max - track * rad_inc;
		_tb.Text = String.Format("Processing track {0}", track);
		Line zeroLine = new Line();
		zeroLine.Stroke = Brushes.White;
		zeroLine.StrokeThickness = gridThickness;
		zeroLine.StrokeDashArray = gridDash;
		zeroLine.X1 = halfWindowSize;
		zeroLine.Y1 = halfWindowSize;
		zeroLine.X2 = 2*halfWindowSize;
		zeroLine.Y2 = halfWindowSize;
		_gd.Children.Add(zeroLine);

		if (tc.sectors.Count > 1) {
			for (int i = 0; i < tc.sectors.Count; i++) {
				SectorInfo sector = tc.sectors[i];
				IDField id = new IDField();
				SectorContent s = new SectorContent();
				if (i > 0) {
					id = Global.Disk[track, side].addrContent.ids[i - 1];
					s = FDC.findSectorContent(track, side, id.number, sector.startTime);
				}

				double startAngle = 0;
				double stopAngle = 0;

				for (byte p = 0; p < 4; p++) {
					if (sector.phases[p].byteCount > 0) {
						Path path = new Path();
						path.StrokeThickness = fineLine;
						PathGeometry pg = new PathGeometry();
						startAngle = timeAngle((double)sector.phases[p].startTime);
						stopAngle = timeAngle((double)(sector.phases[p].startTime + sector.phases[p].timeLength));

						switch (p) {
							case (byte)Phases.p_id:
								path.Stroke = Brushes.Blue;
								addArc(pg, startAngle, stopAngle, center, radius);
								// if (sector.phases[p].crc != 0) plg.Fill = Brushes.Orange;
								break;

							case (byte)Phases.p_gap:
								path.Stroke = Brushes.LightGreen;
								addArc(pg, startAngle, stopAngle, center, radius);
								break;

							case (byte)Phases.p_data:
								path.Stroke = Brushes.DeepSkyBlue;

								if (sector.phases[p].overClock > Global.sectSize(sector.addr.size) / 2)
									path.Stroke = Brushes.Blue;
								if (sector.phases[p].underClock > Global.sectSize(sector.addr.size) / 2)
									path.Stroke = Brushes.Aqua;
								if ((sector.phases[p].underClock > Global.sectSize(sector.addr.size) / 8) && (sector.phases[p].overClock > Global.sectSize(sector.addr.size) / 8))
									path.Stroke = Brushes.BlueViolet;

								if ((i > 0) && (s != null) && ((s.dataBlock.crc != 0) || (s.fuzzyMask != null))) {
									path.StrokeThickness = heavyLine;
									path.Stroke = Brushes.Red;
									//Path patherr = new Path();
									//patherr.StrokeThickness = 5;
									//patherr.Stroke = Brushes.Red;
									//PathGeometry perr = new PathGeometry();
									//addArc(perr, startAngle, stopAngle, center, radius);
									//patherr.Data = perr;
									//cvDisk.Children.Add(patherr);

								//	if (s.fuzzyMask != null) {
								//		int nbFuzzy = 0;
								//		for (int j = 0; j < s.fuzzyMask.Bytes.Count; j++) {
								//			if (s.fuzzyMask.Bytes[j].dataByte != 0) 
								//				nbFuzzy++;
								//		}
								//		if (nbFuzzy > (int)((double)s.fuzzyMask.Bytes.Count * 0.6))
								//			path.Stroke = Brushes.DarkSlateGray;
								//	}	// has many fuzzy = unformatted ?
								}	// crc error or fuzzy bytes
								addArc(pg, startAngle, stopAngle, center, radius);
								break;

							//case (byte)Phases.p_gap:
							//	path.Stroke = Brushes.Green;
							//	addArc(pg, startAngle, stopAngle, center, radius);
							//	break;
							default:
								//plg.Fill = Brushes.Red;
								break;
						}
						path.Data = pg;
						_gd.Children.Add(path);
					}	// non null phase
				}	// all phases
			}	// all sectors
		}	// one or more sectors
		else {
			double startAngle = 0;
			double stopAngle = Math.PI *2 - 0.0001;
			Path path = new Path();
			path.StrokeThickness = heavyLine;
			PathGeometry pg = new PathGeometry();
			path.Stroke = Brushes.DarkSlateGray;
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			_gd.Children.Add(path);
		}	// no sector

		//// display sync
		//foreach (SyncInfo sync in tc.syncs) {
		//	double startAngle = timeAngle(sync.time);
		//	double stopAngle = timeAngle(sync.time + 32000);
		//	Path path = new Path();
		//	path.StrokeThickness = 3;
		//	PathGeometry pg = new PathGeometry();
		//	path.Stroke = Brushes.Black;
		//	addArc(pg, startAngle, stopAngle, center, radius);
		//	path.Data = pg;
		//	cvDisk.Children.Add(path);
		//}


		for (int i = 0; i < tc.byteCount; i++) {
			//double startAngle = timeAngle(tc.buffer.Bytes[i].startTime);
			//double stopAngle = timeAngle(tc.buffer.Bytes[i].startTime + 32000);
			double startAngle = byteAngle(i - 1, tc.byteCount);
			double stopAngle = byteAngle(i, tc.byteCount);
			if (tc.buffer.Bytes[i].mfmWord == 0x4489) {
				Path path = new Path();
				path.StrokeThickness = heavyLine;
				PathGeometry pg = new PathGeometry();
				path.Stroke = Brushes.Red;
				addArc(pg, startAngle, stopAngle, center, radius);
				path.Data = pg;
				_gd.Children.Add(path);
			}
			else if (tc.buffer.Bytes[i].mfmWord == 0) {
				Path path = new Path();
				path.StrokeThickness = heavyLine;
				PathGeometry pg = new PathGeometry();
				path.Stroke = Brushes.Yellow;
				addArc(pg, startAngle, stopAngle, center, radius);
				path.Data = pg;
				_gd.Children.Add(path);
			}
		}

	}	// End of drawDisk()

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
		zeroLine.StrokeThickness = gridThickness;
		zeroLine.StrokeDashArray = gridDash;
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
		SweepDirection direction = SweepDirection.Clockwise;
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
		double angle = Math.PI * time / 100000000.0;
		//while (angle > 2 * Math.PI) angle -= 2 * Math.PI;
		return angle;
	}

	private double byteAngle(int pos, int count) {
		//double angle = Math.PI / 2 + 2 * Math.PI * (double)pos / (double)count;
		double angle = 2 * Math.PI * (double)pos / (double)count;
		return angle;
	}

	//// TESTS BELOW - does not seems faster
	//public void drawDiskTrackData2(int track, int side) {
	//	Point center = new Point(450, 450);
	//	double rad_max = 400;
	//	double rad_start = 80;
	//	double rad_inc = (rad_max - rad_start) / 85;
	//	double radius = rad_max - track * rad_inc;
	//	tbDisk.Text = String.Format("Processing track {0}", track);

	//	TrackContent tc = Global.Disk[track, side].trackContent;
	//	int count = 0;
	//	for (int i = 0; i < tc.byteCount; i++) {
	//		count++;
	//		//if ((tc.buffer.Bytes[i].mfmWord != 0x4489) && (tc.buffer.Bytes[i].mfmWord != 0) && (i % 3 != 0)) continue;
	//		//if ((i % 10 != 0) || (tc.buffer.Bytes[i].mfmWord != 0x4489)) continue;
	//		if (i % 3 != 0) continue;

	//		double startAngle = timeAngle(tc.buffer.Bytes[i].startTime);
	//		double stopAngle = timeAngle(tc.buffer.Bytes[i].startTime + (32000 * (count - 1)));
	//		count = 0;
	//		SolidColorBrush b = new SolidColorBrush();
	//		Path path = new Path();

	//		path.StrokeThickness = 5;
	//		byte data = tc.buffer.Bytes[i].dataByte;
	//		if (tc.buffer.Bytes[i].mfmWord == 0x4489) {
	//			b.Color = Color.FromRgb(255, 0, 0);
	//			//Canvas.SetZIndex(path, 99);
	//		}
	//		else if (tc.buffer.Bytes[i].mfmWord == 0)
	//			b.Color = Color.FromRgb(255, 255, 0);

	//		else if (tc.buffer.Bytes[i].phase == Phases.p_data) {
	//			b.Color = Color.FromRgb(0, data, data);
	//			path.StrokeThickness = 3;
	//		}
	//		else {
	//			b.Color = Color.FromRgb(data, data, data);
	//			path.StrokeThickness = 3;
	//		}
	//		path.Stroke = b;
	//		StreamGeometry geometry = new StreamGeometry();
	//		addArc2(geometry, startAngle, stopAngle, center, radius);
	//		geometry.Freeze();
	//		path.Data = geometry;
	//		cvDisk.Children.Add(path);
	//	}
	//}	// End of drawDiskTrackData()

	//private void addArc2(StreamGeometry sg, double start, double stop, Point center, double radius) {
	//	Point startPoint = center + new Vector(Math.Cos(start), Math.Sin(start)) * radius;
	//	Point stopPoint = center + new Vector(Math.Cos(stop), Math.Sin(stop)) * radius;
	//	SweepDirection direction = SweepDirection.Clockwise;
	//	bool isLargeArc = false;
	//	if ((stop - start) > Math.PI) isLargeArc = true;
	//	using (StreamGeometryContext ctx = sg.Open()) {
	//		ctx.BeginFigure(startPoint, true /* is filled */, false /* is closed */);
	//		ctx.ArcTo(stopPoint, new Size(radius, radius), 0, isLargeArc, direction, true, false);
	//	}
	//	PathFigure pf = new PathFigure();
	//}

}	// class DiskWindow
}	// name space

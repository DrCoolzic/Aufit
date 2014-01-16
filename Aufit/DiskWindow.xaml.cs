/*!
@file DiskWindow.xaml.cs
 * 
<summary>Used to display the Layout of a Disk</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
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

using WD1772Space;
using KFReaderSpace;
using GlobalSpace;

namespace Aufit {

/// <summary>
/// Interaction logic for DiskWindow.xaml
/// </summary>
public partial class DiskWindow : Window {
	/// <summary>
	/// Create a window and call the draw disk function
	/// </summary>
	public DiskWindow() {
		InitializeComponent();
	}

	/// <summary>
	/// Display the layout of a FD
	/// </summary>
	/// <param name="side">Side of the FD</param>
	/// <param name="message">Message to display at end of drawing</param>
	public async void drawDiskLayout(int side, string message) {
		for (int track = 0; track < 84; track++) {
			if (Global.Disk[track, side].fluxData == null) continue;
			await decodeAndReadAsync(track, side, 1);
			drawDiskTrack(track, side);
			ButtonMatrix.setButtonBG(track, side);
		}
		tbDisk.Text = message;
	}


	/// <summary>
	/// Display the Data of a FD
	/// </summary>
	/// <param name="side">Side of the FD</param>
	/// <param name="message">Message to display at end of drawing</param>
	public async void drawDiskData(int side, string message) {
		for (int track = 0; track < 84; track++) {
			if (Global.Disk[track, side].fluxData == null) continue;
			await decodeAndReadAsync(track, side, 1);
			drawDiskTrackData(track, side);
			ButtonMatrix.setButtonBG(track, side);
		}
		tbDisk.Text = message;
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
		FDC.fdcAllRead(track, side, rev);
		trackInfo = Global.Disk[track, side].trackContent;
		return trackInfo;
	}


	/// <summary>
	/// Draw one track of the FD
	/// </summary>
	/// <param name="track">The track to display</param>
	/// <param name="side">The side of the FD</param>
	public void drawDiskTrack(int track, int side) {
		TrackContent tc = Global.Disk[track, side].trackContent;
		Point center = new Point(450, 450);
		double rad_max = 400;
		double rad_start = 80;
		double rad_inc = (rad_max - rad_start) / 85;
		double radius = rad_max - track * rad_inc;
		tbDisk.Text = String.Format("Processing track {0}", track);

		if (tc.sectors.Count > 1) {
			for (int i = 0; i < tc.sectors.Count; i++) {
				SectorInfo sector = tc.sectors[i];
				IDField id = new IDField();
				SectorContent s = new SectorContent();	// TODO why not null?
				if (i > 0) {
					id = Global.Disk[track, side].addrContent.ids[i - 1];
					s = FDC.findSectorContent(track, side, id.number, sector.startTime);
				}

				double startAngle = 0;
				double stopAngle = 0;

				for (byte p = 0; p < 4; p++) {
					if (sector.phases[p].byteCount > 0) {
						Path path = new Path();
						path.StrokeThickness = 3;
						PathGeometry pg = new PathGeometry();
						startAngle = timeAngle((double)sector.phases[p].startTime);
						stopAngle = timeAngle((double)(sector.phases[p].startTime + sector.phases[p].timeLength));

						switch (p) {
							case (byte)Phases.p_id:
								path.Stroke = Brushes.White;
								addArc(pg, startAngle, stopAngle, center, radius);
								// if (sector.phases[p].crc != 0) plg.Fill = Brushes.Orange;
								break;

							case (byte)Phases.p_inner:
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

								if ((i > 0) && (s != null)) {

									if ((s.dataBlock.crc != 0) || (s.fuzzyMask != null)) {
										path.StrokeThickness = 5;
										path.Stroke = Brushes.Red;
										//Path patherr = new Path();
										//patherr.StrokeThickness = 5;
										//patherr.Stroke = Brushes.Red;
										//PathGeometry perr = new PathGeometry();
										//addArc(perr, startAngle, stopAngle, center, radius);
										//patherr.Data = perr;
										//cvDisk.Children.Add(patherr);

										if (s.fuzzyMask != null) {
											int nbFuzzy = 0;
											for (int j = 0; j < s.fuzzyMask.Bytes.Count; j++) {
												if (s.fuzzyMask.Bytes[j].dataByte != 0) nbFuzzy++;
											}
											if (nbFuzzy > (int)((double)s.fuzzyMask.Bytes.Count * 0.6)) 
												path.Stroke = Brushes.DarkSlateGray;
										}

									}
								}
								addArc(pg, startAngle, stopAngle, center, radius);
								break;

							case (byte)Phases.p_outer:
								path.Stroke = Brushes.Green;
								addArc(pg, startAngle, stopAngle, center, radius);
								break;
							default:
								//plg.Fill = Brushes.Red;
								break;
						}
						path.Data = pg;
						cvDisk.Children.Add(path);
					}	// non null phase
				}	// all phases
			}	// all sectors
		}	// one or more sectors
		else {
			double startAngle = 0;
			double stopAngle = Math.PI *2 - 0.0001;
			Path path = new Path();
			path.StrokeThickness = 3;
			PathGeometry pg = new PathGeometry();
			path.Stroke = Brushes.DarkSlateGray;
			addArc(pg, startAngle, stopAngle, center, radius);
			path.Data = pg;
			cvDisk.Children.Add(path);
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
			if (tc.buffer.Bytes[i].mfmWord == 0x4489) {
				double startAngle = timeAngle(tc.buffer.Bytes[i].startTime);
				double stopAngle = timeAngle(tc.buffer.Bytes[i].startTime + 32000);
				Path path = new Path();
				path.StrokeThickness = 3;
				PathGeometry pg = new PathGeometry();
				path.Stroke = Brushes.Black;
				addArc(pg, startAngle, stopAngle, center, radius);
				path.Data = pg;
				cvDisk.Children.Add(path);

			}
			else if (tc.buffer.Bytes[i].mfmWord == 0) {
				double startAngle = timeAngle(tc.buffer.Bytes[i].startTime);
				double stopAngle = timeAngle(tc.buffer.Bytes[i].startTime + 32000);
				Path path = new Path();
				path.StrokeThickness = 3;
				PathGeometry pg = new PathGeometry();
				path.Stroke = Brushes.Yellow;
				addArc(pg, startAngle, stopAngle, center, radius);
				path.Data = pg;
				cvDisk.Children.Add(path);
			}
		}

	}	// End of drawDisk()


	/// <summary>
	/// Draw data of one track of the FD
	/// </summary>
	/// <param name="track">The track to display</param>
	/// <param name="side">The side of the FD</param>
	public void drawDiskTrackData(int track, int side) {
		Point center = new Point(450, 450);
		double rad_max = 400;
		double rad_start = 80;
		double rad_inc = (rad_max - rad_start) / 85;
		double radius = rad_max - track * rad_inc;
		tbDisk.Text = String.Format("Processing track {0}", track);

		TrackContent tc = Global.Disk[track, side].trackContent;
		int count = 0;
		for (int i = 0; i < tc.byteCount; i++) {
			count++;
			//if ((tc.buffer.Bytes[i].mfmWord != 0x4489) && (tc.buffer.Bytes[i].mfmWord != 0) && (i % 3 != 0)) continue;
			if (i % 3 != 0) continue;

			double startAngle = timeAngle(tc.buffer.Bytes[i].startTime);
			double stopAngle = timeAngle(tc.buffer.Bytes[i].startTime + (32000 * count));
			count = 0;
			SolidColorBrush b = new SolidColorBrush();
			Path path = new Path();

			byte data = tc.buffer.Bytes[i].dataByte;
			if (tc.buffer.Bytes[i].mfmWord == 0x4489) {
				path.StrokeThickness = 5;				
				b.Color = Color.FromRgb(255, 0, 0);
				//Canvas.SetZIndex(path, 99);
			}
			else if (tc.buffer.Bytes[i].mfmWord == 0) {
				path.StrokeThickness = 5;
				b.Color = Color.FromRgb(255, 255, 0);
			}
			else if (tc.buffer.Bytes[i].phase == Phases.p_id) {
				b.Color = Color.FromRgb(0, 0, data);
				path.StrokeThickness = 3;
			}
			else if (tc.buffer.Bytes[i].phase == Phases.p_data) {
				b.Color = Color.FromRgb(0, data, data);
				path.StrokeThickness = 3;
			}
			else {
				b.Color = Color.FromRgb(data, data, data);
				path.StrokeThickness = 3;
			}

			PathGeometry pg = new PathGeometry();
			path.Stroke = b;
			addArc(pg, startAngle, stopAngle, center, radius);
			pg.Freeze();
			path.Data = pg;
			cvDisk.Children.Add(path);
		}
	}	// End of drawDiskTrackData()


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
		double angle = Math.PI / 2 + Math.PI * time / 100000000.0;
		//while (angle > 2 * Math.PI) angle -= 2 * Math.PI;
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

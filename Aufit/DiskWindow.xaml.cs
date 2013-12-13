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

using WD1772NS;
using StreamNS;

namespace Aufit {
	/// <summary>
	/// Interaction logic for DiskWindow.xaml
	/// </summary>
	public partial class DiskWindow : Window {
		public DiskWindow() {
			InitializeComponent();
			drawDisk();
		}

		public async void drawDisk() {
			FDC disk = new FDC();
			ProcessStream.Rev = 1;
			// ProcessStream.Side = 0; pick current one
			for (int track = 0; track < 84; track++) {
				ProcessStream.Track = track;
				ProcessStream.TrackData currentTrack = ProcessStream.currentTrack();
				ReadTrackInfo trackInfo = await decodeAndReadAsync(disk);
				drawDiskTrack(trackInfo, track);
			}
		}


		public Task<ReadTrackInfo> decodeAndReadAsync(FDC disk) {
			return Task<ReadTrackInfo>.Run(() => decodeAndRead(disk));
		}


		public ReadTrackInfo decodeAndRead(FDC disk) {
			ProcessStream.mfmDecode();
			ReadTrackInfo trackInfo = disk.readTrack();
			return trackInfo;
		}

		public void drawDiskTrack(ReadTrackInfo trackInfo, int track) {
			Point center = new Point(450, 450);
			double rad_max = 400;
			double rad_start = 80;
			double rad_inc = (rad_max - rad_start) / 85;
			double radius = rad_max - track * rad_inc;
;
		if (trackInfo.sectors.Count > 1) foreach (SectorInfo sector in trackInfo.sectors) {				
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
								path.Stroke = Brushes.Yellow;
								addArc(pg, startAngle, stopAngle, center, radius);
								// if (sector.phases[p].crc != 0) plg.Fill = Brushes.Orange;
								break;

							case (byte)Phases.p_inner:
								path.Stroke = Brushes.LightGreen;
								addArc(pg, startAngle, stopAngle, center, radius);
								break;

							case (byte)Phases.p_data:
								path.Stroke = Brushes.DeepSkyBlue;
								addArc(pg, startAngle, stopAngle, center, radius);
								// if (sector.phases[p].crc != 0) plg.Fill = Brushes.Plum;
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


		bool dispSync = false;
			if (dispSync)
			foreach (SyncInfo sync in trackInfo.syncs) {
				double startAngle = timeAngle(sync.time);
				double stopAngle = timeAngle(sync.time + 32000);
				Path path = new Path();
				path.StrokeThickness = 3;
				PathGeometry pg = new PathGeometry();
				path.Stroke = Brushes.Black;
				addArc(pg, startAngle, stopAngle, center, radius);
				path.Data = pg;
				cvDisk.Children.Add(path);
			}
		}	// End of drawDisk()



		private void addArc(PathGeometry pg, double start, double stop, Point center, double radius) {
			Point startPoint = center + new Vector(Math.Cos(start), Math.Sin(start)) * radius;
			Point stopPoint = center + new Vector(Math.Cos(stop), Math.Sin(stop)) * radius;
			SweepDirection direction = SweepDirection.Clockwise;
			bool isLargeArc = false;
			if ((stop - start) > Math.PI)
				isLargeArc = true;

			ArcSegment aseg = new ArcSegment(stopPoint, new Size(radius, radius), 0, isLargeArc, direction, true);
			PathFigure pf = new PathFigure();
			pf.IsClosed = false;
			pf.StartPoint = startPoint;
			pf.Segments.Add(aseg);
			pg.Figures.Add(pf);
		}


		private double timeAngle(double time) {
			double angle = Math.PI / 2 + Math.PI * time / 100000000.0;
			//while (angle > 2 * Math.PI) angle -= 2 * Math.PI;
			return angle;
		}

	}	// class DiskWindow
}

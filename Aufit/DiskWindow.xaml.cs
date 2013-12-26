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
using KFReaderNS;

namespace Aufit {
	/// <summary>
	/// Interaction logic for DiskWindow.xaml
	/// </summary>
	public partial class DiskWindow : Window {
		public DiskWindow(string message) {
			InitializeComponent();
			drawDisk(message);
		}

		public async void drawDisk(string message) {
			int saveRev = Global.Rev;
			int saveTrack = Global.Track;

			Global.Rev = 1;
			for (int track = 0; track < 84; track++) {
				Global.Track = track;
				TrackContent tc = Global.Disk[Global.Track, Global.Side].trackContent;
				if (Global.Disk[Global.Track, Global.Side].fluxData == null) continue;
				tc = await decodeAndReadAsync();
				drawDiskTrack(tc, track, message);
				ButtonMatrix.setButtonBG(Global.Track, Global.Side);

			}
			Global.Track = saveTrack;
			Global.Rev = saveRev;
			tbDisk.Text = message;
		}


		public Task<TrackContent> decodeAndReadAsync() {
			return Task<TrackContent>.Run(() => decodeAndRead());
		}


		public TrackContent decodeAndRead() {
			TrackContent trackInfo = Global.Disk[Global.Track, Global.Side].trackContent;
			if (trackInfo != null) return trackInfo;
			ProcessStream.mfmDecode();
			FDC.readAll(Global.Track, Global.Side, Global.Side);
			trackInfo = Global.Disk[Global.Track, Global.Side].trackContent;
			return trackInfo;
		}


		public void drawDiskTrack(TrackContent trackInfo, int track, string message) {
			Point center = new Point(450, 450);
			double rad_max = 400;
			double rad_start = 80;
			double rad_inc = (rad_max - rad_start) / 85;
			double radius = rad_max - track * rad_inc;
			tbDisk.Text = String.Format("Processing track {0}", track);

			if (trackInfo.sectors.Count > 1) {
				for (int i = 0; i < trackInfo.sectors.Count; i++) {
					SectorInfo sector = trackInfo.sectors[i];
					IDField id = new IDField();
					SectorContent s = new SectorContent();
					if (i > 0) {
						id = Global.Disk[Global.Track, Global.Side].addrContent.ids[i - 1];
						s = FDC.findSectorContent(Global.Track, Global.Side, id.number, sector.startTime);
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

									if (sector.phases[p].overClock > DSR.actualSize(sector.addr.size) / 2)
										path.Stroke = Brushes.Blue;
									if (sector.phases[p].underClock > DSR.actualSize(sector.addr.size) / 2)
										path.Stroke = Brushes.Aqua;
									if ((sector.phases[p].underClock > DSR.actualSize(sector.addr.size) / 8) && (sector.phases[p].overClock > DSR.actualSize(sector.addr.size) / 8))
										path.Stroke = Brushes.BlueViolet;

									if ((i > 0) && (s != null)) {

										if ((s.dataBlock.crc != 0) || (s.mask != null)) {
											path.StrokeThickness = 5;
											path.Stroke = Brushes.Red;
											//Path patherr = new Path();
											//patherr.StrokeThickness = 5;
											//patherr.Stroke = Brushes.Red;
											//PathGeometry perr = new PathGeometry();
											//addArc(perr, startAngle, stopAngle, center, radius);
											//patherr.Data = perr;
											//cvDisk.Children.Add(patherr);

											if (s.mask != null) {
												int nbFuzzy = 0;
												for (int j = 0; j < s.mask.Bytes.Count; j++) {
													if (s.mask.Bytes[j].dataByte != 0) nbFuzzy++;
												}
												if (nbFuzzy > (int)((double)s.mask.Bytes.Count * 0.6)) path.Stroke = Brushes.DarkSlateGray;
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
			}
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
			}


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

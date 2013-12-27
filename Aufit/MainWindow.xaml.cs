/*!
@file MainWindow.xaml.cs
 * 
<summary>The Main Window for the AUFIT program</summary>

<div class="jlg">Copyright (C) 2011 Jean Louis-Guerin\n\n
This file is part of the Atari Universal FD Image Tool project.\n\n
The Atari Universal FD Image Tool project may be used and distributed without restriction provided
that this copyright statement is not removed from the file and that any
derivative work contains the original copyright notice and the associated
disclaimer.\n\n
The Atari Universal FD Image Tool project is free software; you can redistribute it
and/or modify  it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using System.Threading;

using Ookii.Dialogs.Wpf;
using ChartNS;
using KFReaderNS;
using WD1772NS;
using SCPReaderNS;
using GlobalNS;

namespace Aufit {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private FluxDrawing _fluxDrawing = null;
		private HistoDrawing _histoDrawing = null;
		private ClockDrawing _clockDrawing = null;
		private DataDrawing _dataDrawing = null;

		//private FDC _trackInfo = null;
		private BufferWindow _trackWindow = null;
		private bool _trackWindowIsOpen = false;
		private DiskWindow _diskWindow = null;
		private bool _diskWindowIsOpen = false;
		private BufferWindow _sectorWindow = null;
		private bool _sectorWindowIsOpen = false;

		private string _diskName;

		//public double MinClock { get; set; }
		//public double MaxClock { get; set; }
		//public double minFlux { get; set; }
		//public double maxFlux { get; set; }

		/// <summary>
		/// Initialize the main window
		/// </summary>
		public MainWindow() {
			InitializeComponent();

			_fluxDrawing = new FluxDrawing(fluxGrid, cvTextFlux, cvGraphicFlux, this);
			_fluxDrawing.SlowDraw = cbSlow.IsChecked.Value;
			//_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_fluxDrawing.Chart.FixOffset = true;

			_histoDrawing = new HistoDrawing(histoGrid, cvTextHisto, cvGraphicHisto, this);

			_clockDrawing = new ClockDrawing(clockGrid, cvTextClock, cvGraphicClock);
			_clockDrawing.Chart.OptimalYSpacing = 18;
			_clockDrawing.Chart.FixOffset = true;

			_dataDrawing = new DataDrawing(dataGrid, cvTextData, cvGraphicData);
			_dataDrawing.Chart.HasYGrid = false;
			_dataDrawing.Chart.FixOffset = true;

			//_trackInfo = new FDC();

			// this.DataContext = this;	// needed for property changed event

		}


		#region Button Matrix Procedures
		/// <summary>
		/// When the matrix grid is loaded we draw the 2 matrices of buttons for side 1 & 2
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void matrixGridLoaded(object sender, RoutedEventArgs e) {
			ButtonMatrix.drawButtons(BM1, 1, btMatrixClicked);
			ButtonMatrix.drawButtons(BM2, 2, btMatrixClicked);
			// we hide at startup
			BM1.Visibility = Visibility.Hidden;
			BM2.Visibility = Visibility.Hidden;
			tabCtrl.Width = matrixGrid.ActualWidth;
			Grid.SetColumn(tabCtrl, 0);
			Grid.SetColumnSpan(tabCtrl, 3);
		}


		/// <summary>
		/// One button of the two matrices has been clicked. We find the button and we set the current Track/Side 
		/// accordingly. We draw all the charts.
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btMatrixClicked(object sender, RoutedEventArgs e) {
			Button button = (Button)sender;
			if (ButtonMatrix.buttonClicked(button)) {
				drawCharts();
			}
		}

		/// <summary>
		/// Called when the the size of button matrix grid is changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void matrixGridSizeChanged(object sender, SizeChangedEventArgs e) {
			if (BM2.Visibility == Visibility.Visible)
				tabCtrl.Width = matrixGrid.ActualWidth - 320;
			else
				tabCtrl.Width = matrixGrid.ActualWidth;
		}
		#endregion

		#region flux drawing related
		/// <summary>
		/// If the size of the flux grid is changed we need to redraw
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void fluxGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_fluxDrawing.drawFlux(Global.Track, Global.Side, Global.Rev);
		}

		private void fluxMouseLBU(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseLBU(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.ClockMin, _clockDrawing.ClockMax);
			_clockDrawing.drawClock();
			//_clockDrawing.drawClock(_fluxDrawing.SpeedUp);
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData();

		}

		private void fluxMouseLBD(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseLBD(sender, e);
		}

		private void fluxMouseWheel(object sender, MouseWheelEventArgs e) {
			_fluxDrawing.mouseWheel(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.ClockMin, _clockDrawing.ClockMax);
			_clockDrawing.drawClock();
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData();
		}

		private void fluxMouseMove(object sender, MouseEventArgs e) {
			_fluxDrawing.mouseMove(sender, e);
		}

		private void fluxMouseRBDown(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseRBD(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.ClockMin, _clockDrawing.ClockMax);
			_clockDrawing.drawClock();
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData();

		}
		#endregion


		#region histogram related
		private void histoGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_histoDrawing.drawHisto();
		}
		#endregion


		#region options
		private void cbHistoLogClick(object sender, RoutedEventArgs e) {
			Global.computeHisto(Global.Track, Global.Side, Global.Rev, infoBox);
			_histoDrawing.drawHisto();
		}

		private void cbLimitClick(object sender, RoutedEventArgs e) {
			Global.computeHisto(Global.Track, Global.Side, Global.Rev, infoBox);
			//_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_histoDrawing.drawHisto();
			_fluxDrawing.setFluxDefault();
			_fluxDrawing.drawFlux(Global.Track, Global.Side, Global.Rev);
		}

		private void cbSlowClick(object sender, RoutedEventArgs e) {
			_fluxDrawing.SlowDraw = cbSlow.IsChecked.Value;
			_fluxDrawing.drawFlux(Global.Track, Global.Side, Global.Rev);
			_clockDrawing.drawClock();
		}

		private void minusRevClicked(object sender, RoutedEventArgs e) {
			// now we limit the Rev number so we never use the last one

			//if (Global.currentTrack().indexes == null) return;
			if (Global.Rev == 0)
				//Global.Rev = Global.currentTrack().indexCount - 2;
				Global.Rev = Global.currentTrack().revData.Count();
			else Global.Rev--;

			revolution.Text = Global.Rev.ToString();
			Global.computeHisto(Global.Track, Global.Side, Global.Rev, infoBox);
			_histoDrawing.drawHisto();
			//if ((Global.Rev == 0) || (Global.Rev == Global.currentTrack().indexes.Length - 2))
			//	_fluxDrawing.setFluxDefault();

			FDC.clearTrackContent(Global.Track, Global.Side);
			drawCharts();
		}

		private void plusRevClicked(object sender, RoutedEventArgs e) {
			// now we limit the Rev number so we never use the last one
			//if (Global.currentTrack().indexes == null) return;
			if (Global.Rev == (Global.currentTrack().revData.Count()) )
			//if (Global.Rev == (Global.currentTrack().indexCount - 2))
				Global.Rev = 0;
			else Global.Rev++;

			revolution.Text = Global.Rev.ToString();
			Global.computeHisto(Global.Track, Global.Side, Global.Rev, infoBox);
			_histoDrawing.drawHisto();
			//if ((Global.Rev == 0) || (Global.Rev == 1))
			//	_fluxDrawing.setFluxDefault();
			//_fluxDrawing.drawFlux();
			FDC.clearTrackContent(Global.Track, Global.Side);
			drawCharts();
		}
		#endregion


		/// <summary>
		/// Display all the charts for the current Track/Side/Rev
		/// </summary>
		public void drawCharts() {
			int track = Global.Track;
			int side = Global.Side;
			int rev = Global.Rev;

			revolution.Text = rev.ToString();
			if (Global.Disk[track, side].trackContent == null)
				Global.mfmDecode(track, side);

			cbLimitFlux.IsChecked = (Global.computeHisto(Global.Track, Global.Side, Global.Rev, infoBox) > 0);
			_histoDrawing.drawHisto(track, side, rev);

			_fluxDrawing.setFluxDefault(track, side, rev);
			_fluxDrawing.drawFlux(track, side, rev);

			_clockDrawing.setClockDefault(track, side, rev);
			_clockDrawing.drawClock(track, side, rev);

			_dataDrawing.Chart.setDefaultViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData(track, side, rev);
			ButtonMatrix.setButtonBG(track, side);

		}


		private void readKFRawFile(string fileName) {
			BM1.Visibility = Visibility.Hidden;
			BM2.Visibility = Visibility.Hidden;
			tabCtrl.Width = matrixGrid.ActualWidth;
			Grid.SetColumn(tabCtrl, 0);
			Grid.SetColumnSpan(tabCtrl, 3);
			Global.resetDisk();

			bool status = ProcessStream.readStreamTrack(fileName, Global.Track, Global.Side);
			if (status) {
				infoBox.Clear();
				drawCharts();
			}
		}

		private async void readSPCFile(string fileName) {
			BM1.Visibility = Visibility.Visible;
			BM2.Visibility = Visibility.Visible;
			tabCtrl.Width = matrixGrid.ActualWidth - 320;
			Grid.SetColumn(tabCtrl, 2);
			Grid.SetColumnSpan(tabCtrl, 1);
			_diskName = System.IO.Path.GetFileNameWithoutExtension(fileName);

			bool fileOK = await SCPReader.readSCPAsync(fileName);

			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {
					Global.computeHisto(track, side, 1, infoBox);
					ButtonMatrix.setButtonBG(track, side);
				}
			}

			infoBox.Clear();
			drawCharts();
		}


		private void openFileClick(object sender, RoutedEventArgs e) {
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Filter = "SuperCard Pro files|*.scp|Stream Files|*.raw|All Files|*.*";
			bool? ok = ofd.ShowDialog();
			if (ok == true) {
				Global.Track = Global.Side = 0;
				Global.Rev = 1;
				Global.resetDisk();
				FileName.Text = ofd.FileName;
				string extension = System.IO.Path.GetExtension(ofd.FileName);
				switch (extension) {
					case ".raw":
						readKFRawFile(ofd.FileName);
						break;
					case ".scp":
						readSPCFile(ofd.FileName);
						break;
				}

			}
		}


		private void openDirectoryClick(object sender, RoutedEventArgs e) {
			VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
			dialog.Description = "Please select a folder.";
			dialog.UseDescriptionForTitle = true;
			if ((bool)dialog.ShowDialog(this)) {
				string selectedDir = dialog.SelectedPath;
				DirectoryInfo di = new DirectoryInfo(selectedDir);
				FileInfo[] fi = di.GetFiles("*.raw");
				if (fi.Length != 0) {
					Global.Rev = 1;

					BM1.Visibility = Visibility.Visible;
					BM2.Visibility = Visibility.Visible;
					tabCtrl.Width = matrixGrid.ActualWidth - 320;
					Grid.SetColumn(tabCtrl, 2);
					Grid.SetColumnSpan(tabCtrl, 1);

					infoBox.Clear();
					string file = fi[0].Name;
					string path = fi[0].DirectoryName;
					string root = file.Remove(file.Length - 8, 8);
					FileName.Text = selectedDir;
					Global.resetDisk();

					if (cbDispRead.IsChecked == true)
						readAllTracksAsync(path, root);
					else
						readAllTracksTasks(path, root);
					_diskName = root;
				}
				else
					FileName.Text = "Sorry no Stream files found in this directory";
			}
		}

		/// <summary>
		/// Read the stream files for all the tracks of a FD using 170 Tasks!
		/// </summary>
		/// <param name="path">Path to directory that contains the streams</param>
		/// <param name="root">Root name expanded with xx.x to create the final name</param>
		public void readAllTracksTasks(string path, string root) {
			Task<bool>[] tasks = new Task<bool>[168];
			for (int i = 0; i < 84; i++) {
				for (int j = 0; j < 2; j++) {
					string fname = String.Format("{0}\\{1}{2:D2}.{3}.raw", path, root, i, j);
					tasks[i + j * 84] = ProcessStream.readStreamTrackAsync(fname, i, j);
				}
			}
			Task.WaitAll(tasks);

			ButtonMatrix.clearButtons();
			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {
					double entropy = Global.computeHisto(track, side, 1, infoBox);
					// Brush color = (entropy < 2000) ? (entropy < 0) ? Brushes.LightSlateGray : Brushes.Orange : Brushes.LimeGreen;
					if (Global.Disk[track, side].fluxData != null)
						ButtonMatrix.setButtonBG(track, side);
				}
			}
			ButtonMatrix.selectButton(0, 0);
			Global.Track = Global.Side = 0;
			Global.Rev = 1;
			infoBox.Clear();
			drawCharts();
		}


		/// <summary>
		/// Read the stream files for all the tracks of a FD using one asynchronous Tasks
		/// </summary>
		/// <param name="path">Path to directory that contains the streams</param>
		/// <param name="root">Root name expanded with xx.x to create the final name</param>
		public async void readAllTracksAsync(string path, string root) {
			string fname;
			_fluxDrawing.FastDraw = true;
			infoBox.Clear();
			ButtonMatrix.clearButtons();

			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {
					fname = String.Format("{0}\\{1}{2:D2}.{3}.raw", path, root, track, side);
					bool streamOK = await ProcessStream.readStreamTrackAsync(fname, track, side);
					//bool streamOK = ProcessStream.readStreamTrack(fname, track, side);
					if (streamOK) {
						//drawCharts();
						Global.computeHisto(track, side, 1, infoBox);
						//double entropy = Global.Disk[track, side].entropy;
						//Brush color = (entropy < 2000) ? (entropy < 0) ? Brushes.LightSlateGray : Brushes.Orange : Brushes.LimeGreen;
						ButtonMatrix.setButtonBG(track, side);
					}
				}
			}

			_fluxDrawing.FastDraw = false;
			Global.Track = Global.Side = 0;
			Global.Rev = 1;
			if (Global.currentTrack().fluxData != null) {
				ButtonMatrix.selectButton(Global.Track, Global.Side);
			}
			drawCharts();
		}


		private void keyDown(object sender, KeyEventArgs e) {
			e.Handled = true;

			if (BM1.Visibility != Visibility.Visible) return;

			int track = Global.Track;
			int side = Global.Side;
			if (e.Key == Key.Left) {
				if (track > 0) track--;
				else {
					track = 83;
					if (side > 0) side--;
					else side = 1;
				}
			}
			else if (e.Key == Key.Right) {
				if (track < 83) track++;
				else {
					track = 0;
					if (side < 1) side++;
					else side = 0;
				}
			}
			else if (e.Key == Key.Down) {
				if (track >= 80) {
					track -= 80;
					side = (side + 1) % 2;
				}
				else {
					if (track > 73) {
						track -= 70;
						side = (side + 1) % 2;
					}
					else track = track + 10;
				}
			}
			else if (e.Key == Key.Up) {
				if (track >= 10) track -= 10;
				else {
					if (track < 4) track = track + 80;
					else track = track + 70;
					side = (side + 1) % 2;
				}
			}
			else
				return;	// other keys

			if (Global.Disk[track, side].fluxData == null) return;
			Global.Track = track;
			Global.Side = side;
			ButtonMatrix.deselectButton();
			ButtonMatrix.selectButton(track, side);
			drawCharts();
		}


		private void clockGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_clockDrawing.drawClock();
		}

		private void dataGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_dataDrawing.drawTrackData();
		}

		private void sectGridSizeChanged(object sender, SizeChangedEventArgs e) {

		}

		private void dataMouseLBD(object sender, MouseButtonEventArgs e) {

			if (e.ClickCount > 1) {
				Point p = e.GetPosition(_dataDrawing.Chart.GraphicCanvas);
				int pos = (int) _dataDrawing.Chart.WorldPoint(p).X * 1000000;

				byte index;
				for (index = 0; index < Global.currentTrack().trackContent.sectors.Count; index++) {
					if ((Global.currentTrack().trackContent.sectors[index].startTime < pos) && (Global.currentTrack().trackContent.sectors[index].endTime > pos)) {
						// infoBox.AppendText(String.Format("Mouse {0} - {1} - sector {2}\n", _dataDrawing.Chart.WorldPoint(p).X, _dataDrawing.Chart.WorldPoint(p).Y, index));
						byte number = Global.currentTrack().trackContent.sectors[index].addr.number;
						SectorContent sect = FDC.findSectorContent(Global.Track, Global.Side, number, Global.currentTrack().trackContent.sectors[index].startTime);
						TrackBuffer buffer = sect.buffer;
						string message = String.Format("Sector {0} with {1} bytes {2} {3}\n", number, sect.dataBlock.byteCount, (sect.dataBlock.crc == 0) ? "crc OK" : "Bad crc", (sect.mask != null) ? "Has fuzzy bits" : "");
						if (_sectorWindowIsOpen)
							_sectorWindow.Close();

						_sectorWindow = new BufferWindow();
						_sectorWindow.drawBuffer(buffer, message);

						_sectorWindow.Closed += new EventHandler(sectorWindowClosed);
						_sectorWindowIsOpen = true;
						_sectorWindow.Show();

					}
				}
			}
		}

		void sectorWindowClosed(object sender, EventArgs e) {
			_sectorWindowIsOpen = false;
		}


		private void mainWinClosing(object sender, CancelEventArgs e) {
			if (_trackWindowIsOpen)
				_trackWindow.Close();
			if (_diskWindowIsOpen)
				_diskWindow.Close();
			if (_sectorWindowIsOpen)
				_sectorWindow.Close();
		}

		void trackWindowClosed(object sender, EventArgs e) {
			_trackWindowIsOpen = false;
		}

		private void btTrackWindowClick(object sender, RoutedEventArgs e) {
			if (Global.currentTrack().trackContent == null) return;			
			if (_trackWindowIsOpen)
				_trackWindow.Close();

			_trackWindow = new BufferWindow();
			_trackWindow.drawTrackBuffer(Global.Track, Global.Side);
			_trackWindow.Closed += new EventHandler(trackWindowClosed);
			_trackWindowIsOpen = true;
			_trackWindow.Show();
		}

		private void btSectorWindowClick(object sender, EventArgs e) {
			if (Global.currentTrack().trackContent == null) return;	
			if (_sectorWindowIsOpen)
				_sectorWindow.Close();

			_sectorWindow = new BufferWindow();
			_sectorWindow.drawSectorBuffer(Global.Track, Global.Side);
			_sectorWindow.Closed += new EventHandler(sectorWindowClosed);
			_sectorWindowIsOpen = true;
			_sectorWindow.Show();
		}


		private void btDiskClick(object sender, RoutedEventArgs e) {
			// TODO if (_trackInfo.TrackInfo == null) return;
			if (BM1.Visibility == Visibility.Hidden) return;
			if (_diskWindowIsOpen)
				_diskWindow.Close();

			string message = String.Format("Disk {0} (Side {1})", _diskName, Global.Side);
			_diskWindow = new DiskWindow(Global.Side, message);
			_diskWindow.Closed += new EventHandler(diskWindowClosed);
			_diskWindowIsOpen = true;
			_diskWindow.Show();
		}

		void diskWindowClosed(object sender, EventArgs e) {
			_diskWindowIsOpen = false;
		}

		private void btProtectionClick(object sender, RoutedEventArgs e) {


		}


	}
}
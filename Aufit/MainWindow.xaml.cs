/*!
@file MainWindow.xaml.cs
 * 
<summary>The Main Window for the AUFIT program</summary>

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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using System.Threading;

using Ookii.Dialogs.Wpf;
using ChartPackage;
using KFReaderPackage;
using WD1772Package;
using SCPReaderPackage;
using GlobalPackage;
using PastiPackage;

namespace Aufit {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private FluxDrawing _fluxDrawing = null;
		private HistoDrawing _histoDrawing = null;
		private ClockDrawing _clockDrawing = null;
		private DataDrawing _dataDrawing = null;

		private BufferWindow _trackWindow = null;
		private bool _trackWindowIsOpen = false;
		private DiskWindow _diskWindow = null;
		private bool _diskWindowIsOpen = false;
		private BufferWindow _sectorWindow = null;
		private bool _sectorWindowIsOpen = false;
		private string _fileName;

		// current display
		/// <summary>
		/// The track currently selected and displayed
		/// </summary>
		public static int Track { get; set; }
		/// <summary>
		/// The side currently selected and displayed
		/// </summary>
		public static int Side { get; set; }
		/// <summary>
		/// The revolution currently selected and displayed
		/// </summary>
		public static int Rev { get; set; }


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
				drawAllCharts();
				Protections.checkProtection(Track, Side, infoBox);
			}
		}

		/// <summary>
		/// Called when the the size of button matrix grid is changed
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void matrixGridSizeChanged(object sender, SizeChangedEventArgs e) {
			if (BM2.Visibility == Visibility.Visible)
				tabCtrl.Width = matrixGrid.ActualWidth - 370;
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
			_fluxDrawing.drawFlux(Track, Side, Rev);
		}

		private void fluxMouseLBU(object sender, MouseButtonEventArgs e) {
			// NASTI BUG we receive a mouse up event from Openfiledialog
			if (!_fluxDrawing.Chart.GraphicCanvas.IsMouseCaptured) 
				return;		// we receive a mouse up event from Openfiledialog !!! Bug?

			_fluxDrawing.mouseLBU(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.ClockMin, _clockDrawing.ClockMax);
			_clockDrawing.drawClock();
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
			Global.computeHisto(Track, Side, Rev, infoBox);
			_histoDrawing.drawHisto();
		}

		private void cbLimitClick(object sender, RoutedEventArgs e) {
			Global.computeHisto(Track, Side, Rev, infoBox);
			//_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_histoDrawing.drawHisto();
			_fluxDrawing.setFluxDefault();
			_fluxDrawing.drawFlux(Track, Side, Rev);
		}

		private void cbSlowClick(object sender, RoutedEventArgs e) {
			_fluxDrawing.SlowDraw = cbSlow.IsChecked.Value;
			_fluxDrawing.drawFlux(Track, Side, Rev);
			_clockDrawing.drawClock();
		}

		private void minusRevClicked(object sender, RoutedEventArgs e) {
			if (Rev == 0)
				Rev = Global.Disk[Track, Side].revData.Count();
			else Rev--;

			revolution.Text = Rev.ToString();
			Global.computeHisto(Track, Side, Rev, infoBox);
			_histoDrawing.drawHisto();
			FDC.clearTrackContent(Track, Side);
			drawAllCharts();
		}

		private void plusRevClicked(object sender, RoutedEventArgs e) {
			if (Rev == (Global.Disk[Track, Side].revData.Count()))
				Rev = 0;
			else Rev++;

			revolution.Text = Rev.ToString();
			Global.computeHisto(Track, Side, Rev, infoBox);
			_histoDrawing.drawHisto();
			FDC.clearTrackContent(Track, Side);
			drawAllCharts();
		}
		#endregion


		/// <summary>
		/// Display all the charts for the current Track/Side/Rev
		/// </summary>
		public void drawAllCharts() {
			int track = Track;
			int side = Side;
			int rev = Rev;

			revolution.Text = rev.ToString();
			if (Global.Disk[track, side].decodeData == null)
				Global.mfmDecode(track, side);

			// cbLimitFlux.IsChecked = (Global.computeHisto(track, side, rev, infoBox) > 0);
			cbLimitFlux.IsChecked = (Global.computeHisto(track, side, rev, infoBox) < Global.unformattedEntropy);
			_histoDrawing.drawHisto(track, side, rev);

			_fluxDrawing.setFluxDefault(track, side, rev);
			_fluxDrawing.drawFlux(track, side, rev);

			_clockDrawing.setClockDefault(track, side, rev);
			_clockDrawing.drawClock(track, side, rev);

			_dataDrawing.Chart.setDefaultViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData(track, side, rev);
			ButtonMatrix.setButtonBG(track, side);
		}


		private async void openFileClick(object sender, RoutedEventArgs e) {
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Filter = "SuperCard Pro files|*.scp|Stream Files|*.raw|All Files|*.*";
			bool? ok = ofd.ShowDialog();
			bool readOK = false;			
			if (ok == true) {
				Track = Side = 0;
				Rev = 1;
				Global.resetDisk();

				tbFileName.Text = ofd.FileName;
				string extension = System.IO.Path.GetExtension(ofd.FileName);
				switch (extension) {
					case ".raw":
						readOK = readKFRawFile(ofd.FileName);
						break;
					case ".scp":
						readOK = await readSPCFile(ofd.FileName);
						break;
				}
			}
			if (!readOK)
				tbFileName.Text = "Sorry could not read correctly the input file";
		}


		private bool readKFRawFile(string fileName) {
			BM1.Visibility = Visibility.Hidden;
			BM2.Visibility = Visibility.Hidden;
			tabCtrl.Width = matrixGrid.ActualWidth;
			Grid.SetColumn(tabCtrl, 0);
			Grid.SetColumnSpan(tabCtrl, 3);

			_fileName = fileName;
			bool ok = ProcessStream.readStreamTrack(fileName, Track, Side);
			if (ok) {
				infoBox.Clear();
				infoBox.FontSize = 11;
				drawAllCharts();
			}
			return ok;
		}


		private async Task<bool> readSPCFile(string fileName) {
			BM1.Visibility = Visibility.Visible;
			BM2.Visibility = Visibility.Visible;
			tabCtrl.Width = matrixGrid.ActualWidth - 370;
			Grid.SetColumn(tabCtrl, 2);
			Grid.SetColumnSpan(tabCtrl, 1);
				infoBox.Clear();
				infoBox.FontSize = 11;

			_fileName = System.IO.Path.GetFileNameWithoutExtension(fileName);
			bool ok = await SCPReader.readScpAsync(fileName, infoBox);
			if (ok) {
				selectAndCheck(0, 0);
				drawAllCharts();
			}

			return ok;
		}

		// Do we read KF directory using Asynchronous read or as tasks.
		private bool _readKFAsync = true;

		private void openDirectoryClick(object sender, RoutedEventArgs e) {
			VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
			dialog.Description = "Please select a folder.";
			dialog.UseDescriptionForTitle = true;
			if ((bool)dialog.ShowDialog(this)) {
				string selectedDir = dialog.SelectedPath;
				DirectoryInfo di = new DirectoryInfo(selectedDir);
				// for some reason this may also includes the directories
				FileInfo[] fi = di.GetFiles("*.raw");
				// to remove the number of directories from the count
				DirectoryInfo[] children = di.GetDirectories();
				if (fi.Length != 0) {
					if (fi.Length == children.Length) {
						tbFileName.Text = "Sorry no Stream files found in this directory";
						return;
					}

					BM1.Visibility = Visibility.Visible;
					BM2.Visibility = Visibility.Visible;
					tabCtrl.Width = matrixGrid.ActualWidth - 370;
					Grid.SetColumn(tabCtrl, 2);
					Grid.SetColumnSpan(tabCtrl, 1);

					infoBox.Clear();
					infoBox.FontSize = 11;
					string file = fi[0].Name;
					string path = fi[0].DirectoryName;
					string root = file.Remove(file.Length - 8, 8);
					tbFileName.Text = selectedDir;
					_fileName = di.Name;
					Global.resetDisk();

					if (_readKFAsync)
						readAllTracksAsync(path, root);
					else
						readAllTracksTasks(path, root);
				}
				else
					tbFileName.Text = "Sorry no Stream files found in this directory";
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
					if (Global.Disk[track, side].fluxData != null) {
						Global.computeHisto(track, side, 1, infoBox);
						ButtonMatrix.setButtonBG(track, side);
					}
				}
			}
			selectAndCheck(0 , 0);
			Track = Side = 0;
			Rev = 1;
			infoBox.Clear();
			infoBox.FontSize = 11;
			drawAllCharts();
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
			infoBox.FontSize = 11;
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
			Track = Side = 0;
			Rev = 1;
			if (Global.Disk[Track, Side].fluxData != null) {
				ButtonMatrix.selectButton(Track, Side);
			}
			drawAllCharts();
		}


		private void keyDown(object sender, KeyEventArgs e) {
			e.Handled = true;

			if (BM1.Visibility != Visibility.Visible) return;

			int track = Track;
			int side = Side;
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
			Track = track;
			Side = side;
			ButtonMatrix.deselectButton();
			//ButtonMatrix.selectButton(track, side);
			drawAllCharts();
			selectAndCheck(track, side);
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
				for (index = 0; index < Global.Disk[Track, Side].trackContent.sectors.Count; index++) {
					if ((Global.Disk[Track, Side].trackContent.sectors[index].startTime < pos) && (Global.Disk[Track, Side].trackContent.sectors[index].endTime > pos)) {
						// infoBox.AppendText(String.Format("Mouse {0} - {1} - sector {2}\n", _dataDrawing.Chart.WorldPoint(p).X, _dataDrawing.Chart.WorldPoint(p).Y, index));
						byte number = Global.Disk[Track, Side].trackContent.sectors[index].addr.number;
						SectorContent sect = FDC.findSectorContent(Track, Side, number, Global.Disk[Track, Side].trackContent.sectors[index].startTime);
						if (sect == null) return;	// TODO for now we return
						TrackBuffer buffer = sect.data;
						string message = String.Format("Sector {0} with {1} bytes {2} {3}\n", 
							number, sect.dataBlock.byteCount, (sect.dataBlock.crc == 0) ? "crc OK" : "Bad crc", (sect.fuzzyMask != null) ? "Has fuzzy bits" : "");
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
			if (Global.Disk[Track, Side].trackContent == null) return;			
			if (_trackWindowIsOpen)
				_trackWindow.Close();

			_trackWindow = new BufferWindow();
			_trackWindow.drawTrackBuffer(Track, Side, Rev);
			_trackWindow.Closed += new EventHandler(trackWindowClosed);
			_trackWindowIsOpen = true;
			_trackWindow.Show();
		}

		void sectorWindowClosed(object sender, EventArgs e) {
			_sectorWindowIsOpen = false;
		}


		private void btSectorWindowClick(object sender, EventArgs e) {
			if (Global.Disk[Track, Side].trackContent == null) return;	
			if (_sectorWindowIsOpen)
				_sectorWindow.Close();

			_sectorWindow = new BufferWindow();
			_sectorWindow.drawSectorBuffer(Track, Side, Rev);
			_sectorWindow.Closed += new EventHandler(sectorWindowClosed);
			_sectorWindowIsOpen = true;
			_sectorWindow.Show();
		}


		private void btDiskLayoutClick(object sender, RoutedEventArgs e) {
			if (BM1.Visibility == Visibility.Hidden) return;
			if (_diskWindowIsOpen)
				_diskWindow.Close();

			string message = String.Format("{0}", _fileName);
			_diskWindow = new DiskWindow();
			_diskWindow.drawDiskLayout(message);
			_diskWindow.Closed += new EventHandler(diskWindowClosed);
			_diskWindowIsOpen = true;
			_diskWindow.Show();
		}

		void diskWindowClosed(object sender, EventArgs e) {
			_diskWindowIsOpen = false;
		}


		private async void btProtectionClick(object sender, RoutedEventArgs e) {
			await checkProtectionAsync();
		}


		private async Task checkProtectionAsync() {
			for (int side = 0; side < 2; side++)
				for (int track = 0; track < 84; track++) {
					await DiskWindow.decodeAndReadAsync(track, side, 1);
					Protections.checkProtection(track, side, infoBox);
					ButtonMatrix.setButtonBG(track, side);
				}
		}

		private void btDiskDataClick(object sender, RoutedEventArgs e) {
			if (BM1.Visibility == Visibility.Hidden) return;
			if (_diskWindowIsOpen)
				_diskWindow.Close();

			string message = String.Format("{0}", _fileName);
			_diskWindow = new DiskWindow();
			_diskWindow.drawDiskData(message);
			_diskWindow.Closed += new EventHandler(diskWindowClosed);
			_diskWindowIsOpen = true;
			_diskWindow.Show();
		}

		private void selectAndCheck(int track, int side) {
			ButtonMatrix.selectButton(track, side);
			Protections.checkProtection(track, side, infoBox);
		}

		private async void btWriteDiskClick(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.Filter = "Pasti file|*.stx|All Files|*.*";
			bool? ok = sfd.ShowDialog();
			if (ok == true) {
				await checkProtectionAsync();
				tbFileName.Text  = sfd.FileName;
				if (Global.DataIncomplete) {
					MessageBoxResult result = MessageBox.Show(
						"The image does not contains enough data\nYou should have imaged more revolutions\nDo you still want to proceed?", 
						"Incomplete Image Data", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
					if (result == MessageBoxResult.No) return;				
				}	// incomplete
				//PastiWriter pasti = new PastiWriter(infoBox);
				PastiWriter pasti = new PastiWriter();
				pasti.writePasti(sfd.FileName, cbForceDS.IsChecked.Value);
			}

		}

		private MessageBoxResult ask(string caption, string messageBoxText) {
			MessageBoxButton button = MessageBoxButton.YesNoCancel;
			MessageBoxImage icon = MessageBoxImage.Question;
			MessageBoxResult defaultResult = MessageBoxResult.Cancel;
			MessageBoxResult result;
			result = MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
			return result;
		}


		private void cbRecoverClick(object sender, RoutedEventArgs e) {
			// TODO
		}
	}
}
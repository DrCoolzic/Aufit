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
using System.Windows.Threading;

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
		private BitWidthDrawing _clockDrawing = null;
		private DataDrawing _dataDrawing = null;

		private BufferWindow _trackWindow = null;
		private bool _trackWindowIsOpen = false;
		private BufferWindow _sectorWindow = null;
		private bool _sectorWindowIsOpen = false;

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

		public static string ImageName { get; set; }


		/// <summary>
		/// Initialize the main window
		/// </summary>
		public MainWindow() {
			InitializeComponent();

			_fluxDrawing = new FluxDrawing(fluxGrid, cvTextFlux, cvGraphicFlux, this);
			_fluxDrawing.DetailDraw = cbDetail.IsChecked.Value;
			//_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_fluxDrawing.Chart.FixOffset = true;

			_histoDrawing = new HistoDrawing(histoGrid, cvTextHisto, cvGraphicHisto, this);

			_clockDrawing = new BitWidthDrawing(clockGrid, cvTextClock, cvGraphicClock);
			_clockDrawing.Chart.OptimalYSpacing = 18;
			_clockDrawing.Chart.FixOffset = true;

			_dataDrawing = new DataDrawing(dataGrid, cvTextData, cvGraphicData);
			_dataDrawing.Chart.HasYGrid = false;
			_dataDrawing.Chart.FixOffset = true;

			// this.DataContext = this;	// needed for property changed event
			string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			Title = "Aufit " + version;
			tbVersion.Text = "Aufit " + version;

			this.Loaded += new RoutedEventHandler(container_loaded);
			//MessageBox.Show(Application.Current.Properties["FileName"].ToString());
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
				//drawAllCharts();
				//Protections.checkProtection(Track, Side, infoBox);
				selectTrack(Track, Side);
			}
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
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.MinBitWidth, _clockDrawing.MaxBitWidth);
			_clockDrawing.drawBitWidth();
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData();
		}

		private void fluxMouseLBD(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseLBD(sender, e);
		}

		private void fluxMouseWheel(object sender, MouseWheelEventArgs e) {
			_fluxDrawing.mouseWheel(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.MinBitWidth, _clockDrawing.MaxBitWidth);
			_clockDrawing.drawBitWidth();
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData();
		}

		private void fluxMouseMove(object sender, MouseEventArgs e) {
			_fluxDrawing.mouseMove(sender, e);
		}

		private void fluxMouseRBDown(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseRBD(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.MinBitWidth, _clockDrawing.MaxBitWidth);
			_clockDrawing.drawBitWidth();
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData();

		}
		#endregion


		#region histogram related
		private void histoGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_histoDrawing.drawHisto();
		}
		#endregion


		#region options handlers
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

		private void cbDetailClick(object sender, RoutedEventArgs e) {
			_fluxDrawing.DetailDraw = cbDetail.IsChecked.Value;
			_fluxDrawing.drawFlux(Track, Side, Rev);
			_clockDrawing.drawBitWidth();
		}

		private void minusRevClicked(object sender, RoutedEventArgs e) {
			int RevCount = 1;
			if (Global.Disk[Track, Side].fluxDataRev != null) {
				RevCount = Global.Disk[Track, Side].fluxDataRev.Count();
			}
			else if (Global.Disk[Track, Side].packedFluxDataRev != null) {
				RevCount = Global.Disk[Track, Side].packedFluxDataRev.Count();
			}
			else return;

			if (Rev == 0)
				Rev = RevCount - 1;
			else
				Rev--;

			//tbRevolution.Text = "Revolution " + (Rev + 1).ToString() + " / " + RevCount.ToString();
			Global.computeHisto(Track, Side, Rev, infoBox);
			_histoDrawing.drawHisto();
			FDC.releaseTrack(Track, Side);
			drawAllCharts();
		}

		private void plusRevClicked(object sender, RoutedEventArgs e) {
			int RevCount = 1;
			if (Global.Disk[Track, Side].fluxDataRev != null) {
				RevCount = Global.Disk[Track, Side].fluxDataRev.Count();
			}
			else if (Global.Disk[Track, Side].packedFluxDataRev != null) {
				RevCount = Global.Disk[Track, Side].packedFluxDataRev.Count();
			}
			else return;

			Rev = (Rev + 1) % RevCount;

			//tbRevolution.Text = "Revolution " + (Rev + 1).ToString() + " / " + RevCount.ToString();
			Global.computeHisto(Track, Side, Rev, infoBox);
			_histoDrawing.drawHisto();
			FDC.releaseTrack(Track, Side);
			drawAllCharts();
		}
		#endregion


		/// <summary>
		/// Display all the charts for the current Track/Side/Rev
		/// </summary>
		private void drawAllCharts() {
			int track = Track;
			int side = Side;
			int rev = Rev;
			if ((Global.Disk[track, side].fluxDataRev == null) && (Global.Disk[track, side].packedFluxDataRev == null)) return;

			//int RevCount = Global.Disk[track, side].packedFluxDataRev.Count();
			string revString = (Global.Disk[track, side].fluxDataRev != null) ? Global.Disk[track, side].fluxDataRev.Count().ToString() :
				((Global.Disk[track, side].packedFluxDataRev != null) ? (Global.Disk[track, side].packedFluxDataRev.Count().ToString()) : "*");
			tbRevolution.Text = "Rev. " + (Rev + 1).ToString() + "/" + revString;
			if (Global.Disk[track, side].packedFluxData == null)
				Global.mfmDecode(track, side);

			// cbLimitFlux.IsChecked = (Global.computeHisto(track, side, rev, infoBox) > 0);
			cbLimitFlux.IsChecked = (Global.computeHisto(track, side, rev, infoBox) < Global.UNFORMATED_ENTROPY);
			_histoDrawing.drawHisto(track, side, rev);

			if (!tabItemTrack.IsSelected) {
				// still want to read track/id/sector
				FDC.fdcRealAll(track, side, rev);
				return;
			}			

			_fluxDrawing.setFluxDefault(track, side, rev);
			_fluxDrawing.drawFlux(track, side, rev);

			_clockDrawing.setBitWidthDefault(track, side, rev);
			_clockDrawing.drawBitWidth(track, side, rev);

			_dataDrawing.Chart.setDefaultViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData(track, side, rev);
			ButtonMatrix.setButtonBG(track, side);

		}

		////////////////////////
		// DEBUG FLAGS
		////////////////////////
		// when set to true the KF files are read without treading for debug
		private bool _debugNoThread = false;
		// Do we read KF directory using Asynchronous read or as tasks.
		private bool _readKFAsync = true;


		private void openFileClick(object sender, RoutedEventArgs e) {
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Filter = "Aufit|*.scp;*.raw|All Files|*.*";

			bool? ok = ofd.ShowDialog();
			if (ok == true) {
				processFile(ofd.FileName);
			}
		}


		private void fileDrop(object sender, DragEventArgs e) {
			string[] droppedFiles = null;
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				droppedFiles = e.Data.GetData(DataFormats.FileDrop, true) as string[];
			}

			if ((null == droppedFiles) || (!droppedFiles.Any())) { return; }
			processFile(droppedFiles[0]);
		}

		public static void test(string name) {
			// use pipe
			// http://msdn.microsoft.com/en-us/library/bb546085.aspx
			// http://elegantcode.com/2011/03/02/wpf-advanced-jump-lists-using-a-single-instance-application/
			// http://danielvaughan.org/post/Enforcing-Single-Instance-WPF-Applications.aspx
			// http://stackoverflow.com/questions/19147/what-is-the-correct-way-to-create-a-single-instance-application
			// http://msdn.microsoft.com/en-us/library/ms771662(v=VS.90).aspxv
			// http://stackoverflow.com/questions/7901007/sending-event-between-2-wpf-windows
			MessageBox.Show(name + " ***** do not know what to do :(");
			//MessageBox.Show("container: " + mainWin.ToString());
			MessageBox.Show("Fuck");
			//mainWin.processFile(name);
		}


		private void container_loaded(object sender, RoutedEventArgs e) {
			if (Application.Current.Properties["FileName"] != null) {
				string fname = Application.Current.Properties["FileName"].ToString();
				FileInfo file = new FileInfo(fname);
				if (file.Exists) {
					processFile(fname);
				}
			}
		}


		private void buttonsEnabled(bool enable) {
			//btTrack.Visibility = visibility;
			//btSector.Visibility = visibility;
			//btDisk.Visibility = visibility;
			//btProt.Visibility = visibility;
			//btSave.Visibility = visibility;
			//btWrite.Visibility = visibility;
			//btFile.Visibility = visibility;

			gridCommand.IsEnabled = enable;
			cbLimitFlux.IsEnabled = enable;
			tabDisplay.IsEnabled = enable;

			if (enable) {
				tbStatus.Visibility = System.Windows.Visibility.Hidden;
				progress.Visibility = System.Windows.Visibility.Hidden;
			}
			else {
				tbStatus.Visibility = System.Windows.Visibility.Visible;
				progress.Visibility = System.Windows.Visibility.Visible;
			}
		}


		private async void processFile(string fileName) {
			bool readOK = false;
			Track = Side = Rev = 0;
			Global.resetDisk();
			ImageName = fileName;
			buttonsEnabled(false);
			string extension = System.IO.Path.GetExtension(fileName);
			tbStatus.Text = " *** READING INPUT ***";
			tabCtrl.SelectedIndex = 1;
			tabDisplay.SelectedIndex = 0;

			switch (extension) {
				case ".raw":
					string path = System.IO.Path.GetDirectoryName(fileName);
					ImageName = path;
					string file = System.IO.Path.GetFileName(fileName);
					string root = file.Remove(file.Length - 8, 8);
					if (_debugNoThread)
						readOK = readAllTracks(path, root);
					else if (_readKFAsync)
						readOK = await readAllTracksAsync(path, root);
					else
						readOK = readAllTracksTasks(path, root);
					break;

				case ".scp":
						readOK = await SCPReader.readScpAsync(fileName, infoBox);
					break;

				default:
					// TODO
					break;
			}

			if (readOK) {
				tbFileName.Text = ImageName;
				selectTrack(0, 0);
			}
			else {
				tbFileName.Text = "Sorry could not read correctly the input file";
				buttonsEnabled(true);
				return;
			}

			tbStatus.Text = "*** PROCESSING DATA ***";
			tabCtrl.SelectedIndex = 0;
			if (_debugNoThread) {
				for (int i = 0; i < 84; i++) {
					for (int j = 0; j < 2; j++) {
						Global.decodeAndRead(i, j, Rev);
						ButtonMatrix.setButtonBG(i, j);
					}
				}
			}
			else {
				BackgroundWorker bgWorker;
				bgWorker = new BackgroundWorker();
				bgWorker.DoWork += new DoWorkEventHandler(bgWorker_DoWork);
				bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgWorker_RunWorkerCompleted);
				// Launch background thread to do decode flux.  This will
				// trigger BackgroundWorker.DoWork().
				bgWorker.RunWorkerAsync();
			}
		}


		// http://stuff.seans.com/2009/05/21/net-basics-do-work-in-background-thread-to-keep-gui-responsive/
		void bgWorker_DoWork(object sender, DoWorkEventArgs e) {
			Task<List<Sector>>[] tasks = new Task<List<Sector>>[168];
			for (int i = 0; i < 84; i++) {
				for (int j = 0; j < 2; j++) {
					tasks[i + j * 84] = Global.decodeAndReadAsync(i, j, Rev);
				}
			}
			Task.WaitAll(tasks);
		}


		void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			if (e.Error != null) {
				MessageBox.Show(e.Error.Message);
				return;
			}

			buttonsEnabled(true);
			for (int i = 0; i < 84; i++) {
				for (int j = 0; j < 2; j++) {
					ButtonMatrix.setButtonBG(i, j);
				}
			}
		}


		/// <summary>
		/// Read the stream files for all the tracks of a FD using 170 Tasks!
		/// </summary>
		/// <param name="path">Path to directory that contains the streams</param>
		/// <param name="root">Root name expanded with xx.x to create the final name</param>
		private bool readAllTracksTasks(string path, string root) {
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
			Track = Side = Rev = 0;
			infoBox.Clear();
			infoBox.FontSize = 11;
			selectTrack(0, 0);
			return true;
		}


		/// <summary>
		/// Read the stream files for all the tracks of a FD using one asynchronous Tasks
		/// </summary>
		/// <param name="path">Path to directory that contains the streams</param>
		/// <param name="root">Root name expanded with xx.x to create the final name</param>
		private async Task<bool> readAllTracksAsync(string path, string root) {
			string fname;
			infoBox.Clear();
			infoBox.FontSize = 11;
			ButtonMatrix.clearButtons();

			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {
					fname = String.Format("{0}\\{1}{2:D2}.{3}.raw", path, root, track, side);
					bool streamOK = await ProcessStream.readStreamTrackAsync(fname, track, side);
					if (streamOK) {
						//Global.mfmDecode(track, side);
						Global.computeHisto(track, side, 0, infoBox);
						ButtonMatrix.setButtonBG(track, side);
					}
					// else return false;
				}
			}

			Track = Side = Rev = 0;
			if (Global.Disk[Track, Side].fluxData != null) {
				ButtonMatrix.selectButton(Track, Side);
			}
			drawAllCharts();
			return true;
		}




		/// <summary>
		/// For debug: Reads track without multi-threading
		/// </summary>
		/// <param name="path"></param>
		/// <param name="root"></param>
		/// <returns></returns>
		private bool readAllTracks(string path, string root) {
			string fname;
			infoBox.Clear();
			infoBox.FontSize = 11;
			ButtonMatrix.clearButtons();

			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {
					fname = String.Format("{0}\\{1}{2:D2}.{3}.raw", path, root, track, side);
					bool streamOK = ProcessStream.readStreamTrack(fname, track, side);
					if (streamOK) {
						Global.computeHisto(track, side, 1, infoBox);
						ButtonMatrix.setButtonBG(track, side);
					}
					// else return false;
				}
			}

			Track = Side = Rev = 0;
			if (Global.Disk[Track, Side].fluxData != null) {
				ButtonMatrix.selectButton(Track, Side);
			}
			drawAllCharts();
			return true;
		}



		private void keyDown(object sender, KeyEventArgs e) {
			e.Handled = true;

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
			selectTrack(track, side);
		}


		private void clockGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_clockDrawing.drawBitWidth();
		}

		private void dataGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_dataDrawing.drawTrackData();
		}

		private void sectGridSizeChanged(object sender, SizeChangedEventArgs e) {
			// TODO ?
		}

		private void dataMouseLBD(object sender, MouseButtonEventArgs e) {

			if (e.ClickCount > 1) {
				Point p = e.GetPosition(_dataDrawing.Chart.GraphicCanvas);
				int pos = (int)_dataDrawing.Chart.WorldPoint(p).X * 1000000;

				foreach (Sector sector in Global.Disk[Track, Side].sectors) {
					int startPos = sector.id.info.startTime;
					int endPos = (sector.data != null) ? sector.data.info.startTime + sector.data.info.timeLength :
						sector.id.info.startTime + sector.id.info.timeLength;
					if ((startPos < pos) && (endPos > pos)) {
						// infoBox.AppendText(String.Format("Mouse {0} - {1} - sector {2}\n", _dataDrawing.Chart.WorldPoint(p).X, _dataDrawing.Chart.WorldPoint(p).Y, indexStart));

						string message = String.Format("Sector {0} - {1} bytes IAM={2:X2} {3}={4:X4} {5}\n",
							sector.id.number, Global.sectSize(sector.id.size), sector.data.iam,
							(sector.data.info.crc == 0) ? "Good CRC" : "Bad CRC", sector.data.crc,
							(sector.data.fuzzy != null) ? "Has fuzzy bits" : "");
						if (_sectorWindowIsOpen)
							_sectorWindow.Close();

						_sectorWindow = new BufferWindow();
						_sectorWindow.drawBuffer(sector.data.buffer, message);

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
			//if (_diskWindowIsOpen)
			//	_diskWindow.Close();
			if (_sectorWindowIsOpen)
				_sectorWindow.Close();
		}


		void trackWindowClosed(object sender, EventArgs e) {
			_trackWindowIsOpen = false;
		}


		private void btTrackWindowClick(object sender, RoutedEventArgs e) {
			//if (Global.Disk[Track, Side].trackContent == null) return;
			if (Global.Disk[Track, Side].track == null) return;
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
			//if (Global.Disk[Track, Side].trackContent == null) return;
			if (Global.Disk[Track, Side].sectors == null) return;
			if (_sectorWindowIsOpen)
				_sectorWindow.Close();

			_sectorWindow = new BufferWindow();
			_sectorWindow.drawSectorBuffer(Track, Side, Rev);
			_sectorWindow.Closed += new EventHandler(sectorWindowClosed);
			_sectorWindowIsOpen = true;
			_sectorWindow.Show();
		}


		private async void btProtectionClick(object sender, RoutedEventArgs e) {
			await checkProtectionAsync();
		}


		private async Task checkProtectionAsync() {
			for (int side = 0; side < 2; side++)
				for (int track = 0; track < 84; track++) {
					await Global.decodeAndReadAsync(track, side, 1);
					Protections.checkProtection(track, side, infoBox);
					ButtonMatrix.setButtonBG(track, side);
				}
		}


		private void btSavePictClick(object sender, RoutedEventArgs e) {
			Global.saveImage(mainGrid, 1);
		}



		private async void btWriteDiskClick(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.Filter = "Pasti file|*.stx|All Files|*.*";
			bool? ok = sfd.ShowDialog();
			if (ok == true) {
				await checkProtectionAsync();
				//tbFileName.Text = sfd.FileName;
				if (Global.DataIncomplete) {
					MessageBoxResult result = MessageBox.Show(
						"The disk contains shifted tracks and you are on the last revolution\nThe output file will be incorrect\nDo you still want to proceed?",
						"Incomplete Image Data", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
					if (result == MessageBoxResult.No) return;
				}	// incomplete
				PastiWriter pasti = new PastiWriter(tbSaveImage);
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

		private void infoSaveClick(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.Filter = "Text file|*.txt|All Files|*.*";
			if (sfd.ShowDialog() == true)
				File.WriteAllText(sfd.FileName, infoBox.Text);
		}

		private void infoClearClick(object sender, RoutedEventArgs e) {
			infoBox.Clear();
		}






		private async void tabDisplaySelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (tabItemTrack.IsSelected) {
				drawAllCharts();
			}
			if (tabItemDisk.IsSelected) {
				await diskClearAndDraw();
			}
			if (tabItemContent.IsSelected) {

			}
			if (tabItemAnalysis.IsSelected) {

			}
			if (tabItemSave.IsSelected) {

			}
		}


		private void selectTrack(int track, int side) {
			ButtonMatrix.deselectButton();
			ButtonMatrix.selectButton(track, side);
			Protections.checkProtection(track, side, infoBox);
			//Global.computeHisto(Track, Side, Rev, infoBox);
			//_histoDrawing.drawHisto();
			FDC.releaseTrack(Track, Side);
			drawAllCharts();
			if (tabItemDisk.IsSelected)
				drawDiskSelectedTrack(track, side);
		}

	}
}
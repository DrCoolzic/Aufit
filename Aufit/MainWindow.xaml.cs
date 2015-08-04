/*!
@file MainWindow.xaml.cs
 * 
<summary>The Main Window for the AUFIT program</summary>

<div class="jlg">Copyright (C) 2013-2015 Jean Louis-Guerin\n\n
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
using System.Deployment.Application;

using ChartPackage;
using KFReaderPackage;
using WD1772Package;
using SCPReaderPackage;
using GlobalPackage;
using PastiPackage;
using StdFormatPackage;
using System.Globalization;

namespace Aufit {
	//http://www.thomaslevesque.com/2008/11/18/wpf-binding-to-application-settings-using-a-markup-extension/
	public class SettingBindingExtension : Binding {
		public SettingBindingExtension() {
			Initialize();
		}

		public SettingBindingExtension(string path)
			: base(path) {
			Initialize();
		}

		private void Initialize() {
			this.Source = Aufit.Properties.Settings.Default;
			this.Mode = BindingMode.TwoWay;
		}
	}

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private FluxDrawing _fluxDrawing = null;
		private HistoDrawing _histoDrawing = null;
		private BitWidthDrawing _clockDrawing = null;
		private DataDrawing _dataDrawing = null;
		private bool _modeAuto = false;

		//private BufferWindow _trackWindow = null;
		//private bool _trackWindowIsOpen = false;
		//private BufferWindow _sectorWindow = null;
		//private bool _sectorWindowIsOpen = false;

		// current display
		/// <summary>
		/// The track currently selected and displayed
		/// </summary>
		public static int CurrentTrack { get; set; }
		/// <summary>
		/// The side currently selected and displayed
		/// </summary>
		public static int CurrentSide { get; set; }
		/// <summary>
		/// The revolution currently selected and displayed
		/// </summary>
		public static int CurrentRev { get; set; }

		public static string ImageName { get; set; }
		public static string DirectoryName { get; set; }
		//public bool HasIncompleteData { get; set; }
		//public bool HasProtection { get; set; }
		//public bool STIncompatible { get; set; }


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
			
			//string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			//string[] parts = version.Split('.');
			//int major = int.Parse(parts[0]);
			//int minor = int.Parse(parts[1]);
			//int build = int.Parse(parts[2]);
			//int revision = int.Parse(parts[3]);

			Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			DateTime dateTimeOfBuild = new DateTime(2000, 1, 1) 
                                + new TimeSpan(version.Build, 0, 0, 0) 
                                + TimeSpan.FromSeconds(version.Revision * 2);
			Title = "Aufit Build " + version.Major + "." + version.Minor + " - " + dateTimeOfBuild;
			tbVersion.Text = "Aufit " + version;

			this.Loaded += new RoutedEventHandler(container_loaded);
			//MessageBox.Show(Application.Current.Properties["FileName"].ToString());
			tbFileName.Text = Aufit.Properties.Settings.Default.ImageName;

			// http://stackoverflow.com/questions/896574/forcing-a-wpf-tooltip-to-stay-on-the-screen
			ToolTipService.ShowDurationProperty.OverrideMetadata(
				typeof(DependencyObject), new FrameworkPropertyMetadata(60000));

			initDiskMagnifier();
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
				selectTrack(CurrentTrack, CurrentSide);
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
			_fluxDrawing.drawFlux(CurrentTrack, CurrentSide, CurrentRev);
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
			Global.computeHisto(CurrentTrack, CurrentSide, CurrentRev, infoBox);
			_histoDrawing.drawHisto();
		}

		private void cbLimitClick(object sender, RoutedEventArgs e) {
			Global.computeHisto(CurrentTrack, CurrentSide, CurrentRev, infoBox);
			//_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_histoDrawing.drawHisto();
			_fluxDrawing.setFluxDefault();
			_fluxDrawing.drawFlux(CurrentTrack, CurrentSide, CurrentRev);
		}

		private void cbDetailClick(object sender, RoutedEventArgs e) {
			_fluxDrawing.DetailDraw = cbDetail.IsChecked.Value;
			_fluxDrawing.drawFlux(CurrentTrack, CurrentSide, CurrentRev);
			_clockDrawing.drawBitWidth();
		}

		private void minusRevClicked(object sender, RoutedEventArgs e) {
			int RevCount = 1;
			if (Global.Disk[CurrentTrack, CurrentSide].fluxDataRev != null) {
				RevCount = Global.Disk[CurrentTrack, CurrentSide].fluxDataRev.Count();
			}
			else if (Global.Disk[CurrentTrack, CurrentSide].packedFluxDataRev != null) {
				RevCount = Global.Disk[CurrentTrack, CurrentSide].packedFluxDataRev.Count();
			}
			else return;

			if (CurrentRev == 0)
				CurrentRev = RevCount - 1;
			else
				CurrentRev--;

			//tbRevolution.Text = "Revolution " + (Rev + 1).ToString() + " / " + RevCount.ToString();
			Global.computeHisto(CurrentTrack, CurrentSide, CurrentRev, infoBox);
			_histoDrawing.drawHisto();
			FDC.resetTrack(CurrentTrack, CurrentSide);
			drawAllCharts();
		}

		private void plusRevClicked(object sender, RoutedEventArgs e) {
			int RevCount = 1;
			if (Global.Disk[CurrentTrack, CurrentSide].fluxDataRev != null) {
				RevCount = Global.Disk[CurrentTrack, CurrentSide].fluxDataRev.Count();
			}
			else if (Global.Disk[CurrentTrack, CurrentSide].packedFluxDataRev != null) {
				RevCount = Global.Disk[CurrentTrack, CurrentSide].packedFluxDataRev.Count();
			}
			else return;

			CurrentRev = (CurrentRev + 1) % RevCount;

			//tbRevolution.Text = "Revolution " + (Rev + 1).ToString() + " / " + RevCount.ToString();
			Global.computeHisto(CurrentTrack, CurrentSide, CurrentRev, infoBox);
			_histoDrawing.drawHisto();
			FDC.resetTrack(CurrentTrack, CurrentSide);
			drawAllCharts();
		}
		#endregion


		/// <summary>
		/// Display all the charts for the current Track/Side/Rev
		/// </summary>
		private void drawAllCharts() {
			int track = CurrentTrack;
			int side = CurrentSide;
			int rev = CurrentRev;
			if ((Global.Disk[track, side].fluxDataRev == null) && (Global.Disk[track, side].packedFluxDataRev == null)) return;

			//int RevCount = Global.Disk[track, side].packedFluxDataRev.Count();
			string revString = (Global.Disk[track, side].fluxDataRev != null) ? Global.Disk[track, side].fluxDataRev.Count().ToString() :
				((Global.Disk[track, side].packedFluxDataRev != null) ? (Global.Disk[track, side].packedFluxDataRev.Count().ToString()) : "*");
			tbRevolution.Text = "Rev. " + (CurrentRev + 1).ToString() + "/" + revString;
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

			Global.DiskTrack dt = Global.Disk[track, side];
			tbFluxInfo.AppendText(String.Format(" - {0} bytes {1} sectors", dt.track.buffer.DataByte.Count, dt.sectors.Count));

		}

		////////////////////////
		// DEBUG FLAGS
		////////////////////////
		// when set to true the KF files are read without treading for debug
		private bool _debugNoThread = false;
		// Do we read KF directory using Asynchronous read or as tasks.
		private bool _readKFAsync = true;


		private async void openFileClick(object sender, RoutedEventArgs e) {
			OpenFileDialog ofd = new OpenFileDialog();
			//ofd.FileName = Aufit.Properties.Settings.Default.ImageName;
			ofd.Filter = "Aufit|*.scp;*.raw|All Files|*.*";

			bool? ok = ofd.ShowDialog();
			if (ok == true) {
				Aufit.Properties.Settings.Default.ImageName = ofd.FileName;
				await processFile(ofd.FileName);
			}
		}


		private async void reloadClick(object sender, RoutedEventArgs e) {
			tbFileName.Text = Aufit.Properties.Settings.Default.ImageName;
			await processFile(Aufit.Properties.Settings.Default.ImageName);
		}


		private async void fileDrop(object sender, DragEventArgs e) {
			string[] droppedFiles = null;
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				droppedFiles = e.Data.GetData(DataFormats.FileDrop, true) as string[];
			}

			if ((null == droppedFiles) || (!droppedFiles.Any())) { return; }
			await processFile(droppedFiles[0]);
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


		private async void container_loaded(object sender, RoutedEventArgs e) {
			string suffix = "aufit";
			if (Application.Current.Properties["Suffix"] != null) {
				_modeAuto = true;
				suffix = (string)Application.Current.Properties["Suffix"];
			}

			if (Application.Current.Properties["FileName"] != null) {
				string fname = Application.Current.Properties["FileName"].ToString();
				FileInfo file = new FileInfo(fname);
				if (file.Exists) {
					await processFile(fname);
				}
			}

			if (Application.Current.Properties["DiskView"] != null) {
				_modeAuto = true;
				Thread.Sleep(2000);
				tabDisplay.SelectedIndex = 1;
				await drawDiskLayout();
				double scale = 1.2;
				if (Application.Current.Properties["Scale"] != null) {
					string s = Application.Current.Properties["Scale"].ToString();
					scale = Double.Parse(s, CultureInfo.InvariantCulture);
				}
				Global.saveImage(gridImageView, scale, ".disk_view." + suffix + ".png", false);
			}

			if (Application.Current.Properties["Protections"] != null) {
				_modeAuto = true;
				tabDisplay.SelectedIndex = 3;
				tbAnalysis.Clear();
				tbAnalysis.AppendText(String.Format("Protection analysis for {0}\n\n", ImageName));
				await checkProtectionAsync(tbAnalysis);
				string fileName = Aufit.MainWindow.ImageName + ".protections."+ suffix + ".txt";
				File.WriteAllText(fileName, tbAnalysis.Text);
			}

			if (Application.Current.Properties["SaveImage"] != null) {
				_modeAuto = true;
				tabDisplay.SelectedIndex = 4;
				string fileName = Aufit.MainWindow.ImageName + ".image." + suffix + ".stx";
				uint flags = 0;
				flags |= Global.OPTIMIZE;
				//flags |= Global.FORCE_DS;
				//flags |= Global.DEBUG;
				//flags |= Global.SYNC_HEAD;
				tbSaveImage.Clear();
				tbSaveImage.AppendText(String.Format("Writing STX file {0}\n", fileName));
				PastiWriter pasti = new PastiWriter(tbSaveImage);
				pasti.writePasti(fileName, flags);
				tbSaveImage.AppendText(String.Format("Image {0} write completed", fileName));
			}
			if (_modeAuto) {
				//string fileName = Aufit.MainWindow.ImageName + "." + suffix + ".done";
				//File.Create(fileName).Dispose();
				this.Close();
			}
		}


		private void guiEnable(bool enable) {
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


		private async Task<bool> processFile(string fileName) {
			bool readOK = false;
			CurrentTrack = CurrentSide = CurrentRev = 0;
			Global.resetDisk();
			//HasIncompleteData = false;
			//HasProtection = false;
			//STIncompatible = false;

			tbAnalysis.Clear();
			SectorNotFoundException.resetException();
			diskClear();
			_view = DataView.None;
			fdViewer.Document = null;
			redDot.Visibility = System.Windows.Visibility.Hidden;
			cbOutputFormat.SelectedIndex = 0;
			tbSaveImage.Clear();
			tbMasterView.Clear();
			
			guiEnable(false);
			string extension = System.IO.Path.GetExtension(fileName);
			tbStatus.Text = "READING IMAGE";
			tabCtrl.SelectedIndex = 1;
			tabDisplay.SelectedIndex = 0;

			switch (extension) {
				case ".raw":
					string path = System.IO.Path.GetDirectoryName(fileName);
					string parent = System.IO.Path.GetDirectoryName(path);
					ImageName = path + ".raw";
					DirectoryName = parent;
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
					ImageName = fileName;					
					DirectoryName = System.IO.Path.GetDirectoryName(ImageName);
					readOK = await SCPReader.readScpAsync(fileName, infoBox);
					break;

				default:
					tbFileName.Text = "Sorry input file is an unknown format";
					guiEnable(true);
					return false;
			}

			if (readOK) {
				tbFileName.Text = ImageName;
				selectTrack(0, 0);
			}
			else {
				tbFileName.Text = "Sorry could not read correctly the input file";
				guiEnable(true);
				return false;
			}

			tbStatus.Text = "PROCESSING DATA";
			tabCtrl.SelectedIndex = 0;
			if (_debugNoThread) {
				for (int i = 0; i < 84; i++) {
					for (int j = 0; j < 2; j++) {
						Global.decodeAndRead(i, j, CurrentRev);
						ButtonMatrix.setButtonBG(i, j);
					}
				}
			}
			else {
				BackgroundWorker bgWorker;
				bgWorker = new BackgroundWorker();
				bgWorker.DoWork += new DoWorkEventHandler(bgWorkerDecodeAndReadAll);
				//bgWorker.ProgressChanged += new ProgressChangedEventHandler(xxx)
				bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgWorkerDecodeAndReadCompleted);
				// Launch background thread to do decode flux.  This will trigger BackgroundWorker.DoWork().
				bgWorker.RunWorkerAsync();
			}
			return true;
		}


		// http://stuff.seans.com/2009/05/21/net-basics-do-work-in-background-thread-to-keep-gui-responsive/
		void bgWorkerDecodeAndReadAll(object sender, DoWorkEventArgs e) {
			Task<List<Sector>>[] tasks = new Task<List<Sector>>[168];
			for (int i = 0; i < 84; i++) {
				for (int j = 0; j < 2; j++) {
					FDC.resetTrack(i, j);
					tasks[i + j * 84] = Global.decodeAndReadAsync(i, j, CurrentRev);
				}
			}
			Task.WaitAll(tasks);
		}


		void bgWorkerDecodeAndReadCompleted(object sender, RunWorkerCompletedEventArgs e) {
			if (e.Error != null) {
				MessageBox.Show(e.Error.Message);
				return;
			}

			guiEnable(true);
			for (int i = 0; i < 84; i++) {
				for (int j = 0; j < 2; j++) {
					ButtonMatrix.setButtonBG(i, j);
				}
			}
			string message = SectorNotFoundException.ExceptionMessage;
			if (message != null) {
				infoBox.AppendText(message);
				redDot.Visibility = System.Windows.Visibility.Visible;
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
			CurrentTrack = CurrentSide = CurrentRev = 0;
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

			CurrentTrack = CurrentSide = CurrentRev = 0;
			if (Global.Disk[CurrentTrack, CurrentSide].fluxData != null) {
				ButtonMatrix.selectButton(CurrentTrack, CurrentSide);
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

			CurrentTrack = CurrentSide = CurrentRev = 0;
			if (Global.Disk[CurrentTrack, CurrentSide].fluxData != null) {
				ButtonMatrix.selectButton(CurrentTrack, CurrentSide);
			}
			drawAllCharts();
			return true;
		}



		private void keyDown(object sender, KeyEventArgs e) {
			e.Handled = true;

			int track = CurrentTrack;
			int side = CurrentSide;
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
			CurrentTrack = track;
			CurrentSide = side;
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

		//private void dataMouseLBD(object sender, MouseButtonEventArgs e) {

		//	if (e.ClickCount > 1) {
		//		Point p = e.GetPosition(_dataDrawing.Chart.GraphicCanvas);
		//		int pos = (int)_dataDrawing.Chart.WorldPoint(p).X * 1000000;

		//		foreach (Sector sector in Global.Disk[CurrentTrack, CurrentSide].sectors) {
		//			int startPos = sector.id.info.startTime;
		//			int endPos = (sector.data != null) ? sector.data.info.startTime + sector.data.info.timeLength :
		//				sector.id.info.startTime + sector.id.info.timeLength;
		//			if ((startPos < pos) && (endPos > pos)) {
		//				// infoBox.AppendText(String.Format("Mouse {0} - {1} - sector {2}\n", _dataDrawing.Chart.WorldPoint(p).X, _dataDrawing.Chart.WorldPoint(p).Y, indexStart));

		//				string message = String.Format("Sector {0} - {1} bytes DAM={2:X2} {3}={4:X4} {5}\n",
		//					sector.id.Sector, Global.sectSize(sector.id.Length), sector.data.DAM,
		//					(sector.data.info.crc == 0) ? "Good CRC" : "Bad CRC", sector.data.CRC,
		//					(sector.data.fuzzy != null) ? "Has fuzzy bits" : "");
		//				// TODO
		//				//if (_sectorWindowIsOpen)
		//				//	_sectorWindow.Close();

		//				//_sectorWindow = new BufferWindow();
		//				//_sectorWindow.drawBuffer(sector.data.buffer, message);

		//				//_sectorWindow.Closed += new EventHandler(sectorWindowClosed);
		//				//_sectorWindowIsOpen = true;
		//				//_sectorWindow.Show();

		//			}
		//		}
		//	}
		//}


		private void mainWinClosing(object sender, CancelEventArgs e) {
		}


		private async void btProtectionClick(object sender, RoutedEventArgs e) {
			//await checkProtectionAsync();
			tbAnalysis.Clear();
			tbAnalysis.AppendText(String.Format("Protection analysis for {0}\n\n", ImageName));
			for (int side = 0; side < 2; side++)
				for (int track = 0; track < 84; track++) {
					await Global.decodeAndReadAsync(track, side, 1);
					Protections.checkProtection(track, side, tbAnalysis);
				}
		}


		private async Task checkProtectionAsync(TextBox tb) {
			for (int side = 0; side < 2; side++)
				for (int track = 0; track < 84; track++) {
					await Global.decodeAndReadAsync(track, side, 1);
					Protections.checkProtection(track, side, tb);
					ButtonMatrix.setButtonBG(track, side);
				}
		}


		private void btScreenshotClick(object sender, RoutedEventArgs e) {
			Global.saveImage(mainGrid, 1, "track_view", true);
		}


		private void formatClick(object sender, RoutedEventArgs e) {
		}

		private void btWriteDiskClick(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.InitialDirectory = DirectoryName;
			sfd.RestoreDirectory = true;
			bool status = false;

			uint flags = 0;
			if (cbForceDS.IsChecked.Value == true)
				flags |= Global.FORCE_DS;
			if (cbOptimize.IsChecked.Value == true)
				flags |= Global.OPTIMIZE;
			if (cbDebug.IsChecked.Value == true)
				flags |= Global.DEBUG;
			if (cbSync.IsChecked.Value == true)
				flags |= Global.SYNC_HEAD;


			switch (((ComboBoxItem)cbOutputFormat.SelectedItem).Content.ToString()) {
				case "STX":
					sfd.Filter = "Pasti file|*.stx|All Files|*.*";
					if (sfd.ShowDialog() == true) {
						tbSaveImage.Clear();
						tbSaveImage.AppendText(String.Format("Writing STX file {0}\n", sfd.FileName));
						PastiWriter pasti = new PastiWriter(tbSaveImage);
						status = pasti.writePasti(sfd.FileName, flags);
					}
					break;

				case "ST":
					sfd.Filter = "ST file|*.st|All Files|*.*";
					if (sfd.ShowDialog() == true) {
						tbSaveImage.Clear();
						tbSaveImage.AppendText(String.Format("Writing ST file {0}\n", sfd.FileName));
						StdWriter st = new StdWriter(tbSaveImage);
						status = st.writeStd(sfd.FileName, flags, FileType.ST);
					}
					break;

				case "MSA":
					sfd.Filter = "MSA file|*.msa|All Files|*.*";
					if (sfd.ShowDialog() == true) {
						tbSaveImage.Clear();
						tbSaveImage.AppendText(String.Format("Writing MSA file {0}\n", sfd.FileName));
						StdWriter st = new StdWriter(tbSaveImage);
						status = st.writeStd(sfd.FileName, flags, FileType.MSA);
					}
					break;

				default:
					MessageBox.Show(String.Format("Sorry the {0} format is not yet supported", 
						((ComboBoxItem)cbOutputFormat.SelectedItem).Content.ToString()));
					return;
			}
			if (status)
				tbSaveImage.AppendText(String.Format("Image {0} write succeed\n", sfd.FileName));
			else if (sfd.FileName != "")
				tbSaveImage.AppendText(String.Format("Image {0} write failed\n", sfd.FileName));
		}


		private MessageBoxResult ask(string caption, string messageBoxText) {
			MessageBoxButton button = MessageBoxButton.YesNoCancel;
			MessageBoxImage icon = MessageBoxImage.Question;
			MessageBoxResult defaultResult = MessageBoxResult.Cancel;
			MessageBoxResult result;
			result = MessageBox.Show(messageBoxText, caption, button, icon, defaultResult);
			return result;
		}




		private async void tabDisplaySelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (_modeAuto) return;

			if (tabItemTrack.IsSelected) {
				infoBox.AppendText("Track View Selected\n");
				drawAllCharts();
			}
			if (tabItemDisk.IsSelected) {
				infoBox.AppendText("Disk View Selected\n");
				await drawDiskLayout();
			}
			if (tabItemContent.IsSelected) {
				infoBox.AppendText("Data View Selected\n");
				if ((Global.Disk[CurrentTrack, CurrentSide].track != null) && (fdViewer.Document == null))
					drawLayout(CurrentTrack, CurrentSide, CurrentRev);
			}

			if (tabItemMaster.IsSelected) {
				infoBox.AppendText("Master View Selected\n");
				drawDiskFormat(CurrentTrack, CurrentSide);
			}

			if (tabItemAnalysis.IsSelected) {
				infoBox.AppendText("Analysis Selected\n");

			}

			if (tabItemSave.IsSelected) {
				infoBox.AppendText("Save Image Selected\n");
				await checkWriteImage();
			}
		}

		private async Task checkWriteImage() {
			if (tbSaveImage.Text.Length != 0) return;
			if (Global.Disk[0,0].fluxData == null) return;
			bool hasProtection = false;
			bool STIncompatible = false;
			bool hasIncompleteData = false;
			tbSaveImage.AppendText(Aufit.MainWindow.ImageName);
			tbSaveImage.AppendText("\n");

			await checkProtectionAsync(infoBox);
			cbST.IsEnabled = true;
			cbMSA.IsEnabled = true;
			for (int side = 0; side < 2; side++) {
				for (int track = 0; track < 84; track++) {
					if (Global.Disk[track, side].track != null) {
						hasProtection |= Global.Disk[track, side].track.hasProtections;
						STIncompatible |= (((Global.Disk[track, side].track.flags & Track.NOT_STD_SIZ) != 0) ||
							((Global.Disk[track, side].track.flags & Track.SECTOR_ST) != 0));
					}
					hasIncompleteData |= Global.Disk[track, side].hasIncompleteData;
				}	// all tracks
			}	// all sides

			//tbSaveImage.Clear();
			//tbSaveImage.ScrollToHome();

			if (hasProtection)
				tbSaveImage.AppendText("Has protections - ");
			else
				tbSaveImage.AppendText("No protection found - ");

			if (STIncompatible || hasProtection) {
				tbSaveImage.AppendText("Can only be written with STX format\n");
				cbMSA.IsEnabled = false;
				cbST.IsEnabled = false;
			}	// protected
			else
				tbSaveImage.AppendText("Can be written with STX / ST / MSA formats\n");

			if (hasIncompleteData) {
				MessageBox.Show(
					"The disk contains shifted tracks and you are on the last revolution\nIt is not possible to produce a correct output file",
					"Incomplete Data");
			}	// incomplete data
		}


		private void selectTrack(int track, int side) {
			ButtonMatrix.deselectButton();
			ButtonMatrix.selectButton(track, side);
			FDC.resetTrack(CurrentTrack, CurrentSide);
			drawAllCharts();
			Protections.checkProtection(track, side, infoBox);
			if (tabItemDisk.IsSelected)
				drawDiskSelectedTrack(track, side);

			if (tabItemMaster.IsSelected) {
				drawDiskFormat(track, side);
			}

			if (tabItemContent.IsSelected) {
				switch (_view) {
					case DataView.Track:
						drawTrackBuffer(track, side, CurrentRev);
						break;
					case DataView.Sector:
						drawSectorBuffer(track, side, CurrentRev);
						break;
					case DataView.Layout:
						drawLayout(track, side, CurrentRev);
						break;
				}
			}
		}


		private void anaSaveClick(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.InitialDirectory = DirectoryName;
			sfd.RestoreDirectory = true;

			sfd.Filter = "Text file|*.txt|All Files|*.*";
			if (sfd.ShowDialog() == true)
				File.WriteAllText(sfd.FileName, tbAnalysis.Text);
		}


		private void tabHistoInfoChanged(object sender, SelectionChangedEventArgs e) {
			if (tabItemInfo.IsSelected) {
				btInfClear.Visibility = System.Windows.Visibility.Visible;
				btInfSave.Visibility = System.Windows.Visibility.Visible;
			}
			else {
				btInfClear.Visibility = System.Windows.Visibility.Hidden;
				btInfSave.Visibility = System.Windows.Visibility.Hidden;
			}
		}


		private void infoSaveClick(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.InitialDirectory = DirectoryName;
			sfd.RestoreDirectory = true;

			sfd.Filter = "Text file|*.txt|All Files|*.*";

			if (sfd.ShowDialog() == true)
				File.WriteAllText(sfd.FileName, infoBox.Text);
		}

		private void infoClearClick(object sender, RoutedEventArgs e) {
			infoBox.Clear();
		}

		private async void btWriteClearClick(object sender, RoutedEventArgs e) {
			tbSaveImage.Clear();
			await checkWriteImage();
		}

		private void btWriteSaveClick(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.InitialDirectory = DirectoryName;
			sfd.RestoreDirectory = true;

			sfd.Filter = "Text file|*.txt|All Files|*.*";

			if (sfd.ShowDialog() == true)
				File.WriteAllText(sfd.FileName, tbSaveImage.Text);
		}

		private bool checkForUpdate(bool showMessage) {
			bool doUpdate = false;
			UpdateCheckInfo info = null;

			if (ApplicationDeployment.IsNetworkDeployed) {
				ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

				try {
					info = ad.CheckForDetailedUpdate();
				}
				catch (DeploymentDownloadException dde) {
					MessageBox.Show("A new version of the application cannot be downloaded at this time. \n\nPlease check your network connection, or try again later. Error: " + dde.Message);
					return false;
				}
				catch (InvalidDeploymentException ide) {
					MessageBox.Show("Cannot check for a new version of the application. The ClickOnce deployment is corrupt. Please redeploy the application and try again. Error: " + ide.Message);
					return false;
				}
				catch (InvalidOperationException ioe) {
					MessageBox.Show("This application cannot be updated. It is likely not a ClickOnce application. Error: " + ioe.Message);
					return false;
				}

				if (info.UpdateAvailable) {
					doUpdate = true;
					if (showMessage) {
						MessageBoxResult result = MessageBox.Show(
							"An update is available. Would you like to update the application now?",
							"Update Available", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.OK);
						if (result != MessageBoxResult.OK)
							doUpdate = false;
					}
				}
				else
					if (showMessage) MessageBox.Show("No Update Available at this time");

			}	// network deployed
			else
				MessageBox.Show("This application is not a ClickOnce application");
			return doUpdate;
		}


		private void InstallUpdate() {
			ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;
			try {
				ad.Update();
				MessageBox.Show("The application has been updated and will be restarted");
				//System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
				String ApplicationEntryPoint = ApplicationDeployment.CurrentDeployment.UpdatedApplicationFullName;
				System.Diagnostics.Process.Start(ApplicationEntryPoint);
				Application.Current.Shutdown();
			}
			catch (DeploymentDownloadException dde) {
				MessageBox.Show("Cannot install the latest version of the application. \n\nPlease check your network connection, or try again later. Error: " + dde);
				return;
			}
		}


		private void updateClicked(object sender, RoutedEventArgs e) {
			bool update = checkForUpdate(true);
			if (update)
				InstallUpdate();
		}

		private void helpClicked(object sender, RoutedEventArgs e) {
			try {
				System.Diagnostics.Process.Start("Aufit.pdf");
			}
			catch (Exception exc) {
				MessageBox.Show(String.Format("Error: {0}", exc.Message));
			}
		}


		//MessageBoxResult result = MessageBox.Show(
		//	"The disk contains shifted tracks and you are on the last revolution\nIt is not possible to produce a correct output file",
		//	"Incomplete Image Data", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
		//if (result == MessageBoxResult.No) return;


	}
}
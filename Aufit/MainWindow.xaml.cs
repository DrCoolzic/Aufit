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
using StreamNS;
using WD1772NS;

namespace Aufit {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private FluxDrawing _fluxDrawing = null;
		private HistoDrawing _histoDrawing = null;
		private ClockDrawing _clockDrawing = null;
		private DataDrawing _dataDrawing = null;

		private FDC _trackInfo = null;
		private BufferWindow _bufferWindow = null;
		private bool _bufferWindowIsOpen = false;

		//public double MinClock { get; set; }
		//public double MaxClock { get; set; }
		//public double minFlux { get; set; }
		//public double maxFlux { get; set; }

		/// <summary>
		/// Initialize the main window
		/// </summary>
		public MainWindow() {
			InitializeComponent();

			_fluxDrawing = new FluxDrawing(fluxGrid, cvTextFlux, cvGraphicFlux, tbFluxInfo);
			_fluxDrawing.SlowDraw = cbSlow.IsChecked.Value;
			_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_fluxDrawing.Chart.FixOffset = true;

			_histoDrawing = new HistoDrawing(histoGrid, cvTextHisto, cvGraphicHisto);

			_clockDrawing = new ClockDrawing(clockGrid, cvTextClock, cvGraphicClock);
			_clockDrawing.Chart.OptimalYSpacing = 18;
			_clockDrawing.Chart.FixOffset = true;

			_dataDrawing = new DataDrawing(dataGrid, cvTextData, cvGraphicData);
			_dataDrawing.Chart.HasYGrid = false;
			_dataDrawing.Chart.FixOffset = true;

			_trackInfo = new FDC();

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
			_fluxDrawing.drawFlux();
		}

		private void fluxMouseLBU(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseLBU(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.ClockMin, _clockDrawing.ClockMax);
			_clockDrawing.drawClock(_fluxDrawing.SpeedUp);
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData(_trackInfo);

		}

		private void fluxMouseLBD(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseLBD(sender, e);
		}

		private void fluxMouseWheel(object sender, MouseWheelEventArgs e) {
			_fluxDrawing.mouseWheel(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.ClockMin, _clockDrawing.ClockMax);
			_clockDrawing.drawClock(_fluxDrawing.SpeedUp);
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData(_trackInfo);
		}

		private void fluxMouseMove(object sender, MouseEventArgs e) {
			_fluxDrawing.mouseMove(sender, e);
		}

		private void fluxMouseRBDown(object sender, MouseButtonEventArgs e) {
			_fluxDrawing.mouseRBD(sender, e);
			_clockDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, _clockDrawing.ClockMin, _clockDrawing.ClockMax);
			_clockDrawing.drawClock(_fluxDrawing.SpeedUp);
			_dataDrawing.Chart.setViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData(_trackInfo);

		}
		#endregion


		#region histogram related
		private void histoGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_histoDrawing.drawHisto(cbHistoLog.IsChecked.Value, cbLimitFlux.IsChecked.Value);
		}
		#endregion


		#region options
		private void cbHistoLogClick(object sender, RoutedEventArgs e) {
			ProcessStream.computeHisto(infoBox);
			_histoDrawing.drawHisto(cbHistoLog.IsChecked.Value, cbLimitFlux.IsChecked.Value);
		}

		private void cbLimitClick(object sender, RoutedEventArgs e) {
			ProcessStream.computeHisto(infoBox);
			_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_histoDrawing.drawHisto(cbHistoLog.IsChecked.Value, cbLimitFlux.IsChecked.Value);
			_fluxDrawing.setFluxDefault();
			_fluxDrawing.drawFlux();
		}

		private void cbSlowClick(object sender, RoutedEventArgs e) {
			_fluxDrawing.SlowDraw = cbSlow.IsChecked.Value;
			_fluxDrawing.drawFlux();
			_clockDrawing.drawClock(_fluxDrawing.SpeedUp);
		}

		private void minusRevClicked(object sender, RoutedEventArgs e) {
			if (ProcessStream.currentTrack().indexes == null) return;
			if (ProcessStream.Rev == 0)
				ProcessStream.Rev = ProcessStream.currentTrack().indexCount - 1;
			else ProcessStream.Rev--;

			revolution.Text = ProcessStream.Rev.ToString();
			ProcessStream.computeHisto(infoBox);
			_histoDrawing.drawHisto(cbHistoLog.IsChecked.Value, cbLimitFlux.IsChecked.Value);
			if ((ProcessStream.Rev == 0) || (ProcessStream.Rev == ProcessStream.currentTrack().indexes.Length - 1))
				_fluxDrawing.setFluxDefault();
			//_fluxDrawing.drawFlux();
			drawCharts();
		}

		private void plusRevClicked(object sender, RoutedEventArgs e) {
			if (ProcessStream.currentTrack().indexes == null) return;
			if (ProcessStream.Rev == (ProcessStream.currentTrack().indexCount - 1))
				ProcessStream.Rev = 0;
			else ProcessStream.Rev++;

			revolution.Text = ProcessStream.Rev.ToString();
			ProcessStream.computeHisto(infoBox);
			_histoDrawing.drawHisto(cbHistoLog.IsChecked.Value, cbLimitFlux.IsChecked.Value);
			if ((ProcessStream.Rev == 0) || (ProcessStream.Rev == 1))
				_fluxDrawing.setFluxDefault();
			//_fluxDrawing.drawFlux();
			drawCharts();
		}
		#endregion


		/// <summary>
		/// Display all the charts for the current Track/Side/Rev
		/// </summary>
		public void drawCharts() {
			revolution.Text = ProcessStream.Rev.ToString();
			ProcessStream.mfmDecode();

			cbLimitFlux.IsChecked = (ProcessStream.computeHisto(infoBox) > 0);
			_fluxDrawing.LimitFlux = cbLimitFlux.IsChecked.Value;
			_histoDrawing.drawHisto(cbHistoLog.IsChecked.Value, cbLimitFlux.IsChecked.Value);

			_fluxDrawing.setFluxDefault();
			_fluxDrawing.drawFlux();

			_clockDrawing.setClockDefault();
			_clockDrawing.drawClock(_fluxDrawing.SpeedUp);

			_dataDrawing.Chart.setDefaultViewport(_fluxDrawing.ViewportXmin, _fluxDrawing.ViewportXmax, 0, 1);
			_dataDrawing.drawTrackData(_trackInfo);

		}

		void bufferWindow_Closed(object sender, EventArgs e) {
			_bufferWindowIsOpen = false;
		}


		private void openFileClick(object sender, RoutedEventArgs e) {
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Filter = "Stream Files|*.raw|All Files|*.*";
			bool? ok = ofd.ShowDialog();
			if (ok == true) {
				FileName.Text = ofd.FileName;
				ProcessStream.Track = ProcessStream.Side = 0;
				ProcessStream.Rev = 1;

				BM1.Visibility = Visibility.Hidden;
				BM2.Visibility = Visibility.Hidden;
				tabCtrl.Width = matrixGrid.ActualWidth;
				Grid.SetColumn(tabCtrl, 0);
				Grid.SetColumnSpan(tabCtrl, 3);

				bool status = ProcessStream.readStreamTrack(ofd.FileName, ProcessStream.Track, ProcessStream.Side);
				if (status) {
					infoBox.Clear();
					drawCharts();
				}
			}
		}


		private void openDirectoryClick(object sender, RoutedEventArgs e) {
			VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
			dialog.Description = "Please select a folder.";
			dialog.UseDescriptionForTitle = true;
			if ((bool)dialog.ShowDialog(this)) {
				string selectedDir = dialog.SelectedPath;
				ProcessStream.Rev = 1;

				BM1.Visibility = Visibility.Visible;
				BM2.Visibility = Visibility.Visible;
				tabCtrl.Width = matrixGrid.ActualWidth - 320;
				Grid.SetColumn(tabCtrl, 2);
				Grid.SetColumnSpan(tabCtrl, 1);

				DirectoryInfo di = new DirectoryInfo(selectedDir);
				FileInfo[] fi = di.GetFiles("*.raw");
				if (fi.Length != 0) {
					infoBox.Clear();
					string file = fi[0].Name;
					string path = fi[0].DirectoryName;
					string root = file.Remove(file.Length - 8, 8);
					FileName.Text = selectedDir;

					if (cbDispRead.IsChecked == true)
						readAllTracksAsync(path, root);
					else
						readAllTracksTasks(path, root);
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
			for (ProcessStream.Side = 0; ProcessStream.Side < 2; ProcessStream.Side++) {
				for (ProcessStream.Track = 0; ProcessStream.Track < 84; ProcessStream.Track++) {
					//ProcessStream.mfmDecode();
					double entropy = ProcessStream.computeHisto(infoBox);
					//double entropy = ProcessStream.currentTrack().entropy;
					if (ProcessStream.currentTrack().flux != null)
						ButtonMatrix.setButtonBG(entropy);
				}
			}
			ButtonMatrix.selectButton(0, 0);

			ProcessStream.Track = ProcessStream.Side = 0;
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

			for (ProcessStream.Side = 0; ProcessStream.Side < 2; ProcessStream.Side++) {
				for (ProcessStream.Track = 0; ProcessStream.Track < 84; ProcessStream.Track += 1) {
					fname = String.Format("{0}\\{1}{2:D2}.{3}.raw", path, root, ProcessStream.Track, ProcessStream.Side);
					bool streamOK = await ProcessStream.readStreamTrackAsync(fname, ProcessStream.Track, ProcessStream.Side);
					if (streamOK) {
						drawCharts();
						//StreamProcess.computeHisto(infoBox);
						ButtonMatrix.setButtonBG(ProcessStream.currentTrack().entropy);
					}
				}
			}

			_fluxDrawing.FastDraw = false;
			ProcessStream.Track = 83;
			ProcessStream.Side = 1;
			if (ProcessStream.currentTrack().flux != null) {
				ButtonMatrix.selectButton(83, 1);
			}
			//displayStream();
		}


		private void keyDown(object sender, KeyEventArgs e) {
			e.Handled = true;

			if (BM1.Visibility != Visibility.Visible) return;

			int track = ProcessStream.Track;
			int side = ProcessStream.Side;
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

			if (ProcessStream.DiskTracks[track, side].flux == null) return;
			ProcessStream.Track = track;
			ProcessStream.Side = side;
			ButtonMatrix.deselectButton();
			ButtonMatrix.selectButton(track, side);
			drawCharts();
		}


		private void clockGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_clockDrawing.drawClock(_fluxDrawing.SpeedUp);
		}

		private void dataGridSizeChanged(object sender, SizeChangedEventArgs e) {
			_dataDrawing.drawTrackData(_trackInfo);
		}

		private void sectGridSizeChanged(object sender, SizeChangedEventArgs e) {

		}

		private void dataMouseLBD(object sender, MouseButtonEventArgs e) {

			if (e.ClickCount > 1) {
				Point p = e.GetPosition(_dataDrawing.Chart.GraphicCanvas);
				infoBox.AppendText(String.Format("Mouse {0} - {1} \n", _dataDrawing.Chart.WorldPoint(p).X, _dataDrawing.Chart.WorldPoint(p).Y));
			}
		}

		private void mainWinClosing(object sender, CancelEventArgs e) {
			if (_bufferWindowIsOpen)
				_bufferWindow.Close();
		}

		private void btBufferClick(object sender, RoutedEventArgs e) {
			if (_trackInfo.TrackInfo == null) return;			
			if (_bufferWindowIsOpen)
				_bufferWindow.Close();

			_bufferWindow = new BufferWindow(_trackInfo.TrackInfo.buffer);
			_bufferWindow.Closed += new EventHandler(bufferWindow_Closed);
			_bufferWindowIsOpen = true;
			_bufferWindow.Show();
		}

	}
}
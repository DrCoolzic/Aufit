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
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Diagnostics;

namespace MoveAufitOutput {
	class FileToProcess {
		public string Name;
		public bool Done;
	}


	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private int _count = 0;
		private int _done = 0;
		private List<FileToProcess> _fileToProcessList = null;
		private bool _run = false;

		public MainWindow() {
			InitializeComponent();
			Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			DateTime dateTimeOfBuild = new DateTime(2000, 1, 1)
								+ new TimeSpan(version.Build, 0, 0, 0)
								+ TimeSpan.FromSeconds(version.Revision * 2);
			Title = "Move Aufit Output V" + version.Major + "." + version.Minor + " - " + dateTimeOfBuild;
			tbSource.Text = Properties.Settings.Default.SourcePath;
			tbDest.Text = Properties.Settings.Default.DestPath.ToString();
		}


		private void sourceDirClick(object sender, RoutedEventArgs e) {
			VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
			dialog.Description = "Please select a folder.";
			dialog.UseDescriptionForTitle = true;
			if ((bool)dialog.ShowDialog(this)) {
				string selectedDir = dialog.SelectedPath;
				Properties.Settings.Default.SourcePath = selectedDir;
				Properties.Settings.Default.Save();
				tbSource.Text = selectedDir;
			}
		}


		private void destDirClick(object sender, RoutedEventArgs e) {
			VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
			dialog.Description = "Please select a folder.";
			dialog.UseDescriptionForTitle = true;
			if ((bool)dialog.ShowDialog(this)) {
				string selectedDir = dialog.SelectedPath;
				Properties.Settings.Default.DestPath = selectedDir;
				Properties.Settings.Default.Save();
				tbDest.Text = selectedDir;
			}
		}


		void ProcessDir(string dir) {
			string ai = System.IO.Path.Combine(dir, "aufit.ignore.txt");
			if (File.Exists(ai)) return;	// ignore dir

			foreach (string fileName in Directory.EnumerateFiles(dir)) {
				string aufit = ".aufit.";

				if (fileName.IndexOf(aufit) != -1) {
					FileToProcess ftp = new FileToProcess();
					ftp.Name = fileName;
					ftp.Done = false;
					_fileToProcessList.Add(ftp);
					_count++;
				}	// Aufit file found
			}	// for all files
		}


		private async Task ProcessSubDir(string currentDir) {
			foreach (string subDir in Directory.EnumerateDirectories(currentDir)) { 
				string ai = System.IO.Path.Combine(subDir, "aufit.ignore.txt");
				if (File.Exists(ai)) return;	// ignore dir
				await Task.Run(() => ProcessDir(subDir));
				await ProcessSubDir(subDir);
			}
		}


		private async Task scanTree(string sourceDir) {
			if (Properties.Settings.Default.SourcePath == "") {
				tbResult.AppendText("No source path specified!\n");
				return;
			}
			pbProgress.IsIndeterminate = true;
			tbResult.Clear();
			_count = 0;
			_done = 0;
			_fileToProcessList = new List<FileToProcess>();
			ProcessDir(sourceDir); 
			await ProcessSubDir(sourceDir);
			tbTotal.Text = _count.ToString();
			tbDone.Text = _done.ToString();
			string source = Properties.Settings.Default.SourcePath;
			foreach (FileToProcess ftp in _fileToProcessList) {
				string name = ftp.Name.Substring(source.Length + 1);
				tbResult.AppendText(name + "\n");
			}
			tbResult.ScrollToEnd();
			pbProgress.IsIndeterminate = false;
			pbProgress.Minimum = 0;
			pbProgress.Maximum = _count;
			pbProgress.Value = _done;
		}


		private async void btScanClicked(object sender, RoutedEventArgs e) {
			await scanTree(Properties.Settings.Default.SourcePath);
		}


		private void btStopClicked(object sender, RoutedEventArgs e) {
			if (!_run) return;
			_run = false;
			tbResult.AppendText("   Stop Requested\n");
			tbResult.ScrollToEnd();
		}


		private async void btMoveClicked(object sender, RoutedEventArgs e) {
			if (_fileToProcessList == null) {
				tbResult.AppendText("Nothing to process! Did you scan first?\n");
				return;
			}
			if (Properties.Settings.Default.DestPath == "") {
				tbResult.AppendText("No destination path specified!\n");
				return;
			}
			_run = true;

			pbProgress.IsIndeterminate = false;
			pbProgress.Minimum = 0;
			pbProgress.Maximum = _count;
			pbProgress.Value = _done;
			int processed = 0;
			Stopwatch s = new Stopwatch();
			s.Start();

			foreach (FileToProcess ftp in _fileToProcessList) {
				if (!ftp.Done) {
					string source = ftp.Name.Substring(Properties.Settings.Default.SourcePath.Length + 1);
					string destination = System.IO.Path.Combine(Properties.Settings.Default.DestPath, source);
					//tbResult.AppendText("Processing " + nameFromSource + "\n");

					DirectoryInfo parent = Directory.GetParent(destination);
					if (!System.IO.Directory.Exists(parent.FullName)) {
						System.IO.Directory.CreateDirectory(parent.FullName);
					}

					tbResult.AppendText("Moving to: " + destination + "\n");
					FileStream sourceStream = File.Open(ftp.Name, FileMode.Open);
					FileStream destStream = File.Create(destination);
					await sourceStream.CopyToAsync(destStream);
					sourceStream.Close();
					File.Delete(ftp.Name);
					processed++;
					_done++;
					ftp.Done = true;
					tbDone.Text = _done.ToString();
					pbProgress.Value = _done;

					tbResult.ScrollToEnd();
				}
				if (!_run) break;
			}

			s.Stop();
			TimeSpan elapsed = s.Elapsed;
			tbResult.AppendText(String.Format("***** {0} file moved - Elapsed Time = {1:F2}s\n", processed, elapsed.TotalSeconds));
			_run = false;			
		}

	}
}

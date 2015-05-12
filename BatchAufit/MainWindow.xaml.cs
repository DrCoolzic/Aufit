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
using Microsoft.Win32;


namespace BatchAufit {
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
			string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			Title = "BatchAufit " + version;

		}


		void ProcessDir(DirectoryInfo di, string suffix) {
			foreach (FileInfo fi in di.GetFiles()) {
				string baseName;
				FileToProcess ftp = new FileToProcess();
				ftp.Name = fi.FullName;

				if ((fi.Extension == ".scp") || (fi.Extension == ".raw")) {
					if (fi.Extension == ".scp")
						baseName = fi.FullName;
					else
						baseName = fi.Directory + ".raw";

					if ((File.Exists(baseName + ".disk_view." + suffix + ".png")) ||
						(File.Exists(baseName + ".image." + suffix + ".stx")) ||
						(File.Exists(baseName + ".protections." + suffix + ".txt"))) {
						ftp.Done = true;
						_done++;
					}
					else ftp.Done = false;
					_count++;
					_fileToProcessList.Add(ftp);
					if (fi.Extension == ".raw") break;
				}
			}
		}


		// https://support.microsoft.com/fr-fr/kb/303974
		// http://stackoverflow.com/questions/929276/how-to-recursively-list-all-the-files-in-a-directory-in-c/929418#929418
		private async Task<bool> ProcessSubDir(string sDir) {
			foreach (string d in Directory.GetDirectories(sDir)) {
				string ai = System.IO.Path.Combine(sDir, "aufit.ignore.txt");
				if (File.Exists(ai))
					return false;
				string suffix = tbSuffix.Text;
				DirectoryInfo di = new DirectoryInfo(d);
				await Task.Run(() => ProcessDir(di, suffix));
				await ProcessSubDir(d);
			}
			return true;
		}


		private async void RootDirectoryClicked(object sender, RoutedEventArgs e) {
			VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
			dialog.Description = "Please select a folder.";
			dialog.UseDescriptionForTitle = true;
			if ((bool)dialog.ShowDialog(this)) {
				pbProgress.Visibility = System.Windows.Visibility.Visible;
				tbResult.Clear();
				_count = 0;
				_done = 0;
				_fileToProcessList = new List<FileToProcess>();
				string selectedDir = dialog.SelectedPath;
				tbRootDirName.Text = selectedDir;
				DirectoryInfo di = new DirectoryInfo(selectedDir);
				ProcessDir(di, tbSuffix.Text);
				await ProcessSubDir(selectedDir);
				pbProgress.Visibility = System.Windows.Visibility.Hidden;
				tbTotal.Text = _count.ToString();
				tbDone.Text = _done.ToString();
				foreach (FileToProcess ftp in _fileToProcessList) {
					tbResult.AppendText(ftp.Name);
					tbResult.AppendText(ftp.Done ? " - Already Done\n" : " - To Do\n");
				}
			}
		}


		private void StartAufitAndWait(string arguments) {
			Process p = new Process();
			p.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "\\Aufit.exe";
			p.StartInfo.Arguments = arguments;
			p.Start();
			p.WaitForExit();
		}


		private async void btStartClicked(object sender, RoutedEventArgs e) {
			if (_fileToProcessList == null) return;
			_run = true;
			string arguments = "-suffix " + tbSuffix.Text + " " + "-scale " + tbSCale.Text + " ";
			if (cbDiskView.IsChecked == true) arguments += "-disk_view ";
			if (cbProt.IsChecked == true) arguments += "-protections ";
			if (cbSave.IsChecked == true) arguments += "-save_image ";
			pbProgress.IsIndeterminate = false;
			pbProgress.Minimum = 0;
			pbProgress.Maximum = _count;
			pbProgress.Visibility = System.Windows.Visibility.Visible;
			pbProgress.Value = _done;
			foreach (FileToProcess ftp in _fileToProcessList) {
				if (ftp.Done)
					tbResult.AppendText("Skipping " + ftp.Name + "\n");
				else {
					tbResult.AppendText("Processing " + ftp.Name + "\n");
					string full = arguments + "\"" + ftp.Name + "\"";
					await Task.Run(() => StartAufitAndWait(full));
					ftp.Done = true;
					_done++;
					tbDone.Text = _done.ToString();
					pbProgress.Value = _done;
				}
				if (!_run) break;
			}
			pbProgress.Visibility = System.Windows.Visibility.Hidden;
			_run = false;
		}


		//// http://stackoverflow.com/questions/17180500/wpf-application-starting-process-with-waitforexit-on-ui-thread
		//Task StartAndWaitProcess(string arguments) {
		//	Process p = new Process();
		//	p.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "\\Aufit.exe";
		//	p.StartInfo.Arguments = arguments;
		//	p.EnableRaisingEvents = true;
		//	var tcs = new TaskCompletionSource<bool>();
		//	p.Exited += (sender, args) => { tcs.SetResult(true); p.Dispose(); };
		//	Task.Factory.StartNew(() => p.Start());
		//	return tcs.Task;
		//}


		private void btStopClicked(object sender, RoutedEventArgs e) {
			if (!_run) return;
			_run = false;
			tbResult.AppendText("Batch Processing will stop at completion of current Aufit run\n");
		}


		private void btCleanClick(object sender, RoutedEventArgs e) {
			if (_fileToProcessList == null) return;
			string baseName;
			foreach (FileToProcess ftp in _fileToProcessList) {
				if (ftp.Done) {
					FileInfo fi = new FileInfo(ftp.Name);
					if (fi.Extension == ".raw") {
						 baseName = fi.DirectoryName + ".raw";
					}
					else
						baseName = ftp.Name;
					tbResult.AppendText("Cleaned " + baseName);
					File.Delete(baseName + ".disk_view." + tbSuffix.Text + ".png");
					File.Delete(baseName + ".image." + tbSuffix.Text + ".stx");
					File.Delete(baseName + ".protections." + tbSuffix.Text + ".txt");
					File.Delete(baseName + "." + tbSuffix.Text + ".done");
					ftp.Done = false;
					_done--;
					tbDone.Text = _done.ToString();
				}
				else {
					tbResult.AppendText("Already clean " + ftp.Name);
				}
				tbResult.AppendText("\n");
			}
		}

		private void btExitClick(object sender, RoutedEventArgs e) {
			this.Close();
		}


	}


}

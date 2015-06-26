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

	struct ProcessInfo {
		public int exitCode;
		public DateTime exitTime;
		public DateTime strartTime;
		public TimeSpan processorTime;
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
			tbRootDirName.Text = Properties.Settings.Default.RootDirName;
			tbMaxProcessTime.Text = Properties.Settings.Default.MaxProcessTime.ToString();
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
				string selectedDir = dialog.SelectedPath;
				Properties.Settings.Default.RootDirName = selectedDir;
				Properties.Settings.Default.Save();
				tbRootDirName.Text = selectedDir;
				await ProcessDir(selectedDir);
			}
		}


		private async Task ProcessDir(string selectedDir) {
			pbProgress.IsIndeterminate = true;
			pbProgress.Visibility = System.Windows.Visibility.Visible;
			tbResult.Clear();
			_count = 0;
			_done = 0;
			_fileToProcessList = new List<FileToProcess>();
			DirectoryInfo di = new DirectoryInfo(selectedDir);
			ProcessDir(di, tbSuffix.Text);
			await ProcessSubDir(selectedDir);
			pbProgress.Visibility = System.Windows.Visibility.Hidden;
			tbTotal.Text = _count.ToString();
			tbDone.Text = _done.ToString();
			foreach (FileToProcess ftp in _fileToProcessList) {
				tbResult.AppendText((ftp.Done ? "Done - " : "ToDo - ") + ftp.Name + "\n");
			}
			tbResult.ScrollToEnd();
		}


		private async void ReloadClicked(object sender, RoutedEventArgs e) {
			if (!Directory.Exists(Properties.Settings.Default.RootDirName)) return;
			await ProcessDir(Properties.Settings.Default.RootDirName);
		}


		private ProcessInfo StartAufitAndWait(string arguments) {
			Process p = new Process();
			ProcessInfo pi = new ProcessInfo();
			p.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "\\Aufit.exe";
			p.StartInfo.Arguments = arguments;
			p.Start();
			int pt = Properties.Settings.Default.MaxProcessTime;
			if (pt != -1) pt *= 1000;
			bool done = p.WaitForExit(pt);
			if (!done) p.Kill();
			pi.exitCode = p.ExitCode;
			pi.exitTime = p.ExitTime;
			pi.strartTime = p.StartTime;
			pi.processorTime = p.TotalProcessorTime;
			return pi;
		}


		private async void btStartClicked(object sender, RoutedEventArgs e) {
			
			if (_fileToProcessList == null) return;
			_run = true;
			string arguments = "-suffix " + tbSuffix.Text + " " + "-scale " + tbScale.Text + " ";
			if (cbDiskView.IsChecked == true) arguments += "-disk_view ";
			if (cbProt.IsChecked == true) arguments += "-protections ";
			if (cbSave.IsChecked == true) arguments += "-save_image ";
			pbProgress.IsIndeterminate = false;
			pbProgress.Minimum = 0;
			pbProgress.Maximum = _count;
			pbProgress.Visibility = System.Windows.Visibility.Visible;
			pbProgress.Value = _done;
			int processed = 0;
			int succeed = 0;
			int failed = 0;
			TimeSpan elapsed = new TimeSpan(0);
			TimeSpan cpu = new TimeSpan(0);
			TimeSpan maxElapsed = new TimeSpan();
			foreach (FileToProcess ftp in _fileToProcessList) {
				if (!ftp.Done) {
					ProcessInfo pi = new ProcessInfo();
					tbResult.AppendText("Processing " + ftp.Name + "\n");
					string full = arguments + "\"" + ftp.Name + "\"";
					await Task.Run(() => pi = StartAufitAndWait(full));
					processed++;
					cpu += pi.processorTime;
					TimeSpan elap = pi.exitTime - pi.strartTime;

					if (pi.exitCode == 0) {
						if (elap > maxElapsed) maxElapsed = elap;
						elapsed += elap;
						tbResult.AppendText("   Succeed - ");
						ftp.Done = true;
						_done++;
						tbDone.Text = _done.ToString();
						pbProgress.Value = _done;
						succeed++;
					}
					else {
						tbResult.AppendText("   Failed=" + pi.exitCode.ToString() + " - ");
						failed++;
					}
					tbResult.AppendText(" Started at " + pi.strartTime.ToString());
					tbResult.AppendText(String.Format(" - Time elapsed={0:F2}s CPU={1:F2}s\n",
						elap.TotalSeconds, pi.processorTime.TotalSeconds));
					tbResult.ScrollToEnd();
				}
				if (!_run) break;
			}
			tbResult.AppendText(String.Format("***** {0} images processed - ({1} succeeded, {2} failed) *****\n", processed, succeed, failed));
			tbResult.AppendText(String.Format("      Time elapsed = {0:F2}s (Max={1:F2}s Avg={2:F2}s) - CPU = {3:F2}s\n",
				elapsed.TotalSeconds, maxElapsed.TotalSeconds, maxElapsed.TotalSeconds / succeed, cpu.TotalSeconds));
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
			tbResult.AppendText("   Stop Requested - Waiting for Aufit to finish\n");
			tbResult.ScrollToEnd();
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
					tbResult.AppendText("\n");
					File.Delete(baseName + ".disk_view." + tbSuffix.Text + ".png");
					File.Delete(baseName + ".image." + tbSuffix.Text + ".stx");
					File.Delete(baseName + ".protections." + tbSuffix.Text + ".txt");
					ftp.Done = false;
					_done--;
					tbDone.Text = _done.ToString();
				}                                                 
				tbResult.ScrollToEnd();
			}
		}

		private void btExitClick(object sender, RoutedEventArgs e) {
			this.Close();
		}


		private void SaveLogClicked(object sender, RoutedEventArgs e) {
			SaveFileDialog sfd = new SaveFileDialog();
			sfd.InitialDirectory = Properties.Settings.Default.RootDirName;
			sfd.RestoreDirectory = true;
			sfd.Filter = "Log file|*.txt|All Files|*.*";
			if (sfd.ShowDialog() == true)
				File.WriteAllText(sfd.FileName, tbResult.Text);
		}

		private void MaxProcessTimeChanged(object sender, TextChangedEventArgs e) {
			if (tbMaxProcessTime == null) return;
			if (tbMaxProcessTime.Text == "") return;
			try {
				Properties.Settings.Default.MaxProcessTime = int.Parse(tbMaxProcessTime.Text);
				Properties.Settings.Default.Save();
			}
			catch { }
		}


	}


}

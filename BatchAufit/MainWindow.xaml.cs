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

		public MainWindow() {
			InitializeComponent();
		}


		private void RootDirectoryClicked(object sender, RoutedEventArgs e) {
			VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
			dialog.Description = "Please select a folder.";
			dialog.UseDescriptionForTitle = true;
			if ((bool)dialog.ShowDialog(this)) {
				tbAction.Text = "Scanning Directory";
				pbProgress.Visibility = System.Windows.Visibility.Visible;
				tbResult.Clear();
				_count = 0;
				_done = 0;
				_fileToProcessList = new List<FileToProcess>();
				string selectedDir = dialog.SelectedPath;
				tbRootDirName.Text = selectedDir;
				DirectoryInfo di = new DirectoryInfo(selectedDir);
				ProcessDir(di);
				ProcessSubDir(selectedDir);
				tbAction.Text = "Waiting ...";
				pbProgress.Visibility = System.Windows.Visibility.Hidden;
			}
		}


		// https://support.microsoft.com/fr-fr/kb/303974
		// http://stackoverflow.com/questions/929276/how-to-recursively-list-all-the-files-in-a-directory-in-c/929418#929418
		void ProcessSubDir(string sDir) {
			foreach (string d in Directory.GetDirectories(sDir)) {
				DirectoryInfo di = new DirectoryInfo(d);
				ProcessDir(di);
				ProcessSubDir(d);
			}
		}


		void ProcessDir(DirectoryInfo di) {
			foreach (FileInfo fi in di.GetFiles()) {
				string fileToTest;
				FileToProcess ftp = new FileToProcess();
				ftp.Name = fi.FullName;

				if ((fi.Extension == ".scp") || (fi.Extension == ".raw")) {
					if (fi.Extension == ".scp")
						fileToTest = fi.FullName + "." + tbSuffix.Text + ".done";
					else
						fileToTest = fi.Directory + ".raw." + tbSuffix.Text + ".done";
					if (File.Exists(fileToTest)) {
						ftp.Done = true;
						_done++;
					}
					else ftp.Done = false;
					_count++;
					tbTotal.Text = _count.ToString();
					tbDone.Text = _done.ToString();
					_fileToProcessList.Add(ftp);
					tbResult.AppendText(ftp.Name);
					tbResult.AppendText(ftp.Done ? " - Already Done\n" : " - To Do\n");
					if (fi.Extension == ".raw") break;
				}
			}
		}


		private void btStartClicked(object sender, RoutedEventArgs e) {
			ProcessStartInfo startInfo = new ProcessStartInfo();
			string startDir = AppDomain.CurrentDomain.BaseDirectory;
			startInfo.FileName = startDir + "\\Aufit.exe";

			string arguments = "-suffix ";
			arguments += tbSuffix.Text;
			arguments += " ";
			if (cbDiskView.IsChecked == true)
				arguments += "-disk_view ";
			if (cbProt.IsChecked == true)
				arguments += "-protections ";
			if (cbSave.IsChecked == true)
				arguments += "-save_image ";

			foreach (FileToProcess ftp in _fileToProcessList) {
				if (ftp.Done) {
					tbResult.AppendText("Skipped " + ftp.Name);
				}
				else {
					tbResult.AppendText("Processed " + ftp.Name);
					string full = arguments + "\"" + ftp.Name + "\"";
					startInfo.Arguments = full;
					Process p = new Process();
					p.StartInfo = startInfo;
					p.Start();
					p.WaitForExit();
					ftp.Done = true;
					_done++;
					tbDone.Text = _done.ToString();
				}
				tbResult.AppendText("\n");
			}
		}

		private void btStopClicked(object sender, RoutedEventArgs e) {

		}


		private void btCleanClick(object sender, RoutedEventArgs e) {
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
	}

}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Aufit;
using System.Threading;

namespace Aufit {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		// http://blogs.msdn.com/b/avip/archive/2008/10/27/wpf-supporting-command-line-arguments-and-file-extensions.aspx


		protected override void OnStartup(StartupEventArgs e) {
			//make sure an argument is passed
			//if (e.Args.Length == 1) {
			//	//MessageBox.Show(e.Args[0]);
			//	this.Properties["FileName"] = e.Args[0];
			//}

			for (int i = 0; i < e.Args.Length; i++) {
				//MessageBox.Show(e.Args[i]);
				if (e.Args[i] == "-disk") {
					this.Properties["DiskView"] = "yes";
					Aufit.MainWindow.BatchMode = true;
				}

				else if (e.Args[i] == "-protections") {
					this.Properties["Protections"] = "yes";
					Aufit.MainWindow.BatchMode = true;
				}

				else if (e.Args[i] == "-save") {
					Aufit.MainWindow.BatchMode = true;
					if ((i++ + 1) < e.Args.Length)
						this.Properties["SaveImage"] = e.Args[i];
					else {
						MessageBox.Show("Invalid suffix arguments");
						Application.Current.Shutdown();
					}
				}

				else if (e.Args[i] == "-suffix") {
					Aufit.MainWindow.BatchMode = true;
					if ((i++ + 1) < e.Args.Length)
						this.Properties["Suffix"] = e.Args[i];
					else {
						MessageBox.Show("Invalid suffix arguments");
						Application.Current.Shutdown();
					}
				}

				else if (e.Args[i] == "-scale") {
					Aufit.MainWindow.BatchMode = true;
					if ((i++ + 1) < e.Args.Length)
						this.Properties["Scale"] = e.Args[i];
					else {
						MessageBox.Show("Invalid scale arguments");
						Application.Current.Shutdown();
					}
				}

				else {
					this.Properties["FileName"] = e.Args[i];
					string fname = e.Args[i];
					FileInfo file = new FileInfo(fname);
					if (!file.Exists) {
						MessageBox.Show(String.Format("File {0} not found", fname));
						Application.Current.Shutdown();
					}
				}

			}

			//bool aIsNewInstance = false;
			//myMutex = new Mutex(true, "MyWPFApplication", out aIsNewInstance);
			//if (!aIsNewInstance) {
			//	//MessageBox.Show("Already an instance is running...");
			//	Aufit.MainWindow.test(e.Args[0]);
			//	App.Current.Shutdown();
			//}

			base.OnStartup(e);
		}

		// used to save size and position
		private void Application_Exit(object sender, ExitEventArgs e) {
			Aufit.Properties.Settings.Default.Save();
		}


	}

}

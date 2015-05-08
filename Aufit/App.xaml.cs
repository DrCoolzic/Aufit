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
				if (e.Args[i] == "-disk_view")
					this.Properties["DiskView"] = "yes";
				else if (e.Args[i] == "-protections")
					this.Properties["Protections"] = "yes";
				else if (e.Args[i] == "-save_image")
					this.Properties["SaveImage"] = "stx";
				else if (e.Args[i] == "-suffix") {
					if ((i++ + 1) < e.Args.Length)
						this.Properties["Suffix"] = e.Args[i];
				}
				else
					this.Properties["FileName"] = e.Args[i];
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

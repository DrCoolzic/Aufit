using System.Windows.Data;

//http://www.thomaslevesque.com/2008/11/18/wpf-binding-to-application-settings-using-a-markup-extension/
namespace Aufit {
	public class SettingBindingExtension : Binding {
		public SettingBindingExtension() {
			Initialize();
		}

		public SettingBindingExtension(string path) : base(path) {
			Initialize();
		}

		private void Initialize() {
			this.Source = Aufit.Properties.Settings.Default;
			this.Mode = BindingMode.TwoWay;
		}
	}
}

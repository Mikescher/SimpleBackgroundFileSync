using System;
using System.Windows.Forms;
using SimpleBackgroundFileSync.Model;
using SimpleBackgroundFileSync.Properties;

namespace SimpleBackgroundFileSync
{
	public class Program : Form
	{
		private const string NAME = @"SimpleBackgroundFileSync";
		private const string VERSION = @"1.0";

		private readonly NotifyIcon _trayIcon;

		[STAThread]
		static void Main() { Application.Run(new Program()); }

		private Program()
		{
			_trayIcon = new NotifyIcon
			{
				Text = NAME + " v" + VERSION,
				Icon = Resources.icon_ok,
				Visible = true
			};

			new Sync(this, _trayIcon).Start();
		}

		protected override void OnLoad(EventArgs e)
		{
			Visible = false;
			ShowInTaskbar = false;

			base.OnLoad(e);
		}

		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing) _trayIcon.Dispose();

			base.Dispose(isDisposing);
		}
	}
}

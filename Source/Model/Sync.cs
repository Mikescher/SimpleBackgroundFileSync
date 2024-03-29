﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using SimpleBackgroundFileSync.Properties;

namespace SimpleBackgroundFileSync.Model
{
	public class Sync
	{
		private readonly NotifyIcon _icon;
		private readonly Program _owner;

		private Thread _thread = null;
		private readonly object _threadMonitor = new object();
		private readonly object _threadLock = new object();

		private volatile bool _threadStop = false;
		private volatile bool _threadForce = false;
		private volatile bool _threadForceSingle = false;
		private volatile string _threadForcePath = null;

		private volatile List<SyncState> _entries = new List<SyncState>();

		public Sync(Program prog, NotifyIcon icon)
		{
			_icon = icon;
			_owner = prog;
			
			Reload();
		}

		private void Reload()
		{
			Debug.WriteLine($"Reload()");

			lock (_threadLock)
			{
				try
				{
					Config.Reload();
					foreach (var e in _entries)
					{
						if (e.Watcher != null) e.Watcher.EnableRaisingEvents = false;
						e.Watcher?.Dispose();
					}

					_entries = Config.Entries.Select(p => new SyncState(p)).ToList();
					foreach (var e in _entries)
					{
						if (e.Config.UseFilewatcher)
						{
							TryInitFileWatcher(e);
						}
					}

					UpdateDisplay(true);
				}
				catch (Exception e)
				{
					MessageBox.Show(e.ToString(), "Application Error");
				}
			}
		}

		private void TryInitFileWatcher(SyncState ss)
		{
			Debug.WriteLine($"TryInitFileWatcher('{ss.Config.Source}')");

			if (ss.Watcher != null) ss.Watcher.EnableRaisingEvents = false;

			try
			{
				ss.Watcher = new FileSystemWatcher(Path.GetDirectoryName(ss.Config.Source));
			}
			catch (ArgumentException e)
			{
				Debug.WriteLine($"TryInitFileWatcher->Postpone({e.GetType().Name}, {e.Message})");
				ss.Watcher = null;
				ss.WatcherInitPostponed = true;
				return;
			}
			
			ss.WatcherInitPostponed = false;

			ss.Watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
			ss.Watcher.Changed += (ps, pe) =>
			{
				Debug.WriteLine($"FileWatcher->Changed({pe.FullPath})");
				if (Path.GetFullPath(ss.Config.Source) == Path.GetFullPath(pe.FullPath))
				{
					Debug.WriteLine($"FileWatcher->Changed->Done({ss.Config.Source})");
					ForceSyncSingle(ss.Config.Source);
				}
			};
			ss.Watcher.Created += (ps, pe) =>
			{
				Debug.WriteLine($"FileWatcher->Created({pe.FullPath})");
				if (Path.GetFullPath(ss.Config.Source) == Path.GetFullPath(pe.FullPath))
				{
					Debug.WriteLine($"FileWatcher->Changed->Done({ss.Config.Source})");
					ForceSyncSingle(ss.Config.Source);
				}
			};
			ss.Watcher.EnableRaisingEvents = true;
		}

		private void UpdateDisplay(bool updateContextMenu)
		{
			Debug.WriteLine($"UpdateDisplay({updateContextMenu})");

			if (_entries.Any(p => p.State == SyncStateEnum.ERROR))
				_icon.Icon = Resources.icon_error;
			else if (_entries.Any(p => p.State == SyncStateEnum.WARNING))
				_icon.Icon = Resources.icon_ok;
			else
				_icon.Icon = Resources.icon_ok;

			if (updateContextMenu)
			{
				var trayMenu = new ContextMenu();
				trayMenu.MenuItems.Add("Reload configuration", Reload);
				trayMenu.MenuItems.Add("Synchronize Now", ForceSync);
				trayMenu.MenuItems.Add("-");
				foreach (var entry in _entries)
				{
					var lt = (entry.LastTest == DateTime.MinValue) ? "#" : $"{entry.LastTest:HH:mm:ss}";
					var lc = (entry.LastCopy == DateTime.MinValue) ? "#" : $"{entry.LastCopy:HH:mm:ss}";
					var mi = trayMenu.MenuItems.Add($"[{GetShortState(entry.State)}] {Path.GetFileName(entry.Config.Target)} ({lt}) (Copied: {lc})");
					mi.Enabled = false;
				}
				trayMenu.MenuItems.Add("-");
				trayMenu.MenuItems.Add("About", OnAbout);
				trayMenu.MenuItems.Add("Exit", OnExit);

				_icon.ContextMenu = trayMenu;
			}
		}

		private string GetShortState(SyncStateEnum sse)
		{
			if (sse == SyncStateEnum.OK)      return " OK ";
			if (sse == SyncStateEnum.DEFAULT) return " ~~ ";
			if (sse == SyncStateEnum.WARNING) return "WARN";
			if (sse == SyncStateEnum.ERROR)   return "ERR ";

			return "????";
		}

		private void Reload(object sender, EventArgs e)
		{
			Debug.WriteLine($"Reload()");

			Reload();
		}

		private void ForceSync(object sender, EventArgs e)
		{
			Debug.WriteLine($"ForceSync()");

			lock (_threadLock)
			{
				_threadForce = true;

				Monitor.Enter(_threadMonitor);
				Monitor.PulseAll(_threadMonitor);
				Monitor.Exit(_threadMonitor);
			}
		}

		private void ForceSyncSingle(string p)
		{
			Debug.WriteLine($"ForceSyncSingle({p})");

			lock (_threadLock)
			{
				_threadForceSingle = true;
				_threadForcePath = p;

				Monitor.Enter(_threadMonitor);
				Monitor.PulseAll(_threadMonitor);
				Monitor.Exit(_threadMonitor);
			}
		}

		private void OnAbout(object sender, EventArgs e)
		{
			Debug.WriteLine($"OnAbout()");
			
			Process.Start(Program.ABOUT_URL);
		}

		private void OnExit(object sender, EventArgs e)
		{
			Debug.WriteLine($"OnExit()");

			Stop();
			_owner.Close();
			Application.Exit();
		}

		public void Start()
		{
			Debug.WriteLine($"Start()");

			_thread = new Thread(Run) {IsBackground = false, Name = "SYNC_RUNNER"};
			_thread.Start();

			lock (_threadLock)
			{
				UpdateDisplay(true);
			}
		}

		private void Run()
		{
			try
			{
				Debug.WriteLine($"Run()");

				Monitor.Enter(_threadMonitor);
				Monitor.Wait(_threadMonitor, 3_000);
				Monitor.Exit(_threadMonitor);

				for (var force=true;;force=false)
				{
					Debug.WriteLine($"Run->Loop()");

					lock (_threadLock)
					{
						Debug.WriteLine($"Run->Loop->Lock()");

						if (_threadStop) return;

						_icon.Icon = Resources.icon_sync;

						DoSync(_threadForce || force, _threadForceSingle, _threadForcePath);
						_threadForce = false;
						_threadForceSingle = false;
						_threadForcePath = null;
						Thread.Sleep(1000);

						UpdateDisplay(true);
					}
				
					var mst = Math.Min(60 * 5, Config.Entries.Min(e => e.Interval) / 2);
				
					Monitor.Enter(_threadMonitor);
					Debug.WriteLine($"Run->Loop->Wait()");
					Monitor.Wait(_threadMonitor, 1000 * mst);
					Monitor.Exit(_threadMonitor);
				}
			}
			catch (Exception e)
			{
				MessageBox.Show(e.ToString(), "Application Error");
			}
		}

		private bool DoSync(bool force, bool forceSingle, string forceSinglePath)
		{
			Debug.WriteLine($"DoSync({force}, {forceSingle}, '{forceSinglePath}')");

			var changed = false;

			foreach (var entry in _entries)
			{
				if (_threadStop) return changed;

				if (!force && !(forceSingle && entry.Config.Source == forceSinglePath))
				{
					var delta = DateTime.Now - entry.LastTest;
					if (delta < TimeSpan.FromSeconds(entry.Config.Interval)) continue;
				}
				else
				{
					Debug.WriteLine($"DoSync->Forced(('{entry.Config.Source}'))");
				}

				try
				{
					var dss = DoSyncSingle(entry);
					if (dss) changed = true;
				}
				catch (Exception)
				{
					entry.State = SyncStateEnum.ERROR;
				}

				entry.LastTest = DateTime.Now;
			}

			return changed;
		}

		private bool DoSyncSingle(SyncState entry)
		{
			Debug.WriteLine($"DoSyncSingle({entry.Config.Target})");

			try
			{
				if (!File.Exists(entry.Config.Source))
				{
					if (entry.Config.ModeSourceNotFound == ErrorMode.ERROR)  entry.State = SyncStateEnum.ERROR;
					else if (entry.Config.ModeSourceNotFound == ErrorMode.WARN)   entry.State = SyncStateEnum.WARNING;
					else if (entry.Config.ModeSourceNotFound == ErrorMode.IGNORE) entry.State = SyncStateEnum.OK;
					
					Debug.WriteLine($"DoSyncSingle->FileNotFound()");

					return true;
				}

				if (entry.Config.UseFilewatcher && entry.WatcherInitPostponed)
				{
					Debug.WriteLine($"DoSyncSingle->InitFileWatcher()");
					TryInitFileWatcher(entry);
				}

				if (ShouldSync(entry))
				{
					Debug.WriteLine($"DoSyncSingle->Copy({entry.Config.Target})");
					try
					{
						File.Copy(entry.Config.Source, entry.Config.Target, true);
					}
					catch (Exception e)
					{
						if (entry.Config.ModeCopyFail == ErrorMode.ERROR)  entry.State = SyncStateEnum.ERROR;
						if (entry.Config.ModeCopyFail == ErrorMode.WARN)   entry.State = SyncStateEnum.WARNING;
						if (entry.Config.ModeCopyFail == ErrorMode.IGNORE) entry.State = SyncStateEnum.OK;
						
						Debug.WriteLine($"DoSyncSingle->CopyException({e.GetType().Name}, {e.Message})");
						return true;
					}
					entry.LastCopy = DateTime.Now;
					entry.State = SyncStateEnum.OK;
					return true;
				}

				entry.State = SyncStateEnum.OK;
				return false;
			}
			catch (Exception e)
			{
				Debug.WriteLine($"DoSyncSingle->Exception({e.GetType().Name}, {e.Message})");

				entry.State = SyncStateEnum.ERROR;
				return true;
			}
		}

		private bool ShouldSync(SyncState entry)
		{
			if (entry.Config.Comparison == CompareMode.ALWAYS)
			{
				return true;
			}

			if (entry.Config.Comparison == CompareMode.FILETIME)
			{
				var mt1 = File.GetLastWriteTime(entry.Config.Source);
				var mt2 = File.GetLastWriteTime(entry.Config.Target);
				return !mt1.Equals(mt2);
			}

			if (entry.Config.Comparison == CompareMode.CHECKSUM)
			{
				var cs1 = Hash(entry.Config.Source);
				var cs2 = Hash(entry.Config.Target);

				return cs1 != cs2;
			}

			return false;
		}

		public string Hash(string f)
		{
			using (var css = SHA512.Create())
			{
				using (var stream = File.OpenRead(f))
				{
					return BitConverter.ToString(css.ComputeHash(stream)).Replace("-", "").ToUpper();
				}
			}
		}

		public void Stop()
		{
			Debug.WriteLine($"Stop()");

			lock (_threadLock)
			{
				_threadStop = true;

				Monitor.Enter(_threadMonitor);
				Monitor.PulseAll(_threadMonitor);
				Monitor.Exit(_threadMonitor);
			}
		}
	}
}
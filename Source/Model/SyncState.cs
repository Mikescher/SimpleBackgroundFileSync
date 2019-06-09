using System;
using System.IO;

namespace SimpleBackgroundFileSync.Model
{
	public class SyncState
	{
		public readonly ConfigEntry Config;

		public DateTime LastTest;
		public DateTime LastCopy;

		public SyncStateEnum State;

		public FileSystemWatcher Watcher;
		public bool WatcherInitPostponed = false;

		public SyncState(ConfigEntry e)
		{
			Config   = e;
			LastTest = DateTime.MinValue;
			LastCopy = DateTime.MinValue;
			State    = SyncStateEnum.DEFAULT;
		}
	}
}

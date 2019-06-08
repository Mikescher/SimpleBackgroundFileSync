using System;

namespace SimpleBackgroundFileSync.Model
{
	public class SyncState
	{
		public readonly ConfigEntry Config;

		public DateTime LastTest;
		public DateTime LastCopy;

		public SyncStateEnum State;

		public SyncState(ConfigEntry e)
		{
			Config   = e;
			LastTest = DateTime.MinValue;
			LastCopy = DateTime.MinValue;
			State    = SyncStateEnum.DEFAULT;
		}
	}
}

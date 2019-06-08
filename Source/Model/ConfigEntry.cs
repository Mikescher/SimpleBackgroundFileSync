namespace SimpleBackgroundFileSync.Model
{
	public class ConfigEntry
	{
		public readonly string Source;
		public readonly string Target;

		public readonly int Interval;
		public readonly bool UseFilewatcher;
		public readonly CompareMode Comparison;

		public readonly ErrorMode ModeSourceNotFound;

		public ConfigEntry(string src, string dst, int iv, bool fw, CompareMode cm, ErrorMode snf)
		{
			Source = src;
			Target = dst;
			Interval = iv;
			UseFilewatcher = fw;
			Comparison = cm;
			ModeSourceNotFound = snf;
		}
	}
}

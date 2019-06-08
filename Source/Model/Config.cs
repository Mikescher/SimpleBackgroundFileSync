using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace SimpleBackgroundFileSync.Model
{
	public static class Config
	{
		public static readonly List<ConfigEntry> Entries = new List<ConfigEntry>();

		public static readonly string FILEPATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SBFS", "config.xml");

		public static void Reload()
		{
			var xdoc = XDocument.Load(FILEPATH);

			Entries.Clear();

			foreach (var entry in xdoc.Root?.Elements("file") ?? new List<XElement>())
			{
				var src = entry.Attribute("source")?.Value ?? throw new Exception("no {source} attribute");
				var dst = entry.Attribute("target")?.Value ?? throw new Exception("no {target} attribute");
				var ivl = entry.Attribute("interval")?.Value ?? throw new Exception("no {interval} attribute");
				var em1 = entry.Attribute("source_not_found")?.Value ?? "error";
				var em2 = entry.Attribute("copy_fail")?.Value ?? "error";
				var cm  = entry.Attribute("compare")?.Value ?? "always";
				var ufw = entry.Attribute("fileevents")?.Value ?? "false";

				Entries.Add(new ConfigEntry(src, dst, int.Parse(ivl), bool.Parse(ufw), ParseComparisonMode(cm), ParseErrorMode(em1), ParseErrorMode(em2)));
			}
		}

		private static ErrorMode ParseErrorMode(string em)
		{
			if (em.ToLower() == "error")  return ErrorMode.ERROR;
			if (em.ToLower() == "warn")   return ErrorMode.WARN;
			if (em.ToLower() == "ignore") return ErrorMode.IGNORE;

			throw new Exception("Invalid value for [[ErrorMode]]");
		}

		private static CompareMode ParseComparisonMode(string em)
		{
			if (em.ToLower() == "always")   return CompareMode.ALWAYS;
			if (em.ToLower() == "checksum") return CompareMode.CHECKSUM;
			if (em.ToLower() == "filetime") return CompareMode.FILETIME;

			throw new Exception("Invalid value for [[CompareMode]]");
		}
	}
}

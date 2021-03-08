using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace bcfTool
{
	abstract class BcfSource 
	{
		public abstract Stream GetStream(string name);
		public abstract IEnumerable<string> GetLocalNames(string filter = "");
		public abstract string FullName { get; }

		internal string GetVersion()
		{
			string versionfileContent = GetStringContent("bcf.version");
			if (versionfileContent.Contains("3.0", StringComparison.InvariantCultureIgnoreCase))
				return "v3.0";
			else if (versionfileContent.Contains("2.1", StringComparison.InvariantCultureIgnoreCase))
				return "v2.1";
			else if (versionfileContent.Contains("2.0", StringComparison.InvariantCultureIgnoreCase))
				return "v2.0";
			return "";
		}

		private string GetStringContent(string localName)
		{
			var content = "";
			using (var s = GetStream(localName))
			{
				if (s == null)
					content = "";
				else
				{
					TextReader tr = new StreamReader(s);
					content = tr.ReadToEnd();
				}
			}
			return content;
		}
	}
}

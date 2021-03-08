using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace bcfTool
{
	class ZippedFileSource : BcfSource
	{
		private FileInfo zippedFileInfo;

		public override string FullName => zippedFileInfo.FullName;

		public ZippedFileSource(FileInfo zippedFileInfo)
		{
			this.zippedFileInfo = zippedFileInfo;
		}

		public override IEnumerable<string> GetLocalNames(string filter = "")
		{
			using var zip = ZipFile.OpenRead(zippedFileInfo.FullName);
			foreach (var entry in zip.Entries)
			{
				if (Path.EndsInDirectorySeparator(entry.FullName))
					continue;
				if (entry.FullName.EndsWith(filter))
					yield return entry.FullName;
			}
		}

		public override Stream GetStream(string name)
		{
			using var zip = ZipFile.OpenRead(zippedFileInfo.FullName);
			var entry = zip.Entries.FirstOrDefault(x => x.FullName == name);
			if (entry == null)
				return null;
			MemoryStream ms = new MemoryStream();
			entry.Open().CopyTo(ms);
			ms.Position = 0;
			return ms;
		}
	}
}

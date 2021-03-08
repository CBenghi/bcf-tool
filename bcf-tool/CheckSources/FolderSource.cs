using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace bcfTool
{
	class FolderSource : BcfSource
	{
		private DirectoryInfo unzippedDirInfo;

		public FolderSource(DirectoryInfo unzippedDirInfo)
		{
			this.unzippedDirInfo = unzippedDirInfo;
		}

		public override string FullName => unzippedDirInfo.FullName;

		public override IEnumerable<string> GetLocalNames(string filter)
		{
			foreach (var item in unzippedDirInfo.GetFiles("*.*", SearchOption.AllDirectories))
			{
				if (item.FullName.EndsWith(filter))
				{
					var relative = Path.GetRelativePath(unzippedDirInfo.FullName, item.FullName);
					yield return relative;
				}
			}
		}

		public override Stream GetStream(string name)
		{
			var fullName = Path.Combine(unzippedDirInfo.FullName, name);
			if (!File.Exists(fullName))
			{
				return null;
			}
			return File.OpenRead(fullName);
		}
	}
}

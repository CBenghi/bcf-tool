using CommandLine;
using Force.Crc32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using static bcfTool.Program;

namespace bcfTool
{

	[Verb("check", HelpText = "check files for issues.")]
	internal class CheckOptions
	{
		[Option('s', "no-schema", Required = false, HelpText = "skips xsd schema check.", Default = false)]
		public bool NoSchema { get; set; }

		[Option('m', "no-match", Required = false, HelpText = "skips crc check match between zipped and unzipped.", Default = false)]
		public bool NoZipMatch { get; set; }

		[Option('w', "write-mismatch", Required = false, HelpText = "Writes copy of mismatched file next to unzipped.", Default = false)]
		public bool WriteMismatch { get; set; }

		[Option('u', "no-uniqueGuid", Default = true, Required = false, HelpText = "Verifies that GUID are unique across the fileset.")]
		public bool NoUniqueGuid { get; set; }

		[Value(0, MetaName = "source",
			HelpText = "Input source to be processed can be file or folder",
			Required = true)]
		public string InputSource { get; set; }

		public FileSystemInfo ResolvedSource { get; set; }

		internal static Status Run(CheckOptions opts)
		{
			Console.WriteLine("=== bcf-tool - checking example files.");
			if (Directory.Exists(opts.InputSource))
			{
				var t = new DirectoryInfo(opts.InputSource);
				opts.ResolvedSource = t;
				return ProcessExamplesFolder(t, new CheckInfo(opts));
			}
			if (File.Exists(opts.InputSource))
			{
				var t = new FileInfo(opts.InputSource);
				opts.ResolvedSource = t;
				return ProcessSingleExample(t, new CheckInfo(opts));
			}
			Console.WriteLine($"Error: Invalid input source '{opts.InputSource}'");
			return Status.NotFoundError;
		}

		private static Status ProcessExamplesFolder(DirectoryInfo directoryInfo, CheckInfo c)
		{
			var allBcfs = directoryInfo.GetFiles("*.bcf", SearchOption.AllDirectories)
				.Where(x => !x.FullName.Contains("unzipped", StringComparison.InvariantCultureIgnoreCase))
				.ToList();
			allBcfs.AddRange(
				directoryInfo.GetFiles("*.bcfzip", SearchOption.AllDirectories)
				.Where(x => !x.FullName.Contains("unzipped", StringComparison.InvariantCultureIgnoreCase))
				.ToList()
				);
			foreach (var bcf in allBcfs)
			{
				ProcessSingleExample(bcf, c);
			}
			return c.Status;
		}

		private class CheckInfo
		{
			// todo: rather ugly to have this here... I'm designing classes as I go along
			public CheckOptions Options { get; }

			public CheckInfo(CheckOptions opts)
			{
				Options = opts;
			}

			public Dictionary<string, string> guids = new Dictionary<string, string>();

			// if you think `options` is ugly, this is jsut awful ;-)
			public FileInfo currentFile { get; set; }
			public Status Status { get; internal set; }

			public void validationReporter(object sender, ValidationEventArgs e)
			{
				if (e.Severity == XmlSeverityType.Warning)
				{
					Console.WriteLine($"XML WARNING\t{CleanName(currentFile)}\t{e.Message}");
					Status |= Status.ContentError;
				}
				else if (e.Severity == XmlSeverityType.Error)
				{
					Console.WriteLine($"XML ERROR\t{CleanName(currentFile)}\t{e.Message}");
					Status |= Status.ContentError;
				}
			}

			public string CleanName(FileInfo f)
			{
				return f.FullName.Replace(Options.ResolvedSource.FullName, "");
			}

		}

		private static Status ProcessSingleExample(FileInfo fileInfo, CheckInfo c)
		{
			c.currentFile = fileInfo;
			var dirPath = Path.Combine(fileInfo.DirectoryName, "unzipped");
			var unzippedDir = new DirectoryInfo(dirPath);
			if (!unzippedDir.Exists)
			{
				Console.WriteLine($"Error\t{c.CleanName(fileInfo)}\tUnzipped folder not found.");
			}

			if (!c.Options.NoZipMatch)
			{
				var zip = ZipFile.OpenRead(fileInfo.FullName);
				foreach (var entry in zip.Entries)
				{
					if (Path.EndsInDirectorySeparator(entry.FullName))
						continue;
					var onDisk = Path.Combine(unzippedDir.FullName, entry.FullName);
					var onDiskFile = new FileInfo(onDisk);
					if (!onDiskFile.Exists)
					{
						Console.WriteLine($"Error\t{c.CleanName(onDiskFile)}\tUncompressed file not found.");
						c.Status |= Status.ContentMismatch;
					}
					else
					{
						var dskCont = File.ReadAllBytes(onDiskFile.FullName);
						var zipCont = ReadFully(entry.Open());
						var dskCrc = Crc32Algorithm.Compute(dskCont);
						var zipCrc = Crc32Algorithm.Compute(zipCont);
						var zipCrcFile = entry.Crc32;

						if (dskCrc != zipCrc)
						{
							// might be the same with different line endings
							System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
							string dskS = Clean(enc.GetString(dskCont));
							string zipS = Clean(enc.GetString(zipCont));
							if (dskS != zipS)
							{
								Console.WriteLine($"Error\t{c.CleanName(onDiskFile)}\tCompressed/Uncompressed mismatch.");
								c.Status |= Status.ContentMismatch;
								if (c.Options.WriteMismatch)
								{
									var mismatchName = onDiskFile.FullName + ".zipMismatch";
									File.WriteAllBytes(mismatchName, zipCont);
								}
							}
						}
					}
				}
			}

			var versionfile = Path.Combine(unzippedDir.FullName, "bcf.version");
			if (!File.Exists(versionfile))
			{
				Console.WriteLine($"Error\t{c.CleanName(fileInfo)}\tversion file missing");
				return Status.NotFoundError;
			}
			var version = "";
			var versionfileContent = File.ReadAllText(versionfile);
			if (versionfileContent.Contains("3.0", StringComparison.InvariantCultureIgnoreCase))
				version = "v3.0";
			else if (versionfileContent.Contains("2.1", StringComparison.InvariantCultureIgnoreCase))
				version = "v2.1";
			else if (versionfileContent.Contains("2.0", StringComparison.InvariantCultureIgnoreCase))
				version = "v2.0";
			if (version == "")
			{
				Console.WriteLine($"Error\t{c.CleanName(fileInfo)}\tversion not resolved");
				return Status.ContentError;
			}

			if (!c.Options.NoSchema)
			{
				CheckSchemaCompliance(c, unzippedDir, version, "bcf", $"schemas/{version}/markup.xsd");
				CheckSchemaCompliance(c, unzippedDir, version, "bcfv", $"schemas/{version}/visinfo.xsd");
				CheckSchemaCompliance(c, unzippedDir, version, "bcfp", $"schemas/{version}/project.xsd");
			}

			return Status.Ok;
		}

		private static string Clean(string v)
		{
			// strip differences in eol, tabs and whitespace
			v = v.Replace("\r", "");
			v = v.Replace("\n", "");
			v = v.Replace("\t", "");
			v = v.Replace(" ", "");
			return v;
		}

		private static void CheckSchemaCompliance(CheckInfo c, DirectoryInfo unzippedDir, string version, string fileExtension, string requiredSchema)
		{
			var cache = c.currentFile;
			var markupFiles = unzippedDir.GetFiles($"*.{fileExtension}", SearchOption.AllDirectories);
			List<string> schemas = new List<string>
			{
				requiredSchema
			};
			if (version == "v3.0")
				schemas.Add($"schemas/{version}/shared-types.xsd");
			foreach (var markupFile in markupFiles)
			{
				c.currentFile = markupFile;
				var alltext = File.ReadAllText(markupFile.FullName);
				XmlReaderSettings rSettings = new XmlReaderSettings();
				foreach (var schema in schemas)
				{
					rSettings.Schemas.Add("", schema);
				}
				rSettings.ValidationType = ValidationType.Schema;
				rSettings.ValidationEventHandler += new ValidationEventHandler(c.validationReporter);
				XmlReader content = XmlReader.Create(markupFile.FullName, rSettings);
				while (content.Read())
				{
					// read all files to trigger validation events.
				}
			}

			// restore so that more feedback can be provided
			c.currentFile = cache;
		}

		public static byte[] ReadFully(Stream input)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				input.CopyTo(ms);
				return ms.ToArray();
			}
		}

	}
}

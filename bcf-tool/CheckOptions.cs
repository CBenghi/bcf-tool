using CommandLine;
using Force.Crc32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static bcfTool.Program;
using vbio = Microsoft.VisualBasic.FileIO;

namespace bcfTool
{

	[Verb("check", HelpText = "check files for issues.")]
	internal class CheckOptions
	{
		[Option('s', "schema", Required = false, HelpText = "Check XSD schema compliance against the relevant version.", Default = false)]
		public bool CheckSchema { get; set; }

		[Option('m', "match", Required = false, HelpText = "Check content match between zipped and unzipped versions, ignoring xml newlines.", Default = false)]
		public bool CheckZipMatch { get; set; }

		[Option('z', "rezip", Required = false, SetName = "zip", HelpText = "Recreate mismatching zip files from unzipped folder.", Default = false)]
		public bool ReZip { get; set; }

		[Option('w', "write-mismatch", SetName = "zip", Required = false, HelpText = "Writes a copy of mismatching compressed files next to their unzipped counterpart for comparison.", Default = false)]
		public bool WriteMismatch { get; set; }

		[Option('n', "newLines", Required = false, HelpText = "Check that xml content in unzipped folder is not on a single line, for readability.", Default = false)]
		public bool CheckNewLines { get; set; }

		[Option('u', "uniqueGuid", Default = false, Required = false, HelpText = "Check that GUID are unique across the fileset.")]
		public bool CheckUniqueGuid { get; set; }

		[Option('q', "qualityAssurance", Default = false, Required = false, HelpText = "Perform the checks that are meaningful for the BCF-XML official repository.")]
		public bool QualityAssurance { get; set; }

		// images can be fixed with
		// - (linux):   mogrify -resize 1500x1500\> name.ext
		// - (windows): mogrify -resize 1500x1500^> name.ext

		[Option('i', "imageSize", Default = false, Required = false, HelpText = "Checks that images aren't too large.")]
		public bool CheckImageSize { get; set; }

		[Value(0, 
			MetaName = "source",
			HelpText = "Input source to be processed; it can be a file or a folder.",
			Required = true)]
		public string InputSource { get; set; }

		public FileSystemInfo ResolvedSource { get; set; }

		internal static Status Run(CheckOptions opts)
		{
			Console.WriteLine("=== bcf-tool - checking example files.");

			if (opts.WriteMismatch)
				opts.CheckZipMatch = true;

			if (opts.ReZip)
				opts.CheckZipMatch = true;
			
			if (opts.QualityAssurance)
			{
				// enables default tests and then the ones specific for QA of bcf-xml
				SetBasicChecks(opts);
				opts.CheckZipMatch = true;
				opts.CheckNewLines = true;
			}

			// if no check is required than check default
			if (
				!opts.CheckSchema
				&& !opts.CheckUniqueGuid
				&& !opts.CheckZipMatch
				&& !opts.CheckNewLines
				&& !opts.CheckImageSize
				)
			{
				SetBasicChecks(opts);
				Console.WriteLine("Performing default checks.");
			}
			else
			{
				List<string> checks = new List<string>();
				if (opts.CheckSchema)
					checks.Add("Schema");
				if (opts.CheckUniqueGuid)
					checks.Add("UniqueGuid");
				if (opts.CheckZipMatch)
					checks.Add("ZipMatch");
				if (opts.CheckNewLines)
					checks.Add("Readable Xml");
				if (opts.CheckImageSize)
					checks.Add("ImageSize");
				Console.WriteLine($"Checking: {string.Join(",", checks.ToArray())}." );
			}

			if (Directory.Exists(opts.InputSource))
			{
				var t = new DirectoryInfo(opts.InputSource);
				opts.ResolvedSource = t;
				var ret = ProcessExamplesFolder(t, new CheckInfo(opts));
				Console.WriteLine($"Completed with status: {ret}.");
				return ret;
			}
			if (File.Exists(opts.InputSource))
			{
				var t = new FileInfo(opts.InputSource);
				opts.ResolvedSource = t;
				var ret = ProcessSingleExample(t, new CheckInfo(opts));
				Console.WriteLine($"Completed with status: {ret}.");
				return ret;
			}
			Console.WriteLine($"Error: Invalid input source '{opts.InputSource}'");
			return Status.NotFoundError;
		}

		private static void SetBasicChecks(CheckOptions opts)
		{
			opts.CheckSchema = true;
			opts.CheckUniqueGuid = true;
			opts.CheckImageSize = true;
		}

		private static Status ProcessExamplesFolder(DirectoryInfo directoryInfo, CheckInfo c)
		{
			var allBcfs = directoryInfo.GetFiles("*.bcf", SearchOption.AllDirectories)
				.Where(x => 
					!x.FullName.Contains("unzipped", StringComparison.InvariantCultureIgnoreCase)
					&&
					!x.FullName.EndsWith("markup.bcf")
					)
				.ToList();
			allBcfs.AddRange(
				directoryInfo.GetFiles("*.bcfzip", SearchOption.AllDirectories)
				.Where(x => !x.FullName.Contains("unzipped", StringComparison.InvariantCultureIgnoreCase))
				.ToList()
				);
			foreach (var bcf in allBcfs.OrderBy(x => x.FullName))
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
			public string validatingFile { get; set; }

			public Status Status { get; internal set; }

			public void validationReporter(object sender, ValidationEventArgs e)
			{
				if (e.Severity == XmlSeverityType.Warning)
				{
					Console.WriteLine($"XML WARNING\t{validatingFile}\t{e.Message}");
					Status |= Status.ContentError;
				}
				else if (e.Severity == XmlSeverityType.Error)
				{
					Console.WriteLine($"XML ERROR\t{validatingFile}\t{e.Message}");
					Status |= Status.ContentError;
				}
			}

			public string CleanName(FileInfo f)
			{
				return Path.GetRelativePath(Options.ResolvedSource.FullName, f.FullName);
			}
		}

		private static Status ProcessSingleExample(FileInfo zippedFileInfo, CheckInfo c)
		{
			BcfSource source = new ZippedFileSource(zippedFileInfo);
			var dirPath = Path.Combine(zippedFileInfo.DirectoryName, "unzipped");
			var unzippedDirInfo = new DirectoryInfo(dirPath);
			if (unzippedDirInfo.Exists && c.Options.CheckZipMatch)
			{
				source = new FolderSource(unzippedDirInfo);
			}
			else if (c.Options.CheckZipMatch)
			{
				Console.WriteLine($"MISMATCH\t{c.CleanName(zippedFileInfo)}\tUnzipped folder not found.");
			}

			// this checks the match between zipped and unzipped
			if (c.Options.CheckZipMatch)
			{
				bool rezip = false;
				using (var zip = ZipFile.OpenRead(zippedFileInfo.FullName))
				{
					foreach (var entry in zip.Entries)
					{
						if (Path.EndsInDirectorySeparator(entry.FullName))
							continue;
						var onDisk = Path.Combine(unzippedDirInfo.FullName, entry.FullName);
						var onDiskFile = new FileInfo(onDisk);
						if (!onDiskFile.Exists)
						{
							Console.WriteLine($"MISMATCH\t{c.CleanName(onDiskFile)}\tUncompressed file not found.");
							c.Status |= Status.ContentMismatch;
							if (c.Options.WriteMismatch)
							{
								var zipCont = ReadFully(entry.Open());
								var mismatchName = onDiskFile.FullName;
								File.WriteAllBytes(mismatchName, zipCont);
								Console.WriteLine($"CHANGE\t{c.CleanName(onDiskFile)}\tFile written from zip.");
							}
							if (c.Options.ReZip)
							{
								rezip = true;
								break;
							}
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
									Console.WriteLine($"MISMATCH\t{c.CleanName(onDiskFile)}\tCompressed/Uncompressed mismatch.");
									c.Status |= Status.ContentMismatch;
									if (c.Options.WriteMismatch)
									{
										var mismatchName = onDiskFile.FullName + ".zipMismatch";
										File.WriteAllBytes(mismatchName, zipCont);
										Console.WriteLine($"CHANGE\t{c.CleanName(onDiskFile)}.zipMismatch\tFile written from zip for comparison.");
									}
									if (c.Options.ReZip)
									{
										rezip = true;
										break;
									}
								}
							}
						}
					}
				}
				if (rezip)
				{
					vbio.FileSystem.DeleteFile(zippedFileInfo.FullName, vbio.UIOption.OnlyErrorDialogs, vbio.RecycleOption.SendToRecycleBin);
					ZipFile.CreateFromDirectory(unzippedDirInfo.FullName, zippedFileInfo.FullName, CompressionLevel.Optimal, false);
					Console.WriteLine($"CHANGE\t{c.CleanName(zippedFileInfo)}\tFile recreated from folder.");
				}
			}

			var version = source.GetVersion();
			if (version == "")
			{
				Console.WriteLine($"VERSION\t{c.CleanName(zippedFileInfo)}\tversion not resolved, further checks stopped.");
				return Status.ContentError;
			}

			if (c.Options.CheckSchema)
			{
				CheckSchemaCompliance(c, source, version, "bcf", $"schemas/{version}/markup.xsd");
				CheckSchemaCompliance(c, source, version, "bcfv", $"schemas/{version}/visinfo.xsd");
				CheckSchemaCompliance(c, source, version, "bcfp", $"schemas/{version}/project.xsd");
			}
			if (c.Options.CheckUniqueGuid)
			{
				CheckUniqueIDs(c, unzippedDirInfo, version, "bcf");
				CheckUniqueIDs(c, unzippedDirInfo, version, "bcfv");
			}
			// no need to check new lines on compressed files
			if (c.Options.CheckNewLines && source is FolderSource)
			{
				CheckMultiLine(c, unzippedDirInfo, "bcf");
				CheckMultiLine(c, unzippedDirInfo, "bcfv");
				CheckMultiLine(c, unzippedDirInfo, "bcfp");
			}
			if (c.Options.CheckImageSize)
			{
				CheckImageSizeIsOk(c, unzippedDirInfo);
			}

			return Status.Ok;
		}

		private static void CheckImageSizeIsOk(CheckInfo c, DirectoryInfo unzippedDir)
		{
			List<string> extensions = new List<string> { "png", "jpg" };
			foreach (var ext in extensions)
			{
				var imageFiles = unzippedDir.GetFiles($"*.{ext}", SearchOption.AllDirectories);
				foreach (var imageFile in imageFiles)
				{
					var t = Image.FromFile(imageFile.FullName);
					if (t.Width > 1500 || t.Height > 1500)
					{
						Console.WriteLine($"IMAGE SIZE\t{c.CleanName(imageFile)}\tIs too big ({t.Width} x {t.Height}).");
						c.Status |= Status.ContentError;
					}
				}
			}
		}

		private static void CheckMultiLine(CheckInfo c, DirectoryInfo unzippedDir, string fileExtension)
		{
			var markupFiles = unzippedDir.GetFiles($"*.{fileExtension}", SearchOption.AllDirectories);
			foreach (var markupFile in markupFiles)
			{
				var lineCount = File.ReadLines(markupFile.FullName).Count();
				if (lineCount < 2)
				{
					Console.WriteLine($"NEWLINE ERROR\t{c.CleanName(markupFile)}\tHas {lineCount} lines");
					c.Status |= Status.ContentError;
				}
			}
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

		private static void CheckUniqueIDs(CheckInfo c, DirectoryInfo unzippedDir, string version, string fileExtension)
		{
			var markupFiles = unzippedDir.GetFiles($"*.{fileExtension}", SearchOption.AllDirectories);

			List<string> uniques = new List<string>()
			{
				"/*/Topic/@Guid",
				"/*/Comment/@Guid"
			};

			foreach (var markupFile in markupFiles)
			{
				var docNav = new XPathDocument(markupFile.FullName);
				var nav = docNav.CreateNavigator();
				foreach (var uniquePath in uniques)
				{
					var iterator1 = nav.Select(uniquePath);
					while (iterator1.MoveNext())
					{
						var guid = iterator1.Current.Value;
						if (c.guids.ContainsKey(guid))
						{
							Console.WriteLine($"GUID\t{c.CleanName(markupFile)}\t'Guid {guid}' also encountered in {c.guids[guid]}");
							c.Status |= Status.ContentError;
						}
						else
						{
							c.guids.Add(guid, c.CleanName(markupFile));
						}
					}
				}
			}
		}
		private static void CheckSchemaCompliance(CheckInfo c, BcfSource unzippedDir, string version, string fileExtension, string requiredSchema)
		{
			List<string> schemas = new List<string>
			{
				requiredSchema
			};
			if (version == "v3.0")
				schemas.Add($"schemas/{version}/shared-types.xsd");
			var markupFiles = unzippedDir.GetLocalNames($".{fileExtension}");
			foreach (var markupFile in markupFiles)
			{
				c.validatingFile = c.CleanName(new FileInfo(Path.Combine(unzippedDir.FullName, markupFile)));
				XmlReaderSettings rSettings = new XmlReaderSettings();
				foreach (var schema in schemas)
				{
					rSettings.Schemas.Add("", schema);
				}
				rSettings.ValidationType = ValidationType.Schema;
				rSettings.ValidationEventHandler += new ValidationEventHandler(c.validationReporter);
				XmlReader content = XmlReader.Create(unzippedDir.GetStream(markupFile), rSettings);
				while (content.Read())
				{
					// read all files to trigger validation events.
				}
			}			
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

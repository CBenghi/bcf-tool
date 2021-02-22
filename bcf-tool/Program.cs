using CommandLine;
using System;

namespace bcfTool
{
	partial class Program
	{
		[Verb("zip", HelpText = "rezip the unzipped folders - Not implemented yet.")]
		class ZipOptions
		{
			// options here
		}

		static public int Main(string[] args)
		{
			var t = Parser.Default.ParseArguments<CheckOptions, ZipOptions>(args)
			  .MapResult(
				(CheckOptions opts) => CheckOptions.Run(opts),
				(ZipOptions opts) => RunCommitAndReturnExitCode(opts),
				errs => Status.CommandLineError);
			return (int)t;
		}

		[Flags]
		internal enum Status
		{
			Ok = 0,
			NotImplemented = 1,
			CommandLineError = 2,
			NotFoundError = 4,
			ContentError = 8,
			ContentMismatch = 16,
		}

		private static Status RunCommitAndReturnExitCode(ZipOptions opts)
		{
			Console.WriteLine("Not implemented.");
			return Status.NotImplemented;
		}

	}
}

using CommandLine;
using System;

namespace bcfTool
{
	partial class Program
	{
		static public int Main(string[] args)
		{
			var t = Parser.Default.ParseArguments<CheckOptions, ErrorCodeOptions>(args)
			  .MapResult(
				(CheckOptions opts) => CheckOptions.Run(opts),
				(ErrorCodeOptions opts) => ErrorCodeOptions.Run(opts),
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
			ContentMismatchError = 16,
			XsdSchemaError = 32,
		}
	}
}

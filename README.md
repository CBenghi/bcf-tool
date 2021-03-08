# BCF-Tool

This repository contains a crude tool used for quality assurance of the
examples published in the BCF-XML repository.

The tool itself is a .NET console application tha returns errors and warnings
on the published files, in order to verify their correctness.

## Usage

``` batch
bcf-tool.exe check <options> source

bcf-tool 1.0.0
Copyright (C) 2021 bcf-tool
  check      check files for issues.
  help       Display more information on a specific command.
  version    Display version information.
```

### File Checking Options

Simple usage: `bcf-tool check foldername`

If no checking option is specificed then all checks are performed.

``` batch
  -s, --schema            Check XSD schema compliance against the relevant version.
  -m, --match             Check content match between zipped and unzipped versions, ignoring xml newlines.
  -z, --rezip             Re-creates mis-matching zip files from the unzipped folder.
  -w, --write-mismatch    Writes a copy of mismatching compressed files next to their 
                          unzipped counterpart for comparison.
  -n, --newLines          Check that xml content in unzipped folder is not on a single
                          line, for readability.
  -u, --uniqueGuid        Check that GUID are unique across the fileset.
  -i, --imageSize         Check that images aren't too large.

  --help                  Display this help screen.
  --version               Display version information.

  source                  Required. Input source to be processed can be file or folder
```

Note: the `-z` option currently only recreates files that already exist. 
`unzipped` folders without matching bcf file are ignored.
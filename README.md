# BCF-Tool

This repository contains a crude tool used for quality assurance of the
examples published in the BCF-XML repository.

The tool itself is a .NET console application tha returns errors and warnings
on the published files, in order to verify their correctness.

## Usage

``` batch
bcf-tool.exe <verb> <option>

bcf-tool 1.0.0
Copyright (C) 2021 bcf-tool
  check      check files for issues.
  zip        rezip the unzipped folders.
  help       Display more information on a specific command.
  version    Display version information.
```

### File Checking

``` batch
  -s, --no-schema         (Default: false) skips xsd schema check.
  -m, --no-match          (Default: false) skips crc check match between zipped and unzipped.
  -w, --write-mismatch    (Default: false) Writes copy of mismatched file next to unzipped.
  -u, --no-uniqueGuid     (Default: true) Verifies that GUID are unique across set (not implemented)
  --help                  Display this help screen.
  --version               Display version information.
  source (pos. 0)         Required. Input source to be processed can be file or folder
```

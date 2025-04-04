﻿# ZimWorks dBASE.NET

[![.NET](https://github.com/zimworks/DotNet.dBase/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/zimworks/DotNet.dBase/actions/workflows/dotnet.yml)

## Read and write DBF files with .NET

__ZimWorks.dBASE.NET__ is a .NET (`netstandard2.1`) class library used to read FoxBase, dBASE III and dBASE IV .dbf files. Data read
from a file is returned as a list of typed fields and a list of records. This library is useful to add
data import from dBASE sources to a .NET project.

This code has been tested against a number of dBASE files found in the wild, including FoxBase and dBASE III/IV
files with and without memo files. A .NET unit test project is part of this repository and new test files
may be added to it over time.

There is [an article describing the dBASE file format](http://www.independent-software.com/dbase-dbf-dbt-file-format.html).

## Installing ZimWorks dBASE.NET

ZimWorks dBASE.NET is available from [nuget](https://www.nuget.org/packages/ZimWorks.dBASE.NET/):

* Package manager:

```
PM> Install-Package ZimWorks.dBASE.NET
```

* .NET CLI:

```
> dotnet add package ZimWorks.dBASE.NET
```

* Paket CLI:

```
> paket add ZimWorks.dBASE.NET
```

## Opening a DBF file

```c#
using dBASE.NET;

dbf.Read("database.dbf");
```

This returns an instance of the `Dbf` class. With this, you can iterate over fields found in the table:

```c#
foreach(DbfField field in dbf.Fields) {
	Console.WriteLine(field.Name);
}
```

You can also iterate over records:

```c#
foreach(DbfRecord record in dbf.Records) {
	for(int i = 0;  i < dbf.Fields.Count; i++) {
		Console.WriteLine(record[i]);
	}
}
```

Count the records:

```c#
Console.WriteLine(dbf.Records.Count);
```

## Working with memo files

When memo file accompanying the `.dbf` file is found (either `.dbt` or `.fpt`), with the same base name as the table file, then
dBASE.NET will load the memo file's contents.

## Writing a DBF file

To write DBF data, you can either create a new instance of `Dbf`, then create fields and records, or load an existing table and modify its fields or records.

This sample code creates a new table with a single character field, then saves the .dbf file:

```c#
dbf = new Dbf();
DbfField field = new DbfField("TEST", DbfFieldType.Character, 12);
dbf.Fields.Add(field);
DbfRecord record = dbf.CreateRecord();
record.Data[0] = "HELLO";
dbf.Write("test.dbf", DbfVersion.VisualFoxPro);
```

## Supported Field types

| Code | Field type   | .NET counterpart |
|:-----|:-------------|:-----------------|
| `C`  | Character string | String |
| `D`  | Date             | DateTime |
| `I`  | Integer          | Int32 |
| `L`  | Logical          | Bool |
| `M`  | Memo             | String |
| `N`  | Numeric          | Double |
| `T`  | DateTime         | DateTime |
| `Y`  | Currency         | Float |


## Class diagram

![Class diagram](http://yuml.me/1cc9f823.png)

_yuml:_

```
http://yuml.me/diagram/scruffy/class/edit/[Dbf]+->[DbfRecord], [Dbf]+->[DbfField], [DbfRecord]+->[DbfField], [Dbf]->[DbfHeader], [DbfHeader]^-[Dbf4Header]
````

## Versions

* Version 1.4.1: Target Framework changed from `net6.0` to `netstandard2.1` & small fixes
* Version 1.4.0: Migration to .NET 6 & xUnit
* Version 1.3.0: Migration to .NET 5.0 & Fix encoding UTF8 multi-byte characters
* Version 1.2.2: Fix reading DbfField from file with `invalid` name.
* Version 1.2.1: Fixed buffer overflow on write operation.
  Fixed trimming and padding on write operation.
  Performance improve on write operation.
* Version 1.2.0: Sign assembly.
* Version 1.1.0: Add support for custom character encoding
* Version 1.0.0: Initial release


## Credits

This project was forked from [dBASE.NET](https://github.com/adeotek/DotNet.dBase) by [AdeoTEK Software SRL](https://adeotek.com/), but due to lack of features and maintenance was migrated to a stand-alone repository.

## Original credits

This project was initially forked from [dBASE.NET](https://github.com/henck/dBASE.NET) by [Independent Software](http://www.independent-software.com/), but at some point was migrated to a stand-alone repository.
# Archive Command

## Compress

Compress file or directory into archive.

Warning! this command is unstable, it can be changed.

### Syntax

```pebakery
Compress,<Format>,<SrcPath>,<DestArchive>,[CompressLevel],[Encoding]
```

- Arguments

| Argument | Description |
| --- | --- |
| Format | Archive format. Should be one of `Zip`.<br>TODO: Support `7z` format. |
| SrcPath | Path of file/directory to compress. |
| DestArchive | Path to save compressed archive. |

- Optional Arugments

| Argument | Description |
| --- | --- |
| CompressLevel | This effects archive size and compression time.<br>Should be one of `Store`, `Fastest`, `Normal`, `Best`.<br>By default, `Normal` is used. |
| Encoding | Encoding to be used in filename.<br>Should be one of `UTF8`, `UTF16`, `UTF16BE`, `ANSI`. By default,<br>`UTF8` is used. |

`CompressLevel` and `Encoding` can be used independently.

### Example

```pebakery
Compress,Zip,%BaseDir%\PEBakery.ini,%BaseDir%\Setting.zip
// PEBakery.ini will be compressed into Setting.zip.
```

```pebakery
Compress,Zip,%BaseDir%\PEBakery.ini,%BaseDir%\Setting.zip,Best
// PEBakery.ini will be compressed into Setting.zip.
```

```pebakery
Compress,Zip,%BaseDir%\PEBakery.ini,%BaseDir%\Setting.zip,Best
// PEBakery.ini will be compressed into Setting.zip.
```

## Decompress

Decompress files from archive.

Warning! This command is unstable, it can be changed.

### Syntax

```pebakery
Decompress,<SrcArchive>,<DestDir>,[Encoding]
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcArchive | Path of archive to decompress. |
| DestDir | Directory to decompress archive. |

- Optional Arugments

| Argument | Description |
| --- | --- |
| Encoding | Encoding used in filename.<br>Should be one of `UTF8`, `UTF16`, `UTF16BE`, `ANSI`. |

### Remarks

This command depends on [SharpCompress](https://github.com/adamhathcock/sharpcompress) (Managed) or [SevenZipExtractor](https://github.com/adoconnection/SevenZipExtractor) and `7z.dll` (Native).

If `Encoding` options are not used, native mode (SevenZipExtractor) will be used for maximum performance. Otherwise, managed mode (SharpCompress) will be used.

Archive formats tested to work:

- Zip
- 7z
- Rar

Archive formats only supported in native mode:

- Rar 5

Archive formats should work in both mode, but not tested:

- cab
- tar
- gz
- bz2
- xz
- lz

### Example

```pebakery
Decompress,%SrcDir%\Setting.7z,%DestDir%
// Setting.7z will be decompressed into %DestDir%.
```

## Expand

Decompress files from cabinet.

### Syntax

```pebakery
Expand,<SrcCab>,<DestDir>,[SingleFile],[PRESERVE],[NOWARN]
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcCab | Path of cabinet to decompress. |
| DestDir | Directory to decompress cabinet. |

- Optional Arugments

| Argument | Description |
| --- | --- |
| SingleFile | Extract only this single file. |
| PRESERVE | Do not overwrite when `SingleFile` is specified. |
| NOWARN | Do not log warning if a file is overwritten when `SingleFile` is specified. |

`PRESERVE` and `NOWARN` can be used independently.

### Remarks

This command is mainly used to extract EXE, DLL from EX_, DL_ in NT5 sources.

Depends on `cabinet.dll`, a Windows component.

### Example

```pebakery
Expand,%SrcDir%\ex1.cab,%DestDir%
// ex1.cab will be decompressed into %DestDir%.
```

```pebakery
Expand,%Source_Win%\EXPLORER.EX_,%Target_Win%
// EXPLORER.EXE will be extracted from EXPLORER.EX_
```

```pebakery
Expand,%SrcDir%\multi.cab,%DestDir%,BatteryLine.exe
// Extract only BatteryLine.exe from multi.cab, overwrite with warning if file exists.
```

```pebakery
Expand,%SrcDir%\multi.cab,%DestDir%,BatteryLine.exe,PRESERVE
// Extract only BatteryLine.exe from multi.cab, do not overwrite if file exists.
```

## CopyOrExpand

Copy file or extract file from cabinet.

If source file exists, it will be copied. Otherwise, PEBakery will search compressed cabniet and extract them.

### Syntax

```pebakery
CopyOrExpand,<SrcFile>,<DestPath>,[PRESERVE],[NOWARN]
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcFile | Path of file to copy. |
| DestPath | Path to copy or extract file. |

- Optional Arugments

| Argument | Description |
| --- | --- |
| PRESERVE | Do not overwrite. |
| NOWARN | Do not log warning if a file is overwritten. |

`PRESERVE` and `NOWARN` can be used independently.

### Remarks

If `SrcFile` is `%SrcDir%\EXPLORER.EXE`, PEBakery search for files in `%SrcDir%` in order of:

1. EXPLORER.EXE (Copy)
1. EXPLORER.EX_ (Expand)

Its behavior resembles [SetupDecompressOrCopyFile](https://msdn.microsoft.com/en-us/library/aa376992(v=vs.85).aspx) API.

This command is mainly used to copy EXE, DLL from EX_, DL_ in NT5 sources.

Depends on `cabinet.dll`, a Windows component.

### Example

```pebakery
CppyOrExpand,%SrcDir%\EXPLORER.EXE,%DestDir%
// If EXPLORER.EXE exists, it will be copied to %DestDir%.
// If EXPLORER.EXE does not exist, EXPLORER.EXE will be extracted from EXPLORER.EX_.
```

```pebakery
CppyOrExpand,%SrcDir%\EXPLORER.EXE,%DestDir%\NEWEXP.EXE
// If EXPLORER.EXE exists, it will be copied to %DestDir% with the new name NEWEXP.EXE.
// If EXPLORER.EXE does not exist, EXPLORER.EXE will be extracted from EXPLORER.EX_ with the new name NEWEXP.EXE.
```

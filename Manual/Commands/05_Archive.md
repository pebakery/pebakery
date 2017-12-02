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
| CompressLevel (Opt) | This effects archive size and compression time.<br>Should be one of `Store`, `Fastest`, `Normal`, `Best`.<br>By default, `Normal` is used. |
| Encoding  (Opt) | Encoding to be used in filename.<br>Should be one of `UTF8`, `UTF16`, `UTF16BE`, `ANSI`. <br>By default, `UTF8` is used. |

`CompressLevel` and `Encoding` can be used independently.

### Example

```pebakery
// PEBakery.ini will be compressed into Setting.zip.
Compress,Zip,%BaseDir%\PEBakery.ini,%BaseDir%\Setting.zip
```

```pebakery
// PEBakery.ini will be stored into Setting.zip.
Compress,Zip,%BaseDir%\PEBakery.ini,%BaseDir%\Setting.zip,Store
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
| Encoding (Opt) | Encoding used in filename.<br>Should be one of `UTF8`, `UTF16`, `UTF16BE`, `ANSI`. |

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
// Setting.7z will be decompressed into %DestDir%.
Decompress,%SrcDir%\Setting.7z,%DestDir%
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
| SingleFile (Opt) | Extract only this single file. |

- Flags

| Flag | Description |
| --- | --- |
| PRESERVE | Do not overwrite when `SingleFile` is specified. |
| NOWARN | Do not log warning if a file is overwritten when `SingleFile` is specified. |

Flags can be used independently.

### Remarks

This command is mainly used to extract EXE, DLL from EX_, DL_ in NT5 sources.

Depends on `cabinet.dll`, a Windows component.

### Example

```pebakery
// ex1.cab will be decompressed into %DestDir%.
Expand,%SrcDir%\ex1.cab,%DestDir%
```

```pebakery
// EXPLORER.EXE will be extracted from EXPLORER.EX_
Expand,%Source_Win%\EXPLORER.EX_,%Target_Win%
```

```pebakery
// Extract only BatteryLine.exe from multi.cab, overwrite with warning if file exists.
Expand,%SrcDir%\multi.cab,%DestDir%,BatteryLine.exe
```

```pebakery
// Extract only BatteryLine.exe from multi.cab, do not overwrite if file exists.
Expand,%SrcDir%\multi.cab,%DestDir%,BatteryLine.exe,PRESERVE
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

- Flags

| Flag | Description |
| --- | --- |
| PRESERVE | Do not overwrite. |
| NOWARN | Do not log warning if a file is overwritten. |

### Remarks

If `SrcFile` is `%SrcDir%\EXPLORER.EXE`, PEBakery search for files in `%SrcDir%` in order of:

1. EXPLORER.EXE (Copy)
2. EXPLORER.EX_ (Expand)

Its behavior resembles [SetupDecompressOrCopyFile](https://msdn.microsoft.com/en-us/library/aa376992(v=vs.85).aspx) API.

This command is mainly used to copy EXE, DLL from EX_, DL_ in NT5 sources.

Depends on `cabinet.dll`, a Windows component.

### Example

```pebakery
// If EXPLORER.EXE exists, it will be copied to %DestDir%.
// If EXPLORER.EXE does not exist, EXPLORER.EXE will be extracted from EXPLORER.EX_.
CppyOrExpand,%SrcDir%\EXPLORER.EXE,%DestDir%
```

```pebakery
// If EXPLORER.EXE exists, it will be copied to %DestDir% with the new name NEWEXP.EXE.
// If EXPLORER.EXE does not exist, EXPLORER.EXE will be extracted from EXPLORER.EX_ with the new name NEWEXP.EXE.
CppyOrExpand,%SrcDir%\EXPLORER.EXE,%DestDir%\NEWEXP.EXE
```

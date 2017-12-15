# Decompress

Decompress files from archive.

**Warning!** This command is unstable, it can be changed.

## Syntax

```pebakery
Decompress,<SrcArchive>,<DestDir>,[Encoding]
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcArchive | Path of archive to decompress. |
| DestDir | Directory to decompress archive. |
| Encoding (Opt) | Encoding used in filename.<br>Should be one of `UTF8`, `UTF16`, `UTF16BE`, `ANSI`. |

## Remarks

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

## Example

```pebakery
// Setting.7z will be decompressed into %DestDir%.
Decompress,%SrcDir%\Setting.7z,%DestDir%
```

# Compress

Compress file or directory into archive.

**Warning!** this command is unstable, it can be changed.

## Syntax

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
| Encoding (Opt) | Encoding to be used in filename.<br>Should be one of `UTF8`, `UTF16`, `UTF16BE`, `ANSI`. <br>By default, `UTF8` is used. |

`CompressLevel` and `Encoding` can be used independently.

## Example

```pebakery
// PEBakery.ini will be compressed into Setting.zip.
Compress,Zip,%BaseDir%\PEBakery.ini,%BaseDir%\Setting.zip
```

```pebakery
// PEBakery.ini will be stored into Setting.zip.
Compress,Zip,%BaseDir%\PEBakery.ini,%BaseDir%\Setting.zip,Store
```

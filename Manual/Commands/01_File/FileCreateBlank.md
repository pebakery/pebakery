# FileCreateBlank

Create empty file.

Can also initialize unicode BOM.

## Syntax

```pebakery
FileCreateBlank,<FilePath>,[Encoding],[PRESERVE],[NOWARN]
```

- Arguments

| Argument | Description |
| --- | --- |
| FilePath | Path to create empty file. |
| Encoding (Opt) | Write Unicode BOM in file.<br>Should be one of `UTF8`, `UTF16`, `UTF16BE`, `ANSI`.<br>By default, `ANSI` is used. |

- Flags

| Flag | Description |
| --- | --- |
| PRESERVE | Do not overwrite. |
| NOWARN | Do not log warning if file is overwritten. |

## Remarks

This command is useful to create empty text file.

For batch file, use `ANSI` encoding.

For normal text file, `UTF16` or `UTF8` encoding is highly recommended.

## Example

```pebakery
// Create empty file Hello.cmd. 
FileCreateBlank,%DestDir%\Hello.cmd
```

```pebakery
// Create empty file Unicode.txt, write BOM (U+FEFF) encoded with UTF-16 Little Endian.
FileCreateBlank,%DestDir%\Unicode.txt,UTF16
```

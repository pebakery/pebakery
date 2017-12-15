# FileCopy

Copy file.

When used with wildcard, multiple files can be copied.

## Syntax

```pebakery
FileCopy,<SrcFile>,<DestPath>,[PRESERVE],[NOWARN],[NOREC]
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcFile | File or files to copy.<br>Wildcard (*, ?) can be used in filename. |
| DestPath | Destination to copy files. |

- Flags

| Flag | Description |
| --- | --- |
| PRESERVE | Do not overwrite. |
| NOWARN | Do not log warning if a file is overwritten. |
| NOREC | Do not copy subdirectories when using wildcard. |

## Remarks

If `DestPath` is a directory, `SrcFile` will be copied under `DestPath`.

Elsewhere, `SrcFile` will be copied to the path `DestPath`, renaming if necessary.

When wildcard is used in filename, `FileCopy` copies subdirectories by default. To prevent this, use `NOREC` flag. Note that Wildcard cannot be used in directory.

## Example

Let us assume a directory `%SrcDir%` contains these files:

```pebakery
(D) %SrcDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) Y.ini
|- (F) AZ.txt
```

```pebakery
// %SrcDir%\AZ.txt will be copied into %DestDir%\AZ.txt
FileCopy,%SrcDir%\AZ.txt,%DestDir%

- Result
(D) %DestDir%
|- (F) AZ.txt
```

```pebakery
// %SrcDir%\A.txt will be copied into %DestDir%\B.txt
FileCopy,%SrcDir%\AZ.txt,%DestDir%\B.txt

- Result
(D) %DestDir%
|- (F) B.txt
```

```pebakery
// All .txt files in %SrcDir% will be copied under %DestDir%.
FileCopy,%SrcDir%\*.txt,%DestDir%

- Result
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ACC - (F) C.txt, D.txt
|- (F) AZ.txt
```

```pebakery
// All .txt files in %SrcDir% will be copied under %DestDir%, ignoring subdirectory.
FileCopy,%SrcDir%\*.txt,%DestDir%,NOREC

- Result
(D) %DestDir%
|- (F) AZ.txt
```

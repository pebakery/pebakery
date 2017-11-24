# File Command

## FileCopy

Copy file.

When used with wildcard, multiple files can be copied.

### Syntax

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

### Remarks

If `DestPath` is a directory, `SrcFile` will be copied under `DestPath`.

Elsewhere, `SrcFile` will be copied to the path `DestPath`, renaming if necessary.

When wildcard is used in filename, `FileCopy` copies subdirectories by default. To prevent this, use `NOREC` flag. Note that Wildcard cannot be used in directory.

### Example

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

## FileDelete

Delete file.

When used with wildcard, multiple files can be deleted.

### Syntax

```pebakery
FileDelete,<FilePath>,[NOWARN],[NOREC]
```

- Arguments

| Argument | Description |
| --- | --- |
| FilePath | File or files to delete.<br>Wildcard (*, ?) can be used in filename. |

- Flags

| Flag | Description |
| --- | --- |
| NOWARN | Do not log warning if file not exists. |
| NOREC | Ignore subdirectories when deleting files when using wildcard. |

### Remarks

When wildcard is used, files in subdirectories will also be deleted. To prevent this, use `NOREC` flag.

### Example

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
// %SrcDir%\AZ.txt will be deleted.
FileDelete,%SrcDir%\AZ.txt

Result
(D) %SrcDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) Y.ini
```

```pebakery
// All .txt files in %SrcDir% will be deleted.
FileDelete,%SrcDir%\*.txt

Result
(D) %SrcDir%
|- (D) AAA
|- (D) ABB - (F) B.ini
|- (D) ACC
|- (D) AEE
|- (F) Y.ini
```

```pebakery
// All .ini files in %SrcDir% will be deleted, ignoring subdirectory.
FileDelete,%SrcDir%\*.ini,NOREC

Result
(D) %SrcDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) AZ.txt
```

## FileRename

**Alias**: `FileMove`

Rename or move a file.

### Syntax

```pebakery
FileRename,<SrcPath>,<DestPath>
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcPath | Path of file to rename. |
| DestPath | New path to rename file. |

### Remark

FileRename can also be used to move file.

### Example

```pebakery
// A.txt will be renamed to B.txt.
FileRename,%SrcDir%\A.txt,%SrcDir%\B.txt
```

```pebakery
// %SrcDir%\A.txt will be moved into %DestDir%\B.txt.
FileMove,%SrcDir%\A.txt,%DestDir%\B.txt
```

## FileCreateBlank

Create empty file.

Can also initialize unicode BOM.

### Syntax

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

### Remarks

This command is useful to create empty text file.

For batch file, use `ANSI` encoding.

For normal text file, `UTF16` or `UTF8` encoding is highly recommended.

### Example

```pebakery
// Create empty file Hello.cmd. 
FileCreateBlank,%DestDir%\Hello.cmd
```

```pebakery
// Create empty file Unicode.txt, write BOM (U+FEFF) encoded with UTF-16 Little Endian.
FileCreateBlank,%DestDir%\Unicode.txt,UTF16
```

## FileSize

**Alias**: `Retrieve,FileSize`

Get size of the file.

### Syntax

```pebakery
FileSize,<FilePath>,<DestVar>
```

- Arguments

| Argument | Description |
| --- | --- |
| FilePath | Path of the file. |
| DestVar | Variable name to save file size. |

### Example

```pebakery
// %Dest% is set to "246784".
FileSize,%WindowsDir%\System32\notepad.exe,%Dest%
```

## FileVersion

**Alias**: `Retrieve,FileVersion`

Get version of the file.

### Syntax

```pebakery
FileVersion,<FilePath>,<DestVar>
```

- Arguments

| Argument | Description |
| --- | --- |
| FilePath | Path of the file. |
| DestVar | Variable name to save file version. |

### Example

```pebakery
// %Dest% is set to "10.0.16299.15".
FileVersion,%WindowsDir%\System32\notepad.exe,%Dest%
```

## DirCopy

Copy directory.

When used with wildcard, multiple files can be copied.

### Syntax

```pebakery
DirCopy,<SrcDir>,<DestDir>
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcDir | Directory to copy.<br>Wildcard (*, ?) can be used in name. |
| DestDir | Destination directory. New directory will be created if not exists. |

### Remarks

If `DestDir` is a file, DirCopy fails.

If wildcard is used, `DestDir` copies subdirectories filtered by wildcard.

WinBuilder 082 has a bug that DirCopy works same with FileCopy when wildcard is used. Since many codes relies on this bug, PEBakery has a compatibility option to simulate this bug.

### Example

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
// All files of %SrcDir% will be copied into %DestDir%.
DirCopy,%SrcDir%,%DestDir%

- Result
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) Y.ini
|- (F) AZ.txt
```

```pebakery
// All files of %SrcDir% will be copied into %DestDir%.
DirCopy,%SrcDir%\A*,%DestDir%

- Correct Result
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE

- WB082 Result (Bug)
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) AZ.txt
```

```pebakery
// All files of %SrcDir% will be copied into %DestDir%.
DirCopy,%SrcDir%\A*,%DestDir%

- Correct Result
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE

- WB082 Result (Bug)
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) AZ.txt
```

## DirDelete

Delete a directory.

### Syntax

```pebakery
DirDelete,<DirPath>
```

- Arguments

| Argument | Description |
| --- | --- |
| DirPath | Directory to delete. |

### Example

```pebakery
// %SrcDir% will be deleted.
DirDelete,%SrcDir%
```

## DirMove

Move a directory.

### Syntax

```pebakery
DirMove,<SrcDir>,<DestPath>
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcDir | Source directory to move. |
| DestPath | Destination directory. |

### Example

```pebakery
// If %DestDir% exists : %SrcDir%\A will be moved into %DestDir%\A.
// If %DestDir% not exist : %SrcDir%\A will be moved into %DestDir%.
DirMove,%SrcDir%\A,%DestDir%
```

## DirMake

Create a directory.

### Syntax

```pebakery
DirMake,<DestDir>
```

- Arguments

| Argument | Description |
| --- | --- |
| DestDir | Directory to create. |

### Example

```pebakery
// %DestDir% will be created.
DirDelete,%DestDir%
```

## DirSize

**Alias**: `Retrieve,DirSize`

Get size of the directory.

### Syntax

```pebakery
DirSize,<DirPath>,<DestVar>
```

- Arguments

| Argument | Description |
| --- | --- |
| DirPath | Path of the directory. |
| DestVar | Variable name to save directory size. |

### Remark

If `DirPath` contains files cannot be accessed with `administrator` privilege, PEBakery ignores them.

### Example

```pebakery
// %Dest% is set to "8364393557".
DirSize,%WindowsDir%\System32,%Dest%
```

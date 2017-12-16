# CopyOrExpand

Copy file or extract file from cabinet.

If source file exists, it will be copied. Otherwise, PEBakery will search compressed cabniet and extract them.

## Syntax

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

## Remarks

If `SrcFile` is `%SrcDir%\EXPLORER.EXE`, PEBakery search for files in `%SrcDir%` in order of:

1. EXPLORER.EXE (Copy)
1. EXPLORER.EX_ (Expand)

Its behavior resembles [SetupDecompressOrCopyFile](https://msdn.microsoft.com/en-us/library/aa376992(v=vs.85).aspx) API.

This command is mainly used to copy EXE, DLL from EX\_, DL\_ in NT5 sources.

Depends on `cabinet.dll`, a Windows component.

## Example

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

# Expand

Decompress files from cabinet.

## Syntax

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

## Remarks

This command is mainly used to extract EXE, DLL from EX\_, DL\_ in NT5 sources.

Depends on `cabinet.dll`, a Windows component.

## Example

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

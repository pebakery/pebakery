# System,GetFreeSpace

Returns the free disk space of a path in Megabytes.

## Syntax

```pebakery
System,GetFreeSpace,<Path>,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| Path | The full Path of drive or directory to receive information from. |
| %DestVar% | The variable where the drive letter will be stored. |

## Remarks

PEBakery calculates 1 Megabyte = 1024 Bytes.

## Related

## Examples

### Example 1

```pebakery
[main]
Title=GetFreeSpace Example
Description=Show usage of the System,GetFreeDrive command.
Level=5
Version=1
Author=Homes32

[variables]
%RequiredFreeMB%=800

[process]
// Check to see if we have enough free space to build this project.
System,GetFreeSpace,%TargetDir%,%varTarget%
If,%varTarget%,SMALLER,%RequiredFreeMB%,Then,Halt,"You only have %varTarget% MB free space in your target directory. You need at least %RequiredFreeMB% MB free in order to build this project."
```
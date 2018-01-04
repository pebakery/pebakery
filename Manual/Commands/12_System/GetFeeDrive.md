# System,GetFreeDrive

Returns the highest available free drive letter, Usually the "Z:" drive.

## Syntax

```pebakery
System,GetFreeDrive,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| %DestVar% | The variable where the drive letter will be stored. |

## Remarks

## Related

## Examples

### Example 1

```pebakery
[main]
Title=GetFreeDrive Example
Description=Show usage of the System,GetFreeDrive command.
Level=5
Version=1
Author=Homes32

[variables]

[process]
System,GetFreeDrive,%driveLetter%
Message,"The last available drive letter is: %driveLetter%"
```
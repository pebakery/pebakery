# System,ErrorOff

Prevents a process from failing in the event an error occurs.

Errors that would normally cause a command to fail will be ignored and processing will continue.

## Syntax

```pebakery
System,ErrorOff[,Lines]
```

### Arguments

| Argument | Description |
| --- | --- |
| Lines | **(Optional)** - The number of succeeding lines on which errors will be ignored. The default is to ignore only the line immediately following the `System,ErrorOff` command.|

## Remarks

This command allows you to override the default "Fail on Error" behavior of commands such as `FileCopy` `RegRead`.

An error message will still be generated in the log, however it will have a `Muted` state.

## Related

## Examples

### Example 1

```pebakery
[Main]
Title=ErrorOff Example
Author=Homes32
Description=Demonstrate usage of the System,ErrorOff command.
Version=1
Level=5

[Interface]

[variables]

[process]

// Don't stop processing if myFile.exe can't be copied to our target directory.
System,ERROROFF
FileCopy,C:\Temp\myFile.exe,%TargetDir%\myFile.exe


// Don't stop processing if the following registry values do not exist.
System,ERROROFF,2
RegRead,HKCR,Wow6432Node\Applications\vmware-mount.exe\shell\Mount\command,,%VMtmp%
RegRead,HKLM,"CurrentControlSet\Services\Eventlog\Application\VMware Virtual Mount Service Extended",EventMessageFile,%VMtmp%
```
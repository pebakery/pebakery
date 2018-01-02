# WimUnmount

Unmounts a mounted image in a Windows image (.wim) file from the specified directory.

## Syntax

```pebakery
WimMount,<MountDir>
```

### Arguments

| Argument | Description |
| --- | --- |
| MountDir | The full path to the directory where the .wim file is mounted. If the directory does not exist or there is no image mounted the operation will fail. |

## Remarks

This command internally uses `wimgapi.dll`.

## Related

[WimMount](./WimMount.md)

## Examples

Unmount the install.wim image mounted to our *%BaseDir%\Mount\InstallWim* folder.

### Example 1

```pebakery
[Main]
Title=WimUnmount Example
Description=Show usage of the WimUnmount command.
Author=Homes32
Level=5
Version=1

[Variables]
%InstallWim%=C:\W10\x64\sources\install.wim
%WimIndex%=4
%MountDir%=C:\PEBakery\Mount\Win10PESE\Source\InstallWimSrc

[Process]
// the directory %MountDir% must exist or the mount operation will fail.
If,Not,EXISTDIR,%MountDir%,DirMake,%MountDir%
Echo,"Mounting Install.wim from#$x--> %InstallWim% [Index: %WimIndex%]"
// Mount the image with index 4
WimMount,%InstallWim%,%WimIndex%,%MountDir%
Echo,"This is where you would copy some files, etc..."
Wait,10
// Cleanup after ourselves...
Echo,"UnMounting %MountDir%..."
WimUnmount,%MountDir%
```

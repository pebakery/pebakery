# WimMount

Mounts an image in a Windows image (.wim) file to the specified directory.

## Syntax

```pebakery
WimMount,<ImageFile>,<Index>,<MountDir>
```

### Arguments

| Argument | Description |
| --- | --- |
| ImageFile | The full path to the .wim file that to be mounted. |
| Index | The index of the image in the .wim file to be mounted. |
| MountDir | The full path to the directory where the .wim file is to be mounted. If the directory does not exist or there is already an image mounted the operation will fail. |

## Remarks

You must unmount the image using the `WimUnmount` command when you are finished.

This command internally uses `wimgapi.dll`.

## Related

[WimUnmount](./WimUnmount.md)

## Examples

Mount the install.wim image to our *%BaseDir%\Mount\InstallWim* folder.

### Example 1

```pebakery
[Main]
Title=WimMount Example
Description=Show usage of the WimMount command.
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

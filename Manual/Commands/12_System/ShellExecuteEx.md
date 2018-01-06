# ShellExecuteEx

Runs an external program and continues processing immediately.

## Syntax

```pebakery
ShellExecuteEx,<Action>,<FilePath>[,Params][,WorkDir]
```

### Arguments

| Argument | Description |
| --- | --- |
| Action | The method used to start the external file. Can be one of the following: |
|| Open - If the file is an executable it will be launched. If the file is not an executable it will be opened using the default application associated with that file type.  |
|| Hide - The file will be launched in hidden mode. Console programs writing to the StdOut and StdErr streams will have their output redirected to PEBakerys build window and written to the log. |
|| Print - Print the contents of the file using the systems default printer. |
|| Explore - Open an explorer window. (Can be used to display the contents of the local file system.) |
|| Min - Same as Open, but starts the program minimized to the taskbar. |
| FilePath | The file to execute. If a full path is not specified PEBakery will attempt to locate it using the operating system's %PATH% variable. |
| Params | **(Optional)** Any arguments you wish to pass to the program. Use ("") for none. |
| WorkDir | **(Optional)** The working directory for the program. Use ("") to specify the current working directory. |

## Remarks

Most often used to launch programs and browsers from a plugin interface.

## Related

[ShellExecute](./ShellExecute.md), [ShellExecuteDelete](./ShellExecuteDelete.md), [ShellExecuteSlow](./ShellExecuteSlow.md)

## Examples

### Example 1

```pebakery
[Main]
Title=ShellExecuteEx Example
Author=Homes32
Description=Demonstrate usage of the ShellExecuteEx command.
Version=1
Level=5

[Interface]

[variables]

[process]

// Open notepad.exe
ShellExecuteEx,open,notepad.exe

// Open our %BaseDir% in a file browser.
ShellExecuteEx,explore,%BaseDir%

// "Open" a program "Minimized".
Echo,"Running a console application minimized..."
ShellExecuteEx,min,cmd.exe,"/C PING 127.0.0.1 -n 10"
```
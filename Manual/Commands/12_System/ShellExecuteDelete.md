# ShellExecuteDelete

Runs an external program and waits for it to terminate before processing continues. Once the program terminates it will be deleted.

## Syntax

```pebakery
ShellExecuteDelete,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
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
| %ExitOutVar% | **(Optional)** Variable that will be updated with the *Exit Code* returned by the application. This can be used to validated a successful execution or return a value to the plugin for further processing. If you do not specify this argument you can still read the *Exit Code* from the fixed `%ExitCode%` variable, which will always contain the *Exit Code* from the last `ShellExecute` instance. |

## Remarks

This command is designed to be used to execute small tools/scripts/self-extracting archives and "clean-up" after itself when finished.

**Warning:**
This command is intended for use by experienced users only. There is no undelete!

Using the `Hide` action with an application that does not exit automatically when finished will cause the plugin to hang until you manually end the process.

## Related

[ShellExecute](./ShellExecute.md), [ShellExecuteEx](./ShellExecuteEx.md), [ShellExecuteSlow](./ShellExecuteSlow.md)

## Examples

### Example 1

```pebakery
// run a program to perform a process in our %TargetDir%, then delete myTool.exe when finished.
ShellExecuteDelete,hide,myTool.exe,"/process",%TargetDir%
```
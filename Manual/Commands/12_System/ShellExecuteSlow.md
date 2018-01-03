# ShellExecuteSlow

Runs an external program with *Below Normal* CPU Priority and waits for it to terminate before processing continues.

Contrary to its name, this command does not instruct the program to run slower then normal. Rather the CPU scheduler is instructed to give other running applications higher priority in regard to processor resources. Modern operating system schedulers generally do a good job of regulating CPU resources, but this command is still useful in cases where the application to be executed is known to consume a large amount of system resources.

## Syntax

```pebakery
ShellExecuteSlow,<Action>,<FilePath>[,Params][,WorkDir][,%ExitOutVar%]
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

**Warning:** Using the `Hide` action with an application that does not exit automatically when finished will cause the plugin to hang until you manually end the process.

## Related

[ShellExecute](./ShellExecute.md), [ShellExecuteDelete](./ShellExecuteDelete.md), [ShellExecuteEx](./ShellExecuteEx.md)

## Examples

### Example 1

```pebakery
[Main]
Title=ShellExecuteSlow Example
Author=Homes32
Description=Demonstrate usage of the ShellExecuteSlow command.
Version=1
Level=5

[Interface]

[variables]

[process]

// "Open" a program that stays open until closed.
Echo,"Running notepad.exe...#$xYou must close the notepad application in order to continue processing."
ShellExecuteSlow,open,notepad.exe

// "Open" a program that will exit when it is finished.
Echo,"Running a console application with the OPEN action is annoying!#$xPlease use the HIDE action unless your application require user input!"
ShellExecuteSlow,open,cmd.exe,"/C PING 127.0.0.1 -n 10","",%return%
Message,"ShellExecute returned: %return%"

// Run a program "Hidden" that will exit when it is finished.
Echo,"Running a console application requiring no user input with the HIDE action to prevent annoying pop-up boxes and keep the user from accidentally closing the program before it is finished.#$xIt also allows us to see the result of the program in the log."
ShellExecuteSlow,hide,cmd.exe,"/C PING 127.0.0.1 -n 10","",%return%
Message,"ShellExecute returned: %return%"

// "Open" a program "Minimized".
Echo,"Running a console application minimized..."
ShellExecuteSlow,min,cmd.exe,"/C PING 127.0.0.1 -n 10","",%return%
Message,"ShellExecute returned: %return%"
```
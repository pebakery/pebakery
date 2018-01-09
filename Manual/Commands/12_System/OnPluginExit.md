# System,OnPluginExit

**Alias**: `System,OnScriptExit`

Specifies the command to be executed before the current plugin terminates.

## Syntax

```pebakery
System,OnScriptExit,<Command>
```

### Arguments

| Argument | Description |
| --- | --- |
| Command | The command that will be executed before the plugin terminates. |

### Reason Codes

If the `Command` is `Run`, the *reason* for the exit is passed as parameter #1. This allows you to perform additional processing in order to control what actions are taken.

*Reason* can be one of the following values:

| Reason | Description |
| --- | --- |
| DONE | All commands were processed without errors. |
| STOP | The user clicked the *STOP* button. |
| ERROR | The plugin terminated because of an error. |
| COMMAND | The plugin terminated because of a `System,Halt` or `System,Exit` command. |
| EXCEPTION | A system exception occurred during processing. *Included for compatibility with Winbuilder 082. PEBakery does not currently return this reason.*|

## Remarks

This statement can be written anywhere inside the running part of the plugin. Calling this command additional times will overwrite the last value of `Command`.

## Related

[System,OnBuildExit](./OnBuildExit.md)

## Examples

### Example 1

```pebakery
[Main]
Title=OnPluginExit/OnBuildExit
Author=Homes32
Description=Demonstrates usage of the System,OnPluginExit and System,OnBuildExit commands.
Version=1
Level=5

[Interface]
Callback=Callback,1,14,27,15,117,65,OnPluginExit,OnBuildExit,Both,0
Simulation="Callback Event",1,14,154,15,204,117,SUCCESS,ERROR,STOP,HALT,EXIT,"CRITICAL EXCEPTION",0
RunSimulation="Run Simulation",1,8,390,25,80,25,Process,0,False,_Process_,False

[variables]

[process]

// Define our exit function
If,%Callback%,Equal,0,Begin
  System,OnPluginExit,Run,%PluginFile%,CLEANUP
End
If,%Callback%,Equal,1,Begin
  System,OnBuildExit,Run,%PluginFile%,CLEANUP
End
If,%Callback%,Equal,2,Begin
  System,OnPluginExit,Run,%PluginFile%,CLEANUP
  System,OnBuildExit,Run,%PluginFile%,CLEANUP
End

If,%Simulation%,Equal,0,Echo,"Running successful exit simulation."
If,%Simulation%,Equal,1,Begin
  Echo,"Running error simulation."
  // Force an error to occur
  FileCopy,foo,bar
End
If,%Simulation%,Equal,2,Begin
  Echo,"Running user STOP button simulation. ..#$xThe plugin will now pause for 5 seconds. Now is your chance to press the STOP button..."
  Wait,5
End
If,%Simulation%,Equal,3,Halt,"Halt Simulation."
If,%Simulation%,Equal,4,Exit,"Exit Simulation."
If,%Simulation%,Equal,5,Echo,"PEBakery does not currently return this reason."

[CLEANUP]
Echo,"Entering Cleanup function..."
// Error
If,#1,EQUAL,ERROR,Begin
  Beep,ERROR
  Message,"An error occurred. Exiting...",ERROR,5
End

// User STOP
If,#1,EQUAL,STOP,Begin
  Beep,Asterisk
  Message,"You pressed the STOP button. Exiting...",WARNING,5
End

// Build/Plugin Finished
If,#1,EQUAL,DONE,Begin
  Beep,OK
  Message,"Finished Processing! Exiting...",INFORMATION,5
End

// HALT/EXIT COMMAND
If,#1,EQUAL,COMMAND,Begin
  Beep,CONFIRMATION
  Message,"A halt or exit command was issued. Exiting...",ERROR,5
End

// Critical Exception
If,#1,EQUAL,EXCEPTION,Begin
  Beep,ERROR
  Message,"An critical exception occurred. Exiting...",ERROR,5
End

// ALL
// These commands will all be processed regardless of REASON.
Echo,"Unloading Registry Hives..."
RegHiveUnload,%Software_Temp%
Echo,"Calling another function..."
Run,%PluginFile%,CLEANUP-2
Echo,"End of Cleanup."

[CLEANUP-2]
Echo,"We can run commands from other sections as well..."

```
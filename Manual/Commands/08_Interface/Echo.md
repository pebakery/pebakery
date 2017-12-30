# Echo

Display a message in the processing window while the plugin is running. This message will persist until another message is displayed or the plugin execution finishes.

## Syntax

```pebakery
Echo,<Message>[,WARN]
```

### Arguments

| Argument | Description |
| --- | --- |
| Message | The text that will be displayed to the user. |

### Flags

| Flag | Description |
| --- | --- |
| WARN | **(Optional)** Flags the `Message` as a warning in the log. |

## Remarks

`Echo` supports displaying multi-line text when you specify the newline `#$x` escape code.

`Echo` is most often used for keeping the user updated on what the plugin is working on, however it can also be used as a debugging tool, as the output of the statement is written to the log.

## Related

[EchoFile](./EchoFile.md), [Message](./Message.md)

## Examples

### Example 1

```pebakery
[main]
Title=Echo Example
Description=Show usage of the Echo command.
Level=5
Version=1
Author=Homes32

[variables]
%Message1%="Hello World!"

[process]
Echo,%Message1%
WAIT,5
Echo,"This is a#$xMulti#$xLine#$xMessage!"
WAIT,5
Echo,"Something went very wrong!",WARN
WAIT,5
```
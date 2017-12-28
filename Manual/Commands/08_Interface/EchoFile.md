# EchoFile

Write the contents of a file to the log.

## Syntax

```pebakery
EchoFile,<SrcFile>[,WARN][,ENCODE]
```

### Arguments

| Argument | Description |
| --- | --- |
| SrcFile | The full path of the file to be written to the log. |

### Flags

Flags are optional and may be specified in any order.

| Flag | Description |
| --- | --- |
| WARN | Flags the `Message` as a warning in the log. |
| ENCODE | Embed the file into the log using PEBakery's `Encode` function. |

## Remarks

`EchoFile` is designed to be used by developers in order to collect additional logs, config files, etc. to aid in troubleshooting. The `ENCODE` flag can be used to attach non-text files that can be extracted with PEBakery's `Extract` command.

## Related

[Echo](./Echo.md), [Message](./Message.md)

## Examples

### Example 1

```pebakery
[main]
Title=EchoFile Example
Description=Show usage of the EchoFile command.
Level=5
Version=1
Author=Homes32

[variables]
%LogFile%=%BaseDir%\Temp\myLog.txt

[process]
// Create a dummy log file.
FileCreateBlank,%LogFile%
TXTAddLine,%LogFile%,Line1,Append
TXTAddLine,%LogFile%,Line2,Append
TXTAddLine,%LogFile%,Line3,Append
TXTAddLine,%LogFile%,Line4,Append
TXTAddLine,%LogFile%,Line5,Append

// EchoFile
EchoFile,%LogFile%

// EchoFile with ENCODE
EchoFile,%LogFile%,ENCODE
```
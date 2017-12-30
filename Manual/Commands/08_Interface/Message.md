# Message

Displays a simple message box with optional timeout.

## Syntax

```pebakery
Message,<Message>,<Icon>[,Timeout]
```

### Arguments

| Argument | Description |
| --- | --- |
| Message | The text that will be displayed to the user. |
| Icon | Defines the Icon to display. Valid options are: |
|| Information - Displays an Information Icon |
|| Confirmation - Displays a Question Icon |
|| Error - Displays a Error Icon |
|| Warning - Displays a Warning Icon |
| Timeout | **(Optional)** Time in seconds before the message automatically closes. |

## Remarks

The exact icon displayed may vary based on your operating system configuration.

Messages block further processing until the message is acknowledged. Unless the situation requires user intervention it is recommended to set a reasonable timeout period, or use the `Echo` command in order to allow the plugin to continue processing.

## Related

[Echo](./Echo.md), [If,QUESTION](../13_Branch/If.md)

## Examples

### Example 1

```pebakery
[main]
Title=Message Example
Description=Show usage of the Message command.
Level=5
Version=1
Author=Homes32

[variables]

[process]
Message,"Informational Message",INFORMATION,10
Message,"Error Message",ERROR,10
Message,"Warning Message",WARNING,10
Message,"Confirmation Message",CONFIRMATION,10
```
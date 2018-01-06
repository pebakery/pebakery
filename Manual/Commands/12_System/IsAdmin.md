# System,IsAdmin

Checks to see if PEBakery was started by an account with "Admin" privileges.

## Syntax

```pebakery
System,IsAdmin,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| %DestVar% | The variable will return one of the following values: |
|| True - PEBakery was started with "Admin" privileges. |
|| False - PEBakery was not started with "Admin" privileges. |

## Remarks

This command is included for compatibility with Winbuilder 082. PEBakery always requires Administrator privileges to run.

## Related

## Examples

### Example 1

```pebakery
[main]
Title=IsAdmin Example
Description=Show usage of the System,IsAdmin command.
Level=5
Version=1
Author=Homes32

[variables]

[process]
System,IsAdmin,%isAdmin%
If,%isAdmin%,Equal,True,Message,"PEBakery is running as Admin."
Else,Message,"PEBakery is NOT running as Admin."
```
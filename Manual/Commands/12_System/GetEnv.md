# System,GetEnv

Gets the value of environment variable from the host operating system.

## Syntax

```pebakery
System,GetEnv,<EnvVar>,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| EnvVar | The name of the environment variable to read. Do **not** include surrounding `%` signs. Common examples are `SystemDrive SystemRoot TEMP WINDIR` |
| %DestVar% | The variable where the value of the environment variable will be stored. |

## Remarks

If `EnvVar` does not exist `%DestVar%` will contain a blank value.

## Related

## Examples

### Example 1

```pebakery
[main]
Title=GetEnv Example
Description=Show usage of the System,GetEnv command.
Level=5
Version=1
Author=Homes32

[variables]

[process]
System,GetEnv,TEMP,%hostTemp%
System,GetEnv,PROCESSOR_ARCHITECTURE,%hostARCH%
Message,"#$pTEMP#$p = %hostTemp%#$x#$pPROCESSOR_ARCHITECTURE#$p = %hostARCH%",INFORMATION
```
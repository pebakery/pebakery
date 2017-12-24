# IniReadSection

Read the section from a file that you specify.

## Syntax

```pebakery
IniReadSection,<FileName>,<Section>,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file to read. |
| Section | The section to be read. |
| DestVar | The value will be saved to this variable. |

## Remarks

This command was designed to log more infomation to assist troubleshooting.

The string to be stored in %DestVar% is human-readable rather than coding friendly, and it is mainly used with `Echo`.

PEBakery will optimize multiple `IniReadSection` in a row to single command.

## Example

Let's assume a file `%SrcFile%` contains these lines:

```pebakery
[English]
1=One
2=Two
3=Three

[Korean]
1=하나
2=둘
```

### Example 1

In the following example the section `English` will be stored inside `%Dest%`.

```pebakery
IniReadSection,%SrcFile%,English,%Dest%

// IniReadSection will return these lines into %Dest%.
[English]
1=One
2=Two
3=Three
```

### Example 2

IniReadSection will return the section `Korean` into `%Dest%`.

```pebakery
IniReadSection,%SrcFile%,Korean,%Dest%

// IniReadSection will return these lines into %Dest%.
[Korean]
1=하나
2=둘
```
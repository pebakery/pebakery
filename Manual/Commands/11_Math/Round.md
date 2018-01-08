# Math,Round

Returns a integer rounded to the nearest whole number.

## Syntax

```pebakery
Math,Round,<DestVar>,<Value>,<Unit>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | The number to round. |
| Unit | Unit to round to. (ex. Nearest 10, 20, 100, etc) |

## Remarks

File sizes can be rounded to the nearest Kb/MB/Gb/Tb/Pb using `StrFormat,Round`.

## Related

[Ceil](./Ceil.md), [Floor](./Floor.md), [StrFormat,Round](../10_String/Round.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Round Example
Description=Show usage of the Math,Round Command
Author=Homes32

[Variables]

[Process]
Math,Round,%result%,1024,1000
Message,"1024 rounded to the nearest 1000 = %result%"
```
# Math,Ceil

Returns a number rounded up to the next integer.

## Syntax

```pebakery
Math,Ceil,<DestVar>,<Value>,<Unit>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | The number to round up. |
| Unit | Unit to round up to. (ex. Nearest 10, 20, 100, etc) |

## Remarks

File sizes can be rounded up to the nearest Kb/MB/Gb/Tb/Pb using `StrFormat,Ceil`.

## Related

[Floor](./Floor.md), [Round](./Round.md), [StrFormat,Ceil](../10_String/Ceil.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Ceil Example
Description=Show usage of the Math,Ceil Command
Author=Homes32

[Variables]

[Process]
Math,Ceil,%result%,32,10
Message,"32 rounded UP to the nearest 10 = %result%"
```
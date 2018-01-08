# Math,Floor

Returns a number rounded down to the closest integer.

## Syntax

```pebakery
Math,Floor,<DestVar>,<Value>,<Unit>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | The number to round down. |
| Unit | Unit to round down to. (ex. Nearest 10, 20, 100, etc) |

## Remarks

File sizes can be rounded down to the nearest Kb/MB/Gb/Tb/Pb using `StrFormat,Floor`.

## Related

[Ceil](./Ceil.md), [Round](./Round.md), [StrFormat,Floor](../10_String/Floor.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Floor Example
Description=Show usage of the Math,Floor Command
Author=Homes32

[Variables]

[Process]
Math,Floor,%result%,32,10
Message,"32 rounded DOWN to the nearest 10 = %result%"
```
# Math,Mul

Multiplies two numbers.

## Syntax

```pebakery
Math,Mul,<DestVar>,<Value1>,<Value2>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value1 | The first value  in the equation. |
| Value2 | The second value in the equation. |

## Remarks

None.

## Related

[StrFormat,Mult](../10_String/Mult.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Mul Example
Description=Show usage of the Math,Mul Command
Author=Homes32

[Variables]

[Process]
Math,Mul,%prod%,20,10
Message,"20 * 10 = %prod%"
```
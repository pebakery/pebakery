# Math,IntDiv

Divides two integers and returns the quotient and remainder.

## Syntax

```pebakery
Math,IntDiv,<QuotientVar>,<RemainderVar>,<Value1>,<Value2>
```

### Arguments

| Argument | Description |
| --- | --- |
| QuotientVar | The variable where the result will be stored. |
| RemainderVar | The variable where the remainder will be stored. |
| Value1 | The first value  in the equation. |
| Value2 | The second value in the equation. |

## Remarks

None.

## Related

[Div](./Div.md), [StrFormat,Div](../10_String/Div.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-IntDiv Example
Description=Show usage of the Math,IntDiv Command
Author=Homes32

[Variables]

[Process]
Math,IntDiv,%quot%,%remain%,634,37
Message,"634 / 37 = %quot% r. %remain%"
```
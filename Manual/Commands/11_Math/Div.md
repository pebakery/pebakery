# Math,Div

Divides two numbers.

## Syntax

```pebakery
Math,Div,<DestVar>,<Value1>,<Value2>
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

[IntDiv](./IntDiv.md), [StrFormat,Div](../10_String/Div.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Div Example
Description=Show usage of the Math,Div Command
Author=Homes32

[Variables]

[Process]
Math,Div,%quot%,20,10
Message,"20 / 10 = %quot%"
```
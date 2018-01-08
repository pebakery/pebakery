# Math,Add

Adds two numbers.

## Syntax

```pebakery
Math,Add,<DestVar>,<Value1>,<Value2>
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

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Add Example
Description=Show usage of the Math,Add Command
Author=Homes32

[Variables]

[Process]
Math,Add,%sum%,10,20
Message,"10 + 20 = %sum%"
```
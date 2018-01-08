# Math,Sub

Subtracts two numbers.

## Syntax

```pebakery
Math,Sub,<DestVar>,<Value1>,<Value2>
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
Title=Math-Sub Example
Description=Show usage of the Math,Sub Command
Author=Homes32

[Variables]

[Process]
Math,Sub,%diff%,30,10
Message,"30 - 10 = %diff%"
```
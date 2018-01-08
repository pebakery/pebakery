# Math,BoolAnd

Performs an AND test on two boolean values. A true output results only if both values are true.

## Syntax

```pebakery
Math,BoolAnd,<DestVar>,<Vaue1>,<Value2>
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

[BoolNot](./BoolNot.md), [BoolOr](./BoolOr.md), [BoolXor](./BoolXor.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BoolAnd Example
Description=Show usage of the Math,BoolAnd Command
Author=Homes32

[Variables]

[Process]
Math,BoolAnd,%result1%,True,True
Math,BoolAnd,%result2%,True,False
Math,BoolAnd,%result3%,False,True
Math,BoolAnd,%result4%,False,False

// Output Result
Message,"Boolean AND Comparison:#$x[True/True] Return: %result1%#$x[True/False] Return: %result2%#$x[False/True] Return: %result3%#$x[False/False] Return: %result4%"
```
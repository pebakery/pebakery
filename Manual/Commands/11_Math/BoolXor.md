# Math,BoolXor

Performs an exclusive OR test on two boolean values. A true output results if one, and only one, of the values is true.

## Syntax

```pebakery
Math,BoolXor,<DestVar>,<Vaue1>,<Value2>
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

[BoolAnd](./BoolAnd.md), [BoolNot](./BoolNot.md), [BoolOr](./BoolOr.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BoolXor Example
Description=Show usage of the Math,BoolXor Command
Author=Homes32

[Variables]

[Process]
Math,BoolXor,%result1%,True,True
Math,BoolXor,%result2%,True,False
Math,BoolXor,%result3%,False,True
Math,BoolXor,%result4%,False,False

// Output Result
Message,"Boolean XOR Comparison:#$x[True/True] Return: %result1%#$x[True/False] Return: %result2%#$x[False/True] Return: %result3%#$x[False/False] Return: %result4%"
```
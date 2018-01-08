# Math,BoolNot

Performs an Not test on two boolean values.

## Syntax

```pebakery
Math,BoolNot,<DestVar>,<Vaue>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | The value to operate on. |

## Remarks

None.

## Related

[BoolAnd](./BoolAnd.md), [BoolOr](./BoolOr.md), [BoolXor](./BoolXor.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BoolNot Example
Description=Show usage of the Math,BoolNot Command
Author=Homes32

[Variables]

[Process]
Math,BoolNot,%result1%,True
Math,BoolNot,%result2%,False

// Output Result
Message,"Boolean Not Comparison:#$x[True] Return: %result1%#$x[False] Return: %result2%"
```
# Math,BitAnd

Performs a bitwise AND operation.

## Syntax

```pebakery
Math,BitAnd,<DestVar>,<Vaue1>,<Value2>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value1 | The first value. |
| Value2 | The second value. |

## Remarks

None.

## Related

[BitNot](./BitNot.md), [BitOr](./BitOr.md), [BitShift](./BitShift.md), [BitXor](./BitXor.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BitAnd Example
Description=Show usage of the Math,BitAnd Command
Author=Homes32

[Variables]

[Process]
Math,BitAnd,%result%,13,7

// Output Result
Message,"Bitwise AND:#$x#$x00001101 (13)#$x00000111 (7)#$x--------------#$x00000101 (5)#$x#$xReturn: %result%"
```
# Math,BitXor

Performs a bitwise exclusive OR operation.

## Syntax

```pebakery
Math,BitXor,<DestVar>,<Vaue1>,<Value2>
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

[BitAnd](./BoolAnd.md), [BitNot](./BitNot.md), [BitShift](./BitShift.md), [BitOr](./BitOr.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BitXor Example
Description=Show usage of the Math,BitXor Command
Author=Homes32

[Variables]

[Process]
Math,BitXor,%result%,13,7

// Output Result
Message,"Bitwise XOR:#$x#$x00001101 (13)#$x00000111 (7)#$x--------------#$x00001010 (10)#$x#$xReturn: %result%"
```
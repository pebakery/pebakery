# Math,BitOr

Performs a bitwise OR operation.

## Syntax

```pebakery
Math,BitOr,<DestVar>,<Vaue1>,<Value2>
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

[BitAnd](./BitAnd.md), [BitNot](./BitNot.md), [BitXor](./BitXor.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BitOr Example
Description=Show usage of the Math,BitOr Command
Author=Homes32

[Variables]

[Process]
Math,BitOr,%result%,13,7

// Output Result
Message,"Bitwise OR:#$x#$x00001101 (13)#$x00000111 (7)#$x--------------#$x00001111 (15)#$x#$xReturn: %result%"
```
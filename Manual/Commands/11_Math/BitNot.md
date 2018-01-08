# Math,BitNot

Performs a bitwise exclusive OR operation.

## Syntax

```pebakery
Math,BitNot,<DestVar>,<Vaue>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | The value to operate on. |

## Remarks

None.

## Related

[BitAnd](./BitAnd.md), [BitOr](./BitOr.md), [BitShift](./BitShift.md), [BitXor](./BitXor.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BitNot Example
Description=Show usage of the Math,BitNot Command
Author=Homes32

[Variables]

[Process]
Math,BitNot,%result%,106

// Output Result
Message,"Bitwise NOT:#$x#$x00000000000000000000000001101010 (106)#$x11111111111111111111111110010101 (4294967189)#$x#$xReturn: %result%"
```
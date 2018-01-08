# Math,BitShift

Performs a bit shifting operation.

## Syntax

```pebakery
Math,BitShift,<DestVar>,<Value>,<Direction>,<Shift>[,Size][,UNSIGNED]
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | The number to be shifted. |
| Direction | The direction to shift: `LEFT` or `RIGHT` |
| Shift | The number of bits to shift. |
| Size | **(Optional)** Size of the `Value` in bits: `8` `16` `32` `64` |
| UNSIGNED | **(Optional)** Specify that the `Value` is an unsigned integer. |

## Remarks

None.

## Related

[BitAnd](./BitAnd.md), [BitNot](./BitNot.md), [BitOr](./BitOr.md), [BitXor](./BitXor.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-BitShift Example
Description=Show usage of the Math,BitShift Command
Author=Homes32

[Variables]

[Process]
Math,BitShift,%result1%,14,LEFT,2
Math,BitShift,%result2%,14,RIGHT,2

// Output Result
Message,"Perform a Bitwise Shift 2 bits LEFT:#$x#$x00001110 (14)#$x00111000 (56)#$x#$xReturn: %result1%#$x#$xPerform a Bitwise Shift 2 bits RIGHT:#$x#$x00001110 (14)#$x00000011 (3)#$x#$xReturn: %result2%"
```
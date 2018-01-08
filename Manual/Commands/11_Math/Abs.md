# Math,Abs

Calculates the absolute value of a number.

## Syntax

```pebakery
Math,Abs,<DestVar>,<Value>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | Number. |

## Remarks

None.

## Related

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Abs Example
Description=Show usage of the Math,Abs Command
Author=Homes32

[Variables]

[Process]
Math,Abs,%result%,-32
Message,"Absolute value of -32 = %result%"
```
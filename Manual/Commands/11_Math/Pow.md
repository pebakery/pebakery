# Math,Pow

Raises a number to the power.

## Syntax

```pebakery
Math,Pow,<DestVar>,<Base>,<PowerOf>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Base | Base number to raise. |
| PowerOf | The power to raise `Base`. |

## Remarks

None.

## Related

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Pow Example
Description=Show usage of the Math,Pow Command
Author=Homes32

[Variables]

[Process]
Math,Pow,%result%,2,4
Message,"2^4 = %result%"
```
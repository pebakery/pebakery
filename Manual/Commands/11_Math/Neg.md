# Math,Neg

Returns the arithmetic inverse of a value.

## Syntax

```pebakery
Math,Neg,<DestVar>,<Value>
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Value | The value to inverse. |

## Remarks

None.

## Related

## Examples

### Example 1

```pebakery
[Main]
Title=Math-Neg Example
Description=Show usage of the Math,Neg Command
Author=Homes32

[Variables]

[Process]
Math,Neg,%neg%,32
Message,"32: %neg%"
Math,Neg,%neg%,-32
Message,"-32: %neg%"
```
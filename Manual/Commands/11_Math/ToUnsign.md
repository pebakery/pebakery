# Math,ToUnSign

Converts an Signed Integer to an Unsigned Integer.

## Syntax

```pebakery
Math,ToUnsign,<DestVar>,<Integer>[,Size]
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Integer | The Integer to be converted to Unsigned Integer. |
| Size | **(Optional)** Size of the number in bits: `8` `16` `32` `64` |

## Remarks

None.

## Related

[ToSign](./ToSign.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-ToUnsign Example
Description=Show usage of the Math,ToUnsign Command
Author=Homes32

[Variables]

[Process]
Math,ToUnSign,%result%,-32768
Message,"%result%"
```
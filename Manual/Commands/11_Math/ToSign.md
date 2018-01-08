# Math,ToSign

Converts an Unsigned Integer to a Signed Integer.

## Syntax

```pebakery
Math,ToSign,<DestVar>,<Integer>[,Size]
```

### Arguments

| Argument | Description |
| --- | --- |
| DestVar | The variable where the result will be stored. |
| Integer | The Unsigned Integer to be converted to Signed Integer. |
| Size | **(Optional)** Size of the number in bits: `8` `16` `32` `64` |

## Remarks

None.

## Related

[ToUnsign](./ToUnsign.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Math-ToSign Example
Description=Show usage of the Math,ToSign Command
Author=Homes32

[Variables]

[Process]
Math,ToSign,%result%,4294934528
Message,"%result%"
```
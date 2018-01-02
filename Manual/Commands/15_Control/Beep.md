# Beep

Provide auditory feedback to the user.

## Syntax

```pebakery
Beep,<Type>
```

### Arguments

| Argument | Description |
| --- | --- |
| Type | Select from the following sounds: |
|| Ok |
|| Error |
|| Asterisk |
|| Confirmation |

## Remarks

The exact sound that is played with each `Type` is defined by the operating system's sound scheme.

## Related

## Examples

### Example 1

```pebakery
Beep,Error
```
# TXTDelEmptyLines

This function will remove all empty lines from a given text file.

## Syntax

```pebakery
TXTDelEmptyLines,<FileName>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |

## Remarks

This command was created to make some specific INI files smaller in overall size.

If `FileName` does not exist the operation will fail.

## Examples

### Example 1

```pebakery
TxtDelEmptyLines,C:\myFile.txt
```
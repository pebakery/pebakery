# TXTDelLine

This command will remove lines from a given text file that begin with a specific string.

## Syntax

```pebakery
TXTDelLine,<FileName>,<String>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| String   | String of text used to mark the beginning of the line to be deleted. `String` is Case Sensitive.

## Remarks

Caution should be exercised when using this command, as the value of `String` doesn't need to be the value of the entire line, it only need to match the beginning of the line.

If `FileName` does not exist the operation will fail.

PEBakery will optimize multiple `TXTDelLine` in a row to single command.

## Examples

### Example 1

Assume we have the following file:

```pebakery
// C:\myFile.txt
1. Hello World!
2. HelloWorld!
3. This will be the only line remaining.
```

In this example all lines that begin with "Hello" inside C:\myFile.txt will be removed, leaving line 3 as the only remaining line in the file.

```pebakery
TXTDelLine,C:\myFile.txt,"Hello"
```
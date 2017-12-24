# TXTAddLine

This command will insert a line of text at the specified location inside a file.

## Syntax

```pebakery
TXTAddLine,<FileName>,<String>,<Action>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| String | The text to be added. |
| Action | Choose one of the following directives:|
|| **Prepend** - Will insert the line of text at the top of the file. |
|| **Append** - Will insert the line of text at the end of the file.

## Remarks

If `FileName` does not exist the operation will fail.

WinBuilder's implimentation of TxtAddLine allowed for an `Action` called **Place** which would allow the developer to specify a line number where the text should be inserted. This feature was depreciated in PEBakery due to lack of perceived usefulness.

PEBakery will optimize multiple `TXTAddLine` in a row to single command.

## Examples

### Example 1

In this example we add a line saying `Hello World!` to the end of the file.

```pebakery
TXTAddLine,C:\myFile.txt,Hello World!,Append
```

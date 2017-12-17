# TxtAddLine

This command will insert a line of text at the specified location inside a file.

## Syntax

```pebakery
TXTAddLine,<Filename>,<String>,<Action>[,Line#] 
```

### Arguments

| Argument | Description |
| --- | --- | 
| FileName | The full path of the file. |
| String | The text to be added. |
| Action | Choose one of the following directives:|
|| **Prepend** - Will insert the line of text at the top of the file. |
|| **Append** - Will insert the line of text at the end of the file.
|| **Place** - Will insert the line of text at the line number specified by the `Line#` argument. 
| Line# | If `Action` is `Place` then this argument contains the line number where the text will be placed, otherwise it is ignored.
## Remarks
If `FileName` does not exist the operation will fail.
There is no limit to the `Line#` value. 

## Examples

##### Example 1
In this example we add a line saying `Hello World!` in the 5th line of the file. If the file doesn't have 5 lines, then it will be placed after the current last line.

```pebakery
TxtAddLine,C:\myFile.txt,Hello World!,Place,5 
```

##### Example 2
In this example we add a line saying `Hello World!` to the end of the file. 

```pebakery
TxtAddLine,C:\myFile.txt,Hello World!,Append
```
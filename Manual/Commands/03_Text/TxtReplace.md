# TXTReplace

This command will search inside a given file and replace all text that matches the value of OldString and replace it with NewString. 

## Syntax

```pebakery
TXTReplace,<FileName>,<OldString>,<NewString>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| OldString | String to be replaced in the file. `OldString` is **not** Case Sensitive.
| NewString | String that will replace `OldString`

## Remarks

Caution should be exercised when using this command, as **any** instance of `OldString` will be replaced, even if it is part of another word.

If `FileName` does not exist the operation will fail.

PEBakery will optimize multiple `TXTReplace` in a row to single command.

## Examples

### Example 1

Assume we have the following file:

```pebakery
// C:\myFile.txt
Hello World!
HelloWorld!
hello World!
HelloGoodbyehellogoodbyeHelloGoodbye
```

In this example all instances of the word `Hello` inside C:\myFile.txt will be replaced with the word `Goodbye`

```pebakery
TXTReplace,C:\myFile.txt,"Hello",Goodbye
```

Result:

```pebakery
// C:\myFile.txt
Goodbye World!
GoodbyeWorld!
Goodbye World!
GoodbyeGoodbyeGoodbyegoodbyeGoodbyeGoodbye
```
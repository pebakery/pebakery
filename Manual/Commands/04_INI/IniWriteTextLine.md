# IniWriteTextLine

This command will add a line of text to given section inside the file you specify.

## Syntax

```pebakery
IniWriteTextLine,<Filename>,<Section>,<String>[,APPEND]
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| Section | The Section where the string will be written. |
| String | The string to write. |

### Flags

| Flag | Description |
| --- | --- |
| Append | *(Optional)* If specified this will cause the string to be inserted at the end of the section. |

## Remarks
Unless the `APPEND` flag is specified the `String` will be inserted at the beginning of the section.

## Example

Lets assume we have the following .ini file:
```pebakery
// C:\myFile.ini
[mySection]
myKey=myValue
anotherKey=anotherValue
```

In the following example the string `Hello World!` will be written to the beginning of the section. 
Additionally the string `Goodbye World!` will be written to the end of the section.
```pebakery
IniWriteTextLine,C:\myFile.ini,mySection,"Hello World!"
IniWriteTextLine,C:\myFile.ini,mySection,"Goodbye World!",APPEND
```

The resulting .ini file:
```pebakery
// C:\myFile.ini
[mySection]
Hello World!
myKey=myValue
anotherKey=anotherValue
Goodbye World!
```

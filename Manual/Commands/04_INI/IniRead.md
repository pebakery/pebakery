# IniRead

Read the value of a key from a section inside a file that you specify.

## Syntax

```pebakery
IniRead,<FileName>,<Section>,<Key>,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file to read. |
| Section | The section containing the value to be read. |
| Key | The value to be read. |
| DestVar | The value will be saved to this variable. |

## Remarks


## Example

Let's assume we have the following .ini file:

```pebakery
// C:\myFile.ini
[mySection]
myKey=myValue
anotherKey=anotherValue
```

In the following example the value of the key `myKey` will be stored inside `%myVariable%`.
```pebakery
IniRead,C:\myFile.ini,mySection,myKey,%myVariable%
```
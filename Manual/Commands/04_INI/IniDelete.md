# IniDelete

This command will delete the key from a section inside a file that you specify.

## Syntax

```pebakery
IniDelete,<FileName>,<Section>,<Key>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| Section | The Section containing the value to be removed. |
| Key | The value to be removed.|

## Remarks

## Example

Lets assume we have the following .ini file:

```pebakery
// C:\myFile.ini
[mySection]
myKey=myValue
anotherKey=anotherValue
```

In the following example the key `myKey` and it's value will be removed.

```pebakery
IniDelete,C:\myFile.ini,mySection,myKey
```
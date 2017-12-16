# IniDeleteSection

This command will delete a given section inside the file you specify.

## Syntax

```pebakery
IniDeleteSection,<Filename>,<Section> 
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| Section | The Section containing the value to be removed. |

## Remarks
All keys and values inside this section are completely removed.

## Example

In the following example the section `mySection` will be removed along with any keys and values it contains. 
```pebakery
IniDeleteSection,C:\myFile.ini,mySection 
```
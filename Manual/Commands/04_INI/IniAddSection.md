# IniAddSection

This command will add a new empty section inside the file you specify.

## Syntax

```pebakery
IniAddSection,<Filename>,<Section> 
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| Section | The Section containing the value to be removed. |

## Remarks
If the section already exists no action will be taken.

## Example

In the following example the section `mySection` will be created inside C:\myFile.ini. 
```pebakery
IniAddSection,C:\myFile.ini,mySection 
```
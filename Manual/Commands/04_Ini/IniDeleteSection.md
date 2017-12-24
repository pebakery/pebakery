# IniDeleteSection

This command will delete a given section, along with all keys and values it contains from the file you specify.

## Syntax

```pebakery
IniDeleteSection,<Filename>,<Section>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| Section | The Section to be removed. |

## Remarks

PEBakery will optimize multiple `IniDeleteSection` in a row to single command.

## Example

In the following example the section `mySection` will be removed along with any keys and values it contains.

```pebakery
IniDeleteSection,C:\myFile.ini,mySection
```

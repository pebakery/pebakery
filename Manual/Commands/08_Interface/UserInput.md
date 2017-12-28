# UserInput

**Alias**: `Retrieve,Dir` `Retrieve,File`

Prompts the user to select a file or directory path.

## Syntax

```pebakery
UserInput,<BrowserType>,<InitPath>,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| BrowserType | One of the following types: |
|| FilePath - Display a File Browser. |
|| DirPath - Display a Directory Browser. |
| InitPath | The starting path and filter for the browse dialog. |
| %DestVar% | The variable that will contain the full path of the selected file or directory. |

## Remarks

You may force the user to pick a specific file type (ie a .txt file) by specifying the file extension in `InitPath`. Multiple file type filters are not supported.

If the user clicks the cancel button on the browser dialog the operation will fail.

## Related

## Examples

### Example 1

```pebakery
[main]
Title=UserInput Example
Description=Show usage of the UserInput command.
Level=5
Version=1
Author=Homes32

[variables]

[process]
// File Browser allowing the user to pick any file type.
UserInput,FilePath,C:\.*,%var%
Message,"You selected: %var%"

// File Browser only allowing the user to pick .txt files.
UserInput,FilePath,C:\*.txt,%var%
Message,"You selected: %var%"

// Directory Browser
UserInput,DirPath,C:\,%var%
Message,"You selected: %var%"
```
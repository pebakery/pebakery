# Encode

Embeds files inside a plugin.

PEBakery allows you to embed files into your plugins for easy distribuition. Encoded files are compressed with zlib2 (if they are not already compressed), encoded to base64 and stored as text inside the plugin.

## Syntax

```pebakery
Encode,<PluginFile>,<DirName>,<FilePath>
```

### Arguments

| Argument | Description |
| --- | --- |
| PluginFile | The full path to the plugin. **Hint:** Use `%PluginFile%` to reference the current plugin. |
| DirName | The folder the encoded file will be placed in. If `DirName` does not exist it will be created. If the files to be encoded already exist in the plugin's `DestDir` they will be overwritten.|
| FilePath | The full path of the file(s) to be encoded. Wildcards are accepted. |

## Remarks

**Warning:** Make sure `DirName` does not have the same name as other sections in your plugin or they will be corrupted.

Plugin files do not support nested directories. If you require a complex directory structure consider compressing the files with 7zip and encoding the resulting archive.

## Related

[ExtractAllFiles](./ExtractAllFiles.md), [ExtractFile](./ExtractFile.md)

## Examples

### Example 1

Simple directory structure inside a plugin.

```pebakery
root/
|--- Folder/
     |--- myApp.exe
     |--- myApp.ini
     |--- moreFiles.7z
|--- Reg/
     |--- mySettings.reg
|--- Help/
     |---readme.txt
|--- src/
     |---mySrc.au3
```

Encode the file `readme.txt` into the `Help` directory inside the plugin.

```pebakery
Encode,%ScriptFile%,Folder,c:\readme.txt
```

### Example 2

Example plugin showing the file `readme.txt` embedded in the plugin file.

```pebakery
[main]
Title=Encode Example
Description=How to work with encoded files in PEBakery
Level=5
Version=1
Author=Homes32

[Variables]

[process]
Echo,"This plugin contains an encoded file!"

[Info1]
// The 'EncodedFolders' section contains the names of any directories we have encoded into our plugin.

[EncodedFolders]
Help

[Info2]
// Each directory has a section named after it which contains the file name, file size, and encoded size

[Help]
readme.txt=42822,5776

[Info3]
// Each file is compressed and encoded to base64 then the resulting lines
// are split into 4090 characters each and written to a section called
// [EncodedFile-<dirName>-<fileName>]

[EncodedFile-Help-readme.txt]
lines=1
0=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
1=ZWpIkquZNTleRv6Jh8SlJ0rl7E1VdKmfCqoKhOHOyEZqm8yyTVRuZSiwEYoZTkKPMqMV7+KknaUwgv21h7/sWVXftvJD2UUyspYMd7V7q7nY/0rQ22JW8sfatGX8oiieq1coSkM3ilmbu0c+3ls24pxYwx+M2KpHdhVaWMgGRxGW8PXj2S43iRU3Fp4Ph2Xd7IJyVRq9ipuZe8glwhGRiAg/hnHj5AZYnCXCYPPd4hlYwbSptE92YlqYjQefxH9MZYAQIJVS1Io4VtUWnoPE+i+1FKghlN2g59MCrnSvHp+tj+UZDsnfMkZxrR+HckxP9tz1CcSqIvrHr8dSRU+dwDP7MRnCdmOhEfvYgChejYrk5+vhCOneMwiXTPU1ebX1dJuRAXrie2QnxIC4YiWhqpe0qslGESRTXRA4NW9IN7GV8F70p0cCto3DLhB292rSw28YJtfpTE10Wfg+xROxUJg8Wedjzflnz+23r8onZ1mCSsBJ2Q6C6Is3sQUSEhvfkui80nURM0+tBxEr8SVHXvx8V4MEER7qa2UiPpy+FvOA4H4kdnSNwRKlWj19m/+bCVNNt2r+TSyMCri18SLNEu+66eiOEW+supYMjZB5K8oiMRXuhkZOQNMyudz7D8n9v1142zPew2Ml3kh6Q1gHBiT6UQAovKSQZmI6F7sdyYdCCaJXGmz4qQhamokhMQsPv0LUOxr21yJbp0SrpCctQeZqayB2ciGu1PtJC8qjqLXOzoVYKYa3Quc5eiuAqjzOqVGAXRNNXRCAl0L1KzKLhhb+9N/wycUL6FSJQ7SmEAhDDFKGYZI9G7KN9o82X9M7LOTvMpWez4fGojpi9RvdtoZVonOr0AZ7AyX6Xaaen5uUTevRwsUJVe1KFVOpZBCLOU4HZqDZckAxSM4L/a/xPGFYH12zlywpJnCdUQIyA2DNZpiqUeb2mWq8q/DQ7WcBQDGIMkKYuuxr+FQqDtYyT5kKYhhaJtWjkUPo+0cfTUK88i/SJDJ0zRYV7CxCBJNlKBLmgs3x5r38z5Xcr5Fx/mtjvYMb/S2yBJ7vuJ4HMQkyTOVR7lEo3RC6XszDwYJ/DaJLnvzytS1pPYgqljtlgLH5PUo/GheP6bkhyLQoSVMdalswxmSVBevpibZDmudOiClERsbHwBSZeMRF7aEDxLorcotfEKCemSBAXRkqS7xHsXkr3RrULTh0hkheTSPsdZkmRxgbbU4Zouk+6WkFAWZ4GFa/wmJNnyVcU6UNNTwsSylW8+V8EzYcVcOgJgiqRchmyowwrRWe9CfiNsTTHxMnejmEYk4sr/w3EbyVazjyB5vwuzDhtPUZnlJ3Ty4oJSbiSprflgEkwaXAit5ljINyYp8i0dpZBsNAhPXVVJEr67k8hKAfSJUhBKn4TjOPKKyU/FXWXn6iol8zV9EoGjr87C6P1I8iWfkiSwBBJZz+Al/vGCl7+ZpN29krjMeJ3Q1AbJrSY/R1IrWmsppQgwQ6dqkOjkxZ1J6huD6iRlFv8wggcgqRd/yJrRV5YjHomkZiZ1pVRAjqhrhOTeFt8gKWPIkgMlu0Q9Z59Y/IUDsf4H8t5gunic4ypKTUzJTdUrqShhGAUjD7gth9CLBLDLf/N6wMzABgDTigfPg838wgEAAAACAAAAJgAAAKIQAAAAAAAAAQAAAAAAAAAAAAAA

```
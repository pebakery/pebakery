# Hash

Cacluates hash from file.

## Syntax

```pebakery
Hash,<HashType>,<FilePath>,<%DestVar%>
```

- Arguments

| Argument | Description |
| --- | --- |
| HashType | Hash type to calcuate.<br>Should be one of `MD5`, `SHA1`, `SHA256`, `SHA384`, `SHA512`.
| FilePath | Path of file to calculate hash. |
| DestVar | Variable name to save hash digest. |

## Example

```pebakery
// %Dest% is set to "15750221bbffa36c055d656c46899460".
Hash,MD5,%WindowsDir%\System32\notepad.exe,%Dest%
```

```pebakery
// %Dest% is set to "e9f2fbe8e1bc49d107df36ef09f6d0aeb8901516980d3fe08ee73ab7b4a2325f".
Hash,SHA256,%WindowsDir%\System32\notepad.exe,%Dest%
```

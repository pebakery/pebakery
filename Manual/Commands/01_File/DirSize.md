# DirSize

**Alias**: `Retrieve,DirSize`

Get size of the directory.

## Syntax

```pebakery
DirSize,<DirPath>,<DestVar>
```

- Arguments

| Argument | Description |
| --- | --- |
| DirPath | Path of the directory. |
| DestVar | Variable name to save directory size. |

## Remarks

If `DirPath` contains files cannot be accessed with `administrator` privilege, PEBakery ignores them.

## Example

```pebakery
// %Dest% is set to "8364393557".
DirSize,%WindowsDir%\System32,%Dest%
```

# RegHiveUnload

Unloads an external registry hive from your local registry.

## Syntax

```pebakery
RegHiveLoad,<KeyPath>
```

### Arguments

| Argument | Description |
| --- | --- |
| KeyPath | The registry key where the hive is loaded. |

## Remarks

A registry hive mounted with `RegHiveLoad` should be unmounted with `RegHiveUnload`.

## Related

[RegHiveLoad](./RegHiveLoad.md)

## Examples

#### Example 1

```pebakery
RegHiveUnload,Tmp_Software
```
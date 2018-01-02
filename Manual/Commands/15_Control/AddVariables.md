# AddVariables

Reads variables from another section, plugin, or file into the current plugins run-time environment.

## Syntax

```pebakery
AddVariables,<FileName>,<Section>,[GLOBAL]
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file to read. **Hint:** Use `%PluginFile%` to reference the current plugin.|
| Section | The section containing the variables to be added. |

### Flags

| Flag | Description |
| --- | --- |
| GLOBAL | **(Optional)** The variables are added to the global scope and available to the entire project for the duration of the build. If the variables exist they will be overwritten! |

## Remarks

When a plugin runs, it automatically adds variables defined in the `[Variables]` section of the plugin. The `AddVariables` command gives you the flexibility to add additional variables stored in other plugins and files and can be used in *script.project* to load variables and macros for the entire project.

## Related
[Set](./Set.md), [SetMacro](./SetMacro.md)

## Examples

### Example 1

```pebakery
// Add variables from another section in the current plugin
AddVariables,%PluginFile%,AlternativeVariables
```

### Example 2

The following code if placed in *script.project* will load macros defined in a plugin "library" for use by the entire project.

```pebakery
// Add macros from another plugin so they are available to all plugins
AddVariables,%BaseDir%\Build\Library.script,ApiVar,GLOBAL
```

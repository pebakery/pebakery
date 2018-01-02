# Halt

Forces the current build to terminate.

## Syntax

```pebakery
Halt,<Message>
```

### Arguments

| Argument | Description |
| --- | --- |
| Message | Text that will be displayed and written to the log citing the reason for the `Halt`. |

## Remarks

If the project has defined the `System,OnBuildExit` directive the command will be processed before the build halts. This allows the developer to clean up any temporary files and/or mount points, and unload any registry hives.

## Related

[System,OnBuildExit](../12_System/OnBuildExit.md)

## Examples

### Example 1

Abort the current build.

```pebakery
Halt,"Incompatible Source. Please select a valid source and try building again."
```
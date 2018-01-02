# Wait

Pause execution for a specific amount of time.

## Syntax

```pebakery
Wait,<Seconds>
```

### Arguments

| Argument | Description |
| --- | --- |
| Seconds | Number of seconds to pause before processing continues. |

## Remarks

The `Wait` command can be used to give other programs time to start or finish execution before another operation takes place. Another common use is to pause processing for a few seconds to allow the user to react to an event, such as as an `Echo` message.

## Related

## Examples

### Example 1

```pebakery
// Wait 5 seconds then continue.
Echo,"Waiting 5 seconds..."
Wait,5
```
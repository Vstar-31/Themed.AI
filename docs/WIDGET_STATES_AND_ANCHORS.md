# Widget states and monitor anchoring (schema v6)

Both features are optional. A widget without them behaves exactly as before.

## Meter states

`MeterDefinition.States` lists visual states for one meter. Each state has a trigger, optional
overrides and a transition.

| Trigger | Active when |
| --- | --- |
| `Hover` | the pointer is over the meter |
| `Pressed` | a pointer button is held on the meter |
| `Active` | the named measure is non-zero; with no measure, the widget window is focused |
| `Threshold` | the named measure compares true against `Value` (`Above`, `AtOrAbove`, `Below`, `AtOrBelow`) |

Overrides are relative to the meter's own settings: `Opacity` and `Scale` multiply, `Rotation`
(degrees) and `OffsetX`/`OffsetY` add. `ColorHex` (`#RRGGBB` or `#AARRGGBB`) recolors text meters;
other meter kinds ignore it. Unset overrides leave that property alone.

When several states are active, higher `Priority` wins each property it sets; equal priorities go
to the later list entry. The winning state's `TransitionMs` (0 to 10000) and `Easing`
(`Linear`, `EaseIn`, `EaseOut`, `EaseInOut`) control the blend. Leaving a state blends back at
that state's speed. Retargeting mid-transition continues from the current value. A meter can have
at most 16 states.

```json
{
  "Id": "cpu-text",
  "MeasureName": "Cpu",
  "States": [
    { "Id": "hover", "Trigger": "Hover", "Scale": 1.1, "TransitionMs": 120 },
    { "Id": "press", "Trigger": "Pressed", "Priority": 10, "Scale": 0.95, "TransitionMs": 40 },
    { "Id": "hot", "Trigger": "Threshold", "MeasureName": "Cpu", "Comparison": "Above",
      "Value": 85, "ColorHex": "#FF4444", "TransitionMs": 300 }
  ]
}
```

Threshold and measure-based `Active` states read the measure values from the last widget update, so
they change at the widget's refresh interval. Hover and pressed react immediately.

## Anchoring

`SkinDefinition.Anchor` pins the widget to a monitor edge, corner or center.

```json
"Anchor": {
  "Edge": "BottomRight",
  "Monitor": "Current",
  "UseWorkArea": true,
  "MarginX": 24,
  "MarginY": 24,
  "Responsive": [
    { "Id": "small", "MaxWorkAreaWidth": 1366, "Scale": 0.8 },
    { "Id": "tiny", "MaxWorkAreaWidth": 1024, "Hidden": true }
  ]
}
```

- `Edge`: `None`, `TopLeft`, `Top`, `TopRight`, `Left`, `Center`, `Right`, `BottomLeft`, `Bottom`,
  `BottomRight`. `None` keeps the saved X/Y (rules can still set an edge).
- `Monitor`: `Current` follows the monitor the widget is on; `Primary` always uses the primary one.
- `UseWorkArea`: true excludes the taskbar.
- Margins are DIPs inward from the pinned edge. On a centered axis the margin is an offset from
  center (positive is right/down). Anchored positions are clamped to stay on the monitor.

Responsive rules apply when the monitor area is at most `MaxWorkAreaWidth` and `MaxWorkAreaHeight`
DIPs (0 means no limit on that axis). The most specific match wins: smallest width limit, then
smallest height limit, then list order. A rule can override `Edge`, `MarginX`, `MarginY`, set a
uniform `Scale` (0 to 4, applied to the window and its contents) or set `Hidden`. At most 16 rules.

Dragging an anchored widget updates its margins (or the active rule's margin for that axis), so it
stays pinned at the new distance. **Reset Position** restores 16 DIP margins, including explicit responsive margin overrides. Monitor changes,
resolution changes and DPI changes are picked up on the next widget update.

## Limits

- Editor UI for states and anchors is not built yet; author them in `skin.json` or a package.
- With `Monitor: Primary`, layout uses the widget window's current DPI to convert units.
  Primary and secondary monitors with different scaling are untested.
- Mixed DPI, multiple monitors and monitor hot-plug need verification on a real Windows desktop.

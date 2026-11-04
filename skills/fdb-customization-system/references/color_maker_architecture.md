# Color Maker ("رنگ‌ساز") Architecture

The Color Maker allows users to build custom colors via RGB sliders or direct HEX text input and apply them to any UI target in Fdb Studio.

## 1. Bidirectional Synchronization Architecture

The Color Maker maintains two representations of the color:
- `ColorMakerHex` (string, e.g. `#10B981`)
- `ColorMakerR`, `ColorMakerG`, `ColorMakerB` (doubles from 0 to 255 for Avalonia Slider binding)

### Reentrancy Guard
To prevent infinite recursion between slider changes and text changes, `_isSyncingColorMaker` is checked:

```csharp
partial void OnColorMakerRChanged(double value) => UpdateHexFromRgb();
partial void OnColorMakerGChanged(double value) => UpdateHexFromRgb();
partial void OnColorMakerBChanged(double value) => UpdateHexFromRgb();

private bool _isSyncingColorMaker;

private void UpdateHexFromRgb()
{
    if (_isSyncingColorMaker) return;
    _isSyncingColorMaker = true;
    try
    {
        byte r = (byte)Math.Clamp(ColorMakerR, 0, 255);
        byte g = (byte)Math.Clamp(ColorMakerG, 0, 255);
        byte b = (byte)Math.Clamp(ColorMakerB, 0, 255);
        ColorMakerHex = $"#{r:X2}{g:X2}{b:X2}";
    }
    finally
    {
        _isSyncingColorMaker = false;
    }
}
```

## 2. Dynamic Target Routing

When the user clicks "Apply Color to Target", `ApplyColorMakerToTargetCommand` routes the hex color to the selected element:

```csharp
switch (target)
{
    case "Buttons: Accent / Action":
        ButtonAccentColor = hex;
        break;
    case "Buttons: Background":
        ButtonBackgroundColor = hex;
        break;
    case "Buttons: Text":
        ButtonTextColor = hex;
        break;
    case "Buttons: Border":
        ButtonBorderColor = hex;
        break;
    case "Borders: Global Panels & Dividers":
        BorderColor = hex;
        break;
    case "Borders: Splitters & Separators":
        SplitterColor = hex;
        break;
    case "Sidebar: Main Background":
        SidebarBackground = hex;
        break;
    case "Sidebar: Header & Toolbar":
        SidebarHeaderBackground = hex;
        break;
    case "Sidebar: Item Hover / Selection":
        SidebarItemHoverBackground = hex;
        break;
    case "Sidebar: Text":
        SidebarTextColor = hex;
        break;
    case "Sidebar: Border":
        SidebarBorderColor = hex;
        break;
    case "Text Editor: Background":
        EditorBackground = hex;
        break;
    case "Text Editor: Text":
        EditorForeground = hex;
        break;
    case "Table: Background":
        TableBackground = hex;
        LivePreviewTableCustomization();
        break;
    // ...
}
LivePreviewPanelCustomization();
```

## 3. High-Contrast Luminance Formula

To ensure text remains readable inside the preview swatch regardless of color brightness, perceived luminance is computed using the ITU-R BT.601 standard:

$$\text{YIQ} = \frac{299 \cdot R + 587 \cdot G + 114 \cdot B}{1000}$$

When $\text{YIQ} \ge 128$, the color is light (use dark text). Otherwise, use white text.

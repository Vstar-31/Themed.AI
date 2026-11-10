using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public sealed partial class SkinEditorViewModel
{
    public bool CanChangeMeterStyle => SelectedMeter?.Kind is
        MeterKind.String or MeterKind.Bar or MeterKind.Ring or MeterKind.Graph;

    public void ChangeMeterStyle(MeterKind kind)
    {
        if (!CanChangeMeterStyle || SelectedMeter is not { } current || current.Kind == kind ||
            kind is not (MeterKind.String or MeterKind.Bar or MeterKind.Ring or MeterKind.Graph)) return;
        using var edit = BeginEdit();
        var definition = current.Definition;
        definition.Kind = kind;
        if (kind == MeterKind.Ring)
            definition.Width = definition.Height = Math.Max(40, Math.Min(definition.Width, definition.Height));
        else if (kind == MeterKind.Graph)
            definition.Height = Math.Max(60, definition.Height);
        else if (kind == MeterKind.String)
            definition.Height = Math.Max(Math.Ceiling(definition.FontSize * 1.45), definition.Height);
        var replacement = new MeterEditorItem(definition, MarkDirty);
        Meters[Meters.IndexOf(current)] = replacement;
        SelectedMeter = replacement;
        MarkDirty();
        StatusMessage = "Style changed. Your data source and actions are unchanged. Save when you are ready.";
        DraftRestored?.Invoke();
    }
}

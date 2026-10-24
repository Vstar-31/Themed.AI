using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public abstract class BehaviorEditorItem : ViewModelBase
{
    private readonly Action _changed;
    protected BehaviorEditorItem(Action changed) => _changed = changed;
    protected void Changed() { OnPropertyChanged(string.Empty); _changed(); }
    protected void Change<T>(T before, T after, Action<T> write)
    {
        if (EqualityComparer<T>.Default.Equals(before, after)) return;
        write(after);
        Changed();
    }
    protected void Number(double before, double value, Action<double> write, [CallerMemberName] string? name = null)
    {
        if (!double.IsFinite(value)) { OnPropertyChanged(name); return; }
        Change(before, value, write);
    }
    protected void Optional(double? before, double value, Action<double?> write, [CallerMemberName] string? name = null)
    {
        if (double.IsInfinity(value)) { OnPropertyChanged(name); return; }
        Change(before, double.IsNaN(value) ? null : (double?)value, write);
    }
}

public sealed class MeterStateEditorItem : BehaviorEditorItem
{
    public MeterStateDefinition Definition { get; }
    public MeterStateEditorItem(MeterStateDefinition definition, Action changed) : base(changed) => Definition = definition;
    public static Array Triggers { get; } = Enum.GetValues<MeterStateTrigger>();
    public static Array Comparisons { get; } = Enum.GetValues<StateComparison>();
    public static Array Easings { get; } = Enum.GetValues<TransitionEasing>();
    public string Summary => $"{(string.IsNullOrWhiteSpace(Name) ? Trigger.ToString() : Name)} · {Trigger} · priority {Definition.Priority}";
    public bool UsesMeasure => Trigger is MeterStateTrigger.Active or MeterStateTrigger.Threshold;
    public bool IsThreshold => Trigger == MeterStateTrigger.Threshold;
    public string Name { get => Definition.Name; set => Change(Definition.Name, value, v => Definition.Name = v); }
    public MeterStateTrigger Trigger { get => Definition.Trigger; set => Change(Definition.Trigger, value, v => Definition.Trigger = v); }
    public StateComparison Comparison { get => Definition.Comparison; set => Change(Definition.Comparison, value, v => Definition.Comparison = v); }
    public TransitionEasing Easing { get => Definition.Easing; set => Change(Definition.Easing, value, v => Definition.Easing = v); }
    public string MeasureName { get => Definition.MeasureName ?? ""; set => Change(Definition.MeasureName, string.IsNullOrWhiteSpace(value) ? null : value.Trim(), v => Definition.MeasureName = v); }
    public double Value { get => Definition.Value; set => Number(Definition.Value, value, v => Definition.Value = v); }
    public double Priority { get => Definition.Priority; set { if (double.IsFinite(value)) Change(Definition.Priority, (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue), v => Definition.Priority = v); else OnPropertyChanged(); } }
    public double TransitionMs { get => Definition.TransitionMs; set { if (double.IsFinite(value)) Change(Definition.TransitionMs, (int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue), v => Definition.TransitionMs = v); else OnPropertyChanged(); } }
    public double Opacity { get => Definition.Opacity ?? double.NaN; set => Optional(Definition.Opacity, value, v => Definition.Opacity = v); }
    public double Scale { get => Definition.Scale ?? double.NaN; set => Optional(Definition.Scale, value, v => Definition.Scale = v); }
    public double Rotation { get => Definition.Rotation ?? double.NaN; set => Optional(Definition.Rotation, value, v => Definition.Rotation = v); }
    public double OffsetX { get => Definition.OffsetX ?? double.NaN; set => Optional(Definition.OffsetX, value, v => Definition.OffsetX = v); }
    public double OffsetY { get => Definition.OffsetY ?? double.NaN; set => Optional(Definition.OffsetY, value, v => Definition.OffsetY = v); }
    public string ColorHex { get => Definition.ColorHex ?? ""; set => Change(Definition.ColorHex, string.IsNullOrWhiteSpace(value) ? null : value.Trim(), v => Definition.ColorHex = v); }
}

public sealed class ResponsiveRuleEditorItem : BehaviorEditorItem
{
    public WidgetResponsiveRule Definition { get; }
    public ResponsiveRuleEditorItem(WidgetResponsiveRule definition, Action changed) : base(changed) => Definition = definition;
    public string Summary => $"≤ {(MaxWorkAreaWidth == 0 ? "any" : MaxWorkAreaWidth.ToString("0"))} × {(MaxWorkAreaHeight == 0 ? "any" : MaxWorkAreaHeight.ToString("0"))} DIP{(Hidden ? " · hidden" : "")}";
    public void Refresh() => OnPropertyChanged(string.Empty);
    public bool OverrideEdge { get => Definition.Edge is not null; set { if (value != OverrideEdge) Change(Definition.Edge, value ? WidgetAnchorEdge.TopRight : (WidgetAnchorEdge?)null, v => Definition.Edge = v); } }
    public WidgetAnchorEdge Edge { get => Definition.Edge ?? WidgetAnchorEdge.TopRight; set { if (OverrideEdge) Change(Definition.Edge, (WidgetAnchorEdge?)value, v => Definition.Edge = v); } }
    public double MaxWorkAreaWidth { get => Definition.MaxWorkAreaWidth; set => Number(Definition.MaxWorkAreaWidth, value, v => Definition.MaxWorkAreaWidth = v); }
    public double MaxWorkAreaHeight { get => Definition.MaxWorkAreaHeight; set => Number(Definition.MaxWorkAreaHeight, value, v => Definition.MaxWorkAreaHeight = v); }
    public double MarginX { get => Definition.MarginX ?? double.NaN; set => Optional(Definition.MarginX, value, v => Definition.MarginX = v); }
    public double MarginY { get => Definition.MarginY ?? double.NaN; set => Optional(Definition.MarginY, value, v => Definition.MarginY = v); }
    public double Scale { get => Definition.Scale ?? double.NaN; set => Optional(Definition.Scale, value, v => Definition.Scale = v); }
    public bool Hidden { get => Definition.Hidden; set => Change(Definition.Hidden, value, v => Definition.Hidden = v); }
}

public sealed class AnchorEditorItem : BehaviorEditorItem
{
    public WidgetAnchorDefinition Definition { get; }
    public static Array Edges { get; } = Enum.GetValues<WidgetAnchorEdge>();
    public static Array Monitors { get; } = Enum.GetValues<WidgetMonitorTarget>();
    public ObservableCollection<ResponsiveRuleEditorItem> Rules { get; } = new();
    private ResponsiveRuleEditorItem? _selectedRule;
    public ResponsiveRuleEditorItem? SelectedRule
    {
        get => _selectedRule;
        set { if (SetProperty(ref _selectedRule, value)) OnPropertyChanged(nameof(HasSelectedRule)); }
    }
    public bool HasSelectedRule => SelectedRule is not null;
    public bool CanAddRule => Rules.Count < WidgetDefinitionValidator.MaxResponsiveRules;
    public string RuleCount => $"{Rules.Count} / {WidgetDefinitionValidator.MaxResponsiveRules} rules";
    public AnchorEditorItem(WidgetAnchorDefinition definition, Action changed) : base(changed)
    {
        Definition = definition;
        Definition.Responsive ??= new();
        foreach (var rule in Definition.Responsive) Rules.Add(new ResponsiveRuleEditorItem(rule, Changed));
        SelectedRule = Rules.FirstOrDefault();
    }
    public void AddRule()
    {
        if (!CanAddRule) return;
        var rule = new WidgetResponsiveRule { MaxWorkAreaWidth = 1280, Scale = 0.8 };
        Definition.Responsive.Add(rule);
        var item = new ResponsiveRuleEditorItem(rule, Changed);
        Rules.Add(item);
        SelectedRule = item;
        Changed();
    }
    public void RemoveRule()
    {
        if (SelectedRule is null) return;
        int index = Rules.IndexOf(SelectedRule);
        Definition.Responsive.Remove(SelectedRule.Definition);
        Rules.Remove(SelectedRule);
        SelectedRule = Rules.Count == 0 ? null : Rules[Math.Min(index, Rules.Count - 1)];
        Changed();
    }
    public void ResetMargins()
    {
        Definition.ResetMargins();
        foreach (var rule in Rules) rule.Refresh();
        Changed();
    }
    public WidgetAnchorEdge Edge { get => Definition.Edge; set => Change(Definition.Edge, value, v => Definition.Edge = v); }
    public WidgetMonitorTarget Monitor { get => Definition.Monitor; set => Change(Definition.Monitor, value, v => Definition.Monitor = v); }
    public bool UseWorkArea { get => Definition.UseWorkArea; set => Change(Definition.UseWorkArea, value, v => Definition.UseWorkArea = v); }
    public double MarginX { get => Definition.MarginX; set => Number(Definition.MarginX, value, v => Definition.MarginX = v); }
    public double MarginY { get => Definition.MarginY; set => Number(Definition.MarginY, value, v => Definition.MarginY = v); }
}

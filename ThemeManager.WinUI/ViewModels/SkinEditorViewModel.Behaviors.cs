using System.Collections.ObjectModel;
using ThemeManager.Core.Skins;

namespace ThemeManager.WinUI.ViewModels;

public sealed partial class SkinEditorViewModel
{
    public ObservableCollection<MeterStateEditorItem> States { get; } = new();
    public AnchorEditorItem? AnchorEditor { get; private set; }
    public event Action? BehaviorPreviewChanged;
    public event Action? BehaviorStructureChanged;

    private MeterStateEditorItem? _selectedState;
    public MeterStateEditorItem? SelectedState
    {
        get => _selectedState;
        set { if (SetProperty(ref _selectedState, value)) { OnPropertyChanged(nameof(HasSelectedState)); OnPropertyChanged(nameof(CanMoveStateEarlier)); OnPropertyChanged(nameof(CanMoveStateLater)); } }
    }
    public string[] AvailableMeasureNames => Measures.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public bool CanMoveStateEarlier => SelectedState is not null && States.IndexOf(SelectedState) > 0;
    public bool CanMoveStateLater => SelectedState is not null && States.IndexOf(SelectedState) < States.Count - 1;
    public bool HasSelectedState => SelectedState is not null;
    public bool CanAddState => SelectedMeter is not null && States.Count < WidgetDefinitionValidator.MaxStatesPerMeter;
    public string StateCount => $"{States.Count} / {WidgetDefinitionValidator.MaxStatesPerMeter} states";

    private bool _anchorEnabled;
    public bool AnchorEnabled
    {
        get => _anchorEnabled;
        set
        {
            if (!SetProperty(ref _anchorEnabled, value) || _working is null || AnchorEditor is null) return;
            _working.Anchor = value ? AnchorEditor.Definition : null;
            BehaviorChanged();
        }
    }

    // Preview controls never change the saved widget or its real measures.
    private bool _previewInteractions, _previewActive;
    private double _previewMeasureValue = 42, _previewAreaWidth = 1920, _previewAreaHeight = 1080;
    public bool PreviewInteractions { get => _previewInteractions; set { if (SetProperty(ref _previewInteractions, value)) BehaviorPreviewChanged?.Invoke(); } }
    public bool PreviewActive { get => _previewActive; set { if (SetProperty(ref _previewActive, value)) BehaviorPreviewChanged?.Invoke(); } }
    public double PreviewMeasureValue { get => _previewMeasureValue; set { if (double.IsFinite(value) && SetProperty(ref _previewMeasureValue, value)) BehaviorPreviewChanged?.Invoke(); } }
    public double PreviewAreaWidth { get => _previewAreaWidth; set { if (double.IsFinite(value) && SetProperty(ref _previewAreaWidth, Math.Clamp(value, 320, 16000))) BehaviorPreviewChanged?.Invoke(); } }
    public double PreviewAreaHeight { get => _previewAreaHeight; set { if (double.IsFinite(value) && SetProperty(ref _previewAreaHeight, Math.Clamp(value, 240, 16000))) BehaviorPreviewChanged?.Invoke(); } }
    public double? PreviewMeasure(string name) => Measures.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) ? PreviewMeasureValue : null;
    public WidgetLayout ResolveAnchorPreview() => WidgetAnchorResolver.Resolve(
        AnchorEnabled && AnchorEditor is not null ? AnchorEditor.Definition : new WidgetAnchorDefinition(),
        new WidgetRect(0, 0, PreviewAreaWidth, PreviewAreaHeight), Width, Height, _working?.X ?? 0, _working?.Y ?? 0);

    public string BehaviorValidation => _working is null ? "" : string.Join(" ",
        WidgetDefinitionValidator.Validate(_working).Where(i => i.IsError).Take(3).Select(i => i.Message));

    private void LoadBehaviors()
    {
        _anchorEnabled = _working.Anchor is not null;
        AnchorEditor = new AnchorEditorItem(_working.Anchor ?? new WidgetAnchorDefinition { Edge = WidgetAnchorEdge.TopRight }, BehaviorChanged);
        LoadSelectedStates();
        OnPropertyChanged(nameof(AnchorEditor));
        OnPropertyChanged(nameof(AnchorEnabled));
        OnPropertyChanged(nameof(BehaviorValidation));
    }

    private void LoadSelectedStates()
    {
        States.Clear();
        if (SelectedMeter is { } meter)
        {
            meter.Definition.States ??= new();
            foreach (var state in meter.Definition.States)
                States.Add(new MeterStateEditorItem(state, BehaviorChanged));
        }
        SelectedState = States.FirstOrDefault();
        OnPropertyChanged(nameof(CanAddState));
        OnPropertyChanged(nameof(StateCount));
    }

    private void BehaviorChanged()
    {
        MarkDirty();
    }

    public void AddState(bool duplicate = false)
    {
        if (!CanAddState || SelectedMeter is null) return;
        var state = duplicate && SelectedState is not null ? SelectedState.Definition.Clone()
            : new MeterStateDefinition { Name = "Hover", Trigger = MeterStateTrigger.Hover, Scale = 1.08 };
        state.Id = Guid.NewGuid().ToString();
        if (duplicate) state.Name = $"{state.Name} copy";
        SelectedMeter.Definition.States.Add(state);
        var item = new MeterStateEditorItem(state, BehaviorChanged);
        States.Add(item);
        SelectedState = item;
        StateStructureChanged();
    }

    public void RemoveState()
    {
        if (SelectedMeter is null || SelectedState is null) return;
        int index = States.IndexOf(SelectedState);
        SelectedMeter.Definition.States.Remove(SelectedState.Definition);
        States.Remove(SelectedState);
        SelectedState = States.Count == 0 ? null : States[Math.Min(index, States.Count - 1)];
        StateStructureChanged();
    }

    public void MoveState(int delta)
    {
        if (SelectedState is null || SelectedMeter is null) return;
        int from = States.IndexOf(SelectedState), to = from + delta;
        if (to < 0 || to >= States.Count) return;
        var selected = SelectedState;
        var definitions = SelectedMeter.Definition.States;
        definitions.RemoveAt(from);
        definitions.Insert(to, selected.Definition);
        States.Move(from, to);
        SelectedState = selected;
        StateStructureChanged();
    }

    private void StateStructureChanged()
    {
        OnPropertyChanged(nameof(CanAddState));
        OnPropertyChanged(nameof(StateCount));
        OnPropertyChanged(nameof(CanMoveStateEarlier));
        OnPropertyChanged(nameof(CanMoveStateLater));
        BehaviorChanged();
        BehaviorStructureChanged?.Invoke();
    }
}

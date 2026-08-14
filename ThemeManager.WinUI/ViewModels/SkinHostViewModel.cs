using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Drives a single floating widget: owns the concrete <see cref="IMeasure"/> instances for its
/// skin and the <see cref="MeterViewModelBase"/> collection its <see cref="Views.SkinHostWindow"/>
/// renders. Each widget's measures are private to it — two skins both reading "CPU" each get
/// their own <see cref="CpuMeasure"/>, which is deliberately simple even though it means two
/// GetSystemTimes calls instead of one if you ever run two CPU widgets side by side.
/// </summary>
public sealed class SkinHostViewModel : ViewModelBase
{
    public SkinDefinition Definition { get; }
    public ObservableCollection<MeterViewModelBase> Meters { get; } = new();
    public bool IsClosed { get; set; }

    private readonly Dictionary<string, IMeasure> _measuresByName = new();
    private readonly ILogger? _logger;

    public SkinHostViewModel(SkinDefinition definition, ILogger? logger = null)
    {
        Definition = definition;
        _logger = logger;

        foreach (var measureDef in definition.Measures)
            _measuresByName[measureDef.Name] = MeasureFactory.Create(measureDef, logger);

        foreach (var meterDef in definition.Meters)
        {
            MeterViewModelBase vm = meterDef.Kind switch
            {
                MeterKind.Bar => new BarMeterViewModel(meterDef),
                MeterKind.Graph => new GraphMeterViewModel(meterDef),
                _ => new StringMeterViewModel(meterDef),
            };
            Meters.Add(vm);
        }
    }

    /// <summary>Refreshes every measure this skin owns. Safe to call on a background thread.</summary>
    public void RefreshMeasures()
    {
        foreach (var measure in _measuresByName.Values)
            measure.Refresh();
    }

    /// <summary>Updates every meter from the new values. Must be called on the UI thread.</summary>
    public void UpdateMeters()
    {
        foreach (var meter in Meters)
            meter.Tick(_measuresByName);
    }
}

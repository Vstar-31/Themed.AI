using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Drives a single floating widget: owns the concrete <see cref="IMeasure"/> instances for its
/// skin and the <see cref="MeterViewModelBase"/> collection its <see cref="Views.SkinHostWindow"/>
/// renders.
/// </summary>
public sealed class SkinHostViewModel : ViewModelBase
{
    public SkinDefinition Definition { get; }
    public ObservableCollection<MeterViewModelBase> Meters { get; } = new();
    public bool IsClosed { get; set; }
    public System.Collections.Generic.IEnumerable<IMeasure> Measures => _measuresByName.Values;

    private readonly Dictionary<string, IMeasure> _measuresByName = new();
    private readonly ILogger? _logger;

    public SkinHostViewModel(SkinDefinition definition, ILogger? logger = null, IActiveThemeProvider? activeThemeProvider = null)
    {
        Definition = definition;
        _logger = logger;

        foreach (var measureDef in definition.Measures)
            _measuresByName[measureDef.Name] = MeasureFactory.Create(measureDef, logger, activeThemeProvider);

        foreach (var meterDef in definition.Meters)
        {
            MeterViewModelBase vm = meterDef.Kind switch
            {
                MeterKind.Bar => new BarMeterViewModel(meterDef),
                MeterKind.Graph => new GraphMeterViewModel(meterDef),
                MeterKind.Icon => new IconMeterViewModel(meterDef),
                MeterKind.Ring => new RingMeterViewModel(meterDef),
                MeterKind.WebEmbed => new WebEmbedMeterViewModel(meterDef),
                _ => new StringMeterViewModel(meterDef),
            };
            Meters.Add(vm);
        }
    }

    /// <summary>Refreshes every measure this skin owns. Safe to call on a background thread.</summary>
    public void RefreshMeasures()
    {
        if (IsClosed) return;
        foreach (var measure in _measuresByName.Values)
        {
            try
            {
                measure.Refresh();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skin \"{Skin}\": measure \"{Measure}\" ({MeasureType}) threw during Refresh()",
                    Definition.Name, measure.Name, measure.GetType().Name);
            }
        }
    }

    /// <summary>Updates every meter from the new values. Must be called on the UI thread.</summary>
    public void UpdateMeters()
    {
        if (IsClosed) return;
        foreach (var meter in Meters)
        {
            try
            {
                meter.Tick(_measuresByName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skin \"{Skin}\": meter \"{Meter}\" ({MeterType}) threw during Tick()",
                    Definition.Name, meter.LogLabel, meter.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Stops measure-owned background work before the native widget HWND is torn down. Measures
    /// such as VibeFinder use this hook to cancel network operations tied to the widget lifetime.
    /// </summary>
    public void DisposeMeasures()
    {
        if (IsClosed) return;
        IsClosed = true;
        foreach (var disposable in _measuresByName.Values.OfType<IDisposable>().Distinct())
        {
            try { disposable.Dispose(); }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Skin \"{Skin}\": measure \"{Measure}\" failed during disposal",
                    Definition.Name, disposable.GetType().Name);
            }
        }
    }
}

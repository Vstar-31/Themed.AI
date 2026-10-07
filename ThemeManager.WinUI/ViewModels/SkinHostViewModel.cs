using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Services;
using ThemeManager.Core.Skins;
using ThemeManager.Integration.Skins;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>Drives a single floating widget and owns its measures/meters.</summary>
public sealed class SkinHostViewModel : ViewModelBase
{
    public SkinDefinition Definition { get; }
    // Compatibility shim for older widget-host call sites that treated the host as a wrapper.
    public SkinHostViewModel ViewModel => this;
    public ObservableCollection<MeterViewModelBase> Meters { get; } = new();
    public bool IsClosed { get; set; }
    public System.Collections.Generic.IEnumerable<IMeasure> Measures => _measuresByName.Values;

    private readonly Dictionary<string, IMeasure> _measuresByName = new();
    private readonly List<IMeasure> _formulaMeasures = new();
    private readonly ILogger? _logger;
    private readonly IActiveThemeProvider? _activeThemeProvider;
    private readonly WidgetPluginRegistry? _pluginRegistry;
    private int _measuresDisposed;

    public SkinHostViewModel(
        SkinDefinition definition,
        ILogger? logger = null,
        IActiveThemeProvider? activeThemeProvider = null,
        WidgetPluginRegistry? pluginRegistry = null)
    {
        Definition = definition;
        _logger = logger;
        _activeThemeProvider = activeThemeProvider;
        _pluginRegistry = pluginRegistry;
        // Build base measures first so every Formula measure can resolve them regardless of JSON
        // ordering. Formula measures are then refreshed in their definition order, allowing simple
        // formula chains such as "Total = Cpu + Mem".        BuildMeasures();
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

    private void BuildMeasures()
    {
        foreach (var measureDef in Definition.Measures.Where(m => m.Type != MeasureType.Formula))
            _measuresByName[measureDef.Name] = MeasureFactory.Create(
                measureDef, _logger, _activeThemeProvider, ResolveMeasure, _pluginRegistry);

        foreach (var measureDef in Definition.Measures.Where(m => m.Type == MeasureType.Formula))
        {
            var formula = MeasureFactory.Create(
                measureDef, _logger, _activeThemeProvider, ResolveMeasure, _pluginRegistry);
            _measuresByName[measureDef.Name] = formula;
            _formulaMeasures.Add(formula);
        }
    }

    /// <summary>Rebuilds measure instances after a target/credential change without recreating the native widget window.</summary>
    public void ReloadMeasures()
    {
        if (IsClosed) return;
        foreach (var disposable in _measuresByName.Values.OfType<IDisposable>().Distinct())
        {
            try { disposable.Dispose(); }
            catch (Exception ex) { _logger?.LogDebug(ex, "Skin \"{Skin}\": measure disposal during reload failed", Definition.Name); }
        }

        _measuresByName.Clear();
        _formulaMeasures.Clear();
        Interlocked.Exchange(ref _measuresDisposed, 0);
        BuildMeasures();
    }

    private double? ResolveMeasure(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _measuresByName.TryGetValue(name, out var measure) ? measure.Value : null;
    }

    public void RefreshMeasures()
    {
        if (IsClosed) return;
        foreach (var measure in _measuresByName.Values.Where(m => !_formulaMeasures.Contains(m)))
        {
            try { measure.Refresh(); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skin \"{Skin}\": measure \"{Measure}\" ({MeasureType}) threw during Refresh()",
                    Definition.Name, measure.Name, measure.GetType().Name);
            }
        }

        foreach (var formula in _formulaMeasures)
        {
            try { formula.Refresh(); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skin \"{Skin}\": formula measure \"{Measure}\" threw during Refresh()",
                    Definition.Name, formula.Name);
            }
        }
    }

    public void UpdateMeters()
    {
        if (IsClosed) return;
        foreach (var meter in Meters)
        {
            try { meter.Tick(_measuresByName); }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skin \"{Skin}\": meter \"{Meter}\" ({MeterType}) threw during Tick()",
                    Definition.Name, meter.LogLabel, meter.GetType().Name);
            }
        }
    }

    /// <summary>Disposes all measure-owned resources. Safe to call after IsClosed was already set.</summary>
    public void DisposeMeasures()
    {
        IsClosed = true;
        if (Interlocked.Exchange(ref _measuresDisposed, 1) != 0) return;

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

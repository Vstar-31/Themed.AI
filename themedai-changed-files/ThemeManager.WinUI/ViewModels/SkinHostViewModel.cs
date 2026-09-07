using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using ThemeManager.Core.Services;
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
    public System.Collections.Generic.IEnumerable<IMeasure> Measures => _measuresByName.Values;

    private readonly Dictionary<string, IMeasure> _measuresByName = new();
    private readonly ILogger? _logger;

    /// <param name="activeThemeProvider">Passed straight through to <see cref="MeasureFactory.Create"/>
    /// for every measure this skin owns — only a VibeFinderAI measure targeting "$theme" actually
    /// uses it (see phases.md, Phase 6). <c>SkinManagerService</c> is the only current caller and
    /// passes <c>App.ThemeService</c>; left null here, VibeFinderAI "$theme" widgets fall back to
    /// "Config Err" but every other measure — and a VibeFinderAI widget with a literal typed
    /// phrase — is unaffected.</param>
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
    ///
    /// <remarks>
    /// Each measure is refreshed inside its own try/catch. Previously the whole foreach was
    /// unguarded, so one measure throwing (e.g. VibeFinderMeasure hitting the CurrentIndex race
    /// during a rapid song switch) aborted the loop and left every OTHER measure on this same
    /// widget frozen on its last value for the rest of the tick — a CPU widget going stale
    /// because the Vibe widget three rows down threw. SkinManagerService.TickAll's outer catch
    /// caught the aborted loop, but its log message didn't say which skin OR which measure, so
    /// this was invisible short of attaching a debugger. Now a single bad measure logs by name
    /// and the rest keep refreshing normally.
    /// </remarks>
    public void RefreshMeasures()
    {
        foreach (var measure in _measuresByName.Values)
        {
            try
            {
                measure.Refresh();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skin \"{Skin}\": measure \"{Measure}\" ({MeasureType}) threw during Refresh() — its value is left stale for this tick, other measures on this widget are unaffected",
                    Definition.Name, measure.Name, measure.GetType().Name);
            }
        }
    }

    /// <summary>Updates every meter from the new values. Must be called on the UI thread.</summary>
    /// <remarks>Same per-item isolation as <see cref="RefreshMeasures"/> and for the same
    /// reason — a meter's Tick() throwing (e.g. a binding expression hitting a null it didn't
    /// expect) shouldn't freeze every other meter on the widget.</remarks>
    public void UpdateMeters()
    {
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
}

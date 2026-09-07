using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using ThemeManager.Core.Skins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ThemeManager.Integration.Skins;

/// <summary>
/// Reads media info (Now Playing) from Windows using GlobalSystemMediaTransportControlsSessionManager.
/// </summary>
public sealed class MediaMeasure : IMeasure
{
    public string Name { get; }
    public double Value { get; private set; }
    public string Text { get; private set; } = "—";

    private readonly MeasureType _type;
    private readonly ILogger _logger;
    private static GlobalSystemMediaTransportControlsSessionManager? _manager;
    private static GlobalSystemMediaTransportControlsSession? _currentSession;
    private static GlobalSystemMediaTransportControlsSessionMediaProperties? _currentProperties;
    private static bool _initialized = false;
    private static readonly object _initLock = new();

    // Bumped every time the current session changes (track switch, app switch, session
    // lost). UpdateProperties() captures the generation it started with and discards its
    // result if a newer one has since started — otherwise two overlapping
    // TryGetMediaPropertiesAsync() calls (e.g. two rapid "next" clicks, each raising
    // MediaPropertiesChanged) can complete out of order and let the OLDER fetch overwrite
    // _currentProperties with stale title/artist data *after* the newer one already landed.
    // That out-of-order overwrite is what made this measure look "stuck" on a previous
    // track after a few quick song switches, with nothing in the old logs to show why.
    private static int _generation;

    // A static logger the static-context callbacks below (Manager_CurrentSessionChanged,
    // UpdateProperties' background continuation) can use — instance _logger is per-widget
    // and one of many by the time those fire, but the underlying session/generation state
    // is shared across every MediaMeasure instance, so logging from there needs a shared
    // sink too. Sourced from whichever MediaMeasure was constructed with a real one last;
    // still functions (just silently) if every instance so far used NullLogger.
    private static ILogger _sharedLogger = NullLogger.Instance;

    public MediaMeasure(string name, MeasureType type, ILogger? logger = null)
    {
        Name = name;
        _type = type;
        _logger = logger ?? NullLogger.Instance;
        if (logger is not null) _sharedLogger = logger;

        lock (_initLock)
        {
            if (!_initialized)
            {
                _initialized = true;
                _logger.LogDebug("MediaMeasure: first instance created (Name={MeasureName}, Type={MeasureType}) — starting session manager init", name, type);
                Task.Run(InitializeManagerAsync);
            }
        }
    }

    private async Task InitializeManagerAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_manager != null)
            {
                _manager.CurrentSessionChanged += Manager_CurrentSessionChanged;
                _sharedLogger.LogInformation("MediaMeasure: GlobalSystemMediaTransportControlsSessionManager acquired");
                UpdateCurrentSession();
            }
            else
            {
                _sharedLogger.LogWarning("MediaMeasure: RequestAsync returned a null session manager — Now Playing widgets will stay blank");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Media Transport Controls");
        }
    }

    private void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _sharedLogger.LogDebug("MediaMeasure: CurrentSessionChanged fired");
        UpdateCurrentSession();
    }

    private void UpdateCurrentSession()
    {
        if (_manager == null) return;

        Interlocked.Increment(ref _generation);

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        }

        _currentSession = _manager.GetCurrentSession();

        if (_currentSession != null)
        {
            string? appId = null;
            try { appId = _currentSession.SourceAppUserModelId; } catch { /* not critical, best-effort for the log line only */ }
            _sharedLogger.LogInformation("MediaMeasure: now tracking session for {AppId} (generation {Generation})", appId ?? "unknown app", _generation);

            _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            UpdateProperties();
        }
        else
        {
            _sharedLogger.LogDebug("MediaMeasure: no current session — nothing is registered as Now Playing");
            _currentProperties = null;
        }
    }

    private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        UpdateProperties();
    }

    private void UpdateProperties()
    {
        if (_currentSession == null) return;

        int startedGeneration = _generation;
        var session = _currentSession;

        Task.Run(async () =>
        {
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();

                // Discard if the session moved on while this fetch was in flight — see the
                // _generation field's doc comment. Without this check, a slow fetch for the
                // song someone just skipped PAST can land after the fetch for the song they
                // skipped TO, silently reverting the title/artist shown on the widget.
                if (startedGeneration != _generation)
                {
                    _sharedLogger.LogDebug(
                        "MediaMeasure: discarding stale TryGetMediaPropertiesAsync result (fetched for generation {Fetched}, current is {Current}) — {Title}/{Artist}",
                        startedGeneration, _generation, props?.Title, props?.Artist);
                    return;
                }

                _currentProperties = props;
                _sharedLogger.LogDebug("MediaMeasure: properties updated — {Title} / {Artist}", props?.Title, props?.Artist);
            }
            catch (Exception ex)
            {
                _sharedLogger.LogWarning(ex, "MediaMeasure: TryGetMediaPropertiesAsync failed (generation {Generation})", startedGeneration);
            }
        });
    }

    /// <summary>
    /// Attempts to send a transport control command to the current active media session.
    /// Expected commands: playpause, next, prev
    /// </summary>
    public static async Task TrySendCommandAsync(string command)
    {
        if (_currentSession == null)
        {
            _sharedLogger.LogDebug("MediaMeasure: TrySendCommandAsync({Command}) ignored — no active session", command);
            return;
        }

        try
        {
            bool accepted;
            switch (command.ToLowerInvariant())
            {
                case "playpause":
                    accepted = await _currentSession.TryTogglePlayPauseAsync();
                    break;
                case "next":
                    accepted = await _currentSession.TrySkipNextAsync();
                    break;
                case "prev":
                case "previous":
                    accepted = await _currentSession.TrySkipPreviousAsync();
                    break;
                default:
                    _sharedLogger.LogWarning("MediaMeasure: TrySendCommandAsync received unrecognized command {Command}", command);
                    return;
            }

            if (accepted)
                _sharedLogger.LogDebug("MediaMeasure: {Command} accepted by the current session", command);
            else
                _sharedLogger.LogWarning("MediaMeasure: {Command} was rejected by the current session (the app behind Now Playing declined it)", command);
        }
        catch (Exception ex)
        {
            // Previously a silent `catch {}` — this is exactly the kind of failure the "can't
            // analyse anything from the logs" complaint was about: a click that visibly does
            // nothing on the widget, with zero trace of why.
            _sharedLogger.LogWarning(ex, "MediaMeasure: TrySendCommandAsync({Command}) threw", command);
        }
    }

    public void Refresh()
    {
        if (_currentSession == null || _currentProperties == null)
        {
            Text = "—";
            Value = 0;
            return;
        }

        var previousText = Text;

        switch (_type)
        {
            case MeasureType.MediaTitle:
                Text = string.IsNullOrWhiteSpace(_currentProperties.Title) ? "—" : _currentProperties.Title;
                break;
            case MeasureType.MediaArtist:
                Text = string.IsNullOrWhiteSpace(_currentProperties.Artist) ? "—" : _currentProperties.Artist;
                break;
            case MeasureType.MediaState:
                var info = _currentSession.GetPlaybackInfo();
                if (info != null)
                {
                    Text = info.PlaybackStatus.ToString();
                    Value = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ? 1 : 0;
                }
                else
                {
                    Text = "—";
                    Value = 0;
                }
                break;
        }

        // Debug, not Trace: this measure only recomputes from already-cached properties, so a
        // transition here is meaningful (title changed, or play/pause flipped) rather than
        // routine per-tick noise — worth keeping even at the app's default minimum log level.
        if (!string.Equals(previousText, Text, StringComparison.Ordinal))
            _logger.LogDebug("MediaMeasure[{MeasureName}] ({Type}): {Previous} -> {Current}", Name, _type, previousText, Text);
    }
}

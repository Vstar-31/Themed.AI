using System;
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

    public MediaMeasure(string name, MeasureType type, ILogger? logger = null)
    {
        Name = name;
        _type = type;
        _logger = logger ?? NullLogger.Instance;
        
        lock (_initLock)
        {
            if (!_initialized)
            {
                _initialized = true;
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
                UpdateCurrentSession();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Media Transport Controls");
        }
    }

    private void Manager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession();
    }

    private void UpdateCurrentSession()
    {
        if (_manager == null) return;

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
        }

        _currentSession = _manager.GetCurrentSession();

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            UpdateProperties();
        }
        else
        {
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
        
        Task.Run(async () => 
        {
            try
            {
                _currentProperties = await _currentSession.TryGetMediaPropertiesAsync();
            }
            catch { }
        });
    }

    /// <summary>
    /// Attempts to send a transport control command to the current active media session.
    /// Expected commands: playpause, next, prev
    /// </summary>
    public static async Task TrySendCommandAsync(string command)
    {
        if (_currentSession == null) return;

        try
        {
            switch (command.ToLowerInvariant())
            {
                case "playpause":
                    await _currentSession.TryTogglePlayPauseAsync();
                    break;
                case "next":
                    await _currentSession.TrySkipNextAsync();
                    break;
                case "prev":
                case "previous":
                    await _currentSession.TrySkipPreviousAsync();
                    break;
            }
        }
        catch
        {
            // Ignore command failures
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
    }
}

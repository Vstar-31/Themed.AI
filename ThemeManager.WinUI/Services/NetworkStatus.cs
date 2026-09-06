namespace ThemeManager.WinUI.Services;

/// <summary>
/// Thin wrapper around <c>Windows.Networking.Connectivity.NetworkInformation</c> — used by
/// MainWindow.EnsureVibeFinderPrewarm to skip a doomed login attempt while offline, and to know
/// when to retry once a connection comes back. This WinRT API works fine from this unpackaged
/// Win32/WinUI3 app (same category as the <c>Windows.Graphics.SizeInt32</c> type MainWindow
/// already uses) — no package identity or capability declaration needed for an internet-status
/// query from a desktop process.
/// </summary>
public static class NetworkStatus
{
    /// <summary>True only for actual internet access — a LAN with no WAN uplink (or a captive
    /// portal) reports a lower connectivity level and is treated as offline here, since a login
    /// POST to an external host would fail either way.</summary>
    public static bool IsInternetAvailable()
    {
        try
        {
            var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            return profile?.GetNetworkConnectivityLevel()
                == Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess;
        }
        catch
        {
            // A handful of machines/drivers throw here instead of just returning null — treat
            // that as "assume online" so a query failure never blocks a login attempt outright;
            // the actual fetch inside the WebView2 remains the real source of truth either way.
            return true;
        }
    }
}

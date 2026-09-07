using ThemeManager.Core.Models;

namespace ThemeManager.Core.Services;

/// <summary>
/// Converts a live vibe into deterministic desktop-atmosphere parameters.
/// The adapter is intentionally UI-agnostic so WinUI, widgets and future desktop
/// compositor surfaces can all consume the same smooth signal.
/// </summary>
public static class DesktopVibeAdapter
{
    public sealed record Atmosphere(
        string Mood,
        double Energy,
        double Warmth,
        double Glow,
        double Motion,
        double AudioReactivity,
        double TransitionSeconds);

    public static Atmosphere From(VibeSignal signal, double transitionSeconds = 0.9)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return From(signal.Mood, signal.Energy, signal.Warmth, transitionSeconds);
    }

    public static Atmosphere From(string? mood, double energy, double warmth, double transitionSeconds = 0.9)
    {
        var e = Math.Clamp(energy, 0, 1);
        var w = Math.Clamp(warmth, 0, 1);
        return new Atmosphere(
            string.IsNullOrWhiteSpace(mood) ? "Neutral" : mood.Trim(),
            e,
            w,
            Glow: Math.Clamp(0.18 + e * 0.72, 0, 1),
            Motion: Math.Clamp(0.08 + e * 0.62, 0, 1),
            AudioReactivity: Math.Clamp(0.15 + e * 0.8, 0, 1),
            TransitionSeconds: Math.Clamp(transitionSeconds, 0.15, 8));
    }

    public static Atmosphere Blend(Atmosphere current, Atmosphere target, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        return new Atmosphere(
            t < 0.5 ? current.Mood : target.Mood,
            Lerp(current.Energy, target.Energy, t),
            Lerp(current.Warmth, target.Warmth, t),
            Lerp(current.Glow, target.Glow, t),
            Lerp(current.Motion, target.Motion, t),
            Lerp(current.AudioReactivity, target.AudioReactivity, t),
            Lerp(current.TransitionSeconds, target.TransitionSeconds, t));
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}

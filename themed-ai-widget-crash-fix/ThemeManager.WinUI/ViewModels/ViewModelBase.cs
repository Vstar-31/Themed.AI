using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;

namespace ThemeManager.WinUI.ViewModels;

/// <summary>
/// Lightweight MVVM base. Marshals PropertyChanged notifications to the UI thread
/// via the DispatcherQueue when called from a background task.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    private readonly DispatcherQueue _dispatcher =
        DispatcherQueue.GetForCurrentThread();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        if (_dispatcher.HasThreadAccess)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        else
            _dispatcher.TryEnqueue(() =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));
    }
}

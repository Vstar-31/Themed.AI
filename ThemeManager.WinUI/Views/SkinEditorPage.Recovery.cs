using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ThemeManager.Core.Services;

namespace ThemeManager.WinUI.Views;

public sealed partial class SkinEditorPage
{
    private readonly WidgetDraftRepository _draftRepository = new();
    private readonly SemaphoreSlim _checkpointGate = new(1, 1);
    private Guid _checkpointId = Guid.NewGuid();
    private DispatcherQueueTimer? _checkpointTimer;
    private bool _discardCheckpoint;
    private string? _lastCheckpoint;

    private void StartCheckpoints()
    {
        _checkpointTimer ??= DispatcherQueue.CreateTimer();
        _checkpointTimer.Interval = TimeSpan.FromSeconds(2);
        _checkpointTimer.Tick -= CheckpointTick;
        _checkpointTimer.Tick += CheckpointTick;
        _checkpointTimer.Start();
    }

    private async void CheckpointTick(DispatcherQueueTimer sender, object args)
    {
        if (_checkpointGate.CurrentCount == 0 || _saving || _dragging) return;
        await CheckpointAsync();
    }

    private async Task<bool> CheckpointAsync()
    {
        await _checkpointGate.WaitAsync();
        try
        {
            if (_discardCheckpoint) return true;
            if (!ViewModel.Dirty)
            {
                await _draftRepository.DeleteAsync(_checkpointId);
                _lastCheckpoint = null;
                ViewModel.RecoveryStatus = "All edits saved to the widget.";
                return true;
            }
            var draft = ViewModel.CaptureRecoveryDraft();
            var snapshot = System.Text.Json.JsonSerializer.Serialize(draft, new System.Text.Json.JsonSerializerOptions
            { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
            if (snapshot == _lastCheckpoint) return true;
            ViewModel.RecoveryStatus = "Saving recovery checkpoint…";
            await _draftRepository.SaveAsync(_checkpointId, draft);
            _lastCheckpoint = snapshot;
            ViewModel.RecoveryStatus = "Recovery checkpoint saved locally. Choose Save to update the widget.";
            return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
        {
            ViewModel.RecoveryStatus = "Recovery checkpoint failed. Check storage access and choose Retry checkpoint. Your edits are still open.";
            return false;
        }
        finally { _checkpointGate.Release(); }
    }

    private async Task<bool> DiscardCheckpointAsync()
    {
        await _checkpointGate.WaitAsync();
        try
        {
            await _draftRepository.DeleteAsync(_checkpointId);
            _discardCheckpoint = true;
            _checkpointTimer?.Stop();
            return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            ViewModel.RecoveryStatus = "Couldn't remove the recovery checkpoint. Check storage access and try again.";
            return false;
        }
        finally { _checkpointGate.Release(); }
    }

    private async void RetryCheckpoint_Click(object sender, RoutedEventArgs e) => await CheckpointAsync();
    private void SnapSelection_Click(object sender, RoutedEventArgs e)
    {
        FinishDrag();
        ViewModel.SnapSelectedMeters(MetersList.SelectedItems.OfType<ThemeManager.WinUI.ViewModels.MeterEditorItem>());
    }
}

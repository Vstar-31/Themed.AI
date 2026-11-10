// This harness runs the actual editor ViewModel sources. Only the UI dispatcher and
// persistence boundary are doubles. It cannot verify WinUI bindings, focus, or rendering.
namespace Microsoft.UI.Dispatching
{
    public sealed class DispatcherQueue
    {
        public static DispatcherQueue GetForCurrentThread() => new();
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action action) { action(); return true; }
    }
}
namespace ThemeManager.WinUI.Services
{
    public sealed class SkinManagerService
    {
        public ThemeManager.Core.Skins.SkinDefinition? Saved { get; private set; }
        public Exception? SaveError { get; set; }
        public bool CreatedNew { get; private set; }
        public TaskCompletionSource? SaveGate { get; set; }
        public async Task SaveSkinDraftAsync(ThemeManager.Core.Skins.SkinDefinition definition, bool createNew = false)
        {
            if (SaveError is not null) throw SaveError;
            Saved = definition.Clone();
            CreatedNew = createNew;
            if (SaveGate is not null) await SaveGate.Task;
        }
        public Task DeleteSkinAsync(ThemeManager.Core.Skins.SkinDefinition definition) => Task.CompletedTask;
    }
}

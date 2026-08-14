namespace ThemeManager.Core.Personalization;

public enum FeedbackType
{
    ExplicitLike,
    ExplicitDislike,
    ImplicitApplied,     // e.g. user applied the generated theme
    ImplicitDismissed,   // e.g. user discarded the generated theme without applying
    ImplicitEdited       // e.g. user generated it, then manually changed the colors/size
}

public class FeedbackAction
{
    public string ItemId { get; set; } = string.Empty; // Skin Id or Theme Id
    public bool IsWidget { get; set; }
    public FeedbackType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // The context that led to this item being generated, to tie feedback back to inputs
    public GenerationContext? Context { get; set; } 
}

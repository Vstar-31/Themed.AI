using ThemeManager.Core.Models;
using ThemeManager.Core.Skins;

namespace ThemeManager.Core.Personalization;

/// <summary>
/// The main facade for the UI layer to interact with the personalized generation pipeline.
/// </summary>
public class PersonalizationOrchestrator
{
    private readonly UserProfileManager _profileManager;
    private readonly CandidateEngine _candidateEngine;
    private readonly HeuristicRankingEngine _rankingEngine;

    public PersonalizationOrchestrator(string profileDataPath)
    {
        _profileManager = new UserProfileManager(profileDataPath);
        _profileManager.Load();
        
        _candidateEngine = new CandidateEngine();
        _rankingEngine = new HeuristicRankingEngine();
    }

    public UserProfile GetCurrentProfile() => _profileManager.GetProfile();

    public void SubmitFeedback(FeedbackAction action)
    {
        _profileManager.RecordFeedback(action);
    }

    public void ExportProfile(string path) => _profileManager.ExportProfile(path);
    public void ImportProfile(string path) => _profileManager.ImportProfile(path);

    public ThemeCandidate GenerateBestTheme(GenerationContext context)
    {
        // 1. Generate Candidates
        var candidates = _candidateEngine.GenerateThemeCandidates(context, count: 5);

        // 2. Rank Candidates based on profile and context
        var ranked = _rankingEngine.RankThemes(candidates, _profileManager.GetProfile(), context);

        // 3. Return top result
        return ranked.First();
    }

    public WidgetCandidate GenerateBestWidget(GenerationContext context, double? screenWidth = null, double? screenHeight = null)
    {
        var candidates = _candidateEngine.GenerateWidgetCandidates(context, screenWidth, screenHeight, count: 3);
        var ranked = _rankingEngine.RankWidgets(candidates, _profileManager.GetProfile(), context);
        return ranked.First();
    }
}

using ThemeManager.Core.Models;
using ThemeManager.Core.NLP;
using ThemeManager.Core.Skins;
using ThemeManager.Core.Utilities;

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
        // 1. Generate Candidates — GenerateThemeCandidates implements exactly 3 distinct
        //    strategies (base / mood-injected / constraint-injected) right now; asking for more
        //    than that silently returned fewer than requested, so this matches what's actually
        //    delivered rather than promising a number the method can't back up. Bump this once
        //    a 4th/5th strategy is worth designing (e.g. profile-history-driven prompt variants).
        var candidates = _candidateEngine.GenerateThemeCandidates(context, count: 3);

        // 2. Rank Candidates based on profile and context
        var ranked = _rankingEngine.RankThemes(candidates, _profileManager.GetProfile(), context);

        // 3. Final quality gate at the public generation boundary. Candidate generators also
        // validate their outputs, but the orchestrator is the last shared path for every UI entry
        // point; keep the gate here so future candidate sources cannot accidentally ship a broken
        // palette. Refresh analysis swatches after any repair so the preview never disagrees.
        var selected = ranked.First();
        bool? preferredPolarity = context.Constraints.MustBeDarkTheme
            ? true
            : context.Constraints.MustBeLightTheme
                ? false
                : null;
        var quality = PaletteQualityValidator.ValidateAndRepair(selected.Theme, preferredPolarity);
        if (selected.Analysis is { } analysis)
        {
            var (hue, saturation, lightness) = ColorMath.HexToHsl(selected.Theme.AccentPrimary);
            selected.Analysis = analysis with
            {
                GeneratedName = selected.Theme.Name,
                ComputedHue = hue,
                ComputedSaturation = saturation,
                ComputedLightness = ColorMath.HexToHsl(selected.Theme.BackgroundBase).L,
                IsDark = ColorMath.LuminanceFromHex(selected.Theme.BackgroundBase) < 0.18f,
                Swatches = [selected.Theme.BackgroundBase, selected.Theme.BackgroundAlt, selected.Theme.Surface,
                    selected.Theme.AccentPrimary, selected.Theme.AccentStrong]
            };
        }

        if (!quality.PassesAA)
            throw new InvalidOperationException(
                $"The selected generated palette did not pass its final accessibility gate ({quality.PassingPairs}/{quality.TotalPairs} pairs).");

        return selected;
    }

    public WidgetCandidate GenerateBestWidget(GenerationContext context, double? screenWidth = null, double? screenHeight = null)
    {
        var candidates = _candidateEngine.GenerateWidgetCandidates(context, screenWidth, screenHeight, count: 3);
        var ranked = _rankingEngine.RankWidgets(candidates, _profileManager.GetProfile(), context);
        return ranked.First();
    }
}

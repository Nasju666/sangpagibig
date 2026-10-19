using UnityEngine;

public static class RuleCardsProgress
{
    private const string Prefix = "RuleCards.";
    public const string FinalStageId = "Repository";
    public const string EscapeStageId = "Escape";
    private const string BestTimeKey = Prefix + "BestFinalTime";
    private const string BestEscapeKey = Prefix + "BestEscapeTime";
    private static readonly string[] KnownCards = { "KineticLink", "InvertGravity", "SolidifyAir" };
    private static readonly string[] KnownStages = { "Stage01", "Stage02", "Stage03", FinalStageId, EscapeStageId };

    public static bool HasCard(string cardId) { return string.IsNullOrEmpty(cardId) || PlayerPrefs.GetInt(Prefix + "Card." + cardId, 0) == 1; }
    public static void GrantCard(string cardId) { if (!string.IsNullOrEmpty(cardId)) { PlayerPrefs.SetInt(Prefix + "Card." + cardId, 1); PlayerPrefs.Save(); } }
    public static bool IsStageComplete(string stageId) { return PlayerPrefs.GetInt(Prefix + "Stage." + stageId, 0) == 1; }
    public static void CompleteStage(string stageId) { if (!string.IsNullOrEmpty(stageId)) { PlayerPrefs.SetInt(Prefix + "Stage." + stageId, 1); PlayerPrefs.Save(); } }

    public static int TotalCards { get { return KnownCards.Length; } }

    public static int CardsCollected
    {
        get { int n = 0; foreach (string card in KnownCards) if (HasCard(card)) n++; return n; }
    }

    /// <summary>Any card or stage recorded; drives Continue vs. New Game on the title screen.</summary>
    public static bool HasAnyProgress
    {
        get
        {
            if (CardsCollected > 0) return true;
            foreach (string stage in KnownStages) if (IsStageComplete(stage)) return true;
            return false;
        }
    }

    /// <summary>True once the Final Repository's three nodes are aligned and its ending was left.</summary>
    public static bool IsRepositoryComplete { get { return IsStageComplete(FinalStageId); } }

    /// <summary>True once the player has escaped the Archive (Stage 04), the end of the game.</summary>
    public static bool IsGameComplete { get { return IsStageComplete(EscapeStageId); } }

    /// <summary>Fastest escape in seconds, or 0 when none is recorded.</summary>
    public static float BestEscapeTime { get { return PlayerPrefs.GetFloat(BestEscapeKey, 0f); } }

    /// <summary>Records an escape time; returns true when it is a new best.</summary>
    public static bool SubmitEscapeTime(float seconds)
    {
        if (seconds <= 0f) return false;
        float best = BestEscapeTime;
        if (best > 0f && seconds >= best) return false;
        PlayerPrefs.SetFloat(BestEscapeKey, seconds);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Fastest Final Repository clear in seconds, or 0 when none is recorded.</summary>
    public static float BestFinalTime { get { return PlayerPrefs.GetFloat(BestTimeKey, 0f); } }

    /// <summary>Records a clear time; returns true when it is a new best.</summary>
    public static bool SubmitFinalTime(float seconds)
    {
        if (seconds <= 0f) return false;
        float best = BestFinalTime;
        if (best > 0f && seconds >= best) return false;
        PlayerPrefs.SetFloat(BestTimeKey, seconds);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Erases every Rule Card, stage completion and the best time.</summary>
    public static void ResetAll()
    {
        foreach (string card in KnownCards) PlayerPrefs.DeleteKey(Prefix + "Card." + card);
        foreach (string stage in KnownStages) PlayerPrefs.DeleteKey(Prefix + "Stage." + stage);
        PlayerPrefs.DeleteKey(BestTimeKey);
        PlayerPrefs.DeleteKey(BestEscapeKey);
        PlayerPrefs.Save();
        RuleCardsRunTimer.ResetRun();
        RuleCardsStory.ResetAll();
    }
}

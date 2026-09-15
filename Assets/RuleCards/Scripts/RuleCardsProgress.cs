using UnityEngine;

public static class RuleCardsProgress
{
    private const string Prefix = "RuleCards.";
    public static bool HasCard(string cardId) { return string.IsNullOrEmpty(cardId) || PlayerPrefs.GetInt(Prefix + "Card." + cardId, 0) == 1; }
    public static void GrantCard(string cardId) { if (!string.IsNullOrEmpty(cardId)) { PlayerPrefs.SetInt(Prefix + "Card." + cardId, 1); PlayerPrefs.Save(); } }
    public static bool IsStageComplete(string stageId) { return PlayerPrefs.GetInt(Prefix + "Stage." + stageId, 0) == 1; }
    public static void CompleteStage(string stageId) { if (!string.IsNullOrEmpty(stageId)) { PlayerPrefs.SetInt(Prefix + "Stage." + stageId, 1); PlayerPrefs.Save(); } }
}
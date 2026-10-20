using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RuleCards > Trailer: queue shots for RuleCardsTrailer and enter Play mode.
/// Preview renders a still every half second (fast); Record writes 1080p60 MP4 clips.
/// Builds/Trailer/queue.txt (comma or newline separated shot ids) limits the "Queue" items to some shots.
/// </summary>
public static class RuleCardsTrailerMenu
{
    [MenuItem("RuleCards/Trailer/Preview All Shots", false, 200)]
    private static void PreviewAll() { Begin(RuleCardsTrailerShots.Ids, true); }

    [MenuItem("RuleCards/Trailer/Record All Shots", false, 201)]
    private static void RecordAll() { Begin(RuleCardsTrailerShots.Ids, false); }

    [MenuItem("RuleCards/Trailer/Preview Queue (queue.txt)", false, 220)]
    private static void PreviewQueue() { Begin(Queue(), true); }

    [MenuItem("RuleCards/Trailer/Record Queue (queue.txt)", false, 221)]
    private static void RecordQueue() { Begin(Queue(), false); }

    [MenuItem("RuleCards/Trailer/Open Output Folder", false, 240)]
    private static void Open() { Directory.CreateDirectory(RuleCardsTrailer.OutputFolder); EditorUtility.RevealInFinder(RuleCardsTrailer.OutputFolder); }

    private static string[] Queue()
    {
        string path = Path.Combine(Path.GetDirectoryName(RuleCardsTrailer.OutputFolder), "queue.txt");
        if (!File.Exists(path)) { Debug.LogWarning("[Trailer] No queue file at " + path); return new string[0]; }
        return File.ReadAllText(path).Split(new[] { ',', '\n', '\r', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
    }

    private static void Begin(string[] ids, bool preview)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Trailer] Stop Play mode first."); return; }
        ids = ids.Where(id => RuleCardsTrailerShots.Find(id) != null).ToArray();
        if (ids.Length == 0) { Debug.LogWarning("[Trailer] No shots to run."); return; }
        SessionState.SetString(RuleCardsTrailer.QueueKey, string.Join(",", ids));
        SessionState.SetBool(RuleCardsTrailer.PreviewKey, preview);
        Debug.Log("[Trailer] " + (preview ? "Previewing " : "Recording ") + string.Join(", ", ids));
        EditorApplication.isPlaying = true;
    }
}

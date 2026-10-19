using UnityEditor;
using UnityEngine;

/// <summary>Import settings for Resources/Audio: music streams, SFX are small mono clips decompressed on load.</summary>
public sealed class RuleCardsAudioImport : AssetPostprocessor
{
    private const string Root = "Assets/RuleCards/Art/Resources/Audio/";

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root)) return;
        var ai = (AudioImporter)assetImporter;
        AudioImporterSampleSettings s = ai.defaultSampleSettings;
        bool music = assetPath.StartsWith(Root + "Music/") || assetPath.StartsWith(Root + "Ambience/");
        bool longSfx = assetPath.Contains("_loop") || assetPath.Contains("stinger") || assetPath.Contains("archive_voice");
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = music ? .6f : .75f;
        s.loadType = music ? AudioClipLoadType.Streaming : longSfx ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        ai.defaultSampleSettings = s;
        ai.forceToMono = !music && !assetPath.Contains("stinger") && !assetPath.Contains("archive_voice");
        ai.loadInBackground = music;
    }
}

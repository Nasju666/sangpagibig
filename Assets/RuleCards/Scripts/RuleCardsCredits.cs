using UnityEngine;

/// <summary>
/// Credits panel opened from the title menu. Draw() returns false once the player closes it (Back / Esc).
/// Keep this list in step with Art/AUDIO_CREDITS.txt and the font OFL files.
/// </summary>
public static class RuleCardsCredits
{
    private struct Block
    {
        public string heading;
        public string[] lines;
        public Block(string heading, params string[] lines) { this.heading = heading; this.lines = lines; }
    }

    private static readonly Block[] Left =
    {
        new Block("Created by", "Nasju666", "@itch.io/profile/nasju666"),
        new Block("3D models", "Made in Blender"),
        new Block("Built with", "Unity"),
    };

    private static readonly Block[] Right =
    {
        new Block("Music", "Magic in the Air by Geoff Harvey",
                           "Short Chord Ambiance by kuzu420",
                           "Song from Atlantis by Samuel F. Johanns",
                           "via Pixabay"),
        new Block("Sound effects", "Impact Sounds by Kenney (CC0)",
                                   "arnav_geddada · litupsubway · pwlpl · soundreality",
                                   "soundshelfstudio · storegraphic · tanweraman · yodguard",
                                   "and the Freesound community, via Pixabay"),
        new Block("Typefaces", "Cinzel by Natanael Gama",
                               "Alegreya & Alegreya Sans SC by Huerta Tipográfica",
                               "SIL Open Font License 1.1"),
    };

    public static int LastClosedFrame = -10;

    public static bool Draw(float vw, float vh)
    {
        RuleCardsUI.ApplySkin();
        float pw = Mathf.Min(860f, vw - 24f), ph = Mathf.Min(620f, vh - 24f);
        Rect panel = new Rect((vw - pw) * .5f, (vh - ph) * .5f, pw, ph);
        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, .01f, .02f, .6f));
        RuleCardsUI.Panel(panel);
        GUI.Label(new Rect(panel.x, panel.y + 18f, pw, 38f), "Credits", RuleCardsUI.Title(30, RuleCardsUI.Cyan));
        RuleCardsUI.Divider(panel.center.x, panel.y + 62f, pw - 160f, RuleCardsUI.Gold);

        bool twoColumns = pw >= 640f;
        float pad = 48f, gap = 36f;
        float colW = twoColumns ? (pw - pad * 2f - gap) * .5f : pw - pad * 2f;
        float top = panel.y + 84f;
        float y = top;
        foreach (Block b in Left) y = DrawBlock(b, panel.x + pad, y, colW);
        if (twoColumns) y = top;
        foreach (Block b in Right) y = DrawBlock(b, panel.x + pad + (twoColumns ? colW + gap : 0f), y, colW);
        if (twoColumns) RepositoryGUI.Fill(new Rect(panel.x + pad + colW + gap * .5f, top + 6f, 1f, ph - 190f), new Color(1f, 1f, 1f, .06f));

        GUI.Label(new Rect(panel.x, panel.yMax - 100f, pw, 26f), "Thank you for reading the Archive back to life.",
            RuleCardsUI.ProseItalic(18, RuleCardsUI.GoldBright));

        float bw = Mathf.Min(220f, pw - 80f);
        bool close = GUI.Button(new Rect(panel.center.x - bw * .5f, panel.yMax - 62f, bw, 38f), "Back   (Esc)", RuleCardsUI.PrimaryButton);
        if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { close = true; Event.current.Use(); }
        if (close) LastClosedFrame = Time.frameCount;
        return !close;
    }

    private static float DrawBlock(Block b, float x, float y, float w)
    {
        GUI.Label(new Rect(x, y, w, 20f), b.heading.ToUpperInvariant(), RuleCardsUI.Label(13, RuleCardsUI.Gold));
        y += 22f;
        GUIStyle body = RuleCardsUI.Prose(17, RuleCardsUI.Text, TextAnchor.UpperLeft);
        GUIStyle note = RuleCardsUI.ProseItalic(15, RuleCardsUI.Muted, TextAnchor.UpperLeft);
        for (int i = 0; i < b.lines.Length; i++)
        {
            string line = b.lines[i];
            int bar = line.IndexOf('|');
            if (bar > 0)
            {
                // "Name|Role": name on the left, role in muted caps on the right of the same row.
                GUI.Label(new Rect(x, y, w, 22f), line.Substring(0, bar), body);
                GUI.Label(new Rect(x, y + 1f, w, 22f), line.Substring(bar + 1).ToUpperInvariant(),
                    RuleCardsUI.Label(12, RuleCardsUI.Muted, TextAnchor.UpperRight));
                y += 25f;
                continue;
            }
            if (line.StartsWith("@"))
            {
                // "@url": a clickable link (opens in the browser), drawn as a muted underlined-cyan note.
                string text = line.Substring(1);
                GUIStyle link = RuleCardsUI.ProseItalic(15, RuleCardsUI.Cyan, TextAnchor.UpperLeft);
                Vector2 size = link.CalcSize(new GUIContent(text));
                Rect r = new Rect(x, y, Mathf.Min(size.x, w), 22f);
                bool hover = r.Contains(Event.current.mousePosition);
                GUI.Label(r, text, hover ? RuleCardsUI.ProseItalic(15, RuleCardsUI.GoldBright, TextAnchor.UpperLeft) : link);
                RepositoryGUI.Fill(new Rect(r.x, r.y + 19f, r.width, 1f), hover ? RuleCardsUI.GoldBright : new Color(RuleCardsUI.Cyan.r, RuleCardsUI.Cyan.g, RuleCardsUI.Cyan.b, .5f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Application.OpenURL("https://" + text);
                y += 24f;
                continue;
            }
            bool isNote = line.StartsWith("via ") || line.StartsWith("SIL ") || line.StartsWith("and the ");
            GUIStyle s = isNote ? note : body;
            float h = s.CalcHeight(new GUIContent(line), w);
            GUI.Label(new Rect(x, y, w, h), line, s);
            y += h + 1f;
        }
        return y + 16f;
    }
}

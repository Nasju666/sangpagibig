using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The RuleCards look for every IMGUI screen: fonts, palette, a themed GUISkin (buttons, boxes,
/// sliders) and ornament helpers (framed panels, gem dividers, diamonds).
///
/// Type system (all SIL Open Font License, shipped in Art/Resources/Fonts):
///   Cinzel          — Roman inscription capitals: titles, headings, numbers/timers
///   Alegreya        — calligraphic book serif: story text, descriptions
///   Alegreya Sans SC— small caps: buttons, labels, HUD captions
/// </summary>
public static class RuleCardsUI
{
    // ---------------------------------------------------------------- palette
    public static readonly Color Ink = new Color(.035f, .055f, .09f, .96f);
    public static readonly Color InkSoft = new Color(.03f, .05f, .08f, .82f);
    public static readonly Color Gold = new Color(.86f, .71f, .43f);
    public static readonly Color GoldBright = new Color(1f, .86f, .55f);
    public static readonly Color Cyan = new Color(.45f, 1f, 1f);
    public static readonly Color Text = new Color(.89f, .92f, .98f);
    public static readonly Color Muted = new Color(.58f, .64f, .74f);
    public static readonly Color Danger = new Color(1f, .5f, .4f);

    // ---------------------------------------------------------------- fonts
    private static Font cinzel, cinzelBold, alegreya, alegreyaItalic, alegreyaBold, caps, capsBold;
    private static bool fontsLoaded;

    private static void LoadFonts()
    {
        if (fontsLoaded) return;
        fontsLoaded = true;
        cinzel = Resources.Load<Font>("Fonts/Cinzel-Regular");
        cinzelBold = Resources.Load<Font>("Fonts/Cinzel-Bold");
        alegreya = Resources.Load<Font>("Fonts/Alegreya-Regular");
        alegreyaItalic = Resources.Load<Font>("Fonts/Alegreya-Italic");
        alegreyaBold = Resources.Load<Font>("Fonts/Alegreya-Bold");
        caps = Resources.Load<Font>("Fonts/AlegreyaSansSC-Medium");
        capsBold = Resources.Load<Font>("Fonts/AlegreyaSansSC-Bold");
    }

    public static Font Display { get { LoadFonts(); return cinzelBold; } }
    public static Font DisplayRegular { get { LoadFonts(); return cinzel; } }
    public static Font Body { get { LoadFonts(); return alegreya; } }
    public static Font Italic { get { LoadFonts(); return alegreyaItalic; } }
    public static Font BodyBold { get { LoadFonts(); return alegreyaBold; } }
    public static Font Caps { get { LoadFonts(); return caps; } }
    public static Font CapsBold { get { LoadFonts(); return capsBold; } }

    /// <summary>Font choice for the legacy RepositoryGUI.Text(size, bold) calls used all over the game.</summary>
    public static Font ForLegacy(int size, bool bold)
    {
        if (bold) return size >= 18 ? Display : CapsBold;
        return size <= 13 ? Caps : Body;
    }

    // ---------------------------------------------------------------- text styles
    private static readonly Dictionary<long, GUIStyle> styles = new Dictionary<long, GUIStyle>();

    public static GUIStyle Style(Font font, int size, TextAnchor anchor, Color color, bool wrap = true)
    {
        long key = ((long)(font != null ? font.GetInstanceID() : 0) << 20) ^ (size << 6) ^ ((int)anchor << 1) ^ (wrap ? 1 : 0);
        if (!styles.TryGetValue(key, out GUIStyle s) || s == null)
        {
            s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, alignment = anchor, wordWrap = wrap, richText = true, clipping = TextClipping.Overflow };
            s.padding = new RectOffset(0, 0, 0, 0);
            s.margin = new RectOffset(0, 0, 0, 0);
            styles[key] = s;
        }
        s.normal.textColor = color;
        return s;
    }

    public static GUIStyle Title(int size, Color c, TextAnchor a = TextAnchor.MiddleCenter) { return Style(Display, size, a, c); }
    public static GUIStyle Heading(int size, Color c, TextAnchor a = TextAnchor.MiddleCenter) { return Style(DisplayRegular, size, a, c); }
    public static GUIStyle Prose(int size, Color c, TextAnchor a = TextAnchor.UpperCenter) { return Style(Body, size, a, c); }
    public static GUIStyle ProseItalic(int size, Color c, TextAnchor a = TextAnchor.MiddleCenter) { return Style(Italic, size, a, c); }
    public static GUIStyle Label(int size, Color c, TextAnchor a = TextAnchor.MiddleLeft) { return Style(Caps, size, a, c); }
    public static GUIStyle LabelBold(int size, Color c, TextAnchor a = TextAnchor.MiddleLeft) { return Style(CapsBold, size, a, c); }
    /// <summary>Lining numerals for timers and counts (Alegreya's figures are old-style).</summary>
    public static GUIStyle Number(int size, Color c, TextAnchor a = TextAnchor.MiddleCenter) { return Style(Display, size, a, c, false); }

    // ---------------------------------------------------------------- skin
    private static GUISkin skin;
    public static GUIStyle MenuItem { get; private set; }
    public static GUIStyle PrimaryButton { get; private set; }
    public static GUIStyle TabStyle { get; private set; }
    public static GUIStyle TabActive { get; private set; }

    /// <summary>Use the RuleCards skin for this OnGUI call (ResponsiveIMGUI.Begin calls this).</summary>
    public static void ApplySkin()
    {
        if (skin == null) BuildSkin();
        if (skin != null) GUI.skin = skin;
    }

    private static void BuildSkin()
    {
        LoadFonts();
        skin = Object.Instantiate(GUI.skin);
        skin.name = "RuleCardsSkin";
        skin.font = Body;

        var button = skin.button;
        button.font = CapsBold;
        button.fontSize = 15;
        button.alignment = TextAnchor.MiddleCenter;
        button.wordWrap = false;
        button.border = new RectOffset(5, 5, 5, 5);
        button.padding = new RectOffset(12, 12, 4, 6);
        SetStates(button,
            Tex(new Color(.055f, .085f, .13f, .94f), new Color(Gold.r, Gold.g, Gold.b, .45f)), Text,
            Tex(new Color(.08f, .15f, .2f, .97f), new Color(.45f, 1f, 1f, .85f)), Color.white,
            Tex(new Color(.1f, .26f, .3f, 1f), Cyan), Color.white);

        PrimaryButton = new GUIStyle(button);
        SetStates(PrimaryButton,
            Tex(new Color(.13f, .1f, .06f, .95f), new Color(Gold.r, Gold.g, Gold.b, .95f)), GoldBright,
            Tex(new Color(.2f, .15f, .08f, .98f), GoldBright), Color.white,
            Tex(new Color(.3f, .22f, .1f, 1f), GoldBright), Color.white);

        // Menu list item: no box until hovered, text left-aligned with an inset.
        MenuItem = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft, fontSize = 19, padding = new RectOffset(26, 12, 2, 4) };
        SetStates(MenuItem,
            Tex(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f)), Text,
            Tex(new Color(.86f, .71f, .43f, .1f), new Color(0f, 0f, 0f, 0f), Gold, true), GoldBright,
            Tex(new Color(.86f, .71f, .43f, .2f), new Color(0f, 0f, 0f, 0f), GoldBright, true), Color.white);

        TabStyle = new GUIStyle(button) { fontSize = 14 };
        SetStates(TabStyle,
            Tex(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f)), Muted,
            Tex(new Color(1f, 1f, 1f, .04f), new Color(0f, 0f, 0f, 0f)), Text,
            Tex(new Color(1f, 1f, 1f, .08f), new Color(0f, 0f, 0f, 0f)), Color.white);
        TabActive = new GUIStyle(TabStyle);
        SetStates(TabActive,
            UnderlineTex(Gold), GoldBright, UnderlineTex(GoldBright), Color.white, UnderlineTex(GoldBright), Color.white);

        var box = skin.box;
        box.font = Body;
        box.fontSize = 16;
        box.wordWrap = true;
        box.alignment = TextAnchor.MiddleCenter;
        box.border = new RectOffset(5, 5, 5, 5);
        box.padding = new RectOffset(16, 16, 8, 10);
        box.normal.background = Tex(new Color(.025f, .04f, .07f, .88f), new Color(.45f, 1f, 1f, .32f));
        box.normal.textColor = Text;

        skin.label.font = Body;
        skin.label.normal.textColor = Text;

        var track = skin.horizontalSlider;
        track.normal.background = Tex(new Color(1f, 1f, 1f, .1f), new Color(1f, 1f, 1f, .18f));
        track.border = new RectOffset(2, 2, 2, 2);
        track.fixedHeight = 6f;
        track.margin = new RectOffset(0, 0, 6, 6);
        var thumb = skin.horizontalSliderThumb;
        thumb.normal.background = Tex(GoldBright, new Color(.2f, .14f, .06f, 1f));
        thumb.hover.background = Tex(Color.white, Gold);
        thumb.active.background = Tex(Color.white, Cyan);
        thumb.fixedWidth = 12f;
        thumb.fixedHeight = 18f;
        thumb.border = new RectOffset(2, 2, 2, 2);
        thumb.margin = new RectOffset(0, 0, -6, 0);
    }

    private static void SetStates(GUIStyle s, Texture2D normal, Color normalText, Texture2D hover, Color hoverText, Texture2D active, Color activeText)
    {
        s.normal.background = normal; s.normal.textColor = normalText;
        s.hover.background = hover; s.hover.textColor = hoverText;
        s.active.background = active; s.active.textColor = activeText;
        s.focused.background = normal; s.focused.textColor = normalText;
        s.onNormal.background = active; s.onNormal.textColor = activeText;
        s.onHover.background = active; s.onHover.textColor = activeText;
    }

    /// <summary>Small 9-slice texture: fill, 1px border, faint top highlight; optional left accent bar.</summary>
    private static Texture2D Tex(Color fill, Color border, Color accent = default(Color), bool leftAccent = false)
    {
        const int n = 12;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color c = fill;
                if (y == n - 2 && fill.a > 0f) c = Color.Lerp(fill, Color.white, .07f);
                if (x == 0 || y == 0 || x == n - 1 || y == n - 1) c = border;
                if (leftAccent && x < 3) c = accent;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    private static Texture2D UnderlineTex(Color line)
    {
        const int n = 12;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                t.SetPixel(x, y, y < 2 ? line : new Color(line.r, line.g, line.b, .06f));
        t.Apply();
        return t;
    }

    // ---------------------------------------------------------------- ornaments

    public static void Fill(Rect r, Color c) { RepositoryGUI.Fill(r, c); }

    /// <summary>Ornate panel: ink fill, gold outer line, hairline inset, diamond studs on the corners.</summary>
    /// <summary>Quiet in-game HUD card: dark glass with a faint gold hairline. No accent bars.</summary>
    public static void HudCard(Rect r, float alpha = 1f)
    {
        Fill(r, new Color(.012f, .02f, .032f, .7f * alpha));
        RepositoryGUI.Frame(r, new Color(Gold.r, Gold.g, Gold.b, .22f * alpha), 1f);
    }

    public static void Panel(Rect r, float alpha = 1f, Color? accent = null)
    {
        Color a = accent ?? Gold;
        Fill(r, new Color(Ink.r, Ink.g, Ink.b, Ink.a * alpha));
        RepositoryGUI.Frame(r, new Color(a.r, a.g, a.b, .75f * alpha), 1.5f);
        RepositoryGUI.Frame(new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f), new Color(a.r, a.g, a.b, .22f * alpha), 1f);
        float d = 5f;
        Diamond(new Vector2(r.x, r.y), d, a, alpha);
        Diamond(new Vector2(r.xMax, r.y), d, a, alpha);
        Diamond(new Vector2(r.x, r.yMax), d, a, alpha);
        Diamond(new Vector2(r.xMax, r.yMax), d, a, alpha);
    }

    /// <summary>Thin rule with a gem in the middle (and fading ends).</summary>
    public static void Divider(float cx, float y, float width, Color c, float alpha = 1f)
    {
        float half = width * .5f;
        for (int i = 0; i < 6; i++)
        {
            float k = (i + 1) / 6f; // fade toward the ends
            float seg = half / 6f;
            Fill(new Rect(cx - 10f - seg * (i + 1), y, seg, 1f), new Color(c.r, c.g, c.b, c.a * alpha * (1.05f - k * .9f)));
            Fill(new Rect(cx + 10f + seg * i, y, seg, 1f), new Color(c.r, c.g, c.b, c.a * alpha * (1.05f - k * .9f)));
        }
        Diamond(new Vector2(cx, y + .5f), 4.5f, c, alpha);
        Diamond(new Vector2(cx - 14f, y + .5f), 2f, c, alpha * .8f);
        Diamond(new Vector2(cx + 14f, y + .5f), 2f, c, alpha * .8f);
    }

    private static Texture2D diamondTex;

    /// <summary>A 45°-turned square (gem). Drawn from a small anti-aliased texture rather than by rotating
    /// GUI.matrix, which lands in the wrong place under the scaled IMGUI matrix and inside groups.</summary>
    public static void Diamond(Vector2 center, float size, Color c, float alpha = 1f)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (diamondTex == null)
        {
            const int n = 64;
            diamondTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            diamondTex.wrapMode = TextureWrapMode.Clamp;
            diamondTex.filterMode = FilterMode.Bilinear;
            diamondTex.hideFlags = HideFlags.HideAndDontSave;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / n - .5f), dy = Mathf.Abs((y + .5f) / n - .5f);
                    float a = Mathf.Clamp01((.5f - (dx + dy)) * n * .7f);
                    diamondTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            diamondTex.Apply(false, true);
        }
        float d = size * 1.4142f; // diagonal of the turned square
        Color previous = GUI.color;
        GUI.color = new Color(c.r, c.g, c.b, c.a * alpha);
        GUI.DrawTexture(new Rect(center.x - d * .5f, center.y - d * .5f, d, d), diamondTex, ScaleMode.StretchToFill, true);
        GUI.color = previous;
    }

    private static Texture2D rampH, rampV;

    private static Texture2D Ramp(bool horizontal)
    {
        Texture2D t = horizontal ? rampH : rampV;
        if (t != null) return t;
        const int n = 256;
        t = horizontal ? new Texture2D(n, 1, TextureFormat.RGBA32, false) : new Texture2D(1, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.hideFlags = HideFlags.HideAndDontSave;
        for (int i = 0; i < n; i++)
        {
            float k = i / (n - 1f);
            if (horizontal) t.SetPixel(i, 0, new Color(1f, 1f, 1f, k));
            else t.SetPixel(0, n - 1 - i, new Color(1f, 1f, 1f, k)); // texture y is bottom-up
        }
        t.Apply(false, true);
        if (horizontal) rampH = t; else rampV = t;
        return t;
    }

    /// <summary>Smooth gradient from <paramref name="from"/> to <paramref name="to"/> (left→right or top→bottom).
    /// Drawn as two cross-faded ramp textures, so there is no banding. <paramref name="bands"/> is ignored (kept for callers).</summary>
    public static void Gradient(Rect r, Color from, Color to, int bands = 24, bool horizontal = false)
    {
        if (Event.current.type != EventType.Repaint) return;
        Texture2D ramp = Ramp(horizontal);
        Color previous = GUI.color;
        // "to" fades in along the ramp; "from" fades out along the reversed ramp.
        if (to.a > 0f) { GUI.color = to; GUI.DrawTexture(r, ramp, ScaleMode.StretchToFill, true); }
        if (from.a > 0f)
        {
            GUI.color = from;
            GUI.DrawTextureWithTexCoords(r, ramp, horizontal ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 1f, 1f, -1f), true);
        }
        GUI.color = previous;
    }

    /// <summary>Small caps label over a big Cinzel value — used for stat blocks.</summary>
    public static void Stat(Rect r, string label, string value, Color valueColor, float alpha = 1f)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), label, Label(12, new Color(Muted.r, Muted.g, Muted.b, alpha), TextAnchor.MiddleCenter));
        GUI.Label(new Rect(r.x, r.y + 18f, r.width, r.height - 18f), value, Number(24, new Color(valueColor.r, valueColor.g, valueColor.b, alpha)));
    }
}

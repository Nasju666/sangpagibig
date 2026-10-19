using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lore fragments (Resources/Story/lore.txt): floating pages placed in front of each scene's archive
/// shelves (falling back to checkpoints and portals), read with E, collected into the Codex.
/// </summary>
public static class RuleCardsLore
{
    public sealed class Entry { public string id, scene, title, body; }

    private const string FoundPrefix = "RuleCards.Lore.";
    private static List<Entry> entries;
    private static AudioClip pageClip;
    private static AudioSource pageSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { entries = null; pageClip = null; pageSource = null; }

    public static List<Entry> All { get { Load(); return entries; } }

    public static bool Found(string id) { return PlayerPrefs.GetInt(FoundPrefix + id, 0) == 1; }

    public static void MarkFound(string id) { PlayerPrefs.SetInt(FoundPrefix + id, 1); PlayerPrefs.Save(); }

    public static int FoundCount { get { int n = 0; foreach (Entry e in All) if (Found(e.id)) n++; return n; } }

    public static int IndexOf(string id) { for (int i = 0; i < All.Count; i++) if (All[i].id == id) return i; return -1; }

    public static void ResetAll() { foreach (Entry e in All) PlayerPrefs.DeleteKey(FoundPrefix + e.id); }

    public static string PlaceName(string scene)
    {
        switch (scene)
        {
            case "ArchiveHub": return "The Archive";
            case "Stage_01_KineticLink": return "Kinetic Wing";
            case "Stage_02_InvertGravity": return "Gravity Wing";
            case "Stage_03_SolidifyAir": return "Air Wing";
            case "Repository_Final": return "The Repository";
            default: return scene;
        }
    }

    private static void Load()
    {
        if (entries != null) return;
        entries = new List<Entry>();
        TextAsset asset = Resources.Load<TextAsset>("Story/lore");
        if (asset == null) return;
        Entry cur = null;
        var body = new System.Text.StringBuilder();
        System.Action flush = () =>
        {
            if (cur != null) { cur.body = body.ToString().Trim(); entries.Add(cur); }
            body.Length = 0;
        };
        bool blank = false;
        foreach (string raw in asset.text.Split('\n'))
        {
            string l = raw.TrimEnd('\r');
            if (l.StartsWith("#")) continue;
            if (l.StartsWith("==="))
            {
                flush();
                string[] parts = l.Substring(3).Split('|');
                cur = new Entry
                {
                    id = parts[0].Trim(),
                    scene = parts.Length > 1 ? parts[1].Trim() : "",
                    title = parts.Length > 2 ? parts[2].Trim() : parts[0].Trim()
                };
                blank = false;
                continue;
            }
            if (cur == null) continue;
            if (l.Trim().Length == 0) { blank = body.Length > 0; continue; }
            if (body.Length > 0) body.Append(blank ? "\n\n" : " ");
            body.Append(l.Trim());
            blank = false;
        }
        flush();
    }

    public static void PlayPage()
    {
        if (pageClip == null)
        {
            const int rate = 44100;
            int n = rate / 4;
            var data = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(t * 60f) * Mathf.Exp(-t * 14f) * (1f + .6f * Mathf.Sin(t * 90f));
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * .35f; // soft paper noise
                data[i] = lp * env * .5f;
            }
            pageClip = AudioClip.Create("RuleCards_Page", n, 1, rate, false);
            pageClip.SetData(data, 0);
        }
        if (pageSource == null)
        {
            var go = new GameObject("RuleCards_PageAudio");
            Object.DontDestroyOnLoad(go);
            pageSource = go.AddComponent<AudioSource>();
            pageSource.playOnAwake = false;
            pageSource.spatialBlend = 0f;
        }
        if (!RuleCardsAudio.Play("page", .8f)) pageSource.PlayOneShot(pageClip, .5f);
    }

    // ------------------------------------------------------------------ placement
    public static void SpawnFor(string scene)
    {
        var mine = new List<Entry>();
        foreach (Entry e in All) if (e.scene == scene) mine.Add(e);
        if (mine.Count == 0) return;

        List<Vector3> candidates = ShelfSpots();
        if (candidates.Count < mine.Count) candidates.AddRange(FallbackSpots());
        Vector3 start = Vector3.zero;
        CharacterController player = Object.FindObjectOfType<CharacterController>();
        if (player != null) start = player.transform.position;

        // Spread the fragments out: farthest-point picks, starting from the spot nearest the player.
        var chosen = new List<Vector3>();
        if (candidates.Count > 0)
        {
            int first = 0;
            for (int i = 1; i < candidates.Count; i++)
                if ((candidates[i] - start).sqrMagnitude < (candidates[first] - start).sqrMagnitude) first = i;
            chosen.Add(candidates[first]);
            while (chosen.Count < mine.Count && chosen.Count < candidates.Count)
            {
                int best = -1;
                float bestD = -1f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float d = float.MaxValue;
                    foreach (Vector3 c in chosen) d = Mathf.Min(d, (candidates[i] - c).sqrMagnitude);
                    if (d > bestD) { bestD = d; best = i; }
                }
                if (best < 0 || bestD < .5f) break;
                chosen.Add(candidates[best]);
            }
        }
        for (int i = 0; i < mine.Count && i < chosen.Count; i++)
        {
            var go = new GameObject("Lore_" + mine[i].id);
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            go.transform.position = chosen[i];
            go.AddComponent<LoreFragment>().Init(mine[i]);
        }
    }

    /// <summary>In front of the decorator's wall shelves (it only puts them where 5 m of floor is walkable).</summary>
    private static List<Vector3> ShelfSpots()
    {
        var spots = new List<Vector3>();
        GameObject decor = GameObject.Find("DECOR_Generated");
        if (decor == null) return spots;
        foreach (Transform t in decor.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.Contains("ArchiveShelf") || t.parent == null || t.parent.name != "Shelves" || !t.gameObject.activeInHierarchy) continue;
            Vector3 up = t.up;
            Vector3 p = t.position + t.forward * 1.1f + up * 1.25f;
            if (Physics.CheckSphere(p, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!Physics.Raycast(p, -up, 2.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
            spots.Add(p);
        }
        return spots;
    }

    private static List<Vector3> FallbackSpots()
    {
        var spots = new List<Vector3>();
        foreach (MonoBehaviour b in Object.FindObjectsOfType<MonoBehaviour>())
        {
            string type = b.GetType().Name;
            if (type != "ArchivePuzzleCheckpoint" && type != "StagePortal" && type != "StageReturn") continue;
            Transform t = b.transform;
            Vector3 up = t.up;
            foreach (Vector3 side in new[] { t.right, -t.right, t.forward, -t.forward })
            {
                Vector3 p = t.position + side * 2.4f + up * 1.2f;
                if (Physics.CheckSphere(p, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(p, -up, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                spots.Add(p);
                break;
            }
        }
        // Last resort: a ring around where the player starts.
        CharacterController player = Object.FindObjectOfType<CharacterController>();
        if (player != null)
        {
            Vector3 c = player.transform.position;
            Vector3 up = player.transform.up;
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float dist = 4f + (i % 3) * 2f;
                RaycastHit hit;
                if (Physics.Raycast(c + up * .5f, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore)) dist = hit.distance - 1f;
                if (dist < 2f) continue;
                Vector3 p = c + dir * dist + up * .4f;
                RaycastHit ground;
                if (!Physics.Raycast(p + up, -up, out ground, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                p = ground.point + up * 1.2f;
                if (Physics.CheckSphere(p, .3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                spots.Add(p);
            }
        }
        return spots;
    }
}

/// <summary>A floating open page with a rune above it. Look at it and press E to read.</summary>
public sealed class LoreFragment : MonoBehaviour
{
    private RuleCardsLore.Entry entry;
    private Transform visual, rune;
    private Material pageMat, runeMat;
    private Light glow;
    private Vector3 basePos;
    private bool focused;
    private Camera cam;

    public void Init(RuleCardsLore.Entry e)
    {
        entry = e;
        basePos = transform.position;
        visual = new GameObject("Page").transform;
        visual.SetParent(transform, false);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        pageMat = new Material(shader) { name = "Lore_Page" };
        pageMat.SetColor("_BaseColor", new Color(.92f, .86f, .72f));
        pageMat.EnableKeyword("_EMISSION");
        runeMat = new Material(shader) { name = "Lore_Rune" };
        runeMat.SetColor("_BaseColor", new Color(1f, .8f, .45f));
        runeMat.EnableKeyword("_EMISSION");
        // Two pages opened in a shallow V, plus a spine.
        for (int s = -1; s <= 1; s += 2)
        {
            GameObject page = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(page.GetComponent<Collider>());
            page.transform.SetParent(visual, false);
            page.transform.localPosition = new Vector3(s * .135f, 0f, 0f);
            page.transform.localEulerAngles = new Vector3(0f, 0f, -s * 12f);
            page.transform.localScale = new Vector3(.26f, .008f, .34f);
            page.GetComponent<Renderer>().sharedMaterial = pageMat;
        }
        visual.localEulerAngles = new Vector3(-55f, 0f, 0f);
        visual.localScale = Vector3.one * 1.4f;
        GameObject r = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(r.GetComponent<Collider>());
        r.transform.SetParent(transform, false);
        r.transform.localPosition = new Vector3(0f, .48f, 0f);
        r.transform.localScale = Vector3.one * .09f;
        r.transform.localEulerAngles = new Vector3(45f, 0f, 45f);
        r.GetComponent<Renderer>().sharedMaterial = runeMat;
        rune = r.transform;
        foreach (Renderer rr in GetComponentsInChildren<Renderer>())
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        glow = new GameObject("LoreLight").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.type = LightType.Point;
        glow.color = new Color(1f, .78f, .45f);
        glow.range = 3f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (entry == null) return;
        float t = Time.time + basePos.x;
        bool found = RuleCardsLore.Found(entry.id);
        transform.position = basePos + transform.up * Mathf.Sin(t * 1.4f) * .06f;
        transform.Rotate(0f, 28f * Time.deltaTime, 0f, Space.Self);
        rune.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);

        float pulse = .75f + .25f * Mathf.Sin(t * 2.4f);
        float k = found ? .35f : 1f;
        pageMat.SetColor("_EmissionColor", new Color(1f, .82f, .55f) * (.35f + (focused ? .5f : 0f)) * k);
        runeMat.SetColor("_EmissionColor", (found ? new Color(.7f, .6f, .45f) : new Color(1f, .7f, .3f)) * (2.5f * pulse) * k);
        glow.intensity = (focused ? 1.6f : 1f) * pulse * (found ? .5f : 1f);

        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        focused = false;
        if (cam == null || MouseLook.isPaused || RuleCardsStory.Blocking) return;
        Vector3 to = transform.position - cam.transform.position;
        focused = to.magnitude < 2.6f && Vector3.Dot(cam.transform.forward, to.normalized) > .8f;
        if (focused && Input.GetKeyDown(KeyCode.E)) RuleCardsStory.OpenLore(entry);
    }

    private void OnGUI()
    {
        if (!focused || entry == null || MouseLook.isPaused) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        Rect r = ResponsiveIMGUI.BottomLeft(vw, vh, 380f, 58f);
        RuleCardsUI.HudCard(r);
        GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 16f), RuleCardsLore.Found(entry.id) ? "Lore  ·  read" : "Lore  ·  new",
            RuleCardsUI.Label(12, RuleCardsUI.Gold));
        GUI.Label(new Rect(r.x + 14f, r.y + 26f, r.width - 28f, 24f), "E   " + entry.title, RuleCardsUI.Title(16, RuleCardsUI.Text, TextAnchor.MiddleLeft));
        ResponsiveIMGUI.End(previous);
    }

    private void OnDestroy()
    {
        if (pageMat != null) Destroy(pageMat);
        if (runeMat != null) Destroy(runeMat);
    }
}

/// <summary>Codex panel in the pause menu: every lore fragment found so far.</summary>
public static class RuleCardsCodex
{
    public static int LastClosedFrame = -10;
    private static string selected;
    private static Vector2 scroll;

    /// <summary>Draws the codex; returns false once closed.</summary>
    public static bool Draw(float vw, float vh)
    {
        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, .01f, .02f, .78f));
        float w = Mathf.Min(820f, vw - 32f), h = Mathf.Min(560f, vh - 32f);
        Rect panel = new Rect((vw - w) * .5f, (vh - h) * .5f, w, h);
        RuleCardsUI.Panel(panel);
        GUI.Label(new Rect(panel.x, panel.y + 18f, w, 40f), "The Index", RuleCardsUI.Title(30, RuleCardsUI.Cyan));
        RuleCardsUI.Divider(panel.center.x, panel.y + 62f, w - 160f, RuleCardsUI.Gold);
        GUI.Label(new Rect(panel.x, panel.y + 70f, w, 20f), "Kept by Glyph   ·   pages found   " + RuleCardsLore.FoundCount + " / " + RuleCardsLore.All.Count,
            RuleCardsUI.Label(13, RuleCardsUI.GoldBright, TextAnchor.MiddleCenter));

        float listW = Mathf.Min(260f, w * .36f);
        Rect list = new Rect(panel.x + 28f, panel.y + 104f, listW, h - 170f);
        Rect page = new Rect(list.xMax + 28f, list.y, panel.xMax - list.xMax - 56f, list.height);
        RepositoryGUI.Fill(new Rect(list.xMax + 13f, list.y, 1f, list.height), new Color(1f, 1f, 1f, .08f));

        // List, grouped by place.
        float rowH = 26f;
        string lastScene = null;
        int rows = 0;
        foreach (RuleCardsLore.Entry e in RuleCardsLore.All) { if (e.scene != lastScene) { rows++; lastScene = e.scene; } rows++; }
        Rect view = new Rect(0f, 0f, list.width - 16f, rows * rowH + 8f);
        scroll = GUI.BeginScrollView(list, scroll, view);
        float y = 0f;
        lastScene = null;
        foreach (RuleCardsLore.Entry e in RuleCardsLore.All)
        {
            if (e.scene != lastScene)
            {
                lastScene = e.scene;
                GUI.Label(new Rect(0f, y + 4f, view.width, rowH), RuleCardsLore.PlaceName(e.scene), RuleCardsUI.Label(12, RuleCardsUI.Gold));
                y += rowH;
            }
            bool found = RuleCardsLore.Found(e.id);
            Rect row = new Rect(0f, y, view.width, rowH - 2f);
            bool isSel = selected == e.id;
            if (isSel) RepositoryGUI.Fill(row, new Color(.86f, .71f, .43f, .14f));
            GUI.Label(new Rect(12f, y, view.width - 12f, rowH - 2f), found ? e.title : "Undiscovered",
                RuleCardsUI.Prose(16, found ? (isSel ? Color.white : RuleCardsUI.Text) : RepositoryGUI.WithAlpha(RuleCardsUI.Muted, .6f), TextAnchor.MiddleLeft));
            if (found && GUI.Button(row, GUIContent.none, GUIStyle.none)) selected = e.id;
            y += rowH;
        }
        GUI.EndScrollView();

        // Selected page.
        RuleCardsLore.Entry sel = null;
        foreach (RuleCardsLore.Entry e in RuleCardsLore.All) if (e.id == selected && RuleCardsLore.Found(e.id)) sel = e;
        if (sel != null)
        {
            GUI.Label(new Rect(page.x, page.y, page.width, 18f), RuleCardsLore.PlaceName(sel.scene), RuleCardsUI.Label(12, RuleCardsUI.Gold));
            GUI.Label(new Rect(page.x, page.y + 20f, page.width, 34f), sel.title, RuleCardsUI.Title(22, RuleCardsUI.Text, TextAnchor.MiddleLeft));
            GUI.Label(new Rect(page.x, page.y + 64f, page.width, page.height - 64f), sel.body,
                RuleCardsUI.Prose(17, new Color(.9f, .92f, .96f), TextAnchor.UpperLeft));
        }
        else
        {
            GUI.Label(page, RuleCardsLore.FoundCount == 0
                    ? "No fragments yet. Look for floating pages near the archive shelves, and press E to read them."
                    : "Choose a fragment to read it again.",
                RuleCardsUI.ProseItalic(17, RuleCardsUI.Muted, TextAnchor.MiddleCenter));
        }

        bool close = GUI.Button(new Rect(panel.center.x - 120f, panel.yMax - 58f, 240f, 38f), "Back   (Esc)", RuleCardsUI.PrimaryButton);
        if (Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { close = true; Event.current.Use(); }
        if (close) LastClosedFrame = Time.frameCount;
        return !close;
    }
}

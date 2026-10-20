using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The trailer's shot list. Each shot: a scene, a duration and a script run every frame (camera pose for
/// cinematic shots, autopilot for first-person ones). Times are shot-local seconds; the edit happens later.
/// </summary>
public static class RuleCardsTrailerShots
{
    public static readonly List<RuleCardsTrailer.Shot> All = new List<RuleCardsTrailer.Shot>();

    public static RuleCardsTrailer.Shot Find(string id) { if (All.Count == 0) Build(); return All.FirstOrDefault(s => s.id == id); }
    public static string[] Ids { get { if (All.Count == 0) Build(); return All.Select(s => s.id).ToArray(); } }

    private static RuleCardsTrailer.Shot Add(string id, string scene, float duration, bool firstPerson = false)
    {
        var s = new RuleCardsTrailer.Shot { id = id, scene = scene, duration = duration, firstPerson = firstPerson };
        All.Add(s);
        return s;
    }

    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    private static void Build()
    {
        All.Clear();
        const string Title = "RuleCards_Title", Hub = "ArchiveHub", S1 = "Stage_01_KineticLink", S2 = "Stage_02_InvertGravity",
            S3 = "Stage_03_SolidifyAir", Repo = "Repository_Final", Esc = "Stage_04_Escape";

        // ---------------------------------------------------------------- title diorama push-in
        var title = Add("title_push", Title, 9f);
        title.setup = c =>
        {
            c.vars["p"] = c.cam.transform.position;
            c.vars["f"] = c.cam.transform.forward;
        };
        title.tick = c =>
        {
            Vector3 p = (Vector3)c.vars["p"], f = (Vector3)c.vars["f"];
            float k = c.Ease(-1f, 9f);
            Vector3 pos = p + f * (1.6f * k) + Vector3.up * (.25f * k);
            c.Pose(pos, pos + f * 10f + Vector3.up * (-.4f * k), 52f);
        };

        // ---------------------------------------------------------------- hub
        var hubA = Add("hub_approach", Hub, 7f);
        hubA.tick = c =>
        {
            float k = c.Ease(-.5f, 7f);
            c.Pose(Vector3.Lerp(V(-6.5f, 1.5f, -3f), V(-2.4f, 2.1f, 7.2f), k), V(0f, 2.5f, 14f), 50f);
        };

        var glyph = Add("glyph_wake", Hub, 6.5f);
        glyph.preroll = .6f;
        glyph.setup = c =>
        {
            GlyphGuide.CameraOverride = c.cam;
            GlyphGuide.Speaking = true;
            if (GlyphGuide.Instance == null) GlyphGuide.Spawn(true);
            else GlyphGuide.Instance.Hide();
        };
        glyph.tick = c =>
        {
            c.glyphSpeaking = true;
            if (GlyphGuide.Instance != null && (c.At(1.5f) || c.At(3f)))
                c.Note("glyph " + GlyphGuide.Instance.transform.position.ToString("F2") + " cam " + c.cam.transform.position.ToString("F2") + " scale " + GlyphGuide.Instance.transform.localScale.x.ToString("F2"));
            float k = c.Ease(-.6f, 6.5f);
            c.Pose(Vector3.Lerp(V(-3.2f, 1.7f, 4.6f), V(-2.8f, 1.75f, 5.5f), k), V(-.2f, 2.1f, 14f), 46f);
            if (GlyphGuide.Instance == null) return;
            if (c.At(.5f)) GlyphGuide.Instance.Appear();
            foreach (float p in new[] { 2.0f, 2.6f, 3.3f, 4.1f, 4.7f, 5.4f })
                if (c.At(p)) GlyphGuide.Instance.Pulse(1.1f);
        };

        // ---------------------------------------------------------------- stage 1: weight onto the plate, door rises
        var s1 = Add("s1_weight", S1, 7f);
        s1.setup = c =>
        {
            var puzzle = Object.FindObjectOfType<KineticConnectionPuzzle>();
            GameObject w = puzzle != null ? puzzle.movableWeight : GameObject.Find("MovableArchiveWeight");
            if (w != null)
            {
                w.SetActive(true);
                var rb = w.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.position = puzzle != null ? puzzle.weightStartPosition : w.transform.position; }
                c.vars["w"] = rb;
                c.vars["w0"] = puzzle != null ? puzzle.weightStartPosition : w.transform.position;
            }
            c.Note("weight " + (w == null ? "NOT FOUND" : w.transform.position.ToString("F1") + " active " + w.activeInHierarchy + " rb " + (w.GetComponent<Rigidbody>() != null)));
            GameObject door = GameObject.Find("Kinetic_Door");
            c.Note("door " + (door == null ? "NOT FOUND" : door.transform.position.ToString("F1")));
        };
        s1.tick = c =>
        {
            float k = c.Ease(-.5f, 7f);
            c.Pose(Vector3.Lerp(V(5.5f, 2.0f, 12.5f), V(4.6f, 1.8f, 14.2f), k), V(-3f, 1.5f, 24f), 56f);
            var rb = c.Get<Rigidbody>("w");
            if (rb == null || !c.vars.ContainsKey("w0")) return;
            Vector3 w0 = (Vector3)c.vars["w0"], target = V(0f, 1.4f, 21f);
            if (c.t < .6f) return;
            if (c.t < 2.4f)
            {
                rb.isKinematic = true;
                float e = c.Ease(.6f, 2.4f);
                Vector3 p = Vector3.Lerp(w0, target + Vector3.up * 1.2f, e) + Vector3.up * (2.2f * 4f * e * (1f - e));
                rb.MovePosition(p);
                rb.MoveRotation(Quaternion.Euler(0f, 90f * e, 0f));
            }
            else if (c.At(2.4f)) { rb.isKinematic = false; rb.velocity = Vector3.down * 2f; }
        };

        // ---------------------------------------------------------------- stage 2: first-person gravity flip
        var s2 = Add("s2_flip", S2, 7f, true);
        s2.setup = c => { if (c.motor != null) c.vars["yaw"] = c.motor.Yaw; };
        s2.tick = c =>
        {
            if (c.motor == null) return;
            float yaw = c.vars.ContainsKey("yaw") ? (float)c.vars["yaw"] : 0f;
            if (c.t < 0f) { c.Look(yaw, -6f, 0f); return; }
            c.Move(c.t < 2.6f ? 1f : 0f);
            if (c.At(.9f)) c.motor.SetInverted(true);
            c.Look(yaw + 8f * Mathf.Sin(c.t * .6f), c.motor.IsInverted ? 22f : -6f, 2.5f);
        };

        // ---------------------------------------------------------------- stage 3: the air hardens into a bridge
        var s3 = Add("s3_bridge", S3, 7f);
        s3.setup = c =>
        {
            var nodes = Object.FindObjectsOfType<ArchiveAirPlatform>().Where(n => n.gameObject.activeInHierarchy).OrderBy(n => n.transform.position.z).ToList();
            foreach (var n in nodes) n.SetSolid(false);
            c.vars["nodes"] = nodes;
            c.vars["scales"] = nodes.Select(n => n.transform.localScale).ToList();
        };
        s3.tick = c =>
        {
            float k = c.Ease(-.5f, 7f);
            c.Pose(Vector3.Lerp(V(-2.2f, 3.0f, 6.5f), V(-1.4f, 2.6f, 9.5f), k), V(1.5f, .2f, 32f), 52f);
            var nodes = c.Get<List<ArchiveAirPlatform>>("nodes");
            var scales = c.Get<List<Vector3>>("scales");
            if (nodes == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                float at = .8f + Mathf.Max(0f, n.transform.position.z - 10f) * .07f;
                if (c.t >= at && !n.IsSolid) n.SetSolid(true);
                float pop = Mathf.Clamp01((c.t - at) / .28f);
                Vector3 s = scales[i];
                n.transform.localScale = c.t < at ? s : new Vector3(s.x * Mathf.Lerp(.55f, 1f, pop), s.y * Mathf.Lerp(.2f, 1f, pop), s.z * Mathf.Lerp(.55f, 1f, pop));
            }
        };

        // ---------------------------------------------------------------- repository
        var rise = Add("repo_rise", Repo, 5.5f);
        rise.tick = c =>
        {
            float k = c.Ease(-.5f, 5.5f);
            c.Pose(Vector3.Lerp(V(-5.2f, 1.1f, .2f), V(-5.8f, 1.5f, 1.2f), k), V(-10f, 1.6f, 5f), 48f);
            var f = Object.FindObjectOfType<RepositoryFinale>();
            if (f != null && c.At(.4f)) f.TrailerReveal(0);
        };

        var finale = Add("repo_finale", Repo, 8.5f);
        finale.tick = c =>
        {
            float k = c.Ease(-.5f, 8.5f);
            float a = Mathf.Lerp(-125f, -55f, k) * Mathf.Deg2Rad;
            Vector3 center = V(0f, 4f, 12f);
            c.Pose(center + V(Mathf.Cos(a) * 11f, Mathf.Lerp(-1.6f, .2f, k), Mathf.Sin(a) * 11f), center + V(0f, .6f, 0f), 54f);
            var f = Object.FindObjectOfType<RepositoryFinale>();
            if (f != null && c.At(.3f)) f.TrailerFinale();
        };

        // ---------------------------------------------------------------- escape: first person with the cards
        var kin = Add("esc_kinetic", Esc, 5.5f, true);
        kin.setup = c => { if (c.escape != null) c.escape.TrailerStart(5, 40f, 999f); };
        kin.tick = c =>
        {
            if (c.cards == null) return;
            Vector3 a = V(39f, 9f, 54.75f), b = V(39f, 6.5f, 43f);
            if (c.t < .6f) { c.LookAt(a, c.t < 0f ? 0f : 7f); if (c.At(-.5f)) c.cards.TrailerSelect(RuleCardKind.KineticLink); return; }
            if (c.At(.6f)) { c.LookAt(a, 0f); c.cards.TrailerUse(RuleCardKind.KineticLink); }
            if (c.t > 1.3f && c.t < 2.1f) c.LookAt(b, 8f);
            if (c.At(2.1f)) { c.LookAt(b, 0f); c.cards.TrailerUse(RuleCardKind.KineticLink); }
            if (c.t > 3.0f) c.Look(272f, 6f, 2.5f);
        };

        var air = Add("esc_air", Esc, 7f, true);
        air.setup = c => { if (c.escape != null) c.escape.TrailerStart(1, 40f, 999f); };
        air.tick = c =>
        {
            if (c.cards == null || c.motor == null) return;
            c.Look(180f, 4f, c.t < 0f ? 0f : 4f);
            if (c.At(-.6f)) c.cards.TrailerSelect(RuleCardKind.SolidifyAir);
            if (c.t < .3f) return;
            Vector3 p = c.player.transform.position;
            object last;
            bool place = !c.vars.TryGetValue("last", out last) || Vector3.Distance((Vector3)last, p) > 2.7f;
            if (place && c.cards.TrailerUse(RuleCardKind.SolidifyAir)) c.vars["last"] = p;
            c.Move(c.t > .45f ? 1f : 0f);
        };

        var grav = Add("esc_gravity", Esc, 6.5f, true);
        grav.setup = c => { if (c.escape != null) c.escape.TrailerStart(2, 40f, 999f); };
        grav.tick = c =>
        {
            if (c.cards == null || c.motor == null) return;
            if (c.At(-.6f)) c.cards.TrailerSelect(RuleCardKind.InvertGravity);
            if (c.t < 0f) { c.Look(180f, -12f, 0f); return; }
            bool inv = c.motor.IsInverted;
            c.Look(180f, inv ? 8f : -12f, 3f);
            c.Move(c.t > .1f && c.t < 4.6f ? 1f : 0f);
            if (!c.vars.ContainsKey("up") && c.t > .7f && c.motor.IsGrounded) { if (c.cards.TrailerUse(RuleCardKind.InvertGravity)) c.vars["up"] = true; }
            if (c.vars.ContainsKey("up") && !c.vars.ContainsKey("down") && c.player.transform.position.z < 99.5f && c.motor.IsGrounded && inv)
            { if (c.cards.TrailerUse(RuleCardKind.InvertGravity)) c.vars["down"] = true; }
        };

        Vector3[] startRoute = { V(3f, 10.6f, 191f), V(8f, 10.6f, 187.5f), V(13f, 10.6f, 181f), V(16f, 10.6f, 175f), V(25f, 10.6f, 170f), V(25f, 10.6f, 160f) };
        var run = Add("esc_run", Esc, 5.5f, true);
        run.preroll = 1.5f;
        run.setup = c => { if (c.escape != null) c.escape.TrailerStart(0, 6f, 0f); };
        run.tick = c =>
        {
            if (c.motor == null) return;
            if (c.t < -1f) { c.Look(148.7f, 2f, 0f); return; }
            c.Follow(startRoute, true, 6f, 4f);
            if (c.At(1f) || c.At(4f)) c.Note("player " + c.player.transform.position.ToString("F1"));
        };

        var back = Add("esc_collapse", Esc, 6.5f);
        back.preroll = .5f;
        back.setup = c => { if (c.escape != null) c.escape.TrailerStart(1, 37f, 0f); };
        back.tick = c =>
        {
            float k = c.Ease(-.5f, 6.5f);
            c.Pose(Vector3.Lerp(V(14.5f, 13.6f, 178.5f), V(13.5f, 13.0f, 180.5f), k), V(3f, 9.4f, 192f), 56f);
        };

        // Hub card plinths: cards granted for the shot only (the save is put back afterwards).
        var plinths = Add("hub_cards", Hub, 7f);
        plinths.preroll = .3f;
        plinths.setup = c =>
        {
            var had = new bool[RuleCardInfo.Count];
            for (int i = 0; i < had.Length; i++) { had[i] = RuleCardsProgress.HasCard(RuleCardInfo.Ids[i]); RuleCardsProgress.GrantCard(RuleCardInfo.Ids[i]); }
            c.vars["had"] = had;
            RuleCardsHubDisplay.ResetShown();
            var old = Object.FindObjectOfType<RuleCardsHubDisplay>();
            if (old != null) Object.Destroy(old.gameObject);
            var go = new GameObject("RuleCards_HubCardDisplay");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            go.AddComponent<RuleCardsHubDisplay>();
        };
        plinths.tick = c =>
        {
            float k = c.Ease(-.3f, 7f);
            c.Pose(Vector3.Lerp(V(1.2f, 3.0f, 11.5f), V(.4f, 3.3f, 10f), k), V(0f, 3.3f, 5f), 52f);
        };
        plinths.teardown = c =>
        {
            var had = c.vars["had"] as bool[];
            for (int i = 0; i < had.Length; i++) if (!had[i]) PlayerPrefs.DeleteKey("RuleCards.Card." + RuleCardInfo.Ids[i]);
            RuleCardsHubDisplay.ResetShown();
            PlayerPrefs.Save();
        };

        var exit = Add("esc_exit", Esc, 6.5f);
        exit.setup = c => { if (c.escape != null) c.escape.TrailerStart(8, 40f, 999f); };
        exit.tick = c =>
        {
            float k = c.Ease(-.5f, 6.5f);
            c.Pose(Vector3.Lerp(V(.6f, 3.6f, -15f), V(0f, 2.7f, -24.5f), k), V(0f, 3.2f, -32.5f), 50f);
        };
    }
}

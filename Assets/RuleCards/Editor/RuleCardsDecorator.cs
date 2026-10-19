using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Set-dresses every RuleCards scene with the Rodin model prefabs. Everything it creates lives under
/// one "DECOR_Generated" root per scene (re-running replaces it); the only edits outside that root
/// are visual swaps on gameplay objects (pickup cards, escape anchors, exit gate), which hide the old
/// placeholder renderers instead of deleting anything.
///
/// Placement is driven by physics raycasts against the real level: floors are sampled on a grid,
/// walls found from those samples, and props only go where they leave room to walk, stay clear of
/// gameplay objects and triggers, and don't overlap existing colliders.
/// </summary>
public static class RuleCardsDecorator
{
    public const string RootName = "DECOR_Generated";

    // Scripts that are decoration or player plumbing — not gameplay to keep clear of.
    private static readonly HashSet<string> IgnoredScripts = new HashSet<string>
    {
        "ArchiveTorchFlicker", "EscapeDrift", "FrameRateLimiter", "DecorMotion", "DecorFlame", "Footsteps",
        "HeadBob", "CameraFOV", "MouseLook", "EscapeCrumble",
    };

    private sealed class Sample
    {
        public Vector3 p;
        public float ceiling = float.PositiveInfinity;
        public Vector3 ceilingPoint, ceilingNormal;
        public bool hasWall;
        public Vector3 wallPoint, wallNormal;
        public int wallCount;
    }

    private sealed class Ctx
    {
        public string scene;
        public Transform root;
        public System.Random rng;
        public Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        public List<Vector4> keepOut = new List<Vector4>();     // xyz + radius
        public List<Vector4> placed = new List<Vector4>();      // xyz + radius
        public List<Vector3> torches = new List<Vector3>();
        public List<Sample> floors = new List<Sample>();
        public Bounds bounds;
        public Vector3 centre;
        public Vector3 spawn = new Vector3(float.NaN, 0f, 0f);
        public List<Vector3> seeds = new List<Vector3>();
        public Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        public int count;

        public float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        public bool Chance(float p) { return rng.NextDouble() < p; }
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("RuleCards/Art/2. Decorate All Scenes", false, 220)]
    public static void DecorateAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        var report = new List<string>();
        foreach (string path in RuleCardsArtTools.Scenes)
        {
            if (!File.Exists(path)) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            report.Add(scene.name + ": " + Decorate(scene) + " props");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[RuleCards Decor] " + string.Join(" | ", report));
    }

    [MenuItem("RuleCards/Art/Decorate Current Scene", false, 221)]
    public static void DecorateCurrent()
    {
        Scene scene = SceneManager.GetActiveScene();
        int n = Decorate(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[RuleCards Decor] {scene.name}: {n} props (scene marked dirty — save to keep).");
    }

    [MenuItem("RuleCards/Art/Remove Decoration (current scene)", false, 222)]
    public static void RemoveCurrent()
    {
        Scene scene = SceneManager.GetActiveScene();
        Clear(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // ------------------------------------------------------------------ core

    private static void Clear(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == RootName) Object.DestroyImmediate(go);
        // Restore placeholder renderers hidden by an earlier swap so a re-run starts from scratch.
        foreach (GameObject go in scene.GetRootGameObjects())
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith("DECOR_")) Object.DestroyImmediate(t.gameObject);
        foreach (GameObject go in scene.GetRootGameObjects())
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.enabled) continue;
                string n = r.gameObject.name;
                if (n.EndsWith("_CardVisual") || n.StartsWith("Escape_Exit_Post") || n == "Escape_Exit_Lintel" || r.GetComponent<EscapeAnchor>() != null)
                    r.enabled = true;
            }
    }

    public static int Decorate(Scene scene)
    {
        Clear(scene);
        Physics.SyncTransforms();
        var ctx = new Ctx
        {
            scene = scene.name,
            rng = new System.Random(scene.name.GetHashCode() & 0x7fffffff),
            root = new GameObject(RootName).transform,
        };
        SceneManager.MoveGameObjectToScene(ctx.root.gameObject, scene);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RuleCardsArtTools.PrefabFolder }))
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            ctx.prefabs[p.name.Replace("ART_", "")] = p;
        }

        switch (scene.name)
        {
            case "RuleCards_Title": Title(ctx); return ctx.count;
            case "Stage_04_Escape":
                Analyse(scene, ctx, false);
                Escape(ctx);
                return ctx.count;
        }

        Analyse(scene, ctx, true);
        bool gravity = scene.name == "Stage_02_InvertGravity"; // the player walks the ceiling here
        SwapPickups(scene, ctx);
        PortalGuardians(scene, ctx);
        SafetyRailings(scene, ctx);
        Shelves(ctx, scene.name == "ArchiveHub" || scene.name == "Repository_Final" ? 14 : 10);
        Torches(ctx, 14);
        Corners(ctx, 18);
        if (!gravity) Chandeliers(ctx, 6);
        if (!gravity) FloatingBooks(ctx, scene.name == "ArchiveHub" || scene.name == "Repository_Final" ? 14 : 8);
        Abyss(ctx, 18);
        return ctx.count;
    }

    // ------------------------------------------------------------------ analysis

    private static bool IsGameplay(Component c)
    {
        Transform t = c.transform;
        for (int depth = 0; t != null && depth < 3; depth++, t = t.parent)
        {
            if (t.GetComponent<Rigidbody>() != null) return true;
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) continue;
                if (!IgnoredScripts.Contains(mb.GetType().Name)) return true;
            }
        }
        return false;
    }

    private static bool Visible(Collider c)
    {
        var r = c.GetComponent<Renderer>();
        return r != null && r.enabled;
    }

    private static bool Solid(RaycastHit h)
    {
        return h.collider != null && !h.collider.isTrigger && Visible(h.collider) && !IsGameplay(h.collider);
    }

    private static void Analyse(Scene scene, Ctx ctx, bool requireCeilingIfEnclosed)
    {
        // The player's own capsule must not block sight lines while the level is analysed.
        var controllers = new List<CharacterController>();
        foreach (GameObject go in scene.GetRootGameObjects())
            foreach (CharacterController cc in go.GetComponentsInChildren<CharacterController>())
                if (cc.enabled) { cc.enabled = false; controllers.Add(cc); }
        try { AnalyseLevel(scene, ctx, requireCeilingIfEnclosed); }
        finally { foreach (CharacterController cc in controllers) cc.enabled = true; }
    }

    private static void AnalyseLevel(Scene scene, Ctx ctx, bool requireCeilingIfEnclosed)
    {
        bool any = false;
        var b = new Bounds();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == RootName) continue;
            foreach (Collider c in go.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c.isTrigger)
                {
                    if (c.enabled && c.isTrigger)
                        ctx.keepOut.Add(new Vector4(c.bounds.center.x, c.bounds.center.y, c.bounds.center.z, c.bounds.extents.magnitude + 1.5f));
                    continue;
                }
                if (c.bounds.size.magnitude > 600f) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                string n = mb.GetType().Name;
                if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro") || IgnoredScripts.Contains(n)) continue;
                if (n == "PlayerMovement") ctx.spawn = mb.transform.position;
                if (n == "PlayerMovement" || n == "ArchivePuzzleCheckpoint" || n == "RuleCardPickup" || n == "StageReturn" ||
                    n == "StagePortal" || n == "ArchiveReturnBeacon" || n == "TerminalReveal" || n == "ArchiveTransitSeal")
                    ctx.seeds.Add(mb.transform.position);
                float radius = n == "PlayerMovement" ? 4f : n == "EscapeStage" || n == "ArchiveStageMechanics" ? 0f : 3f;
                if (radius > 0f) ctx.keepOut.Add(new Vector4(mb.transform.position.x, mb.transform.position.y, mb.transform.position.z, radius));
            }
            foreach (ArchiveTorchFlicker t in go.GetComponentsInChildren<ArchiveTorchFlicker>())
                ctx.torches.Add(t.transform.position);
        }
        ctx.bounds = b;

        // Floor samples on a 1 m grid.
        const float step = 1f;
        var all = new List<Sample>();
        for (float x = b.min.x + .5f; x < b.max.x; x += step)
            for (float z = b.min.z + .5f; z < b.max.z; z += step)
            {
                RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, b.max.y + 5f, z), Vector3.down, b.size.y + 10f, ~0, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit h in hits)
                {
                    if (h.normal.y < .85f || !Solid(h)) continue;
                    Vector3 p = h.point;
                    if (Physics.Raycast(p + Vector3.up * .05f, Vector3.up, 2.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var s = new Sample { p = p };
                    if (Physics.Raycast(p + Vector3.up * .05f, Vector3.up, out RaycastHit ch, 80f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        s.ceiling = ch.distance; s.ceilingPoint = ch.point; s.ceilingNormal = ch.normal;
                    }
                    all.Add(s);
                }
            }
        if (requireCeilingIfEnclosed) all = Reachable(all, ctx.seeds);
        int roofed = 0;
        foreach (Sample s in all) if (!float.IsInfinity(s.ceiling)) roofed++;
        bool isEnclosed = requireCeilingIfEnclosed && all.Count > 0 && roofed > all.Count * .6f;
        foreach (Sample s in all)
            if (!isEnclosed || !float.IsInfinity(s.ceiling)) ctx.floors.Add(s);

        // Walls next to each sample.
        Vector3 sum = Vector3.zero;
        foreach (Sample s in ctx.floors)
        {
            sum += s.p;
            float best = 9f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (!SolidRay(s.p + Vector3.up * 1.2f, d, 1.4f, out RaycastHit h)) continue;
                if (Mathf.Abs(h.normal.y) > .25f) continue;
                s.wallCount++;
                if (h.distance < best)
                {
                    best = h.distance; s.hasWall = true; s.wallPoint = h.point;
                    s.wallNormal = new Vector3(h.normal.x, 0f, h.normal.z).normalized;
                }
            }
        }
        ctx.centre = ctx.floors.Count > 0 ? sum / ctx.floors.Count : b.center;
        Shuffle(ctx.floors, ctx.rng);
    }

    /// <summary>
    /// Floor samples the player can actually get to from the spawn: walk links between neighbours,
    /// plus jump/drop links from edges, all requiring a clear line of sight (so walls and roofs
    /// keep the outside of the level out).
    /// </summary>
    private static List<Sample> Reachable(List<Sample> all, List<Vector3> seeds)
    {
        if (all.Count == 0 || seeds.Count == 0) return all;
        var grid = new Dictionary<long, List<int>>();
        System.Func<float, float, long> Key = (x, z) => ((long)Mathf.FloorToInt(x) << 32) ^ (uint)Mathf.FloorToInt(z);
        for (int i = 0; i < all.Count; i++)
        {
            long k = Key(all[i].p.x, all[i].p.z);
            if (!grid.TryGetValue(k, out List<int> l)) grid[k] = l = new List<int>();
            l.Add(i);
        }
        var seen = new bool[all.Count];
        var queue = new Queue<int>();
        foreach (Vector3 seed in seeds)
        {
            int start = -1;
            float bestD = 9f; // within 3 m
            for (int i = 0; i < all.Count; i++)
            {
                Vector3 d = all[i].p - seed;
                if (d.y > .5f || d.y < -4f) continue;
                float h = new Vector2(d.x, d.z).sqrMagnitude;
                if (h >= bestD) continue;
                // Must actually see the seed (keeps roofs above ceiling-mounted gameplay out).
                if (Physics.Linecast(seed, all[i].p + Vector3.up * .5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                bestD = h; start = i;
            }
            if (start < 0 || seen[start]) continue;
            seen[start] = true;
            queue.Enqueue(start);
        }
        if (queue.Count == 0) return all;
        var near = new List<int>();
        while (queue.Count > 0)
        {
            int a = queue.Dequeue();
            Vector3 pa = all[a].p;
            int walkNeighbours = 0;
            near.Clear();
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                    if (grid.TryGetValue(Key(pa.x + dx, pa.z + dz), out List<int> l)) near.AddRange(l);
            foreach (int b in near)
            {
                if (b == a) continue;
                Vector3 pb = all[b].p;
                float h = new Vector2(pb.x - pa.x, pb.z - pa.z).magnitude;
                float dy = pb.y - pa.y;
                if (h > 1.6f || Mathf.Abs(dy) > .6f) continue;
                walkNeighbours++;
                if (seen[b]) continue;
                if (Physics.Linecast(pa + Vector3.up * .9f, pb + Vector3.up * .9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                seen[b] = true;
                queue.Enqueue(b);
            }
            if (walkNeighbours >= 7) continue; // interior sample: no need for jump links
            near.Clear();
            for (int dx = -9; dx <= 9; dx++)
                for (int dz = -9; dz <= 9; dz++)
                    if (grid.TryGetValue(Key(pa.x + dx, pa.z + dz), out List<int> l)) near.AddRange(l);
            foreach (int b in near)
            {
                if (seen[b]) continue;
                Vector3 pb = all[b].p;
                float h = new Vector2(pb.x - pa.x, pb.z - pa.z).magnitude;
                float dy = pb.y - pa.y;
                if (h > 9f || dy > 2f || dy < -14f) continue;
                if (Physics.Linecast(pa + Vector3.up * 1.2f, pb + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                seen[b] = true;
                queue.Enqueue(b);
            }
        }
        var result = new List<Sample>();
        for (int i = 0; i < all.Count; i++) if (seen[i]) result.Add(all[i]);
        return result;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }

    private static bool Clear(Ctx ctx, Vector3 p, float keepRadius, float spacing)
    {
        foreach (Vector4 k in ctx.keepOut)
        {
            float r = k.w + keepRadius;
            if ((new Vector3(k.x, k.y, k.z) - p).sqrMagnitude < r * r) return false;
        }
        foreach (Vector4 q in ctx.placed)
        {
            float r = Mathf.Max(q.w, spacing);
            if ((new Vector3(q.x, q.y, q.z) - p).sqrMagnitude < r * r) return false;
        }
        return true;
    }

    /// <summary>First non-trigger hit along a ray; true only if it is visible, static level geometry.</summary>
    private static bool SolidRay(Vector3 origin, Vector3 dir, float dist, out RaycastHit hit, Collider ignore = null)
    {
        hit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider == ignore) continue;
            hit = h;
            return Solid(h);
        }
        return false;
    }

    private static bool FloorAt(Vector3 p, float expectedY, out Vector3 floor)
    {
        floor = p;
        if (!SolidRay(new Vector3(p.x, expectedY + 1.2f, p.z), Vector3.down, 2.4f, out RaycastHit h) || h.normal.y < .85f) return false;
        floor = h.point;
        return true;
    }

    private static bool Free(Vector3 centre, Vector3 half, Quaternion rot)
    {
        return !Physics.CheckBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore);
    }

    // ------------------------------------------------------------------ placement helpers

    private static Transform Group(Ctx ctx, string name)
    {
        if (ctx.groups.TryGetValue(name, out Transform t)) return t;
        t = new GameObject(name).transform;
        t.SetParent(ctx.root, false);
        ctx.groups[name] = t;
        return t;
    }

    private static GameObject Place(Ctx ctx, string prefab, string group, Vector3 pos, Quaternion rot, float scale, bool collider = false, bool shadows = true, float spacingRadius = 1f)
    {
        if (!ctx.prefabs.TryGetValue(prefab, out GameObject source)) { Debug.LogWarning("[RuleCards Decor] missing prefab " + prefab); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source, ctx.root.gameObject.scene);
        go.transform.SetParent(Group(ctx, group), true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = Vector3.one * scale;
        if (!shadows)
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (collider)
        {
            Bounds b = LocalBounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            Physics.SyncTransforms(); // so the next placement's overlap test sees this one
        }
        ctx.placed.Add(new Vector4(pos.x, pos.y, pos.z, spacingRadius));
        ctx.count++;
        return go;
    }

    private static Bounds LocalBounds(GameObject go)
    {
        bool any = false;
        var b = new Bounds();
        Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    private static void Light(GameObject go, bool on)
    {
        foreach (Light l in go.GetComponentsInChildren<Light>(true)) l.gameObject.SetActive(on);
    }

    private static void Still(GameObject go)
    {
        foreach (DecorMotion m in go.GetComponentsInChildren<DecorMotion>()) m.enabled = false;
    }

    private static void SetChandelierDrop(GameObject go, float drop)
    {
        Transform body = go.transform.Find("Body");
        Transform chain = go.transform.Find("Chain");
        if (body != null) body.localPosition = new Vector3(0f, -drop, 0f);
        float len = Mathf.Max(.1f, drop - 2f);
        if (chain != null)
        {
            chain.localPosition = new Vector3(0f, -len * .5f, 0f);
            chain.localScale = new Vector3(.06f, len * .5f, .06f);
        }
    }

    // ------------------------------------------------------------------ room rules

    private static void Torches(Ctx ctx, int max)
    {
        int n = 0;
        foreach (Sample s in ctx.floors)
        {
            if (n >= max) break;
            if (!s.hasWall) continue;
            Vector3 n0 = s.wallNormal;
            Vector3 mount = new Vector3(s.wallPoint.x, s.p.y + 2.6f, s.wallPoint.z);
            // Wall must be tall and flat here.
            if (!SolidRay(mount + n0 * .6f, -n0, 1.2f, out RaycastHit h)) continue;
            if (Vector3.Dot(new Vector3(h.normal.x, 0f, h.normal.z).normalized, n0) < .95f || Mathf.Abs(h.normal.y) > .2f) continue;
            if (Physics.Raycast(mount + n0 * .05f, Vector3.up, .8f, ~0, QueryTriggerInteraction.Ignore)) continue;
            Vector3 pos = h.point + n0 * .01f;
            bool nearOld = false;
            foreach (Vector3 t in ctx.torches) if ((t - pos).sqrMagnitude < 36f) { nearOld = true; break; }
            if (nearOld || !Clear(ctx, pos, 1f, 0f)) continue;
            Place(ctx, "Torch", "Torches", pos, Quaternion.LookRotation(n0), 1f, false, false, .5f);
            ctx.torches.Add(pos);
            n++;
        }
    }

    private static void Shelves(Ctx ctx, int max)
    {
        int n = 0;
        foreach (Sample s in ctx.floors)
        {
            if (n >= max) break;
            if (!s.hasWall || s.wallCount > 2) continue;
            Vector3 nn = s.wallNormal;
            Vector3 t = Vector3.Cross(Vector3.up, nn);
            // Straight wall for the full shelf width.
            bool straight = true;
            foreach (float k in new[] { -1.35f, 1.35f })
            {
                Vector3 from = s.wallPoint + t * k + nn * .8f + Vector3.up * (s.p.y + 1.2f - s.wallPoint.y);
                if (!SolidRay(from, -nn, 1.2f, out RaycastHit h) ||
                    Vector3.Dot(new Vector3(h.normal.x, 0f, h.normal.z).normalized, nn) < .93f) { straight = false; break; }
            }
            if (!straight) continue;
            Vector3 basePos = new Vector3(s.wallPoint.x, s.p.y, s.wallPoint.z) + nn * .5f;
            if (!FloorAt(basePos, s.p.y, out Vector3 floor) || Mathf.Abs(floor.y - s.p.y) > .2f) continue;
            // Room to walk in front: 5 m of open floor.
            if (Physics.Raycast(floor + Vector3.up, nn, 5.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!FloorAt(floor + nn * 4f, floor.y, out _)) continue;
            Quaternion rot = Quaternion.LookRotation(nn);
            if (!Free(floor + Vector3.up * 1.55f, new Vector3(1.38f, 1.4f, .44f), rot)) continue;
            if (!Clear(ctx, floor, 3f, 2.9f)) continue;
            GameObject shelf = Place(ctx, "ArchiveShelf", "Shelves", floor, rot, 1f, true, true, 1.45f);
            n++;
            if (shelf != null && ctx.Chance(.45f))
            {
                // A book left open on the floor beside it.
                Vector3 bp = floor + nn * ctx.R(.9f, 1.4f) + t * ctx.R(-1.2f, 1.2f);
                if (FloorAt(bp, floor.y, out Vector3 bf))
                {
                    GameObject book = Place(ctx, "FloatingBook", "Shelves", bf, Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), ctx.R(.75f, .95f), false, true, .3f);
                    if (book != null) Still(book);
                }
            }
        }
    }

    private static void Corners(Ctx ctx, int max)
    {
        int n = 0;
        foreach (Sample s in ctx.floors)
        {
            if (n >= max) break;
            if (!s.hasWall || s.wallCount < 3) continue;
            Vector3 pos = s.p + s.wallNormal * .25f;
            if (!Clear(ctx, pos, 2.2f, 5f)) continue;
            int pick = ctx.rng.Next(3);
            Quaternion yaw = Quaternion.Euler(0f, ctx.R(0f, 360f), 0f);
            if (pick == 0)
            {
                float sc = ctx.R(.6f, 1f);
                if (!Free(pos + Vector3.up * .8f * sc, new Vector3(.5f, .7f, .5f) * sc, yaw)) continue;
                GameObject c = Place(ctx, "CrystalCluster", "Corners", pos, yaw, sc, sc > .75f, true, 1f);
                if (c != null) Light(c, n % 2 == 0);
            }
            else if (pick == 1)
            {
                Place(ctx, "Rubble", "Corners", pos, yaw, ctx.R(.45f, .75f), false, true, 1f);
                if (ctx.Chance(.5f)) Place(ctx, "Pebbles", "Corners", pos + s.wallNormal * .9f + Vector3.Cross(Vector3.up, s.wallNormal) * ctx.R(-.8f, .8f), yaw, ctx.R(.4f, .7f), false, true, .5f);
            }
            else Place(ctx, "Pebbles", "Corners", pos, yaw, ctx.R(.5f, .9f), false, true, 1f);
            n++;
        }
    }

    /// <summary>Open books drifting overhead in open floor areas — well above jump height, no colliders.</summary>
    private static void FloatingBooks(Ctx ctx, int max)
    {
        int n = 0;
        foreach (Sample s in ctx.floors)
        {
            if (n >= max) break;
            if (s.hasWall || (!float.IsInfinity(s.ceiling) && s.ceiling < 5.5f)) continue;
            bool open = true;
            for (int i = 0; i < 4 && open; i++)
                if (Physics.Raycast(s.p + Vector3.up * 3.5f, Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward, 2.5f, ~0, QueryTriggerInteraction.Ignore)) open = false;
            if (!open) continue;
            float top = float.IsInfinity(s.ceiling) ? 6f : Mathf.Min(6f, s.ceiling - 1f);
            Vector3 pos = s.p + Vector3.up * ctx.R(3.8f, Mathf.Max(3.9f, top));
            if (!Clear(ctx, pos, -1.5f, 4f)) continue;
            Place(ctx, "FloatingBook", "FloatingBooks", pos, Quaternion.Euler(ctx.R(-25f, 25f), ctx.R(0f, 360f), ctx.R(-15f, 15f)), ctx.R(.9f, 1.3f), false, false, 2f);
            n++;
        }
    }

    private static void Chandeliers(Ctx ctx, int max)
    {
        int n = 0;
        foreach (Sample s in ctx.floors)
        {
            if (n >= max) break;
            if (float.IsInfinity(s.ceiling) || s.ceiling < 7.5f || s.ceiling > 45f || s.ceilingNormal.y > -.7f) continue;
            bool open = true;
            for (int i = 0; i < 4 && open; i++)
                if (Physics.Raycast(s.p + Vector3.up * 2f, Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward, 4f, ~0, QueryTriggerInteraction.Ignore)) open = false;
            if (!open) continue;
            Vector3 hook = s.ceilingPoint;
            Vector3 key = new Vector3(hook.x, s.p.y, hook.z);
            if (!Clear(ctx, key, -2f, 11f)) continue;
            float bottom = Mathf.Max(5.5f, s.ceiling * .5f);
            float drop = s.ceiling - bottom;
            if (drop < 2.6f) continue;
            GameObject ch = Place(ctx, "Chandelier", "Chandeliers", hook, Quaternion.Euler(0f, ctx.R(0f, 90f), 0f), 1f, false, false, 5f);
            if (ch != null) SetChandelierDrop(ch, drop);
            // Medallion as the ceiling rose (flat ceilings only).
            if (s.ceilingNormal.y > -.95f) { n++; continue; }
            Quaternion down = Quaternion.Euler(90f, 0f, 0f);
            Place(ctx, "Medallion", "Chandeliers", hook - Vector3.up * .28f - down * new Vector3(0f, 1.488f, 0f) * .8f, down, .8f, false, false, .1f);
            n++;
        }
    }

    /// <summary>Deep pits/chasms: glowing rune rings and drifting books well below the walkable level.</summary>
    private static void Abyss(Ctx ctx, int max)
    {
        if (ctx.floors.Count == 0) return;
        var ys = new List<float>();
        foreach (Sample s in ctx.floors) ys.Add(s.p.y);
        ys.Sort();
        float main = ys[ys.Count / 2];
        int n = 0;
        foreach (Sample s in ctx.floors)
        {
            if (n >= max) break;
            if (s.p.y > main - 6f) continue; // only samples at the bottom of a pit
            Vector3 pos = s.p;
            if (!Clear(ctx, pos, 1f, 6f)) continue;
            if (ctx.Chance(.5f))
            {
                GameObject c = Place(ctx, "CrystalCluster", "Abyss", pos, Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), ctx.R(1.2f, 2.2f), false, true, 3f);
                if (c != null) Light(c, n % 3 == 0);
            }
            else
            {
                Place(ctx, "RuneRing", "Abyss", pos + Vector3.up * ctx.R(.5f, 2f), Quaternion.Euler(ctx.R(-20f, 20f), ctx.R(0f, 360f), 0f), ctx.R(.8f, 1.4f), false, false, 3f);
            }
            n++;
        }
    }

    // ------------------------------------------------------------------ gameplay-aware rules

    private static void SwapPickups(Scene scene, Ctx ctx)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            foreach (RuleCardPickup pickup in go.GetComponentsInChildren<RuleCardPickup>(true))
            {
                Renderer visual = null;
                foreach (Renderer r in pickup.GetComponentsInChildren<Renderer>(true))
                    if (r.name.EndsWith("_CardVisual")) { visual = r; break; }
                if (visual == null || !ctx.prefabs.ContainsKey("RuleCard")) continue;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(ctx.prefabs["RuleCard"], scene);
                model.name = "DECOR_CardModel";
                model.transform.SetParent(pickup.transform, false);
                float h = visual.bounds.size.y;
                model.transform.position = visual.bounds.center - Vector3.up * h * .5f;
                model.transform.rotation = visual.transform.rotation;
                model.transform.localScale = Vector3.one * h;
                var motion = model.AddComponent<DecorMotion>();
                motion.spin = new Vector3(0f, 35f, 0f);
                Material tinted = CardMaterial(pickup.cardId);
                var renderers = new List<Renderer>();
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    if (tinted != null) r.sharedMaterial = tinted;
                    renderers.Add(r);
                }
                visual.enabled = false;
                var so = new SerializedObject(pickup);
                SerializedProperty list = so.FindProperty("glowRenderers");
                list.arraySize = renderers.Count;
                for (int i = 0; i < renderers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                ctx.count++;
            }
    }

    public static Material CardMaterial(string cardId)
    {
        Color c;
        switch (cardId)
        {
            case "KineticLink": c = new Color(1f, .3f, .85f); break;
            case "InvertGravity": c = new Color(.6f, .45f, 1f); break;
            case "SolidifyAir": c = new Color(.3f, .95f, 1f); break;
            default: return null;
        }
        string path = $"{RuleCardsArtTools.MaterialFolder}/MAT_ART_RuleCard_{cardId}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var source = AssetDatabase.LoadAssetAtPath<Material>($"{RuleCardsArtTools.MaterialFolder}/MAT_ART_RuleCard.mat");
        if (source == null) return null;
        if (mat == null)
        {
            mat = new Material(source);
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.CopyPropertiesFromMaterial(source);
        mat.SetColor("_BaseColor", Color.Lerp(Color.white, c, .3f));
        mat.SetColor("_EmissionColor", c * 3f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void PortalGuardians(Scene scene, Ctx ctx)
    {
        var portals = new List<Transform>();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            foreach (StagePortal p in go.GetComponentsInChildren<StagePortal>()) portals.Add(p.transform);
            foreach (StageReturn p in go.GetComponentsInChildren<StageReturn>()) portals.Add(p.transform);
        }
        foreach (Transform portal in portals)
        {
            Vector3 f = ctx.centre - portal.position;
            f.y = 0f;
            if (f.sqrMagnitude < 1f) f = portal.forward;
            f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            float half = 2.6f;
            var col = portal.GetComponentInChildren<Collider>();
            if (col != null) half = Mathf.Max(Mathf.Abs(Vector3.Dot(col.bounds.extents, new Vector3(Mathf.Abs(r.x), 0f, Mathf.Abs(r.z)))), 1.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 p = portal.position + r * side * (half + 1.3f) + f * .4f;
                if (!FloorAt(p, portal.position.y, out Vector3 floor)) continue;
                Quaternion rot = Quaternion.LookRotation(f);
                if (!Free(floor + Vector3.up * 1.7f, new Vector3(.6f, 1.5f, .5f), rot)) continue;
                Place(ctx, "GuardianStatue", "Guardians", floor, rot, 1f, true, true, 1f);
                // A brazier just in front of each statue.
                Vector3 bp = floor + f * 1.3f - r * side * .2f;
                if (FloorAt(bp, floor.y, out Vector3 bf) && Free(bf + Vector3.up * .6f, new Vector3(.3f, .5f, .3f), rot))
                    Place(ctx, "Brazier", "Guardians", bf, rot, 1f, true, false, .6f);
            }
            ctx.keepOut.Add(new Vector4(portal.position.x, portal.position.y, portal.position.z, half + 2f));
        }
    }

    private static void SafetyRailings(Scene scene, Ctx ctx)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name != "Navigation_SafetyColliders") continue;
            foreach (BoxCollider box in go.GetComponentsInChildren<BoxCollider>())
            {
                Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
                if (size.y > 3.2f) continue;
                Vector3 axis = size.x >= size.z ? box.transform.right : box.transform.forward;
                float length = Mathf.Max(size.x, size.z);
                Vector3 c = box.transform.TransformPoint(box.center);
                if (!SolidRay(c + Vector3.up * 2f, Vector3.down, 6f, out RaycastHit h, box)) continue;
                int segments = Mathf.Max(1, Mathf.RoundToInt(length / 2f));
                float seg = length / segments;
                Quaternion rot = Quaternion.LookRotation(Vector3.Cross(axis, Vector3.up));
                for (int i = 0; i < segments; i++)
                {
                    Vector3 p = new Vector3(c.x, h.point.y, c.z) + axis * ((i + .5f) * seg - length * .5f);
                    GameObject rail = Place(ctx, "Railing", "Railings", p, rot, 1f, false, true, .1f);
                    if (rail != null) rail.transform.localScale = new Vector3(seg / 2f, 1.1f, 1f);
                }
            }
        }
    }

    // ------------------------------------------------------------------ title diorama

    private static void Title(Ctx ctx)
    {
        Camera cam = Object.FindObjectOfType<Camera>();
        Vector3 o = cam != null ? cam.transform.position : new Vector3(0f, 1f, -10f);
        Vector3 f = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        Quaternion face = Quaternion.LookRotation(-f);      // looking back at the camera
        System.Func<float, float, float, Vector3> Pt = (x, y, z) => o + r * x + Vector3.up * y + f * z;
        float g = -1.6f; // ground relative to camera

        // Ground: a dark polished disc that catches the glow.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Ground";
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.SetParent(Group(ctx, "Diorama"), false);
        floor.transform.position = Pt(0f, g - .05f, 14f);
        floor.transform.localScale = new Vector3(40f, .05f, 40f);
        floor.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

        GameObject ring = Place(ctx, "RuneRing", "Diorama", Pt(0f, g + .2f, 17f), face, 2.4f, false, false);
        GameObject card = Place(ctx, "RuleCard", "Diorama", Pt(0f, g + 2.6f, 16.6f), face, 2.6f, false, false);
        if (card != null)
        {
            var m = card.AddComponent<DecorMotion>();
            m.bobHeight = .25f; m.bobSpeed = .6f; m.spin = new Vector3(0f, 22f, 0f);
        }
        AddPointLight(ring != null ? ring.transform : ctx.root, Pt(0f, g + 3.5f, 15.5f), new Color(.35f, .95f, 1f), 10f, 18f);
        AddPointLight(ctx.root, Pt(0f, g + 6f, 6f), new Color(.75f, .85f, 1f), 4f, 20f);
        foreach (float s in new[] { -1f, 1f })
        {
            Place(ctx, "GuardianStatue", "Diorama", Pt(s * 6.2f, g, 13.5f), Quaternion.LookRotation(-f + r * s * -.35f), 1.15f);
            Place(ctx, "Brazier", "Diorama", Pt(s * 3.6f, g, 9.5f), face, 1f, false, false);
            for (int i = 0; i < 4; i++)
                Place(ctx, "Pillar", "Diorama", Pt(s * (9.5f + i * .8f), g, 8f + i * 7.5f), Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), 1.35f);
            Place(ctx, "ArchiveShelf", "Diorama", Pt(s * 5.6f, g, 22f), Quaternion.LookRotation(-f + r * s * .6f), 1.1f);
            Place(ctx, "ArchiveShelf", "Diorama", Pt(s * 8.4f, g, 19.5f), Quaternion.LookRotation(-f + r * s * .9f), 1.1f);
            GameObject crystal = Place(ctx, "CrystalCluster", "Diorama", Pt(s * 7.6f, g, 7.5f), Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), 1.2f);
            if (crystal != null) Light(crystal, true);
            Place(ctx, "CrystalCluster", "Diorama", Pt(s * 2.4f, g, 18.5f), Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), .8f);
            Place(ctx, "Rubble", "Diorama", Pt(s * 4.6f, g, 6.5f), Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), .7f);
            Place(ctx, "Pebbles", "Diorama", Pt(s * 1.8f, g, 7.2f), Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), .8f);
        }
        for (int i = 0; i < 10; i++)
        {
            float x = ctx.R(-7f, 7f);
            if (Mathf.Abs(x) < 2f) x += Mathf.Sign(x) * 2f;
            Place(ctx, "FloatingBook", "Diorama", Pt(x, g + ctx.R(1.4f, 5f), ctx.R(6f, 15f)), Quaternion.Euler(ctx.R(-25f, 25f), ctx.R(0f, 360f), ctx.R(-15f, 15f)), ctx.R(.8f, 1.2f), false, false);
        }
        GameObject ch = Place(ctx, "Chandelier", "Diorama", Pt(0f, g + 11f, 12f), Quaternion.identity, 1.1f, false, false);
        if (ch != null) SetChandelierDrop(ch, 4.5f);

        // Atmosphere for the title only.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.012f, .03f, .045f);
        RenderSettings.fogDensity = .03f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.16f, .2f, .26f);
        RenderSettings.reflectionIntensity = .2f; // the default sky reflection washes the polished floor out
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            EditorUtility.SetDirty(cam);
        }
        foreach (Light l in Object.FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { l.intensity = .7f; l.color = new Color(.6f, .8f, 1f); EditorUtility.SetDirty(l); }
    }

    private static Material GroundMaterial()
    {
        string path = RuleCardsArtTools.MaterialFolder + "/MAT_ART_PolishedFloor.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", new Color(.035f, .045f, .055f));
        mat.SetFloat("_Metallic", .3f);
        mat.SetFloat("_Smoothness", .7f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void AddPointLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject("DecorLight");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    // ------------------------------------------------------------------ Stage 4: the unwritten archive

    private static void Escape(Ctx ctx)
    {
        // Everything walkable collapses, so nothing sits on the route: the archive drifts around it.
        var route = new List<Vector3>();
        foreach (Sample s in ctx.floors) route.Add(s.p);
        System.Func<Vector3, float> Dist = p =>
        {
            float best = float.MaxValue;
            foreach (Vector3 q in route)
            {
                float d = (new Vector2(p.x, p.z) - new Vector2(q.x, q.z)).sqrMagnitude;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        };
        System.Func<Vector3, float> RouteY = p =>
        {
            float best = float.MaxValue, y = 0f;
            foreach (Vector3 q in route)
            {
                float d = (new Vector2(p.x, p.z) - new Vector2(q.x, q.z)).sqrMagnitude;
                if (d < best) { best = d; y = q.y; }
            }
            return y;
        };
        Bounds b = ctx.bounds;
        float minX = b.min.x - 40f, maxX = b.max.x + 40f, minZ = b.min.z - 30f, maxZ = b.max.z + 30f;
        System.Func<int, int, float, float, System.Func<Vector3, bool>, int> Scatter = (want, tries, dMin, dMax, place) =>
        {
            int n = 0;
            for (int i = 0; i < tries && n < want; i++)
            {
                Vector3 p = new Vector3(ctx.R(minX, maxX), 0f, ctx.R(minZ, maxZ));
                float d = Dist(p);
                if (d < dMin || d > dMax) continue;
                p.y = RouteY(p);
                if (place(p)) n++;
            }
            return n;
        };

        // Colossal pillars rising out of the abyss.
        Scatter(26, 2000, 14f, 45f, p =>
        {
            float sc = ctx.R(2.2f, 3.4f);
            Vector3 pos = p + Vector3.up * ctx.R(-38f, -18f);
            if (!Clear(ctx, pos, 0f, 9f)) return false;
            GameObject go = Place(ctx, "Pillar", "Background", pos, Quaternion.Euler(ctx.R(-6f, 6f), ctx.R(0f, 360f), ctx.R(-6f, 6f)), sc, false, false, 5f);
            if (go != null) go.transform.localScale = new Vector3(sc, sc * ctx.R(1.4f, 2.2f), sc);
            return go != null;
        });
        // Great rune rings turning slowly in the distance.
        Scatter(12, 1500, 18f, 50f, p =>
        {
            Vector3 pos = p + Vector3.up * ctx.R(-6f, 16f);
            if (!Clear(ctx, pos, 0f, 14f)) return false;
            return Place(ctx, "RuneRing", "Background", pos, Quaternion.Euler(ctx.R(-25f, 25f), ctx.R(0f, 360f), 0f), ctx.R(2.5f, 5f), false, false, 7f) != null;
        });
        // Drifting shelves and bridge fragments: the archive coming apart.
        Scatter(22, 2000, 9f, 32f, p =>
        {
            Vector3 pos = p + Vector3.up * ctx.R(-14f, 10f);
            if (!Clear(ctx, pos, 0f, 6f)) return false;
            bool shelf = ctx.Chance(.55f);
            GameObject go = Place(ctx, shelf ? "ArchiveShelf" : "BrokenBridge", "Drift", pos,
                Quaternion.Euler(ctx.R(-35f, 35f), ctx.R(0f, 360f), ctx.R(-35f, 35f)), shelf ? ctx.R(.9f, 1.4f) : ctx.R(1.2f, 2.2f), false, false, 3f);
            if (go == null) return false;
            var m = go.AddComponent<DecorMotion>();
            m.spin = new Vector3(ctx.R(-3f, 3f), ctx.R(-6f, 6f), ctx.R(-3f, 3f));
            m.bobHeight = ctx.R(.3f, .9f); m.bobSpeed = ctx.R(.15f, .35f);
            return true;
        });
        // Rubble islands carrying statues.
        Scatter(7, 1500, 16f, 34f, p =>
        {
            Vector3 pos = p + Vector3.up * ctx.R(-10f, 4f);
            if (!Clear(ctx, pos, 0f, 14f)) return false;
            float sc = ctx.R(4f, 6f);
            GameObject island = Place(ctx, "Rubble", "Islands", pos, Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), sc, false, false, 8f);
            if (island == null) return false;
            float top = .74f * sc - .25f;
            GameObject statue = Place(ctx, ctx.Chance(.5f) ? "GuardianStatue" : "ScribeStatue", "Islands", pos + Vector3.up * top,
                Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), ctx.R(1.1f, 1.5f), false, false, .1f);
            if (statue != null) statue.transform.SetParent(island.transform, true);
            GameObject crystal = Place(ctx, "CrystalCluster", "Islands", pos + Vector3.up * top + new Vector3(ctx.R(-1.5f, 1.5f), 0f, ctx.R(-1.5f, 1.5f)),
                Quaternion.Euler(0f, ctx.R(0f, 360f), 0f), ctx.R(.8f, 1.3f), false, false, .1f);
            if (crystal != null) { crystal.transform.SetParent(island.transform, true); Light(crystal, true); }
            var m = island.AddComponent<DecorMotion>();
            m.bobHeight = ctx.R(.4f, .8f); m.bobSpeed = ctx.R(.12f, .25f); m.spin = new Vector3(0f, ctx.R(-2f, 2f), 0f);
            return true;
        });
        // Loose pages of the archive close to the route.
        Scatter(34, 3000, 4.5f, 14f, p =>
        {
            Vector3 pos = p + Vector3.up * ctx.R(-3f, 5f);
            if (!Clear(ctx, pos, 0f, 3f)) return false;
            return Place(ctx, "FloatingBook", "Pages", pos, Quaternion.Euler(ctx.R(-30f, 30f), ctx.R(0f, 360f), ctx.R(-20f, 20f)), ctx.R(.9f, 1.4f), false, false, 1f) != null;
        });

        EscapeSwaps(ctx);
    }

    private static void EscapeSwaps(Ctx ctx)
    {
        Scene scene = ctx.root.gameObject.scene;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (EscapeAnchor anchor in root.GetComponentsInChildren<EscapeAnchor>(true))
            {
                if (!ctx.prefabs.ContainsKey("KineticAnchor")) break;
                Transform t = anchor.transform;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(ctx.prefabs["KineticAnchor"], scene);
                model.name = "DECOR_AnchorModel";
                model.transform.SetParent(t, false);
                float s = t.lossyScale.x;
                model.transform.localRotation = Quaternion.Inverse(t.rotation);
                model.transform.localPosition = Quaternion.Inverse(t.rotation) * new Vector3(0f, -1.0f, 0f) / s;
                model.transform.localScale = Vector3.one * (1.25f / s);
                var r = t.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                ctx.count++;
            }
            Transform exit = FindDeep(root.transform, "Escape_Exit_Lintel");
            if (exit != null && ctx.prefabs.ContainsKey("ExitGate"))
            {
                Transform group = exit.parent;
                Transform postL = group.Find("Escape_Exit_PostL");
                float baseY = postL != null ? postL.position.y - postL.lossyScale.y * .5f : exit.position.y - 6.2f;
                GameObject gate = Place(ctx, "ExitGate", "Exit", new Vector3(exit.position.x, baseY, exit.position.z), Quaternion.identity, 2.05f, false, true, 1f);
                if (gate != null)
                {
                    foreach (string n in new[] { "Escape_Exit_PostL", "Escape_Exit_PostR", "Escape_Exit_Lintel" })
                    {
                        Transform part = group.Find(n);
                        var pr = part != null ? part.GetComponent<Renderer>() : null;
                        if (pr != null) pr.enabled = false;
                    }
                    foreach (float side in new[] { -1f, 1f })
                        if (FloorAt(new Vector3(exit.position.x + side * 6.4f, baseY, exit.position.z + 1.5f), baseY, out Vector3 sf))
                            Place(ctx, "GuardianStatue", "Exit", sf, Quaternion.Euler(0f, side * -20f, 0f), 1.6f, false, true, .5f);
                }
            }
        }
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    // ------------------------------------------------------------------ capture

    [MenuItem("RuleCards/Art/Capture Scene Views (all)", false, 261)]
    public static void CaptureAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtDump/shots"));
        Directory.CreateDirectory(folder);
        string previous = SceneManager.GetActiveScene().path;
        foreach (string path in RuleCardsArtTools.Scenes)
        {
            if (!File.Exists(path)) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Capture(scene, folder);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[RuleCards Decor] Captures written to " + folder);
    }

    private static void Capture(Scene scene, string folder)
    {
        Camera player = null;
        foreach (GameObject go in scene.GetRootGameObjects())
            foreach (Camera c in go.GetComponentsInChildren<Camera>())
                if (c.enabled && c.gameObject.activeInHierarchy && (player == null || c.CompareTag("MainCamera"))) player = c;
        var views = new List<(Vector3 pos, Quaternion rot, float fov)>();
        if (player != null)
            for (int i = 0; i < 2; i++)
                views.Add((player.transform.position, Quaternion.Euler(0f, player.transform.eulerAngles.y + i * 180f, 0f) * Quaternion.Euler(8f, 0f, 0f), 70f));
        // Walk-through views: floor samples spread across the level, each looking down its longest sightline.
        var ctx = new Ctx { scene = scene.name, rng = new System.Random(7) };
        Analyse(scene, ctx, scene.name != "Stage_04_Escape");
        var picks = new List<Vector3>();
        Vector3 seed = player != null ? player.transform.position : ctx.centre;
        for (int k = 0; k < 6 && ctx.floors.Count > 0; k++)
        {
            Vector3 best = Vector3.zero;
            float bestD = -1f;
            foreach (Sample s in ctx.floors)
            {
                if (s.hasWall) continue;
                float d = (s.p - seed).sqrMagnitude;
                foreach (Vector3 q in picks) d = Mathf.Min(d, (s.p - q).sqrMagnitude);
                if (d > bestD) { bestD = d; best = s.p; }
            }
            if (bestD < 0f) break;
            picks.Add(best);
        }
        // Points of interest: the Escape exit gate and the kinetic anchors.
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform exit = FindDeep(root.transform, "Escape_Exit_Lintel");
            if (exit != null)
            {
                Vector3 eye = exit.position + new Vector3(3f, -2.5f, 16f);
                views.Add((eye, Quaternion.LookRotation(exit.position + Vector3.down * 3f - eye), 60f));
            }
            foreach (EscapeAnchor a in root.GetComponentsInChildren<EscapeAnchor>())
            {
                Vector3 eye = a.transform.position + new Vector3(5f, 1.5f, 5f);
                views.Add((eye, Quaternion.LookRotation(a.transform.position - eye), 55f));
            }
        }
        foreach (Vector3 p in picks)
        {
            Vector3 eye = p + Vector3.up * 1.7f;
            float longest = -1f;
            Vector3 dir = Vector3.forward;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                float len = Physics.Raycast(eye, d, out RaycastHit h, 200f, ~0, QueryTriggerInteraction.Ignore) ? h.distance : 200f;
                if (len > longest) { longest = len; dir = d; }
            }
            views.Add((eye, Quaternion.LookRotation(dir) * Quaternion.Euler(6f, 0f, 0f), 75f));
        }
        var go2 = new GameObject("CaptureCam");
        var cam = go2.AddComponent<Camera>();
        var camData = go2.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        var volume = go2.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        UnityEngine.Rendering.VolumeProfile profile = RuleCardsPostFX.CreateProfile(scene.name);
        volume.sharedProfile = profile;
        if (player != null) { cam.clearFlags = player.clearFlags; cam.backgroundColor = player.backgroundColor; cam.farClipPlane = player.farClipPlane; }
        var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        for (int i = 0; i < views.Count; i++)
        {
            cam.transform.SetPositionAndRotation(views[i].pos, views[i].rot);
            cam.fieldOfView = views[i].fov;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(folder, $"{scene.name}_{i}.png"), tex.EncodeToPNG());
        }
        Object.DestroyImmediate(tex);
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go2);
        Object.DestroyImmediate(profile);
    }
}

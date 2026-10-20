using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The three card plinths in the hub (CardArchive_Sigil_0..2 on CardArchive_Pedestal_0..2).
/// A recovered Rule Card replaces its glowing placeholder slab with the sculpted card model, tinted in the
/// card's colour, slowly turning above the pedestal. Slots still empty stay as a dim ghost slab.
/// The first time a card is shown it rises out of the pedestal with a chime.
/// Installs itself whenever the hub loads; no scene edits.
/// </summary>
public sealed class RuleCardsHubDisplay : MonoBehaviour
{
    public const string ShownPrefix = "RuleCards.Hub.CardShown.";

    private sealed class Slot
    {
        public Transform root;
        public Vector3 basePosition;
        public float phase, appear = 1f, appearDelay;
        public bool fresh;
        public Light light;
        public Color color;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly List<Object> owned = new List<Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != RuleCardsScenes.Hub) return;
        var go = new GameObject("RuleCards_HubCardDisplay");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<RuleCardsHubDisplay>();
    }

    /// <summary>Called by RuleCardsProgress.ResetAll so a new run plays the reveal again.</summary>
    public static void ResetShown()
    {
        foreach (string id in RuleCardInfo.Ids) PlayerPrefs.DeleteKey(ShownPrefix + id);
    }

    private void Start()
    {
        var byName = new Dictionary<string, Transform>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName.Add(t.name, t);

        GameObject model = Resources.Load<GameObject>("RuleCardsHeldCard");
        float delay = 1.6f;
        for (int i = 0; i < RuleCardInfo.Count; i++)
        {
            Transform sigil;
            if (!byName.TryGetValue("CardArchive_Sigil_" + i, out sigil)) continue;
            Renderer slab = sigil.GetComponent<Renderer>();
            string id = RuleCardInfo.Ids[i];
            Color color = RuleCardInfo.Colors[i];

            if (!RuleCardsProgress.HasCard(id) || model == null || slab == null)
            {
                Dim(slab);
                continue;
            }

            // The recovered card, at the slab's height, standing where the slab stood.
            float height = sigil.lossyScale.y;
            var root = new GameObject("HubCard_" + id).transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(slab.bounds.center, Quaternion.Euler(0f, sigil.eulerAngles.y, 0f));
            GameObject card = Instantiate(model, root, false);
            card.name = "Model";
            foreach (Collider c in card.GetComponentsInChildren<Collider>()) Destroy(c);
            card.transform.localScale = Vector3.one * height;
            card.transform.localPosition = new Vector3(0f, -height * .5f, 0f);
            foreach (Renderer r in card.GetComponentsInChildren<Renderer>())
            {
                Material m = r.material;
                owned.Add(m);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(Color.white, color, .35f));
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    Color e = m.GetColor("_EmissionColor");
                    if (e.maxColorComponent < .05f) e = Color.white;
                    m.SetColor("_EmissionColor", new Color(e.r * color.r, e.g * color.g, e.b * color.b) * 2.4f);
                }
            }
            slab.enabled = false;

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(0f, 0f, .6f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 4f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;

            var slot = new Slot { root = root, basePosition = root.position, phase = i * 1.7f, light = light, color = color };
            if (PlayerPrefs.GetInt(ShownPrefix + id, 0) == 0)
            {
                // First visit since recovering it: the card rises out of the pedestal.
                slot.fresh = true;
                slot.appear = 0f;
                slot.appearDelay = delay;
                delay += .9f;
                PlayerPrefs.SetInt(ShownPrefix + id, 1);
                PlayerPrefs.Save();
            }
            slots.Add(slot);
            Apply(slot, 0f);
        }
    }

    private void Dim(Renderer slab)
    {
        if (slab == null) return;
        Material m = slab.material;
        owned.Add(m);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * .45f);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", m.GetColor("_EmissionColor") * .3f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (Slot s in slots)
        {
            if (s.appear < 1f)
            {
                if (s.appearDelay > 0f) { s.appearDelay -= dt; if (s.appearDelay <= 0f) RuleCardsAudio.PlayAt("pickup", s.basePosition, .7f, .9f); }
                else s.appear = Mathf.MoveTowards(s.appear, 1f, dt / 1.4f);
            }
            Apply(s, dt);
        }
    }

    private void Apply(Slot s, float dt)
    {
        if (s.root == null) return;
        float t = Time.time + s.phase;
        float a = Mathf.SmoothStep(0f, 1f, s.appear);
        // Rises out of the pedestal, overshooting a little, then bobs and turns slowly.
        float rise = Mathf.Lerp(-1.1f, 0f, a) + Mathf.Sin(a * Mathf.PI) * .12f;
        s.root.position = s.basePosition + Vector3.up * (rise + Mathf.Sin(t * 1.1f) * .06f);
        s.root.Rotate(0f, (24f + (1f - a) * 260f) * dt, 0f, Space.World);
        s.root.localScale = Vector3.one * Mathf.Lerp(.3f, 1f, a);
        if (s.light != null) s.light.intensity = (1.2f + .25f * Mathf.Sin(t * 2.3f)) * a + (s.fresh ? 3f * Mathf.Sin(a * Mathf.PI) : 0f);
    }

    private void OnDestroy()
    {
        foreach (Object o in owned) if (o != null) Destroy(o);
    }
}

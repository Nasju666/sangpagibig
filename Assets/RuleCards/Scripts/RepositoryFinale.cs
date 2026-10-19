using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Final Repository: three Archive Nodes (one per Rule Card), the card convergence finale
/// and the ending screen. Installs itself when Repository_Final loads and finds the existing
/// level objects by name, so the scene file does not need editing. A hand-placed instance in
/// the scene is respected instead (fill the fields in the inspector or leave them for auto-find).
/// </summary>
public sealed class RepositoryFinale : MonoBehaviour
{
    public const string SceneName = "Repository_Final";
    public const string HubScene = "ArchiveHub";

    [Header("Scene references (auto-found by name when empty)")]
    public Transform[] nodes = new Transform[3];
    public Renderer[] suspendedCards = new Renderer[0];
    public GameObject exitRoot;
    public Light coreLight;
    public Renderer exitSeam;

    [Header("Tuning")]
    public float interactDistance = 3.3f;
    public float nodeRiseDepth = 2.4f;

    [Header("Ending text")]
    public string endingTitle = "THE REPOSITORY IS RESTORED";
    [TextArea(2, 5)]
    public string endingBody = "Kinetic Link. Invert Gravity. Solidify Air.\nThree rules recovered and written back into the Repository. But an Archive that has been rewritten cannot hold you for long: the walls are already unwriting themselves. Take the cards and get out.";

    // ------------------------------------------------------------------ bootstrap
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName) return;
        foreach (RepositoryFinale existing in FindObjectsOfType<RepositoryFinale>(true))
            if (existing.gameObject.scene == scene) return;
        var root = new GameObject("Repository_Finale");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<RepositoryFinale>();
    }

    // ------------------------------------------------------------------ data
    private enum NodeState { Hidden, Rising, Ready, Solved }

    private sealed class Node
    {
        public Transform root;
        public Renderer screen;
        public Material screenMaterial;
        public Light glow;
        public NodeState state;
        public Vector3 restLocal;
        public float rise;
        public RepositoryNodePuzzle puzzle;
        public Color color;
        public string numeral;
        public string rule;
    }

    private static readonly string[] Numerals = { "I", "II", "III" };
    private static readonly string[] Rules = { "KINETIC LINK", "INVERT GRAVITY", "SOLIDIFY AIR" };
    private static readonly Color[] RuleColors = {
        new Color(1f, .3f, .85f), new Color(.6f, .5f, 1f), new Color(1f, .85f, .4f)
    };
    private static readonly string[] PausedBehaviours = { "PlayerMovement", "MouseLook", "HeadBob", "CameraFOV", "Footsteps" };

    private readonly Node[] nodeData = new Node[3];
    private CharacterController player;
    private readonly List<Behaviour> paused = new List<Behaviour>();

    private Node openNode, focusNode;
    private float solvedAt = -1f;
    private int pendingReveal = -1;
    private float pendingRevealAt, finaleStartAt = -1f;

    private bool finaleRunning, exitOpen, atExit;
    private float finaleTime, sceneTime, clearTime = -1f;
    private bool newBest;

    private bool endingOpen;
    private float endingTime;
    private bool confirmReset;
    private int endingOpenedFrame;

    private string banner = "";
    private float bannerUntil;

    // cards
    private Material[] cardMaterials = new Material[0];
    private Vector3[] cardStartPos = new Vector3[0];
    private Quaternion[] cardStartRot = new Quaternion[0];
    private Vector3 orbitCenter;
    private float coreBaseIntensity;
    private Color coreBaseColor;
    private Material seamMaterial;
    private GameObject beam;
    private Material beamMaterial;
    private Light beamLight;

    // audio
    private AudioSource audioSource;
    private AudioClip chimeClip, riseClip, finaleClip;
    private readonly List<Object> owned = new List<Object>();

    // ------------------------------------------------------------------ setup
    private void Awake()
    {
        AutoFind();
        DisablePlaceholderLogic();
        SetupNodes();
        SetupCards();
        SetupAudio();
    }

    private void Start()
    {
        player = FindObjectOfType<CharacterController>();
    }

    private void AutoFind()
    {
        Scene scene = gameObject.scene;
        var byName = new Dictionary<string, Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName.Add(t.name, t);

        string[] nodeNames = { "ArchiveNode_A", "ArchiveNode_B", "ArchiveNode_C" };
        if (nodes == null || nodes.Length != 3) nodes = new Transform[3];
        for (int i = 0; i < 3; i++)
            if (nodes[i] == null) byName.TryGetValue(nodeNames[i], out nodes[i]);

        if (suspendedCards == null || suspendedCards.Length == 0)
        {
            var cards = new List<Renderer>();
            for (int i = 0; i < 32; i++)
            {
                Transform t;
                if (byName.TryGetValue("SuspendedRuleCard_" + i, out t) && t.GetComponent<Renderer>() != null)
                    cards.Add(t.GetComponent<Renderer>());
            }
            suspendedCards = cards.ToArray();
        }

        Transform found;
        if (exitRoot == null && byName.TryGetValue("Repository_ExitUnlocked", out found)) exitRoot = found.gameObject;
        if (coreLight == null && byName.TryGetValue("Repository_CoreLight", out found)) coreLight = found.GetComponent<Light>();
        if (exitSeam == null && byName.TryGetValue("ExitTeaser_Seam", out found)) exitSeam = found.GetComponent<Renderer>();
    }

    /// <summary>The blockout used TerminalReveal (press E) and an always-ready StageReturn. Both are replaced here.</summary>
    private void DisablePlaceholderLogic()
    {
        foreach (TerminalReveal reveal in FindObjectsOfType<TerminalReveal>(true))
        {
            if (reveal.gameObject.scene != gameObject.scene) continue;
            if (reveal.revealedObjects != null)
                foreach (GameObject item in reveal.revealedObjects) if (item != null) item.SetActive(false);
            reveal.enabled = false;
        }
        if (exitRoot != null)
        {
            foreach (StageReturn stageReturn in exitRoot.GetComponentsInChildren<StageReturn>(true)) stageReturn.enabled = false;
            exitRoot.SetActive(false);
        }
    }

    private void SetupNodes()
    {
        for (int i = 0; i < 3; i++)
        {
            var node = new Node { root = nodes[i], color = RuleColors[i], numeral = Numerals[i], rule = Rules[i] };
            node.puzzle = i == 0 ? (RepositoryNodePuzzle)new LinkMatrixPuzzle()
                : i == 1 ? new GravityDriftPuzzle() : (RepositoryNodePuzzle)new AirBridgePuzzle();
            nodeData[i] = node;
            if (node.root == null) { node.state = NodeState.Hidden; continue; }

            node.restLocal = node.root.localPosition;
            foreach (Renderer r in node.root.GetComponentsInChildren<Renderer>(true))
                if (r.name.EndsWith("_Screen")) { node.screen = r; break; }
            if (node.screen != null)
            {
                node.screenMaterial = node.screen.material; // instance, owned by the renderer
                owned.Add(node.screenMaterial);
                SeatScreen(node);
            }

            var lightObject = new GameObject("Node_" + node.numeral + "_Glow");
            lightObject.transform.SetParent(node.root, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.6f, -1.2f);
            node.glow = lightObject.AddComponent<Light>();
            node.glow.type = LightType.Point;
            node.glow.range = 7f;
            node.glow.intensity = 0f;
            node.glow.color = node.color;
            node.glow.shadows = LightShadows.None;

            if (i == 0)
            {
                node.root.gameObject.SetActive(true);
                node.state = NodeState.Ready;
            }
            else
            {
                node.root.gameObject.SetActive(false);
                node.state = NodeState.Hidden;
            }
        }
    }

    private void SetupCards()
    {
        int n = suspendedCards.Length;
        cardMaterials = new Material[n];
        cardStartPos = new Vector3[n];
        cardStartRot = new Quaternion[n];
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Renderer r = suspendedCards[i];
            cardStartPos[i] = r.transform.position;
            cardStartRot[i] = r.transform.rotation;
            sum += cardStartPos[i];
        }
        orbitCenter = n > 0 ? sum / n : new Vector3(0f, 7f, 12f);
        if (coreLight != null)
        {
            coreBaseIntensity = coreLight.intensity;
            coreBaseColor = coreLight.color;
            orbitCenter.x = coreLight.transform.position.x;
            orbitCenter.z = coreLight.transform.position.z;
        }
    }

    private Material CardMaterial(int i)
    {
        if (cardMaterials[i] == null)
        {
            cardMaterials[i] = suspendedCards[i].material;
            owned.Add(cardMaterials[i]);
        }
        return cardMaterials[i];
    }

    // ------------------------------------------------------------------ update
    private void Update()
    {
        sceneTime += Time.deltaTime;
        if (player == null) player = FindObjectOfType<CharacterController>();
        AnimateNodes(Time.deltaTime);
        AnimateFinale(Time.deltaTime);

        if (pendingReveal >= 0 && Time.time >= pendingRevealAt)
        {
            RevealNode(pendingReveal);
            pendingReveal = -1;
        }
        if (finaleStartAt > 0f && Time.time >= finaleStartAt)
        {
            finaleStartAt = -1f;
            BeginFinale();
        }

        if (endingOpen) { ArchiveModal.MarkOpen(); UpdateEnding(); return; }
        if (openNode != null) { ArchiveModal.MarkOpen(); UpdateConsole(); return; }

        focusNode = null;
        atExit = false;
        if (player == null || MouseLook.isPaused || Time.timeScale == 0f) return;
        focusNode = FindFocusNode();
        atExit = exitOpen && PlayerAtExit();
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (focusNode != null) OpenConsole(focusNode);
            else if (atExit) OpenEnding();
        }
    }

    private Node FindFocusNode()
    {
        Vector3 p = player.transform.position;
        Node best = null;
        float bestDistance = interactDistance;
        foreach (Node node in nodeData)
        {
            if (node == null || node.root == null || node.state != NodeState.Ready) continue;
            Vector3 d = node.root.position - p;
            d.y = 0f;
            if (d.magnitude < bestDistance) { bestDistance = d.magnitude; best = node; }
        }
        return best;
    }

    private bool PlayerAtExit()
    {
        if (exitRoot == null || !exitRoot.activeInHierarchy) return false;
        Collider zone = exitRoot.GetComponent<Collider>();
        Bounds playerBounds = player.bounds;
        if (zone != null)
        {
            Bounds b = zone.bounds;
            b.Expand(1f);
            return b.Intersects(playerBounds);
        }
        Vector3 d = exitRoot.transform.position - player.transform.position;
        d.y = 0f;
        return d.magnitude < 4f;
    }

    // ------------------------------------------------------------------ console
    private void OpenConsole(Node node)
    {
        openNode = node;
        solvedAt = -1f;
        PausePlayer();
    }

    private void CloseConsole()
    {
        openNode = null;
        solvedAt = -1f;
        ResumePlayer();
    }

    private void UpdateConsole()
    {
        RepositoryNodePuzzle puzzle = openNode.puzzle;
        puzzle.Tick(Time.unscaledDeltaTime);
        if (puzzle.Solved)
        {
            if (solvedAt < 0f)
            {
                solvedAt = Time.unscaledTime;
                Play(chimeClip, .4f);
            }
            if (Time.unscaledTime - solvedAt > 1.5f)
            {
                Node solved = openNode;
                CloseConsole();
                OnNodeSolved(System.Array.IndexOf(nodeData, solved));
            }
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape)) CloseConsole();
        else if (Input.GetKeyDown(KeyCode.R)) puzzle.Reset();
    }

    private void PausePlayer()
    {
        paused.Clear();
        if (player != null)
            foreach (MonoBehaviour b in player.GetComponentsInChildren<MonoBehaviour>())
                if (b != null && b.enabled && System.Array.IndexOf(PausedBehaviours, b.GetType().Name) >= 0)
                {
                    paused.Add(b);
                    b.enabled = false;
                }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ResumePlayer()
    {
        foreach (Behaviour b in paused) if (b != null) b.enabled = true;
        paused.Clear();
        // First-person scenes always return to a locked cursor; MouseLook re-locks on enable as well.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ------------------------------------------------------------------ progression
    private void OnNodeSolved(int index)
    {
        if (index < 0) return;
        Node node = nodeData[index];
        node.state = NodeState.Solved;
        for (int i = index; i < suspendedCards.Length; i += 3) Tint(CardMaterial(i), node.color, 2.2f);
        Banner("NODE " + node.numeral + " ALIGNED — " + node.rule);

        int next = index + 1;
        if (next < 3 && nodeData[next].state == NodeState.Hidden)
        {
            pendingReveal = next;
            pendingRevealAt = Time.time + 1.2f;
        }
        bool all = true;
        foreach (Node n in nodeData) if (n.root != null && n.state != NodeState.Solved) all = false;
        if (all) finaleStartAt = Time.time + 1.4f;
    }

    private void RevealNode(int index)
    {
        Node node = nodeData[index];
        if (node.root == null) { node.state = NodeState.Ready; return; }
        node.root.localPosition = node.restLocal - Vector3.up * nodeRiseDepth;
        node.root.gameObject.SetActive(true);
        node.state = NodeState.Rising;
        node.rise = 0f;
        Play(riseClip, .5f);
        Banner("NODE " + node.numeral + " HAS RISEN — FOLLOW ITS LIGHT");
    }

    private void AnimateNodes(float dt)
    {
        float t = Time.time;
        foreach (Node node in nodeData)
        {
            if (node == null || node.root == null) continue;
            switch (node.state)
            {
                case NodeState.Rising:
                    node.rise = Mathf.Clamp01(node.rise + dt / 1.6f);
                    node.root.localPosition = Vector3.Lerp(node.restLocal - Vector3.up * nodeRiseDepth, node.restLocal,
                        Mathf.SmoothStep(0f, 1f, node.rise));
                    SetNodeLook(node, node.color, node.rise * 1.4f, node.rise * 3f);
                    if (node.rise >= 1f) node.state = NodeState.Ready;
                    break;
                case NodeState.Ready:
                    float pulse = .65f + .35f * Mathf.Sin(t * 3.2f);
                    // Kept low: bloom is on now, anything far above 1 blows out to white.
                    SetNodeLook(node, node.color, .8f + .7f * pulse, 1.6f + 1.8f * pulse);
                    break;
                case NodeState.Solved:
                    SetNodeLook(node, Color.Lerp(node.color, Color.white, .25f), 1.3f, 1.4f);
                    break;
            }
        }
    }

    /// <summary>
    /// The authored screen sat half inside the console's top edge (and z-fought with it). Stand it on
    /// top of the base instead, tilted back toward the player like a lectern.
    /// </summary>
    private static void SeatScreen(Node node)
    {
        Renderer baseRenderer = null;
        foreach (Renderer r in node.root.GetComponentsInChildren<Renderer>(true))
            if (r.name.EndsWith("_Base")) { baseRenderer = r; break; }
        if (baseRenderer == null) return;
        // Transform maths, not renderer bounds: nodes II and III start inactive (their bounds are empty).
        Transform baseT = baseRenderer.transform, screen = node.screen.transform;
        Vector3 baseHalf = baseT.lossyScale * .5f;          // unit-cube primitives
        Vector3 screenHalf = screen.lossyScale * .5f;
        Vector3 top = baseT.position + baseT.up * baseHalf.y;
        screen.rotation = baseT.rotation * Quaternion.Euler(18f, 0f, 0f);
        screen.position = top + screen.up * (screenHalf.y + .04f) - baseT.forward * (baseHalf.z - screenHalf.z - .35f);
    }

    private static void SetNodeLook(Node node, Color color, float emission, float lightIntensity)
    {
        if (node.screenMaterial != null) Tint(node.screenMaterial, color, emission);
        if (node.glow != null) { node.glow.color = color; node.glow.intensity = lightIntensity; }
    }

    // ------------------------------------------------------------------ finale
    private void BeginFinale()
    {
        if (finaleRunning) return;
        finaleRunning = true;
        finaleTime = 0f;
        clearTime = sceneTime;
        newBest = RuleCardsProgress.SubmitFinalTime(clearTime);
        Play(finaleClip, .45f);
        Banner("THE REPOSITORY IS RESTORED", 5f);

        if (exitSeam != null)
        {
            seamMaterial = exitSeam.material;
            owned.Add(seamMaterial);
        }
        BuildBeam();
    }

    private void BuildBeam()
    {
        Material template = suspendedCards.Length > 0 ? suspendedCards[0].sharedMaterial
            : exitSeam != null ? exitSeam.sharedMaterial : null;
        beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Repository_RestoredBeam";
        Collider c = beam.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        SceneManager.MoveGameObjectToScene(beam, gameObject.scene);
        beam.transform.position = new Vector3(orbitCenter.x, 6f, orbitCenter.z);
        beam.transform.localScale = new Vector3(0f, 6f, 0f);
        var r = beam.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (template != null)
        {
            beamMaterial = new Material(template) { name = "Repository_BeamRuntime" };
            owned.Add(beamMaterial);
            if (beamMaterial.HasProperty("_BaseMap")) beamMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (beamMaterial.HasProperty("_MainTex")) beamMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            Tint(beamMaterial, new Color(.55f, 1f, 1f), 3f);
            r.sharedMaterial = beamMaterial;
        }
        beamLight = new GameObject("Repository_RestoredLight").AddComponent<Light>();
        beamLight.transform.SetParent(beam.transform, false);
        beamLight.type = LightType.Point;
        beamLight.color = new Color(.55f, 1f, 1f);
        beamLight.range = 18f;
        beamLight.intensity = 0f;
        beamLight.shadows = LightShadows.None;
    }

    private void AnimateFinale(float dt)
    {
        if (!finaleRunning) return;
        finaleTime += dt;
        float gather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(finaleTime / 3.5f));

        int n = suspendedCards.Length;
        float spin = finaleTime * 22f;
        for (int i = 0; i < n; i++)
        {
            if (suspendedCards[i] == null) continue;
            float angle = (i * 360f / Mathf.Max(1, n) + spin) * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 target = orbitCenter + outward * 3.4f + Vector3.up * (.35f * Mathf.Sin(finaleTime * 1.3f + i));
            Transform card = suspendedCards[i].transform;
            card.position = Vector3.Lerp(cardStartPos[i], target, gather);
            card.rotation = Quaternion.Slerp(cardStartRot[i], Quaternion.LookRotation(outward, Vector3.up), gather);
        }

        if (coreLight != null)
        {
            coreLight.intensity = Mathf.Lerp(coreBaseIntensity, Mathf.Max(coreBaseIntensity * 2.5f, 3f), gather);
            coreLight.color = Color.Lerp(coreBaseColor, new Color(.6f, 1f, 1f), gather);
        }
        if (beam != null)
        {
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((finaleTime - 1f) / 2f));
            float width = .35f * grow * (1f + .08f * Mathf.Sin(finaleTime * 6f));
            beam.transform.localScale = new Vector3(width, 6f, width);
            if (beamLight != null) beamLight.intensity = 3f * grow;
        }
        if (seamMaterial != null) Tint(seamMaterial, new Color(.55f, 1f, 1f), 2f + 3f * gather);

        if (!exitOpen && finaleTime > 2.5f)
        {
            exitOpen = true;
            if (exitRoot != null) exitRoot.SetActive(true);
            Banner("THE EXIT IS OPEN — LEAVE THE REPOSITORY", 5f);
        }
    }

    // ------------------------------------------------------------------ ending
    private void OpenEnding()
    {
        endingOpen = true;
        endingTime = 0f;
        confirmReset = false;
        endingOpenedFrame = Time.frameCount;
        PausePlayer();
    }

    private void UpdateEnding()
    {
        endingTime += Time.unscaledDeltaTime;
        if (Time.frameCount == endingOpenedFrame || endingTime < 2.2f) return;
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return)) ReturnToArchive();
    }

    private void ReturnToArchive()
    {
        RuleCardsProgress.CompleteStage(RuleCardsProgress.FinalStageId);
        ResumePlayer();
        // Stage 04 (the escape) follows when it has been built; otherwise straight back to the hub.
        SceneManager.LoadScene(RuleCardsScenes.EscapeAvailable ? RuleCardsScenes.Escape : HubScene);
    }

    private void ResetEverything()
    {
        RuleCardsProgress.ResetAll();
        ResumePlayer();
        SceneManager.LoadScene(HubScene);
    }

    // ------------------------------------------------------------------ GUI
    private void OnGUI()
    {
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        int oldDepth = GUI.depth;
        if (endingOpen) { GUI.depth = -100; DrawEnding(vw, vh); }
        else if (openNode != null) { GUI.depth = -50; DrawConsole(vw, vh); }
        else if (!MouseLook.isPaused)
        {
            DrawHud(vw, vh);
            DrawPrompt(vw, vh);
            DrawBanner(vw, vh);
        }
        GUI.depth = oldDepth;
        ResponsiveIMGUI.End(previous);
    }

    private void DrawHud(float vw, float vh)
    {
        Rect box = new Rect(12f, 12f, Mathf.Min(330f, vw - 24f), 112f);
        RepositoryGUI.Fill(box, new Color(.01f, .03f, .05f, .72f));
        RepositoryGUI.Fill(new Rect(box.x, box.y, 3f, box.height), new Color(.3f, 1f, 1f, .9f));
        GUI.Label(new Rect(box.x + 12f, box.y + 6f, box.width - 16f, 22f), "THE FINAL REPOSITORY",
            RepositoryGUI.Text(14, TextAnchor.MiddleLeft, new Color(.4f, 1f, 1f), true));
        for (int i = 0; i < 3; i++)
        {
            Node node = nodeData[i];
            string state = node.state == NodeState.Solved ? "ALIGNED" : node.state == NodeState.Hidden ? "SEALED" : "ACTIVE";
            Color c = node.state == NodeState.Solved ? node.color
                : node.state == NodeState.Hidden ? new Color(.5f, .55f, .6f) : Color.white;
            GUI.Label(new Rect(box.x + 12f, box.y + 30f + i * 20f, box.width - 16f, 20f),
                "Node " + node.numeral + " · " + node.rule + " — " + state, RepositoryGUI.Text(12, TextAnchor.MiddleLeft, c));
        }
        string objective = exitOpen ? "Leave through the open exit."
            : finaleRunning || finaleStartAt > 0f ? "The Repository is waking..."
            : "Align the active node.";
        GUI.Label(new Rect(box.x + 12f, box.y + 90f, box.width - 16f, 20f), objective,
            RepositoryGUI.Text(12, TextAnchor.MiddleLeft, new Color(1f, .85f, .45f)));
    }

    private void DrawPrompt(float vw, float vh)
    {
        string text = null;
        if (focusNode != null) text = "E • NODE " + focusNode.numeral + " — " + focusNode.rule;
        else if (atExit) text = "E • Leave the Repository";
        if (text == null) return;
        Rect r = ResponsiveIMGUI.BottomCenter(vw, vh, 420f, 48f);
        RepositoryGUI.Fill(r, new Color(.01f, .03f, .05f, .8f));
        RepositoryGUI.Frame(r, new Color(.3f, 1f, 1f, .6f), 1f);
        GUI.Label(r, text, RepositoryGUI.Text(16, TextAnchor.MiddleCenter, Color.white, true));
    }

    private void Banner(string text, float seconds = 3.5f)
    {
        banner = text;
        bannerUntil = Time.unscaledTime + seconds;
    }

    private void DrawBanner(float vw, float vh)
    {
        if (Time.unscaledTime > bannerUntil || string.IsNullOrEmpty(banner)) return;
        float left = bannerUntil - Time.unscaledTime;
        float alpha = Mathf.Clamp01(left / .6f);
        float w = Mathf.Min(620f, vw - 24f);
        Rect r = new Rect((vw - w) * .5f, Mathf.Min(140f, vh * .22f), w, 46f);
        RepositoryGUI.Fill(r, new Color(.01f, .03f, .05f, .75f * alpha));
        GUI.Label(r, banner, RepositoryGUI.Text(18, TextAnchor.MiddleCenter, new Color(.5f, 1f, 1f, alpha), true));
    }

    private void DrawConsole(float vw, float vh)
    {
        Node node = openNode;
        RepositoryNodePuzzle puzzle = node.puzzle;
        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(0f, 0f, 0f, .55f));
        float pw = Mathf.Min(660f, vw - 24f), ph = Mathf.Min(690f, vh - 24f);
        Rect panel = new Rect((vw - pw) * .5f, (vh - ph) * .5f, pw, ph);
        RepositoryGUI.Fill(panel, new Color(.03f, .05f, .09f, .98f));
        RepositoryGUI.Frame(panel, node.color, 2f);

        GUI.Label(new Rect(panel.x, panel.y + 8f, pw, 30f), "ARCHIVE NODE " + node.numeral + " · " + node.rule,
            RepositoryGUI.Text(20, TextAnchor.MiddleCenter, node.color, true));
        GUI.Label(new Rect(panel.x + 24f, panel.y + 40f, pw - 48f, 52f), puzzle.Instruction,
            RepositoryGUI.Text(13, TextAnchor.MiddleCenter, new Color(.85f, .9f, 1f)));

        Rect area = new Rect(panel.x + 24f, panel.y + 98f, pw - 48f, ph - 98f - 104f);
        puzzle.Draw(area);

        string status = puzzle.Status;
        Color statusColor = puzzle.StatusIsError ? new Color(1f, .5f, .35f) : new Color(.4f, 1f, .6f);
        GUI.Label(new Rect(panel.x + 16f, panel.yMax - 100f, pw - 32f, 24f), status,
            RepositoryGUI.Text(14, TextAnchor.MiddleCenter, statusColor, true));
        GUI.Label(new Rect(panel.x + 16f, panel.yMax - 74f, pw - 32f, 22f), puzzle.Controls,
            RepositoryGUI.Text(11, TextAnchor.MiddleCenter, new Color(.6f, .66f, .75f)));

        GUI.enabled = !puzzle.Solved;
        float bw = 150f;
        if (GUI.Button(new Rect(panel.center.x - bw - 8f, panel.yMax - 46f, bw, 34f), "RESET (R)")) puzzle.Reset();
        if (GUI.Button(new Rect(panel.center.x + 8f, panel.yMax - 46f, bw, 34f), "CLOSE (Esc)")) CloseConsole();
        GUI.enabled = true;

        if (puzzle.Solved && solvedAt >= 0f)
        {
            float a = Mathf.Clamp01((Time.unscaledTime - solvedAt) / .35f);
            RepositoryGUI.Fill(area, RepositoryGUI.WithAlpha(node.color, .18f * a));
            GUI.Label(new Rect(area.x, area.center.y - 30f, area.width, 60f), "NODE " + node.numeral + " ALIGNED",
                RepositoryGUI.Text(30, TextAnchor.MiddleCenter, RepositoryGUI.WithAlpha(Color.white, a), true));
        }
    }

    private void DrawEnding(float vw, float vh)
    {
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(endingTime / 1.2f));
        RepositoryGUI.Fill(new Rect(0f, 0f, vw, vh), new Color(.005f, .015f, .025f, .97f * fade));
        float textAlpha = Mathf.Clamp01((endingTime - .8f) / .8f);
        if (textAlpha <= 0f) return;

        float w = Mathf.Min(700f, vw - 32f);
        float x = (vw - w) * .5f;
        float y = vh * .2f;
        GUI.Label(new Rect(x, y, w, 50f), endingTitle,
            RepositoryGUI.Text(Mathf.Clamp(Mathf.RoundToInt(vw / 26f), 22, 34), TextAnchor.MiddleCenter, new Color(.45f, 1f, 1f, textAlpha), true));
        for (int i = 0; i < 3; i++)
            RepositoryGUI.Fill(new Rect(vw * .5f - 75f + i * 52f, y + 60f, 46f, 4f), RepositoryGUI.WithAlpha(RuleColors[i], textAlpha));
        GUI.Label(new Rect(x, y + 80f, w, 90f), endingBody,
            RepositoryGUI.Text(15, TextAnchor.UpperCenter, new Color(.85f, .9f, 1f, textAlpha)));

        float statsAlpha = Mathf.Clamp01((endingTime - 1.6f) / .6f);
        string time = "Time in the Repository: " + FormatTime(clearTime > 0f ? clearTime : sceneTime);
        float best = RuleCardsProgress.BestFinalTime;
        if (best > 0f) time += "   •   Best: " + FormatTime(best) + (newBest ? "  (NEW)" : "");
        GUI.Label(new Rect(x, y + 180f, w, 26f), time,
            RepositoryGUI.Text(14, TextAnchor.MiddleCenter, new Color(1f, .85f, .45f, statsAlpha)));

        if (endingTime < 2.2f) return;
        float bw = Mathf.Min(260f, (w - 24f) * .5f);
        float by = Mathf.Min(y + 240f, vh - 90f);
        if (GUI.Button(new Rect(vw * .5f - bw - 8f, by, bw, 42f), RuleCardsScenes.EscapeAvailable ? "ESCAPE (E)" : "RETURN TO THE ARCHIVE (E)")) ReturnToArchive();
        if (GUI.Button(new Rect(vw * .5f + 8f, by, bw, 42f), confirmReset ? "CLICK AGAIN TO ERASE" : "RESET ALL PROGRESS"))
        {
            if (confirmReset) ResetEverything();
            else confirmReset = true;
        }
        if (confirmReset)
            GUI.Label(new Rect(x, by + 48f, w, 22f), "Erases every Rule Card and stage. The Archive starts over.",
                RepositoryGUI.Text(12, TextAnchor.MiddleCenter, new Color(1f, .5f, .4f)));
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    // ------------------------------------------------------------------ helpers
    private static void Tint(Material m, Color c, float emission)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emission);
        }
    }

    private void SetupAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        chimeClip = Chime("Repository_NodeAligned", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, .11f, 1.6f);
        riseClip = Rumble("Repository_NodeRise", 1.7f);
        finaleClip = Chime("Repository_Restored", new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, .2f, 3.4f);
        owned.Add(chimeClip);
        owned.Add(riseClip);
        owned.Add(finaleClip);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
    }

    private static AudioClip Chime(string clipName, float[] notes, float gap, float length)
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * length)];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(n * gap * rate);
            for (int i = start; i < samples.Length; i++)
            {
                float age = (i - start) / (float)rate;
                float env = Mathf.Clamp01(age / .01f) * Mathf.Exp(-age * 3.2f);
                samples[i] += (Mathf.Sin(age * notes[n] * Mathf.PI * 2f) + .3f * Mathf.Sin(age * notes[n] * 2f * Mathf.PI * 2f)) * env * .16f;
            }
        }
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= Mathf.Clamp01((length - i / (float)rate) / .15f);
        var clip = AudioClip.Create(clipName, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip Rumble(string clipName, float length)
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * length)];
        var random = new System.Random(4127);
        float low = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            low = Mathf.Lerp(low, (float)random.NextDouble() * 2f - 1f, .02f);
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
            samples[i] = (low * 2.2f + .25f * Mathf.Sin(t * 55f * Mathf.PI * 2f)) * env * .35f;
        }
        var clip = AudioClip.Create(clipName, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDisable()
    {
        if (openNode != null || endingOpen) ResumePlayer();
    }

    private void OnDestroy()
    {
        foreach (Object o in owned) if (o != null) Destroy(o);
        owned.Clear();
    }
}

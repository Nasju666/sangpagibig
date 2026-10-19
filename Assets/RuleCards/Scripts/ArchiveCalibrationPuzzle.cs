using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local vault calibration. Progress resets on re-entry; collected cards remain governed by RuleCardsProgress.</summary>
public class ArchiveCalibrationPuzzle : MonoBehaviour
{
    public enum CalibrationMode { Polarity, AirLattice }
    public CalibrationMode mode;
    public string puzzleTitle = "ARCHIVE CALIBRATION";
    public string instruction = "Read the nearby archive inscription.";
    public GameObject[] rewardObjects;
    public GameObject[] sealedObjects;
    public float interactionDistance = 4f;
    public int[] correctPolarity = { 0, 2, 0 };
    public int[] latticeSolution = { 0, 4, 5, 9, 10, 14, 15 };
    public bool IsSolved { get; private set; }

    readonly int[] polarity = { 1, 1, 1 };
    readonly bool[] cells = new bool[16];
    readonly string[] directionNames = { "UP", "RIGHT", "DOWN", "LEFT" };
    readonly List<Behaviour> paused = new List<Behaviour>();
    bool open, canInteract;
    CursorLockMode oldLock;
    bool oldCursor;
    string feedback = "";
    GUIStyle titleStyle, labelStyle, buttonStyle;

    void Start()
    {
        foreach (var item in rewardObjects ?? new GameObject[0])
            RuleCardPickup.SetVaultOpen(item, false);
    }

    void Update()
    {
        if (open)
        {
            ArchiveModal.MarkOpen();
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
            else if (Input.GetKeyDown(KeyCode.R)) ResetBoard();
            return;
        }
        canInteract = false;
        if (IsSolved || Camera.main == null) return;
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.ViewportPointToRay(new Vector3(.5f,.5f,0)), out hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            canInteract = hit.collider.GetComponentInParent<ArchiveCalibrationPuzzle>() == this;
        if (canInteract && !MouseLook.isPaused && Input.GetKeyDown(KeyCode.E)) Open();
    }

    void Open()
    {
        oldLock = Cursor.lockState;
        oldCursor = Cursor.visible;
        paused.Clear();
        var camera = Camera.main;
        var player = camera != null ? camera.GetComponentInParent<CharacterController>() : null;
        if (player != null)
        {
            foreach (var behaviour in player.GetComponentsInChildren<MonoBehaviour>())
            {
                string type = behaviour.GetType().Name;
                if (behaviour.enabled && (type == "PlayerMovement" || type == "MouseLook"))
                {
                    paused.Add(behaviour);
                    behaviour.enabled = false;
                }
            }
        }
        open = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close()
    {
        open = false;
        foreach (var behaviour in paused) if (behaviour != null) behaviour.enabled = true;
        paused.Clear();
        Cursor.lockState = oldLock;
        Cursor.visible = oldCursor;
    }

    void OnDisable() { if (open) Close(); }

    void ResetBoard()
    {
        for (int i=0;i<polarity.Length;i++) polarity[i]=1;
        for (int i=0;i<cells.Length;i++) cells[i]=false;
        feedback = "Calibration cleared.";
    }

    bool ConfigurationMatches()
    {
        if (mode == CalibrationMode.Polarity)
        {
            if (correctPolarity == null || correctPolarity.Length != polarity.Length) return false;
            for (int i=0;i<polarity.Length;i++) if (polarity[i] != correctPolarity[i]) return false;
            return true;
        }
        if (latticeSolution == null || latticeSolution.Length == 0) return false;
        for (int i=0;i<16;i++)
            if (cells[i] != System.Array.Exists(latticeSolution, value => value == i)) return false;
        return true;
    }

    void TrySolve()
    {
        if (!ConfigurationMatches())
        {
            feedback = mode == CalibrationMode.Polarity ? "Polarity mismatch. Check the three vault glyphs." : "Lattice mismatch. Match every lit cell in the wall blueprint.";
            return;
        }
        IsSolved = true;
        foreach (var item in sealedObjects ?? new GameObject[0]) if (item != null) item.SetActive(false);
        foreach (var item in rewardObjects ?? new GameObject[0]) RuleCardPickup.SetVaultOpen(item, true);
        Close();
    }

    void OnGUI()
    {
        RuleCardsUI.ApplySkin();
        // Screen-space rectangles are shared by rendering and hit-testing. No GUI.matrix/input double scaling.
        Matrix4x4 savedMatrix = GUI.matrix;
        Color savedColor = GUI.color;
        GUI.matrix = Matrix4x4.identity;
        float scale = Mathf.Max(.25f, Mathf.Min(Screen.width / 1000f, Screen.height / 760f));
        titleStyle = new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, fontSize=Mathf.Max(10,Mathf.RoundToInt(26*scale)), wordWrap=true };
        labelStyle = new GUIStyle(GUI.skin.label) { alignment=TextAnchor.MiddleCenter, fontSize=Mathf.Max(9,Mathf.RoundToInt(17*scale)), wordWrap=true };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize=Mathf.Max(9,Mathf.RoundToInt(20*scale)) };
        if (open)
        {
            float w=700*scale, h=690*scale;
            Rect panel = new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h);
            GUI.color = new Color(.04f,.07f,.12f,.98f);
            GUI.DrawTexture(panel,Texture2D.whiteTexture);
            GUI.color = Color.cyan;
            GUI.Label(new Rect(panel.x+20*scale,panel.y+15*scale,w-40*scale,58*scale),puzzleTitle,titleStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x+25*scale,panel.y+80*scale,w-50*scale,65*scale),instruction,labelStyle);
            if (mode == CalibrationMode.Polarity)
            {
                for (int i=0;i<3;i++)
                {
                    float x=panel.x+(50+i*210)*scale;
                    GUI.Label(new Rect(x,panel.y+205*scale,180*scale,40*scale),"SEAL "+(i+1),labelStyle);
                    if (GUI.Button(new Rect(x,panel.y+255*scale,180*scale,125*scale),directionNames[polarity[i]],buttonStyle))
                    { polarity[i]=(polarity[i]+1)%4; feedback=""; }
                }
            }
            else
            {
                float size=80*scale, startX=panel.center.x-size*2, startY=panel.y+165*scale;
                for (int i=0;i<16;i++)
                {
                    GUI.backgroundColor=cells[i] ? Color.cyan : new Color(.22f,.28f,.36f);
                    if (GUI.Button(new Rect(startX+(i%4)*size,startY+(i/4)*size,size-4*scale,size-4*scale),cells[i] ? "+" : "",buttonStyle))
                    { cells[i]=!cells[i]; feedback=""; }
                }
                GUI.backgroundColor=Color.white;
            }
            GUI.Label(new Rect(panel.x+25*scale,panel.y+500*scale,w-50*scale,60*scale),feedback,labelStyle);
            if (GUI.Button(new Rect(panel.x+45*scale,panel.y+580*scale,180*scale,52*scale),"RESET",buttonStyle)) ResetBoard();
            if (GUI.Button(new Rect(panel.x+245*scale,panel.y+580*scale,210*scale,52*scale),"CALIBRATE",buttonStyle)) TrySolve();
            if (GUI.Button(new Rect(panel.x+475*scale,panel.y+580*scale,180*scale,52*scale),"CLOSE",buttonStyle)) Close();
            GUI.Label(new Rect(panel.x+20*scale,panel.y+638*scale,w-40*scale,35*scale),"R resets • Esc closes • Read the nearby wall clue",labelStyle);
        }
        else if (canInteract)
        {
            Rect hint=new Rect(Screen.width*.2f,Screen.height-90*scale,Screen.width*.6f,65*scale);
            GUI.Box(hint,"");
            GUI.Label(hint,"E • "+puzzleTitle,labelStyle);
        }
        GUI.color=savedColor;
        GUI.matrix=savedMatrix;
    }
}

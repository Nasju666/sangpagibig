using System.Collections.Generic;
using UnityEngine;

// Console puzzles for the three Archive Nodes in Repository_Final.
// Plain C# classes driven by RepositoryFinale: it owns the panel, the player pause and the
// node state; each puzzle only draws its board inside the area it is given and reports Solved.
// All boards were verified solvable offline (link: unique fill, gravity: 8 flips minimum,
// air: exactly one 3-field answer).

public abstract class RepositoryNodePuzzle
{
    public bool Solved { get; protected set; }
    public abstract string Instruction { get; }
    public abstract string Controls { get; }

    private string status = "";
    private float statusUntil;
    private bool statusIsError;

    public string Status { get { return Time.unscaledTime < statusUntil ? status : ""; } }
    public bool StatusIsError { get { return statusIsError; } }

    protected void Say(string text, bool error = false, float seconds = 2.2f)
    {
        status = text;
        statusIsError = error;
        statusUntil = Time.unscaledTime + seconds;
    }

    public abstract void Reset();
    public virtual void Tick(float unscaledDeltaTime) { }
    /// <summary>Called from OnGUI inside the ResponsiveIMGUI matrix. Draws and handles mouse input.</summary>
    public abstract void Draw(Rect area);

    protected static Rect FitSquare(Rect area, float reservedBottom)
    {
        float side = Mathf.Max(60f, Mathf.Min(area.width, area.height - reservedBottom));
        return new Rect(area.x + (area.width - side) * 0.5f, area.y, side, side);
    }
}

/// <summary>Small IMGUI helpers shared by the Repository console and HUD.</summary>
public static class RepositoryGUI
{
    private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();

    public static void Fill(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static void Frame(Rect rect, Color color, float thickness)
    {
        Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
        Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    public static GUIStyle Text(int size, TextAnchor anchor, Color color, bool bold = false)
    {
        int key = size * 100 + (int)anchor * 2 + (bold ? 1 : 0);
        GUIStyle style;
        if (!styles.TryGetValue(key, out style) || style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = true, richText = false };
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            styles[key] = style;
        }
        style.normal.textColor = color;
        return style;
    }

    public static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
}

// ---------------------------------------------------------------------------------------------
// NODE I — KINETIC LINK: connect each colour pair, every cell must carry a link.
// ---------------------------------------------------------------------------------------------
public sealed class LinkMatrixPuzzle : RepositoryNodePuzzle
{
    private const int Size = 5;
    private static readonly Vector2Int[,] Ends = {
        { new Vector2Int(1, 1), new Vector2Int(2, 3) },
        { new Vector2Int(2, 2), new Vector2Int(4, 0) },
        { new Vector2Int(3, 0), new Vector2Int(1, 4) },
        { new Vector2Int(2, 4), new Vector2Int(4, 2) }
    };
    private static readonly Color[] Colors = {
        new Color(1f, .18f, .82f), new Color(.15f, 1f, .2f),
        new Color(.05f, .85f, 1f), new Color(1f, .68f, .05f)
    };
    private const int Pairs = 4;

    private readonly int[,] occupied = new int[Size, Size];
    private readonly List<Vector2Int>[] paths = new List<Vector2Int>[Pairs];
    private int drawing = -1;

    public override string Instruction
    {
        get { return "Re-link the kinetic lattice. Join each pair of matching nodes. Links may not cross, and every cell must carry a link."; }
    }
    public override string Controls { get { return "Drag from a node to draw • Drag back to undo • R resets • Esc closes"; } }

    public LinkMatrixPuzzle()
    {
        for (int i = 0; i < Pairs; i++) paths[i] = new List<Vector2Int>();
        Reset();
    }

    public override void Reset()
    {
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) occupied[x, y] = -1;
        for (int i = 0; i < Pairs; i++)
        {
            paths[i].Clear();
            occupied[Ends[i, 0].x, Ends[i, 0].y] = i;
            occupied[Ends[i, 1].x, Ends[i, 1].y] = i;
        }
        drawing = -1;
        Solved = false;
    }

    public override void Draw(Rect area)
    {
        Rect grid = FitSquare(area, 6f);
        float cell = grid.width / Size;
        RepositoryGUI.Fill(grid, new Color(.07f, .02f, .14f, 1f));
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color tint = occupied[x, y] >= 0 && EndColor(new Vector2Int(x, y)) < 0
                    ? RepositoryGUI.WithAlpha(Colors[occupied[x, y]], .16f) : new Color(.22f, .1f, .42f, .7f);
                RepositoryGUI.Fill(CellRect(grid, x, y), tint);
            }
        for (int c = 0; c < Pairs; c++)
            for (int i = 1; i < paths[c].Count; i++)
                Segment(Center(grid, paths[c][i - 1]), Center(grid, paths[c][i]), Colors[c], cell * .34f);
        for (int c = 0; c < Pairs; c++)
            for (int e = 0; e < 2; e++)
            {
                Vector2 v = Center(grid, Ends[c, e]);
                float s = cell * .58f;
                RepositoryGUI.Fill(new Rect(v.x - s * .5f, v.y - s * .5f, s, s), Colors[c]);
                RepositoryGUI.Frame(new Rect(v.x - s * .5f, v.y - s * .5f, s, s), Color.white, 2f);
            }
        if (!Solved) HandleMouse(grid);
    }

    private void HandleMouse(Rect grid)
    {
        Event e = Event.current;
        if (e == null) return;
        Vector2Int cell = MouseCell(grid, e.mousePosition);
        if (e.type == EventType.MouseDown && e.button == 0 && cell.x >= 0)
        {
            int end = EndColor(cell);
            if (end >= 0)
            {
                ClearPath(end);
                drawing = end;
                paths[end].Add(cell);
                e.Use();
            }
            else if (occupied[cell.x, cell.y] >= 0)
            {
                // Grab an existing link mid-way and keep drawing from there.
                int c = occupied[cell.x, cell.y];
                Truncate(c, paths[c].IndexOf(cell));
                drawing = c;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && drawing >= 0 && cell.x >= 0)
        {
            // Fast drags can skip cells: walk there one orthogonal step at a time.
            int guard = 0;
            while (drawing >= 0 && guard++ < Size * 2)
            {
                Vector2Int last = paths[drawing][paths[drawing].Count - 1];
                if (last == cell) break;
                Vector2Int step = last;
                if (Mathf.Abs(cell.x - last.x) >= Mathf.Abs(cell.y - last.y)) step.x += cell.x > last.x ? 1 : -1;
                else step.y += cell.y > last.y ? 1 : -1;
                if (!Extend(step)) break;
            }
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            drawing = -1;
        }
    }

    private bool Extend(Vector2Int cell)
    {
        List<Vector2Int> path = paths[drawing];
        int existing = path.IndexOf(cell);
        if (existing >= 0) { Truncate(drawing, existing); return true; }

        int end = EndColor(cell);
        if (end >= 0)
        {
            if (end != drawing) { Say("Match the same colour.", true); return false; }
            path.Add(cell);
            drawing = -1;
            if (IsComplete()) { Solved = true; Say("Kinetic lattice linked."); }
            return false;
        }
        if (occupied[cell.x, cell.y] >= 0) { Say("Links cannot cross.", true); return false; }
        occupied[cell.x, cell.y] = drawing;
        path.Add(cell);
        return true;
    }

    private void Truncate(int c, int keepIndex)
    {
        List<Vector2Int> path = paths[c];
        if (keepIndex < 0) return;
        for (int i = path.Count - 1; i > keepIndex; i--)
        {
            Vector2Int p = path[i];
            if (EndColor(p) < 0) occupied[p.x, p.y] = -1;
            path.RemoveAt(i);
        }
    }

    private void ClearPath(int c)
    {
        foreach (Vector2Int p in paths[c]) if (EndColor(p) < 0) occupied[p.x, p.y] = -1;
        paths[c].Clear();
    }

    private bool IsComplete()
    {
        for (int c = 0; c < Pairs; c++)
        {
            List<Vector2Int> path = paths[c];
            if (path.Count < 2) return false;
            Vector2Int a = path[0], b = path[path.Count - 1];
            bool joined = (a == Ends[c, 0] && b == Ends[c, 1]) || (a == Ends[c, 1] && b == Ends[c, 0]);
            if (!joined) return false;
        }
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) if (occupied[x, y] < 0) return false;
        return true;
    }

    private int EndColor(Vector2Int p)
    {
        for (int c = 0; c < Pairs; c++) if (p == Ends[c, 0] || p == Ends[c, 1]) return c;
        return -1;
    }

    private static Vector2Int MouseCell(Rect grid, Vector2 mouse)
    {
        if (!grid.Contains(mouse)) return new Vector2Int(-1, -1);
        float s = grid.width / Size;
        return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt((mouse.x - grid.x) / s), 0, Size - 1),
            Mathf.Clamp(Mathf.FloorToInt((mouse.y - grid.y) / s), 0, Size - 1));
    }

    private static Rect CellRect(Rect g, int x, int y)
    {
        float s = g.width / Size;
        return new Rect(g.x + x * s + 2f, g.y + y * s + 2f, s - 4f, s - 4f);
    }

    private static Vector2 Center(Rect g, Vector2Int p)
    {
        float s = g.width / Size;
        return new Vector2(g.x + (p.x + .5f) * s, g.y + (p.y + .5f) * s);
    }

    private static void Segment(Vector2 a, Vector2 b, Color c, float width)
    {
        float half = width * .5f;
        Rect r = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y)
            ? new Rect(Mathf.Min(a.x, b.x) - half, a.y - half, Mathf.Abs(b.x - a.x) + width, width)
            : new Rect(a.x - half, Mathf.Min(a.y, b.y) - half, width, Mathf.Abs(b.y - a.y) + width);
        RepositoryGUI.Fill(r, c);
    }
}

// ---------------------------------------------------------------------------------------------
// NODE II — INVERT GRAVITY: choose where gravity pulls; the core AND the ballast both slide
// until they hit stone (or each other). Rest the core on the socket within the flip limit.
// ---------------------------------------------------------------------------------------------
public sealed class GravityDriftPuzzle : RepositoryNodePuzzle
{
    // '#' stone, 'C' core, 'B' ballast, 'S' socket. Grid y grows downward (screen space).
    private static readonly string[] Layout = {
        ".##...",
        ".#..B.",
        ".S....",
        "..#C..",
        ".....#",
        "..#..#"
    };
    public const int FlipLimit = 12;
    private const float SlideSpeed = 14f; // cells per second (visual only)

    private readonly int width, height;
    private readonly bool[,] stone;
    private readonly Vector2Int socket, coreStart, ballastStart;
    private Vector2Int core, ballast;
    private Vector2 coreView, ballastView;
    private Vector2Int gravity = new Vector2Int(0, 1);
    private int flips;
    private bool checkPending;
    private readonly List<Vector2Int[]> history = new List<Vector2Int[]>();

    public override string Instruction
    {
        get { return "Choose where gravity pulls. The core and the ballast both fall until they hit stone or each other. Rest the core on the socket in " + FlipLimit + " flips or fewer."; }
    }
    public override string Controls { get { return "W A S D / arrows flip gravity • Z undoes • R resets • Esc closes"; } }

    public GravityDriftPuzzle()
    {
        height = Layout.Length;
        width = Layout[0].Length;
        stone = new bool[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                char c = Layout[y][x];
                stone[x, y] = c == '#';
                if (c == 'C') coreStart = new Vector2Int(x, y);
                if (c == 'B') ballastStart = new Vector2Int(x, y);
                if (c == 'S') socket = new Vector2Int(x, y);
            }
        Reset();
    }

    public override void Reset()
    {
        core = coreStart;
        ballast = ballastStart;
        coreView = core;
        ballastView = ballast;
        gravity = new Vector2Int(0, 1);
        flips = 0;
        checkPending = false;
        history.Clear();
        Solved = false;
    }

    private bool Animating { get { return (coreView - (Vector2)core).sqrMagnitude > 1e-4f || (ballastView - (Vector2)ballast).sqrMagnitude > 1e-4f; } }

    public override void Tick(float dt)
    {
        coreView = Vector2.MoveTowards(coreView, core, SlideSpeed * dt);
        ballastView = Vector2.MoveTowards(ballastView, ballast, SlideSpeed * dt);
        if (checkPending && !Animating)
        {
            checkPending = false;
            if (core == socket) { Solved = true; Say("Core seated. Polarity restored."); }
            else if (flips >= FlipLimit) Say("Flip limit reached. Undo (Z) or reset (R).", true, 4f);
        }
        if (Solved) return;
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Flip(new Vector2Int(0, -1));
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Flip(new Vector2Int(0, 1));
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Flip(new Vector2Int(-1, 0));
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Flip(new Vector2Int(1, 0));
        else if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace)) Undo();
    }

    public void Flip(Vector2Int direction)
    {
        if (Solved || Animating) return;
        if (flips >= FlipLimit) { Say("Flip limit reached. Undo (Z) or reset (R).", true, 4f); return; }
        Vector2Int[] blocks = { core, ballast };
        Vector2Int[] before = { core, ballast };
        gravity = direction;
        Settle(blocks, direction);
        if (blocks[0] == before[0] && blocks[1] == before[1]) { Say("Nothing shifts that way.", true, 1.4f); return; }
        history.Add(before);
        core = blocks[0];
        ballast = blocks[1];
        flips++;
        checkPending = true;
    }

    /// <summary>Same rule as the offline solver: the leading block moves first, one cell per pass.</summary>
    private void Settle(Vector2Int[] blocks, Vector2Int d)
    {
        for (int pass = 0; pass < 64; pass++)
        {
            bool moved = false;
            int first = Dot(blocks[0], d) >= Dot(blocks[1], d) ? 0 : 1;
            for (int k = 0; k < 2; k++)
            {
                int i = k == 0 ? first : 1 - first;
                Vector2Int next = blocks[i] + d;
                if (IsFree(next) && next != blocks[1 - i]) { blocks[i] = next; moved = true; }
            }
            if (!moved) return;
        }
    }

    private static int Dot(Vector2Int a, Vector2Int b) { return a.x * b.x + a.y * b.y; }

    private bool IsFree(Vector2Int p)
    {
        return p.x >= 0 && p.y >= 0 && p.x < width && p.y < height && !stone[p.x, p.y];
    }

    private void Undo()
    {
        if (Solved || history.Count == 0 || Animating) return;
        Vector2Int[] last = history[history.Count - 1];
        history.RemoveAt(history.Count - 1);
        core = last[0];
        ballast = last[1];
        flips = Mathf.Max(0, flips - 1);
        checkPending = false;
    }

    public override void Draw(Rect area)
    {
        const float controlsHeight = 92f;
        Rect grid = FitSquare(area, controlsHeight);
        float cell = grid.width / width;
        RepositoryGUI.Fill(grid, new Color(.03f, .05f, .1f, 1f));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Rect r = new Rect(grid.x + x * cell + 2f, grid.y + y * cell + 2f, cell - 4f, cell - 4f);
                RepositoryGUI.Fill(r, stone[x, y] ? new Color(.36f, .33f, .3f, 1f) : new Color(.1f, .14f, .24f, 1f));
                if (stone[x, y]) RepositoryGUI.Frame(r, new Color(.5f, .46f, .4f, 1f), 2f);
            }

        // Socket ring
        Rect s = Inset(CellAt(grid, socket, cell), cell * .14f);
        RepositoryGUI.Frame(s, new Color(.3f, 1f, .5f, .9f), Mathf.Max(2f, cell * .07f));
        GUI.Label(s, "SOCKET", RepositoryGUI.Text(Mathf.Clamp(Mathf.RoundToInt(cell * .16f), 8, 13), TextAnchor.MiddleCenter, new Color(.3f, 1f, .5f, .8f)));

        // Ballast and core
        Rect b = Inset(CellAt(grid, ballastView, cell), cell * .1f);
        RepositoryGUI.Fill(b, new Color(.48f, .42f, .62f, 1f));
        RepositoryGUI.Frame(b, new Color(.75f, .7f, .9f, 1f), 2f);
        GUI.Label(b, "BALLAST", RepositoryGUI.Text(Mathf.Clamp(Mathf.RoundToInt(cell * .15f), 8, 12), TextAnchor.MiddleCenter, Color.white, true));
        Rect c = Inset(CellAt(grid, coreView, cell), cell * .16f);
        float pulse = .75f + .25f * Mathf.Sin(Time.unscaledTime * 5f);
        RepositoryGUI.Fill(Inset(c, -cell * .06f), new Color(.55f, .45f, 1f, .35f * pulse));
        RepositoryGUI.Fill(c, new Color(.62f, .5f, 1f, 1f));
        RepositoryGUI.Frame(c, Color.white, 2f);
        GUI.Label(c, "CORE", RepositoryGUI.Text(Mathf.Clamp(Mathf.RoundToInt(cell * .17f), 8, 14), TextAnchor.MiddleCenter, Color.white, true));

        // Gravity indicator on the board edge the blocks are falling toward.
        Color arrow = new Color(.62f, .5f, 1f, .9f);
        float t = 6f;
        if (gravity.y > 0) RepositoryGUI.Fill(new Rect(grid.x, grid.yMax + 2f, grid.width, t), arrow);
        else if (gravity.y < 0) RepositoryGUI.Fill(new Rect(grid.x, grid.y - t - 2f, grid.width, t), arrow);
        else if (gravity.x > 0) RepositoryGUI.Fill(new Rect(grid.xMax + 2f, grid.y, t, grid.height), arrow);
        else RepositoryGUI.Fill(new Rect(grid.x - t - 2f, grid.y, t, grid.height), arrow);

        // Controls
        float rowY = grid.yMax + 16f;
        float bw = Mathf.Min(96f, (area.width - 40f) / 5f);
        float total = bw * 5f + 32f;
        float x0 = area.x + (area.width - total) * .5f;
        GUI.enabled = !Solved;
        if (GUI.Button(new Rect(x0, rowY, bw, 34f), "UP (W)")) Flip(new Vector2Int(0, -1));
        if (GUI.Button(new Rect(x0 + (bw + 8f), rowY, bw, 34f), "LEFT (A)")) Flip(new Vector2Int(-1, 0));
        if (GUI.Button(new Rect(x0 + (bw + 8f) * 2f, rowY, bw, 34f), "DOWN (S)")) Flip(new Vector2Int(0, 1));
        if (GUI.Button(new Rect(x0 + (bw + 8f) * 3f, rowY, bw, 34f), "RIGHT (D)")) Flip(new Vector2Int(1, 0));
        if (GUI.Button(new Rect(x0 + (bw + 8f) * 4f, rowY, bw, 34f), "UNDO (Z)")) Undo();
        GUI.enabled = true;
        Color counter = flips >= FlipLimit ? new Color(1f, .45f, .3f) : new Color(.8f, .85f, 1f);
        GUI.Label(new Rect(area.x, rowY + 40f, area.width, 24f), "FLIPS  " + flips + " / " + FlipLimit,
            RepositoryGUI.Text(15, TextAnchor.MiddleCenter, counter, true));
    }

    private static Rect CellAt(Rect grid, Vector2 p, float cell) { return new Rect(grid.x + p.x * cell, grid.y + p.y * cell, cell, cell); }
    private static Rect Inset(Rect r, float by) { return new Rect(r.x + by, r.y + by, r.width - by * 2f, r.height - by * 2f); }
}

// ---------------------------------------------------------------------------------------------
// NODE III — SOLIDIFY AIR: solidify at most three air cells so stone carries the pulse from
// the source to the relay and the vault. Unstable air cannot hold a field.
// ---------------------------------------------------------------------------------------------
public sealed class AirBridgePuzzle : RepositoryNodePuzzle
{
    // '#' stone (conducts), '.' air (can be solidified), 'x' unstable air, S source, R relay, G vault.
    private static readonly string[] Layout = {
        "..#x.#",
        "Sx.x.G",
        "..#x..",
        "##xx##",
        "#x.###",
        "#.R#.#"
    };
    public const int FieldBudget = 3;
    private const float PulseDuration = 1.1f;

    private readonly int width, height;
    private readonly char[,] kind;
    private readonly bool[,] field;
    private readonly int[,] pulseDistance;
    private Vector2Int source, relay, vault;
    private int maxDistance;
    private float pulseTime = -1f;
    private bool showReach;

    public override string Instruction
    {
        get { return "Stone carries the pulse; air does not. Solidify up to " + FieldBudget + " air cells so one pulse powers both the RELAY and the VAULT. Unstable air (red) cannot hold a field."; }
    }
    public override string Controls { get { return "Click air to solidify or reclaim • Space or SEND PULSE fires • R resets • Esc closes"; } }

    public AirBridgePuzzle()
    {
        height = Layout.Length;
        width = Layout[0].Length;
        kind = new char[width, height];
        field = new bool[width, height];
        pulseDistance = new int[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                char c = Layout[y][x];
                kind[x, y] = c;
                if (c == 'S') source = new Vector2Int(x, y);
                if (c == 'R') relay = new Vector2Int(x, y);
                if (c == 'G') vault = new Vector2Int(x, y);
            }
        Reset();
    }

    public override void Reset()
    {
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) field[x, y] = false;
        pulseTime = -1f;
        showReach = false;
        Solved = false;
    }

    private int FieldCount
    {
        get { int n = 0; for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (field[x, y]) n++; return n; }
    }

    private bool Conducts(int x, int y)
    {
        char c = kind[x, y];
        return c == '#' || c == 'S' || c == 'R' || c == 'G' || field[x, y];
    }

    public override void Tick(float dt)
    {
        if (pulseTime >= 0f)
        {
            pulseTime += dt;
            if (pulseTime >= PulseDuration)
            {
                pulseTime = -1f;
                bool relayLit = pulseDistance[relay.x, relay.y] >= 0;
                bool vaultLit = pulseDistance[vault.x, vault.y] >= 0;
                if (relayLit && vaultLit) { Solved = true; Say("Relay and vault powered."); }
                else if (!relayLit && !vaultLit) Say("The pulse died near the source.", true, 3f);
                else Say(relayLit ? "Relay lit, but the vault is still dark." : "Vault lit, but the relay is still dark.", true, 3f);
            }
        }
        if (!Solved && pulseTime < 0f && Input.GetKeyDown(KeyCode.Space)) SendPulse();
    }

    private void SendPulse()
    {
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pulseDistance[x, y] = -1;
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(source);
        pulseDistance[source.x, source.y] = 0;
        maxDistance = 0;
        Vector2Int[] steps = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            foreach (Vector2Int s in steps)
            {
                Vector2Int n = p + s;
                if (n.x < 0 || n.y < 0 || n.x >= width || n.y >= height) continue;
                if (pulseDistance[n.x, n.y] >= 0 || !Conducts(n.x, n.y)) continue;
                pulseDistance[n.x, n.y] = pulseDistance[p.x, p.y] + 1;
                maxDistance = Mathf.Max(maxDistance, pulseDistance[n.x, n.y]);
                queue.Enqueue(n);
            }
        }
        pulseTime = 0f;
        showReach = true;
    }

    private void Toggle(int x, int y)
    {
        char c = kind[x, y];
        if (c == 'x') { Say("Unstable air cannot hold a field.", true); return; }
        if (c != '.') return;
        if (!field[x, y] && FieldCount >= FieldBudget) { Say("Only three fields can hold. Reclaim one first.", true); return; }
        field[x, y] = !field[x, y];
        showReach = false;
    }

    public override void Draw(Rect area)
    {
        const float controlsHeight = 92f;
        Rect grid = FitSquare(area, controlsHeight);
        float cell = grid.width / width;
        float reach = pulseTime >= 0f ? Mathf.Lerp(0f, maxDistance + 1f, pulseTime / PulseDuration) : float.MaxValue;
        Color pulseColor = new Color(1f, .86f, .4f, 1f);
        int glyphSize = Mathf.Clamp(Mathf.RoundToInt(cell * .2f), 9, 15);

        RepositoryGUI.Fill(grid, new Color(.02f, .03f, .06f, 1f));
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Rect r = new Rect(grid.x + x * cell + 2f, grid.y + y * cell + 2f, cell - 4f, cell - 4f);
                char c = kind[x, y];
                bool lit = showReach && pulseDistance[x, y] >= 0 && pulseDistance[x, y] <= reach;
                if (c == '.')
                {
                    if (field[x, y])
                    {
                        RepositoryGUI.Fill(r, new Color(.15f, .85f, 1f, .75f));
                        RepositoryGUI.Frame(r, new Color(.6f, 1f, 1f, 1f), 2f);
                    }
                    else
                    {
                        RepositoryGUI.Fill(r, new Color(.06f, .09f, .15f, 1f));
                        RepositoryGUI.Frame(r, new Color(.2f, .35f, .45f, .55f), 1f);
                    }
                }
                else if (c == 'x')
                {
                    RepositoryGUI.Fill(r, new Color(.22f, .04f, .05f, 1f));
                    GUI.Label(r, "UNSTABLE", RepositoryGUI.Text(Mathf.Clamp(Mathf.RoundToInt(cell * .13f), 7, 11), TextAnchor.MiddleCenter, new Color(1f, .35f, .3f, .8f)));
                }
                else
                {
                    RepositoryGUI.Fill(r, new Color(.62f, .58f, .5f, 1f));
                }
                if (lit) RepositoryGUI.Fill(Inset(r, cell * .18f), RepositoryGUI.WithAlpha(pulseColor, .85f));
                if (c == 'S' || c == 'R' || c == 'G')
                {
                    string name = c == 'S' ? "SOURCE" : c == 'R' ? "RELAY" : "VAULT";
                    RepositoryGUI.Frame(r, c == 'S' ? pulseColor : new Color(.3f, 1f, .55f, 1f), 3f);
                    GUI.Label(r, name, RepositoryGUI.Text(glyphSize, TextAnchor.MiddleCenter, new Color(.05f, .05f, .08f, 1f), true));
                }
            }

        if (!Solved && pulseTime < 0f)
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.MouseDown && e.button == 0 && grid.Contains(e.mousePosition))
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.x - grid.x) / cell), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - grid.y) / cell), 0, height - 1);
                Toggle(x, y);
                e.Use();
            }
        }

        float rowY = grid.yMax + 16f;
        GUI.enabled = !Solved && pulseTime < 0f;
        if (GUI.Button(new Rect(area.x + (area.width - 220f) * .5f, rowY, 220f, 36f), "SEND PULSE (Space)")) SendPulse();
        GUI.enabled = true;
        GUI.Label(new Rect(area.x, rowY + 42f, area.width, 24f), "FIELDS  " + FieldCount + " / " + FieldBudget,
            RepositoryGUI.Text(15, TextAnchor.MiddleCenter, new Color(.6f, 1f, 1f), true));
    }

    private static Rect Inset(Rect r, float by) { return new Rect(r.x + by, r.y + by, r.width - by * 2f, r.height - by * 2f); }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KineticConnectionPuzzle : MonoBehaviour
{
    public GameObject movableWeight;
    public MonoBehaviour weightDrag;
    public Camera playerCamera;
    public float interactionDistance = 6f;
    public Vector3 weightStartPosition = new Vector3(-9.012f, 1.18f, 20.04f);
    public Vector3 weightStartEuler = Vector3.zero;

    const int Size = 6;
    readonly Vector2Int[,] ends = {
        { new Vector2Int(0,0), new Vector2Int(3,1) },
        { new Vector2Int(2,1), new Vector2Int(5,2) },
        { new Vector2Int(5,3), new Vector2Int(2,4) },
        { new Vector2Int(3,4), new Vector2Int(0,5) }
    };
    readonly Color[] colors = {
        new Color(1f,.18f,.82f), new Color(.15f,1f,.2f),
        new Color(.05f,.85f,1f), new Color(1f,.68f,.05f)
    };
    readonly int[,] occupied = new int[Size,Size];
    List<Vector2Int>[] paths;
    Texture2D pixel;
    bool open, solved, focused;
    int drawing = -1;
    Vector2Int target, hovered = new Vector2Int(-1,-1);
    string message = "";
    float messageUntil;
    CursorLockMode oldLock;
    bool oldVisible;
    Behaviour mouseLook;

    void Awake()
    {
        pixel = new Texture2D(1,1);
        pixel.SetPixel(0,0,Color.white);
        pixel.Apply();
        paths = new List<Vector2Int>[4];
        for(int i=0;i<4;i++) paths[i]=new List<Vector2Int>();
        if(playerCamera==null) playerCamera=Camera.main;
        if(playerCamera!=null) foreach(MonoBehaviour b in playerCamera.GetComponents<MonoBehaviour>())
            if(b!=null && b.GetType().Name=="MouseLook"){ mouseLook=b; break; }
        ResetBoard();
    }

    void Start(){ ResetWeight(); }

    void Update()
    {
        if(open) ArchiveModal.MarkOpen();
        if(solved || playerCamera==null) return;
        if(!open)
        {
            RaycastHit hit;
            focused=Physics.Raycast(playerCamera.ViewportPointToRay(new Vector3(.5f,.5f)),out hit,
                interactionDistance,~0,QueryTriggerInteraction.Ignore)
                && (hit.transform==transform || hit.transform.IsChildOf(transform));
            if(focused && !MouseLook.isPaused && Input.GetKeyDown(KeyCode.E)) Open();
        }
        else if(Input.GetKeyDown(KeyCode.Escape)){ ResetBoard(); Close(); }
        else if(Input.GetKeyDown(KeyCode.R)){ ResetBoard(); Status("CONNECTIONS RESET"); }
    }

    void Open()
    {
        open=true; ResetBoard();
        oldLock=Cursor.lockState; oldVisible=Cursor.visible;
        Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        if(mouseLook!=null) mouseLook.enabled=false;
    }

    void Close()
    {
        open=false; drawing=-1;
        Cursor.lockState=oldLock; Cursor.visible=oldVisible;
        if(mouseLook!=null) mouseLook.enabled=true;
    }

    void ResetBoard()
    {
        for(int y=0;y<Size;y++) for(int x=0;x<Size;x++) occupied[x,y]=-1;
        if(paths!=null) for(int i=0;i<4;i++) paths[i].Clear();
        for(int i=0;i<4;i++){ occupied[ends[i,0].x,ends[i,0].y]=i; occupied[ends[i,1].x,ends[i,1].y]=i; }
        drawing=-1; hovered=new Vector2Int(-1,-1);
    }

    void ResetWeight()
    {
        if(movableWeight==null) return;
        movableWeight.SetActive(true);
        Rigidbody rb=movableWeight.GetComponent<Rigidbody>();
        if(rb!=null)
        {
            rb.isKinematic=true; rb.velocity=Vector3.zero; rb.angularVelocity=Vector3.zero;
            rb.position=weightStartPosition; rb.rotation=Quaternion.Euler(weightStartEuler);
        }
        else
        {
            movableWeight.transform.position=weightStartPosition;
            movableWeight.transform.rotation=Quaternion.Euler(weightStartEuler);
        }
        if(weightDrag!=null) weightDrag.enabled=false;
        movableWeight.SetActive(false);
        Physics.SyncTransforms();
    }

void OnGUI()
    {
        if(!open && (!focused || solved)) return;

        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);

        if(!open)
        {
            GUI.Box(ResponsiveIMGUI.BottomLeft(vw, vh, 490f, 65f),
                "KINETIC RULE DIAGRAM\nPress E to open the connection matrix");
            ResponsiveIMGUI.End(previous);
            return;
        }

        float side=Mathf.Min(Mathf.Min(vw*.58f,vh*.70f),600f);
        side=Mathf.Max(side,180f);
        float panelWidth=Mathf.Min(side+84f,vw-24f);
        float panelHeight=Mathf.Min(side+124f,vh-24f);
        side=Mathf.Min(side,Mathf.Min(panelWidth-84f,panelHeight-124f));
        Rect panel=new Rect((vw-panelWidth)*.5f,(vh-panelHeight)*.5f,panelWidth,panelHeight);
        Rect grid=new Rect(panel.x+(panel.width-side)*.5f,panel.y+58f,side,side);
        float cell=side/Size;
        GUI.color=new Color(.055f,.008f,.12f,.98f); GUI.DrawTexture(panel,pixel); GUI.color=Color.white;

        GUIStyle title=new GUIStyle(GUI.skin.label);
        title.alignment=TextAnchor.MiddleCenter; title.fontSize=23; title.normal.textColor=new Color(.25f,.95f,1f);
        GUI.Label(new Rect(panel.x,panel.y+10f,panel.width,38f),"KINETIC CONNECTION MATRIX",title);

        for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
        {
            GUI.color=new Color(.25f,.10f,.48f,.72f);
            GUI.Box(CellRect(grid,x,y),"");
        }

        for(int c=0;c<4;c++)
            for(int i=1;i<paths[c].Count;i++)
                Line(Center(grid,paths[c][i-1]),Center(grid,paths[c][i]),colors[c],cell*.34f);

        for(int c=0;c<4;c++){ Node(grid,ends[c,0],colors[c],cell); Node(grid,ends[c,1],colors[c],cell); }

        GUI.color=Color.white;
        GUIStyle help=new GUIStyle(GUI.skin.label);
        help.alignment=TextAnchor.MiddleCenter; help.fontSize=13; help.wordWrap=true;
        help.normal.textColor=Time.unscaledTime<messageUntil?new Color(1f,.42f,.28f):Color.white;
        string text=Time.unscaledTime<messageUntil?message:
            "Fill every cell • No overlaps/intersections • R or Reset clears • Esc exits and resets";
        float resetWidth=Mathf.Min(82f,panel.width*.20f);
        GUI.Label(new Rect(panel.x+8f,panel.yMax-56f,panel.width-resetWidth-28f,44f),text,help);
        if(GUI.Button(new Rect(panel.xMax-resetWidth-16f,panel.yMax-47f,resetWidth,32f),"RESET"))
        { ResetBoard(); Status("CONNECTIONS RESET"); }

        InputGrid(grid);
        GUI.color=Color.white;
        ResponsiveIMGUI.End(previous);
    }

void InputGrid(Rect grid)
    {
        Event e=Event.current;
        Vector2Int cell=MouseCell(grid,e.mousePosition);
        bool inside=cell.x>=0;

        if(e.type==EventType.MouseDown && e.button==0 && inside)
        {
            int c=EndColor(cell);
            if(c>=0)
            {
                Clear(c); drawing=c; target=cell==ends[c,0]?ends[c,1]:ends[c,0];
                paths[c].Add(cell); hovered=cell; e.Use();
            }
        }
        else if((e.type==EventType.MouseDrag || e.type==EventType.MouseMove) && drawing>=0 && inside && cell!=hovered)
        { Extend(cell); hovered=cell; e.Use(); }
        else if(e.type==EventType.MouseUp && e.button==0)
        { drawing=-1; hovered=new Vector2Int(-1,-1); e.Use(); }
    }

    void Extend(Vector2Int cell)
    {
        List<Vector2Int> path=paths[drawing];
        Vector2Int last=path[path.Count-1];
        if(Mathf.Abs(cell.x-last.x)+Mathf.Abs(cell.y-last.y)!=1){ Status("USE ADJACENT GRID CELLS"); return; }

        if(path.Count>1 && cell==path[path.Count-2])
        {
            if(EndColor(last)<0) occupied[last.x,last.y]=-1;
            path.RemoveAt(path.Count-1); return;
        }

        int endpoint=EndColor(cell);
        if(endpoint>=0)
        {
            if(endpoint!=drawing || cell!=target){ Status("MATCH THE SAME COLOR"); return; }
            path.Add(cell); drawing=-1;
            if(Solved()) StartCoroutine(Complete());
            return;
        }

        if(occupied[cell.x,cell.y]>=0){ Status("INTERSECTION BLOCKED"); return; }
        occupied[cell.x,cell.y]=drawing; path.Add(cell);
    }

    void Clear(int c)
    {
        for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
            if(occupied[x,y]==c && EndColor(new Vector2Int(x,y))<0) occupied[x,y]=-1;
        paths[c].Clear();
    }

    bool Solved()
    {
        for(int c=0;c<4;c++)
        {
            if(paths[c].Count<2) return false;
            Vector2Int a=paths[c][0], b=paths[c][paths[c].Count-1];
            if(!((a==ends[c,0]&&b==ends[c,1])||(a==ends[c,1]&&b==ends[c,0]))) return false;
        }
        for(int y=0;y<Size;y++) for(int x=0;x<Size;x++) if(occupied[x,y]<0) return false;
        return true;
    }

    IEnumerator Complete()
    {
        solved=true; Status("MATRIX STABLE — ARCHIVE WEIGHT RELEASED");
        movableWeight.SetActive(true);
        movableWeight.transform.position=weightStartPosition;
        movableWeight.transform.rotation=Quaternion.Euler(weightStartEuler);
        Rigidbody rb=movableWeight.GetComponent<Rigidbody>();
        if(rb!=null){ rb.isKinematic=false; rb.velocity=Vector3.zero; rb.angularVelocity=Vector3.zero; rb.WakeUp(); }
        if(weightDrag!=null) weightDrag.enabled=true;
        Physics.SyncTransforms();
        yield return new WaitForSecondsRealtime(1.2f);
        Close();
    }

    int EndColor(Vector2Int p)
    {
        for(int c=0;c<4;c++) if(p==ends[c,0]||p==ends[c,1]) return c;
        return -1;
    }

    Vector2Int MouseCell(Rect grid,Vector2 mouse)
    {
        if(!grid.Contains(mouse)) return new Vector2Int(-1,-1);
        float s=grid.width/Size;
        return new Vector2Int(Mathf.FloorToInt((mouse.x-grid.x)/s),Mathf.FloorToInt((mouse.y-grid.y)/s));
    }

    Rect CellRect(Rect g,int x,int y)
    {
        float s=g.width/Size; return new Rect(g.x+x*s+2,g.y+y*s+2,s-4,s-4);
    }

    Vector2 Center(Rect g,Vector2Int p)
    {
        float s=g.width/Size; return new Vector2(g.x+(p.x+.5f)*s,g.y+(p.y+.5f)*s);
    }

    void Node(Rect g,Vector2Int p,Color c,float cell)
    {
        Vector2 v=Center(g,p); float s=cell*.56f; GUI.color=c;
        GUI.DrawTexture(new Rect(v.x-s*.5f,v.y-s*.5f,s,s),pixel);
    }

void Line(Vector2 a,Vector2 b,Color c,float width)
    {
        GUI.color=c;
        float dx=Mathf.Abs(b.x-a.x);
        float dy=Mathf.Abs(b.y-a.y);
        if(dx>=dy)
        {
            float left=Mathf.Min(a.x,b.x);
            GUI.DrawTexture(new Rect(left,a.y-width*.5f,dx,width),pixel);
        }
        else
        {
            float top=Mathf.Min(a.y,b.y);
            GUI.DrawTexture(new Rect(a.x-width*.5f,top,width,dy),pixel);
        }
    }

    void Status(string value){ message=value; messageUntil=Time.unscaledTime+1.3f; }

    void OnDisable(){ if(open){ ResetBoard(); Close(); } }
}

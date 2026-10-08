using UnityEngine;

public class ArchiveStageMechanics : MonoBehaviour
{
    public enum StageMode { Gravity, Air }
    public StageMode mode;
    public CharacterController player;
    public ArchiveGravityPlayer gravityPlayer;
    public ArchivePhysicsCore core;
    public ArchiveAirPlatform[] airPlatforms=new ArchiveAirPlatform[0];
    public ArchiveGravityAnchor[] anchors=new ArchiveGravityAnchor[0];
    public ArchiveTransitSeal transitSeal;
    public Collider plateSupport;
    public Vector3 platePosition;
    public ArchiveAirPlatform requiredSupport;
    public GameObject[] rewardObjects=new GameObject[0], sealedObjects=new GameObject[0];
    public int platformBudget=3;
    public float nodeReach=18;
    public bool IsSolved { get; private set; }
    public int CheckpointIndex { get; private set; }
    public int ActivePlatforms { get { int count=0;foreach(var p in airPlatforms)if(p!=null&&p.IsSolid)count++;return count; } }
    Vector3 checkpoint, initialSpawn;
    bool checkpointInverted;
    PlayerFallRespawn oldRespawn;
    bool oldRespawnEnabled;
    float contactTime, messageUntil;
    string message="", hint="";
    bool initialized;

    void Start()
    {
        if(player==null)player=FindObjectOfType<CharacterController>();
        if(core!=null)Physics.IgnoreCollision(core.GetComponent<Collider>(),player,true);
        initialSpawn=checkpoint=player.transform.position;
        oldRespawn=player.GetComponent<PlayerFallRespawn>();
        if(oldRespawn!=null){oldRespawnEnabled=oldRespawn.enabled;oldRespawn.enabled=false;}
        foreach(var r in rewardObjects)if(r!=null)r.SetActive(false);
        foreach(var s in sealedObjects)if(s!=null)s.SetActive(true);
        foreach(var p in airPlatforms)if(p!=null)p.SetSolid(false);
        initialized=true;
    }

void Update()
    {
        if(player==null)return;
        if(player.transform.position.y<-7||player.transform.position.y>24){if(!IsSolved&&core!=null)core.ResetCore();RespawnAtCheckpoint();}
        if(MouseLook.isPaused||Time.timeScale==0)return;
        hint="";
        if(mode==StageMode.Gravity) {
            ArchiveGravityAnchor nearest=null;float distance=3.2f;
            foreach(var anchor in anchors){
                if(anchor==null)continue;
                float d=Vector3.Distance(player.transform.position,anchor.transform.position);
                if(d<distance){distance=d;nearest=anchor;}
            }
            if(nearest!=null){
                hint="E • "+nearest.anchorLabel+" | R • Recover checkpoint";
                if(Input.GetKeyDown(KeyCode.E))ActivateAnchor(nearest);
            }
        }else{
            var target=FocusedAirPlatform();
            if(target!=null){
                hint="E • "+(target.IsSolid?"Reclaim ":"Solidify ")+target.nodeLabel+" | "+ActivePlatforms+"/"+platformBudget+" active";
                if(Input.GetKeyDown(KeyCode.E))TogglePlatform(target);
            }
        }
        if(Input.GetKeyDown(KeyCode.R)){
            if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))ResetPuzzle();
            else ResetAtCheckpoint();
        }
        if(core!=null&&(core.IsFocused||core.Held))hint=core.Held?"Move view to drag • Scroll adjusts reach • Release to place":"Hold Left Mouse • Drag Archive core";
        EvaluateVault(Time.deltaTime);
    }
    public void ActivateAnchor(ArchiveGravityAnchor anchor)
    {
        if(anchor==null)return;
        if(anchor.controlsCore){core.SetGravityUp(!core.GravityUp);Notify(core.GravityUp?"Core polarity: UP.":"Core polarity: DOWN.");return;}
        gravityPlayer.SetInverted(anchor.invertPlayer);
        bool linked=anchor.linkedCargo&&core!=null&&Vector3.Distance(core.Body.position,anchor.transform.position)<=anchor.cargoRadius;
        if(linked)core.SetGravityUp(anchor.invertPlayer);
        Notify((anchor.invertPlayer?"Gravity: CEILING.":"Gravity: FLOOR.")+(linked?" Nearby cargo follows.":" Cargo out of anchor range."));
    }
    public void PlatformOverloaded(){Notify("LIGHT-ONLY field overloaded by cargo. Use reinforced fields for the core.");}

    ArchiveAirPlatform FocusedAirPlatform()
    {
        var camera=Camera.main;if(camera==null)return null;
        var hits=Physics.RaycastAll(camera.ViewportPointToRay(new Vector3(.5f,.5f)),nodeReach,~0,QueryTriggerInteraction.Collide);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits){
            if(hit.collider.GetComponent<CharacterController>()!=null)continue;
            var node=hit.collider.GetComponent<ArchiveAirPlatform>();
            if(node!=null)return node.stage==this?node:null;
            if(!hit.collider.isTrigger)return null;
        }
        return null;
    }

    public bool TogglePlatform(ArchiveAirPlatform node)
    {
        if(node==null||node.stage!=this)return false;
        if(node.IsSolid){
            if(node.SupportsOccupant(player,core)){Notify("Occupied platform: move yourself and the core onto safe stone first.");return false;}
            node.SetSolid(false);return true;
        }
        if(ActivePlatforms>=platformBudget){Notify("Three-platform limit. Reclaim an unused platform behind you.");return false;}
        node.SetSolid(true);return true;
    }

public void RecordCheckpoint(int index,Vector3 position,bool inverted)
    {
        if(index<=CheckpointIndex)return;
        if(mode==StageMode.Gravity&&transitSeal!=null&&!transitSeal.IsLatched)return;
        CheckpointIndex=index;checkpoint=position;checkpointInverted=inverted;
        if(core!=null)core.SetRecoveryPoint(core.Body.position,core.GravityUp);
        Notify("Cargo checkpoint "+index+" secured.");
    }

    public void RespawnAtCheckpoint()
    {
        if(gravityPlayer!=null)gravityPlayer.Respawn(checkpoint,checkpointInverted);
        else {player.enabled=false;player.transform.position=checkpoint;player.enabled=true;Physics.SyncTransforms();}
        Notify("Returned to checkpoint "+CheckpointIndex+".");
    }
public void ResetAtCheckpoint()
    {
        if(!IsSolved&&core!=null)core.ResetCore();
        RespawnAtCheckpoint();
        foreach(var p in airPlatforms)if(p!=null)p.SetSolid(false);
        Notify("Player and cargo recovered at checkpoint "+CheckpointIndex+".");
    }
public void ResetPuzzle()
    {
        IsSolved=false;contactTime=0;CheckpointIndex=0;checkpoint=initialSpawn;checkpointInverted=false;
        if(transitSeal!=null)transitSeal.ResetSeal();
        foreach(var r in rewardObjects)if(r!=null)r.SetActive(false);
        foreach(var s in sealedObjects)if(s!=null)s.SetActive(true);
        if(core!=null)core.ResetToStart();
        foreach(var p in airPlatforms)if(p!=null)p.SetSolid(false);
        RespawnAtCheckpoint();
    }
    public void EvaluateVault(float deltaTime)
    {
        if(IsSolved||core==null||plateSupport==null)return;
        if(transitSeal!=null&&!transitSeal.IsLatched){contactTime=0;return;}
        Vector3 delta=core.Body.position-platePosition;
        bool aligned=Mathf.Abs(delta.x)<.85f&&Mathf.Abs(delta.z)<.85f;
        bool directionCorrect=mode==StageMode.Gravity?core.GravityUp:!core.GravityUp;
        bool supportReady=requiredSupport==null||requiredSupport.IsSolid;
        RaycastHit hit;
        bool touching=Physics.Raycast(core.Body.position,mode==StageMode.Gravity?Vector3.up:Vector3.down,out hit,1.2f,~0,QueryTriggerInteraction.Ignore)
            &&hit.collider==plateSupport;
        if(!core.Held&&directionCorrect&&supportReady&&aligned&&touching&&core.Body.velocity.sqrMagnitude<2.25f)contactTime+=deltaTime;
        else contactTime=0;
        if(contactTime<1)return;
        IsSolved=true;
        foreach(var s in sealedObjects)if(s!=null)s.SetActive(false);
        foreach(var r in rewardObjects)if(r!=null)r.SetActive(true);
        Notify("Vault open. Enter and press E to archive the Rule Card.");
    }
    void Notify(string text){message=text;messageUntil=Time.time+4;}
    void OnDisable(){if(initialized&&oldRespawn!=null)oldRespawn.enabled=oldRespawnEnabled;}
    void OnGUI()
    {
        float scale=Mathf.Min(Screen.width/1100f,Screen.height/760f);
        var style=new GUIStyle(GUI.skin.box){fontSize=Mathf.Max(10,Mathf.RoundToInt(18*scale)),wordWrap=true,alignment=TextAnchor.MiddleCenter};
        string status=mode==StageMode.Gravity?"INVERT GRAVITY • "+(gravityPlayer!=null&&gravityPlayer.IsInverted?"CEILING":"FLOOR"):"SOLIDIFY AIR • "+ActivePlatforms+"/"+platformBudget+" platforms";
        GUI.Box(new Rect(12,12,Mathf.Min(Screen.width-24,560*scale),58*scale),status+" • Checkpoint "+CheckpointIndex,style);
        string text=Time.time<messageUntil?message:hint;
        if(string.IsNullOrEmpty(text)&&mode==StageMode.Air)text="Aim + E: toggle field • Cargo needs reinforced fields • R: recover • Shift+R: restart";
        if(!string.IsNullOrEmpty(text))
            GUI.Box(new Rect(Screen.width*.12f,Screen.height-95*scale,Screen.width*.76f,76*scale),text,style);
    }
}
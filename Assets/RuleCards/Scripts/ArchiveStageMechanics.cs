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
        foreach(var r in rewardObjects)RuleCardPickup.SetVaultOpen(r,false);
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
        CoreSounds();
    }
    bool coreWasHeld;
    void CoreSounds()
    {
        if(core==null||core.Body==null)return;
        if(core.Held&&!coreWasHeld)RuleCardsAudio.PlayAt("stone_push",core.Body.position,.6f,1.1f);
        coreWasHeld=core.Held;
        float speed=core.Body.velocity.magnitude;
        if(core.Held&&speed>.4f)RuleCardsAudio.LoopAt("core_drag","drag_loop",Mathf.Min(.28f,speed*.04f),core.Body.position,.8f);
    }
    public void ActivateAnchor(ArchiveGravityAnchor anchor)
    {
        if(anchor==null)return;
        if(anchor.controlsCore){RuleCardsAudio.PlayAt("whoosh",core.Body.position,.7f,1.3f);core.SetGravityUp(!core.GravityUp);Notify(core.GravityUp?"Core polarity: UP.":"Core polarity: DOWN.");return;}
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
            if(node.SupportsOccupant(player,core)){RuleCardsAudio.Play("deny",.5f);Notify("Occupied platform: move yourself and the core onto safe stone first.");return false;}
            node.SetSolid(false);RuleCardsAudio.PlayAt("whoosh",node.transform.position,.35f,1.7f);return true;
        }
        if(ActivePlatforms>=platformBudget){RuleCardsAudio.Play("deny",.5f);Notify("Three-platform limit. Reclaim an unused platform behind you.");return false;}
        node.SetSolid(true);RuleCardsAudio.PlayAt("connected",node.transform.position,.45f,1.15f);return true;
    }

public void RecordCheckpoint(int index,Vector3 position,bool inverted)
    {
        if(index<=CheckpointIndex)return;
        if(mode==StageMode.Gravity&&transitSeal!=null&&!transitSeal.IsLatched)return;
        CheckpointIndex=index;checkpoint=position;checkpointInverted=inverted;
        if(core!=null)core.SetRecoveryPoint(core.Body.position,core.GravityUp);
        RuleCardsAudio.Play("solved",.45f,.85f);
        Notify("Cargo checkpoint "+index+" secured.");
    }

    public void RespawnAtCheckpoint()
    {
        if(gravityPlayer!=null)gravityPlayer.Respawn(checkpoint,checkpointInverted);
        else {player.enabled=false;player.transform.position=checkpoint;player.enabled=true;Physics.SyncTransforms();}
        RuleCardsAudio.Play("respawn",.7f);
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
        foreach(var r in rewardObjects)RuleCardPickup.SetVaultOpen(r,false);
        foreach(var s in sealedObjects)if(s!=null)s.SetActive(true);
        if(core!=null)core.ResetToStart();
        foreach(var p in airPlatforms)if(p!=null)p.SetSolid(false);
        RespawnAtCheckpoint();
    }
    public void EvaluateVault(float deltaTime)
    {
        if(IsSolved||core==null||plateSupport==null)return;
        // The vault only needs the gate to have been opened once; it no longer waits on the strict latch.
        if(transitSeal!=null&&!transitSeal.IsLatched&&!transitSeal.WasPowered){contactTime=0;return;}
        // When the plate itself is the required air field (Stage 3), align to where that field really is:
        // the field/plate were moved after platePosition was authored, which made the vault unsolvable.
        Vector3 plate=platePosition;
        if(requiredSupport!=null&&plateSupport.gameObject==requiredSupport.gameObject){
            Vector3 c=plateSupport.bounds.center;plate=new Vector3(c.x,platePosition.y,c.z);
        }
        Vector3 delta=core.Body.position-plate;
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
        foreach(var r in rewardObjects)RuleCardPickup.SetVaultOpen(r,true);
        RuleCardsAudio.PlayAt("door_stop",core.Body.position,.9f,.9f,.03f,5f);
        RuleCardsAudio.Play("solved",.8f);
        Notify("Vault open. Enter and press E to archive the Rule Card.");
    }
    void Notify(string text){message=text;messageUntil=Time.time+4;}
    void OnDisable(){if(initialized&&oldRespawn!=null)oldRespawn.enabled=oldRespawnEnabled;}
    void OnGUI()
    {
        if(RuleCardsStory.Blocking)return;
        float vw,vh;
        Matrix4x4 previous=ResponsiveIMGUI.Begin(out vw,out vh);
        // Rule status stacks under the run timer card.
        string status=mode==StageMode.Gravity?"Invert Gravity  ·  "+(gravityPlayer!=null&&gravityPlayer.IsInverted?"Ceiling":"Floor"):"Solidify Air  ·  "+ActivePlatforms+" / "+platformBudget+" fields";
        Rect top=new Rect(RuleCardsRunHud.Margin,RuleCardsRunHud.NextY,Mathf.Min(300f,vw-32f),30f);
        RuleCardsUI.HudCard(top);
        GUI.Label(new Rect(top.x+14f,top.y,top.width-28f,top.height),status,RuleCardsUI.Label(13,RuleCardsUI.Text));
        GUI.Label(new Rect(top.x+14f,top.y,top.width-28f,top.height),"Checkpoint "+CheckpointIndex,RuleCardsUI.Label(12,RuleCardsUI.Muted,TextAnchor.MiddleRight));
        string text=Time.time<messageUntil?message:hint;
        if(string.IsNullOrEmpty(text)&&mode==StageMode.Air)text="Aim + E: toggle field  ·  Cargo needs reinforced fields  ·  R: recover  ·  Shift+R: restart";
        if(!string.IsNullOrEmpty(text))
        {
            Rect r=ResponsiveIMGUI.BottomCenter(vw,vh,Mathf.Min(720f,vw*.8f),58f);
            RuleCardsUI.HudCard(r);
            GUI.Label(new Rect(r.x+16f,r.y,r.width-32f,r.height),text,RuleCardsUI.Prose(17,RuleCardsUI.Text,TextAnchor.MiddleCenter));
        }
        ResponsiveIMGUI.End(previous);
    }
}
using UnityEngine;

public class KineticWeightDrag : MonoBehaviour
{
    public Camera playerCamera;
    public Rigidbody body;
    public float grabDistance=8f;
    public float dragResponsiveness=12f;
    public float scrollSpeed=2.5f;
    public float minimumHeight=.75f;
    public float maximumHeight=4.5f;
    bool dragging,focused,oldGravity;
    float heldDistance;
    Vector3 targetPosition,smoothVelocity;
    RigidbodyConstraints oldConstraints;
    RigidbodyInterpolation oldInterpolation;
    CollisionDetectionMode oldCollisionMode;

    void Awake(){ if(body==null)body=GetComponent<Rigidbody>(); if(playerCamera==null)playerCamera=Camera.main; }

    void Update()
    {
        if(playerCamera==null||body==null)return;
        if(MouseLook.isPaused){ if(dragging)Release(); return; }
        RaycastHit hit;
        focused=!dragging&&Physics.Raycast(playerCamera.ViewportPointToRay(new Vector3(.5f,.5f)),out hit,
            grabDistance,~0,QueryTriggerInteraction.Ignore)
            &&(hit.rigidbody==body||hit.transform==transform||hit.transform.IsChildOf(transform));
        if(!dragging&&focused&&Input.GetMouseButtonDown(0))BeginDrag();
        if(!dragging)return;
        if(!Input.GetMouseButton(0)){Release();return;}
        float speed=body.velocity.magnitude;
        if(speed>.3f)RuleCardsAudio.LoopAt("weight_drag","drag_loop",Mathf.Min(.3f,.08f+speed*.05f),body.worldCenterOfMass,.75f);
        heldDistance=Mathf.Clamp(heldDistance+Input.mouseScrollDelta.y*scrollSpeed,2f,grabDistance);
        targetPosition=playerCamera.transform.position+playerCamera.transform.forward*heldDistance;
        targetPosition.y=Mathf.Clamp(targetPosition.y,minimumHeight,maximumHeight);
    }

    void BeginDrag()
    {
        dragging=true;
        RuleCardsAudio.PlayAt("stone_push",body.worldCenterOfMass,.6f);
        heldDistance=Mathf.Clamp(Vector3.Distance(playerCamera.transform.position,body.worldCenterOfMass),2f,grabDistance);
        targetPosition=body.position; smoothVelocity=Vector3.zero;
        oldConstraints=body.constraints; oldGravity=body.useGravity;
        oldInterpolation=body.interpolation; oldCollisionMode=body.collisionDetectionMode;
        body.useGravity=false; body.constraints=RigidbodyConstraints.FreezeRotation;
        body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        body.velocity=Vector3.zero; body.angularVelocity=Vector3.zero; body.WakeUp();
    }

    void FixedUpdate()
    {
        if(!dragging||body==null)return;
        Vector3 next=Vector3.SmoothDamp(body.position,targetPosition,ref smoothVelocity,
            1f/Mathf.Max(1f,dragResponsiveness),20f,Time.fixedDeltaTime);
        body.MovePosition(next); body.velocity=Vector3.zero; body.angularVelocity=Vector3.zero;
    }

    void Release()
    {
        dragging=false; if(body==null)return;
        body.useGravity=oldGravity; body.constraints=oldConstraints;
        body.interpolation=oldInterpolation; body.collisionDetectionMode=oldCollisionMode;
        body.velocity=Vector3.zero; body.WakeUp();
    }

    void OnDisable(){if(dragging)Release();}

void OnGUI()
    {
        if((!focused && !dragging) || MouseLook.isPaused) return;
        float vw, vh;
        Matrix4x4 previous = ResponsiveIMGUI.Begin(out vw, out vh);
        if(focused && !dragging)
            GUI.Box(ResponsiveIMGUI.BottomCenter(vw, vh, 380f, 58f),
                "ARCHIVE WEIGHT\nHold Left Mouse to drag");
        else if(dragging)
            GUI.Box(ResponsiveIMGUI.BottomCenter(vw, vh, 460f, 52f),
                "Move view to position • Mouse wheel adjusts depth • Release to drop");
        ResponsiveIMGUI.End(previous);
    }
}

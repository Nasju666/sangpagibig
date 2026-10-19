using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ArchiveGravityPlayer : MonoBehaviour
{
    public Camera viewCamera;
    public float gravityStrength=22f, walkSpeed=4f, sprintSpeed=7f, jumpHeight=1.3f;
    public bool IsInverted { get; private set; }
    public bool IsGrounded { get; private set; }
    /// <summary>While true, mouse look still works but walking/gravity are paused (used by Kinetic Link pulls).</summary>
    public bool MovementLocked { get; set; }
    [Header("Feel")]
    public float coyoteTime=.12f, jumpBuffer=.15f;
    /// <summary>Extra field of view in degrees; decays by itself (e.g. a Kinetic pull adds a kick).</summary>
    public float FovKick { get; set; }
    /// <summary>Camera shake strength in degrees, set every frame by whoever wants shake (e.g. the collapse).</summary>
    public float Shake { get; set; }
    /// <summary>Raised when the player lands; the argument is the impact speed in m/s.</summary>
    public event System.Action<float> Landed;
    float lastGroundedTime=-1, jumpPressedTime=-1, landDip;
    bool wasGrounded;
    public Vector3 GravityDirection { get { return IsInverted ? Vector3.up : Vector3.down; } }
    CharacterController controller;
    readonly List<Behaviour> suspended=new List<Behaviour>();
    Vector3 planarVelocity, originalCameraPosition;
    Quaternion originalCameraRotation;
    float verticalSpeed, pitch, yaw, roll, sensitivity=210f;
    bool initialized;

    void Awake()
    {
        controller=GetComponent<CharacterController>();
        if(viewCamera==null)viewCamera=GetComponentInChildren<Camera>();
        yaw=transform.eulerAngles.y;
        if(viewCamera!=null) {
            originalCameraPosition=viewCamera.transform.localPosition;
            originalCameraRotation=viewCamera.transform.localRotation;
            var look=viewCamera.GetComponent<MouseLook>();
            if(look!=null){float t=look.sensitivity/100f;sensitivity=Mathf.Lerp(80,600,t*t);}
        }
        foreach(var b in GetComponentsInChildren<MonoBehaviour>()) {
            string n=b.GetType().Name;
            if(b!=this && b.enabled && (n=="PlayerMovement"||n=="MouseLook"||n=="HeadBob"||n=="CameraFOV")) {
                suspended.Add(b); b.enabled=false;
            }
        }
        initialized=true;
        Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
    }

    void Update()
    {
        if(MouseLook.isPaused||Time.timeScale==0)return;
        Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
        float sign=IsInverted?-1:1;
        // Mouse deltas are per-frame already: no deltaTime, so look speed no longer depends on frame rate.
        float deg=RuleCardsSettings.LookDegreesPerUnit;
        yaw+=Input.GetAxisRaw("Mouse X")*deg*sign;
        pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*deg*(RuleCardsSettings.InvertY?-1:1),-85,85);
        transform.rotation=Quaternion.Euler(0,yaw,0);
        if(!MovementLocked) SimulateMovement(new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")),Input.GetKeyDown(KeyCode.Space),Time.deltaTime);
        roll=Mathf.MoveTowardsAngle(roll,IsInverted?180:0,360*Time.deltaTime);
        if(viewCamera!=null){
            landDip=Mathf.MoveTowards(landDip,0,Time.deltaTime*1.1f);
            float motion=RuleCardsSettings.CameraMotion;
            FovKick=Mathf.MoveTowards(FovKick,0,Time.deltaTime*28);
            viewCamera.transform.localPosition=Vector3.Lerp(viewCamera.transform.localPosition,new Vector3(0,sign*(.65f-landDip*motion),0),14*Time.deltaTime);
            float n=Time.time*26f;
            Vector3 shake=new Vector3(Mathf.PerlinNoise(n,.3f)-.5f,Mathf.PerlinNoise(.7f,n)-.5f,(Mathf.PerlinNoise(n,n*.5f)-.5f)*.6f)*(Shake*2f*motion);
            viewCamera.transform.localRotation=Quaternion.Euler(0,0,roll)*Quaternion.Euler(pitch,0,0)*Quaternion.Euler(shake);
            float baseFov=RuleCardsSettings.FieldOfView;
            viewCamera.fieldOfView=Mathf.Lerp(viewCamera.fieldOfView,baseFov+(Input.GetKey(KeyCode.LeftShift)?10:0)+FovKick,1-Mathf.Exp(-8*Time.deltaTime));
        }
    }

    public void SetInverted(bool inverted)
    {
        if(IsInverted==inverted)return;
        RuleCardsAudio.Play("whoosh",.75f,inverted?.92f:1.08f);
        IsInverted=inverted; verticalSpeed=0; IsGrounded=false; wasGrounded=false; lastGroundedTime=-1;
    }

    public void SimulateMovement(Vector2 input,bool jump,float dt)
    {
        if(controller==null||!controller.enabled)return;
        dt=Mathf.Clamp(dt,0,.05f);
        RaycastHit hit;Vector3 gravity=GravityDirection;
        IsGrounded=Physics.SphereCast(transform.TransformPoint(controller.center),controller.radius*.9f,gravity,out hit,
            controller.height*.5f-controller.radius+.18f,~0,QueryTriggerInteraction.Ignore);
        if(IsGrounded&&!wasGrounded&&verticalSpeed*gravity.y>0){
            float impact=Mathf.Abs(verticalSpeed);
            if(impact>6f)landDip=Mathf.Clamp(impact*.012f,0f,.22f);
            if(Landed!=null)Landed(impact);
        }
        wasGrounded=IsGrounded;
        if(IsGrounded&&verticalSpeed*gravity.y>=0)verticalSpeed=gravity.y*2;
        // Coyote time (jump just after leaving an edge) and jump buffering (press just before landing).
        if(IsGrounded)lastGroundedTime=Time.time;
        if(jump)jumpPressedTime=Time.time;
        if(jumpPressedTime>0&&Time.time-jumpPressedTime<=jumpBuffer&&lastGroundedTime>0&&Time.time-lastGroundedTime<=coyoteTime){
            verticalSpeed=-gravity.y*Mathf.Sqrt(2*gravityStrength*jumpHeight);
            jumpPressedTime=-1;lastGroundedTime=-1;
        }
        Vector3 desired=(transform.right*input.x*(IsInverted?-1:1)+transform.forward*input.y);
        desired=Vector3.ClampMagnitude(desired,1)*(Input.GetKey(KeyCode.LeftShift)?sprintSpeed:walkSpeed);
        planarVelocity=Vector3.Lerp(planarVelocity,desired,1-Mathf.Exp(-10*dt));
        verticalSpeed=Mathf.Clamp(verticalSpeed+gravity.y*gravityStrength*dt,-20,20);
        CollisionFlags flags=controller.Move((planarVelocity+Vector3.up*verticalSpeed)*dt);
        if((verticalSpeed>0&&(flags&CollisionFlags.Above)!=0)||(verticalSpeed<0&&(flags&CollisionFlags.Below)!=0))verticalSpeed=0;
    }

    public void Respawn(Vector3 position,bool inverted)
    {
        controller.enabled=false;transform.position=position;controller.enabled=true;
        IsInverted=inverted;verticalSpeed=0;planarVelocity=Vector3.zero;pitch=0;roll=inverted?180:0;
        landDip=0;FovKick=0;jumpPressedTime=-1;lastGroundedTime=-1;wasGrounded=false;
        Physics.SyncTransforms();
    }

    public void Respawn(Vector3 position,bool inverted,float facingYaw)
    {
        yaw=facingYaw;transform.rotation=Quaternion.Euler(0,yaw,0);MovementLocked=false;
        Respawn(position,inverted);
    }

    void OnDisable()
    {
        if(!initialized)return;
        foreach(var b in suspended)if(b!=null)b.enabled=true;
        suspended.Clear();
        if(viewCamera!=null){viewCamera.transform.localPosition=originalCameraPosition;viewCamera.transform.localRotation=originalCameraRotation;}
    }
}
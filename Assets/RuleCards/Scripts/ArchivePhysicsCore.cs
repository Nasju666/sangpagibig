using UnityEngine;

[RequireComponent(typeof(Rigidbody),typeof(BoxCollider))]
public class ArchivePhysicsCore : MonoBehaviour
{
    public float grabDistance=5f, maximumCarryHeight=2.6f, gravityStrength=22f;
    public bool Held { get; private set; }
    public bool GravityUp { get; private set; }
    public bool IsFocused { get; private set; }
    public Rigidbody Body { get; private set; }
    public Vector3 SpawnPosition { get; private set; }
    public bool allowCeilingCarry;
    public float maximumCarryReach=3f;
    Vector3 recoveryPosition;
    bool recoveryGravity;
    Vector3 target;
    Quaternion spawnRotation;
    float distance;

void Awake()
    {
        Body=GetComponent<Rigidbody>();SpawnPosition=recoveryPosition=transform.position;spawnRotation=transform.rotation;
        Body.useGravity=false;Body.constraints=RigidbodyConstraints.FreezeRotation;
        Body.interpolation=RigidbodyInterpolation.Interpolate;Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
    }
void Update()
    {
        var camera=Camera.main;if(camera==null)return;
        RaycastHit hit;
        IsFocused=!Held&&Physics.Raycast(camera.ViewportPointToRay(new Vector3(.5f,.5f)),out hit,grabDistance,~0,QueryTriggerInteraction.Ignore)&&hit.rigidbody==Body;
        if(!MouseLook.isPaused&&IsFocused&&Input.GetMouseButtonDown(0)){
            Held=true;distance=Mathf.Clamp(Vector3.Distance(camera.transform.position,Body.position),1.5f,maximumCarryReach);
        }
        if(Held){
            if(MouseLook.isPaused||!Input.GetMouseButton(0)){Held=false;Body.velocity=Vector3.zero;}
            else {
                distance=Mathf.Clamp(distance+Input.mouseScrollDelta.y,1.5f,maximumCarryReach);
                target=camera.transform.position+camera.transform.forward*distance;
                var motor=camera.GetComponentInParent<ArchiveGravityPlayer>();
                bool ceiling=allowCeilingCarry&&motor!=null&&motor.IsInverted;
                target.y=ceiling?Mathf.Clamp(target.y,17.25f,19.2f):Mathf.Clamp(target.y,1,maximumCarryHeight);
            }
        }
        if(Body.position.y<-7||Body.position.y>24)ResetCore();
    }
    void FixedUpdate()
    {
        if(Held){
            Vector3 desired=Vector3.ClampMagnitude((target-Body.position)*10,8);
            Body.velocity=Vector3.Lerp(Body.velocity,desired,1-Mathf.Exp(-12*Time.fixedDeltaTime));
        }else Body.AddForce((GravityUp?Vector3.up:Vector3.down)*gravityStrength,ForceMode.Acceleration);
    }
    public void SetGravityUp(bool up)
    {
        GravityUp=up;Held=false;Body.velocity=Vector3.zero;Body.WakeUp();
    }
public void SetRecoveryPoint(Vector3 position,bool up)
    { recoveryPosition=position;recoveryGravity=up; }
    public void ResetToStart()
    { recoveryPosition=SpawnPosition;recoveryGravity=false;ResetCore(); }
    public void ResetCore()
    {
        Held=false;GravityUp=recoveryGravity;Body.position=recoveryPosition;Body.rotation=spawnRotation;
        Body.velocity=Vector3.zero;Body.angularVelocity=Vector3.zero;Body.WakeUp();
    }
    void OnDisable(){Held=false;}
}
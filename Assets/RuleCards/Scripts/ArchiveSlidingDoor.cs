using UnityEngine;

// The leaf remains a physical obstacle while travelling into its wall pocket.
public class ArchiveSlidingDoor : MonoBehaviour
{
    public Vector3 openOffset=new Vector3(0,21,0);
    public float duration=1.8f;
    public bool IsFullyOpen { get { return progress>=.999f; } }
    public float Progress { get { return progress; } }
    Vector3 closedPosition;
    float progress;
    bool wantsOpen,initialized;
    void Awake(){Initialize();}
    void Initialize(){if(initialized)return;closedPosition=transform.localPosition;initialized=true;}
    public void SetOpen(bool open){Initialize();wantsOpen=open;}
    void Update(){Step(Time.deltaTime);}
    public void Step(float dt)
    {
        Initialize();
        float next=Mathf.MoveTowards(progress,wantsOpen?1:0,Mathf.Max(0,dt)/Mathf.Max(.1f,duration));
        var position=closedPosition+openOffset*Mathf.SmoothStep(0,1,next);
        if(next<progress){
            Vector3 worldDelta=transform.parent!=null?transform.parent.TransformVector(position-transform.localPosition):position-transform.localPosition;
            foreach(var shape in GetComponentsInChildren<Collider>()){
                if(shape.isTrigger)continue;
                Bounds sweep=shape.bounds;sweep.Encapsulate(shape.bounds.min+worldDelta);sweep.Encapsulate(shape.bounds.max+worldDelta);
                foreach(var hit in Physics.OverlapBox(sweep.center,sweep.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore)){
                    if(hit.GetComponent<CharacterController>()!=null||hit.GetComponent<ArchivePhysicsCore>()!=null)return;
                }
            }
        }
        progress=next;transform.localPosition=position;
    }
    public void SnapClosed(){Initialize();wantsOpen=false;progress=0;transform.localPosition=closedPosition;}
}
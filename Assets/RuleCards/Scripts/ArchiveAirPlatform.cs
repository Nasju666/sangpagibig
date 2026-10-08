using UnityEngine;

[RequireComponent(typeof(BoxCollider),typeof(MeshRenderer))]
public class ArchiveAirPlatform : MonoBehaviour
{
    public ArchiveStageMechanics stage;
    public Material ghostMaterial, solidMaterial;
    public string nodeLabel="Air node";
    public bool supportsCargo=true;
    public bool IsSolid { get; private set; }
    BoxCollider shape;
    Renderer surface;
    void Awake(){shape=GetComponent<BoxCollider>();surface=GetComponent<Renderer>();SetSolid(false);}
public void SetSolid(bool solid)
    {
        if(shape==null)shape=GetComponent<BoxCollider>();
        if(surface==null)surface=GetComponent<Renderer>();
        IsSolid=solid;shape.isTrigger=!solid;surface.sharedMaterial=solid?solidMaterial:ghostMaterial;
        if(stage!=null&&stage.core!=null)
            Physics.IgnoreCollision(shape,stage.core.GetComponent<Collider>(),!supportsCargo);
    }
    public bool HasCargoLoad()
    {
        if(supportsCargo||!IsSolid||stage==null||stage.core==null)return false;
        Bounds load=shape.bounds;load.center+=Vector3.up*1.5f;load.Expand(new Vector3(.05f,3f,.05f));
        bool weight=load.Intersects(stage.core.GetComponent<Collider>().bounds);
        bool carried=stage.core.Held&&stage.player!=null&&load.Intersects(stage.player.bounds);
        return weight||carried;
    }
    void Update()
    {
        if(HasCargoLoad()){SetSolid(false);stage.PlatformOverloaded();}
    }
    public bool SupportsOccupant(CharacterController player,ArchivePhysicsCore core)
    {
        Bounds safe=shape.bounds;safe.center+=Vector3.up*1.2f;safe.Expand(new Vector3(.1f,2.3f,.1f));
        if(player!=null&&safe.Intersects(player.bounds))return true;
        return core!=null&&safe.Intersects(core.GetComponent<Collider>().bounds);
    }
}
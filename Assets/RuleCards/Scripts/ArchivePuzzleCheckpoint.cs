using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ArchivePuzzleCheckpoint : MonoBehaviour
{
    public ArchiveStageMechanics stage;
    public Vector3 respawnPosition;
    public int checkpointIndex;
    public bool requireCargo;
    public bool inverted;
    void OnTriggerEnter(Collider other)
    {
        if(stage!=null && other.GetComponent<CharacterController>()==stage.player)TryRecord();
    }
    void Update() { TryRecord(); }
public void TryRecord()
    {
        if(stage==null||stage.player==null||stage.CheckpointIndex>=checkpointIndex)return;
        if(stage.gravityPlayer!=null && stage.gravityPlayer.IsInverted!=inverted)return;
        var bounds=GetComponent<BoxCollider>().bounds;
        if(!bounds.Intersects(stage.player.bounds))return;
        if(requireCargo){
            var cargo=stage.core;
            if(cargo==null||cargo.Body==null||cargo.Held||cargo.Body.velocity.sqrMagnitude>2.25f||!bounds.Contains(cargo.Body.position))return;
            RaycastHit support;
            if(!Physics.Raycast(cargo.Body.position,inverted?Vector3.up:Vector3.down,out support,1.3f,~0,QueryTriggerInteraction.Ignore)
                ||support.collider.GetComponent<ArchiveAirPlatform>()!=null)return;
        }
        RaycastHit hit;
        if(Physics.Raycast(stage.player.transform.position,inverted?Vector3.up:Vector3.down,out hit,1.3f,~0,QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponent<ArchiveAirPlatform>()==null)
            stage.RecordCheckpoint(checkpointIndex,respawnPosition,inverted);
    }
}
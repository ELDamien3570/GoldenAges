using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways]
public class WorkSpotsAuthoring : MonoBehaviour
{
    public float snapMaxDistance = 2f;
    public Color freeColor = new Color(0f, 1f, 0f, 0.6f);
    public Color badColor = new Color(1f, 0f, 0f, 0.6f);
    public float gizmoSize = 0.15f;

    void OnDrawGizmos()
    {
        foreach (Transform t in transform)
        {
            if (!t) continue;
            bool onMesh = NavMesh.SamplePosition(t.position, out _, 0.05f, NavMesh.AllAreas);
            Gizmos.color = onMesh ? freeColor : badColor;
            Gizmos.DrawSphere(t.position, gizmoSize);
        }
    }

#if UNITY_EDITOR
    void OnValidate() { SnapAll(); }
    [ContextMenu("Snap All to NavMesh")]
    public void SnapAll()
    {
        foreach (Transform t in transform)
        {
            if (!t) continue;
            if (NavMesh.SamplePosition(t.position, out var hit, snapMaxDistance, NavMesh.AllAreas))
            {
                t.position = hit.position;
            }
        }
    }
#endif
}

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorkSpotRingGenerator : MonoBehaviour
{
    public Transform workSpotsParent;   // assign or will be created
    public int count = 4;
    public float ringRadius = 1.2f;     // visualRadius + agent.radius + 0.2
    public float yOffset = 0f;          // tweak if your mesh pivot isn’t ground level

#if UNITY_EDITOR
    [ContextMenu("Generate Ring Spots")]
    void Generate()
    {
        if (!workSpotsParent)
        {
            var go = new GameObject("WorkSpots");
            go.transform.SetParent(transform, false);
            workSpotsParent = go.transform;
        }

        // Clear existing
        var toDelete = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in workSpotsParent) toDelete.Add(c);
        foreach (var c in toDelete) DestroyImmediate(c.gameObject);

        for (int i = 0; i < count; i++)
        {
            float ang = (Mathf.PI * 2f / count) * i;
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ringRadius;
            var go = new GameObject($"Spot_{i}");
            go.transform.SetParent(workSpotsParent, false);
            go.transform.position = pos + Vector3.up * yOffset;
        }

        // Add visual helper if missing
        if (!workSpotsParent.GetComponent<WorkSpotsAuthoring>())
            workSpotsParent.gameObject.AddComponent<WorkSpotsAuthoring>();

        // Snap to NavMesh (editor-time)
        workSpotsParent.GetComponent<WorkSpotsAuthoring>().SnapAll();

        EditorUtility.SetDirty(workSpotsParent);
        Debug.Log($"{name}: generated {count} spots at radius {ringRadius}");
    }
#endif
}

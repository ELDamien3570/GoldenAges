using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ResourceNode : MonoBehaviour, IWorkProvider
{
    [Header("Type")]
    [SerializeField] private WorkType type = WorkType.Chop;
    public WorkType Type => type;

    [Header("Work Spots")]
    [Tooltip("Parent containing Spot_0, Spot_1, ... children (each a Transform placed on the NavMesh).")]
    [SerializeField] private Transform workSpotsParent;

    [Tooltip("If true, we’ll auto-scan workSpotsParent for child spots in Awake/Validate.")]
    [SerializeField] private bool autoCollectSpots = true;

    [Header("Facing")]
    [Tooltip("If set, workers will face this point while working. Otherwise they face the node's transform.position.")]
    [SerializeField] private Transform lookAtOverride;

    private readonly List<Transform> _spots = new();
    private readonly HashSet<Transform> _taken = new();
    private readonly Dictionary<WorkerUnit, Transform> _reservations = new();

    public bool TryReserve(WorkerUnit worker, out Transform spot)
    {
        spot = null;
        if (!worker) return false;

        if (_reservations.TryGetValue(worker, out var existing) && existing)
        {
            spot = existing;
            return true;
        }

        float best = float.MaxValue;
        Transform pick = null;
        Vector3 wp = worker.transform.position;

        for (int i = 0; i < _spots.Count; i++)
        {
            var s = _spots[i];
            if (!s || _taken.Contains(s)) continue;

            float d = (s.position - wp).sqrMagnitude;
            if (d < best)
            {
                best = d;
                pick = s;
            }
        }

        if (!pick) return false;

        _taken.Add(pick);
        _reservations[worker] = pick;
        spot = pick;
        return true;
    }

    public void Release(WorkerUnit worker)
    {
        if (!worker) return;

        if (_reservations.TryGetValue(worker, out var spot) && spot)
        {
            _taken.Remove(spot);
        }
        _reservations.Remove(worker);
    }

    public Vector3 GetLookAt(Transform reservedSpot)
    {
        return lookAtOverride ? lookAtOverride.position : transform.position;
    }

    private void Awake()
    {
        CollectSpots();
    }

    private void OnValidate()
    {
        if (autoCollectSpots)
            CollectSpots();
    }

    private void OnDisable()
    {
        foreach (var kv in _reservations)
        {
            var worker = kv.Key;
            if (worker) worker.CancelWorkIfTarget(this);
        }
        _reservations.Clear();
        _taken.Clear();
    }

    private void CollectSpots()
    {
        _spots.Clear();
        _taken.Clear();

        if (!workSpotsParent)
        {
            Debug.LogWarning($"{name}: ResourceNode has no WorkSpots parent assigned.", this);
            return;
        }

        foreach (Transform t in workSpotsParent)
        {
            if (t) _spots.Add(t);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (workSpotsParent)
        {
            foreach (Transform t in workSpotsParent)
            {
                if (!t) continue;
                Gizmos.color = (_taken.Contains(t)) ? new Color(1f, 0.4f, 0.2f, 0.8f) : new Color(0.2f, 1f, 0.3f, 0.8f);
                Gizmos.DrawSphere(t.position, 0.12f);
                Gizmos.DrawLine(t.position, (lookAtOverride ? lookAtOverride.position : transform.position));
            }
        }

        Gizmos.color = Color.yellow;
        Vector3 p = lookAtOverride ? lookAtOverride.position : transform.position;
        Gizmos.DrawWireSphere(p, 0.2f);
    }
#endif
}

using UnityEngine;
using System.Collections.Generic;   

public class DropOffBuilding : MonoBehaviour, IDropOff
{
    [Header("Configuration")]
    [SerializeField] private TownEconomy economy;
    [SerializeField] private bool acceptAll = false;
    [SerializeField] private bool acceptWood = false;
    [SerializeField] private bool acceptFood = false;
    [SerializeField] private bool acceptStone = false;

    [Header("Drop Spots")]
    [SerializeField] private Transform dropSpotsParent;

    private readonly List<Transform> _spots = new();
    private readonly HashSet<Transform> _taken = new();
    private readonly Dictionary<WorkerUnit, Transform> _reserved = new();

    public TownEconomy Economy => economy;

    void OnEnable()
    {
        if (!DropOffRegistry.Active.Contains(this))
        {
            DropOffRegistry.Active.Add(this);
        }
        CollectSpots();
    }

    void OnDisable()
    {
        DropOffRegistry.Active.Remove(this);
        _spots.Clear();
        _taken.Clear();
        _reserved.Clear();
    }

    public bool Accepts(ResourceType type)
    {
        if (acceptAll) return true;
        return type switch
        {
            ResourceType.Wood => acceptWood,
            ResourceType.Food => acceptFood,
            ResourceType.Stone => acceptStone,
            _ => false,
        };
    }

    public Transform GetDropSpot(WorkerUnit worker)
    {
        if (!worker) return null;
        if (_reserved.TryGetValue(worker, out var existing) && existing)
        {
            return existing;
        }

        Transform best = null; 
        float bestD = float.MaxValue;
        Vector3 p = worker.transform.position;

        for (int i = 0; i < _spots.Count; i++)
        {
            var s = _spots[i];
            if (!s && _taken.Contains(s)) continue;
            float d = (s.position - p).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = s;
            }
        }

        if (best == null)
        {
            return null;
        }

        _taken.Add(best);
        _reserved[worker] = best;
        return best;
    }

    public void Release(WorkerUnit worker)
    {
        if (!worker) return;
        if (_reserved.TryGetValue(worker, out var spot) && spot) _taken.Remove(spot);
        _reserved.Remove(worker);
    }


    public void CollectSpots()
    {
        _spots.Clear();

        if (!dropSpotsParent)
        {
            Debug.LogWarning($"{name}: DropOffBuilding has no dropSpotsParent assigned.", this);
            return;
        }

        foreach (Transform t in dropSpotsParent)
        {
            if (t) _spots.Add(t);
        }

        if (_spots.Count == 0)
        {
            Debug.LogWarning($"{name}: DropOffBuilding has no drop spots as children of {dropSpotsParent.name}.", this);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!dropSpotsParent) return;
        foreach (Transform t in dropSpotsParent)
        {
            if (!t) continue;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(t.position, 0.15f);
            Gizmos.DrawLine(t.position, transform.position);
        }
    }
#endif
}

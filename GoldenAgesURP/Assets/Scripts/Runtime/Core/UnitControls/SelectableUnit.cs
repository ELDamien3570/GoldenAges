using UnityEngine;
using UnityEngine.AI;

// NOTE: Agent and Collider are optional now.
// Buildings can add this script without an agent.
public class SelectableUnit : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] private SpriteRenderer selectionSprite;
    [Tooltip("Register in RTSUnitRegistry for drag-box selection. Turn OFF for buildings.")]
    [SerializeField] private bool registerInRegistry = true;

    private NavMeshAgent agent;   // optional
    private bool selected;

    private void Awake()
    {
        // Try find an agent on self, child, or parent (units will have one; buildings will not).
        agent = GetComponent<NavMeshAgent>()
             ?? GetComponentInChildren<NavMeshAgent>(true)
             ?? GetComponentInParent<NavMeshAgent>();

        if (selectionSprite) selectionSprite.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (registerInRegistry)
            RTSUnitRegistry.Register(this);
    }

    private void OnDisable()
    {
        if (registerInRegistry)
            RTSUnitRegistry.Unregister(this);

        // Safety: if disabled while selected, ensure manager doesn't hold a dead ref
        if (SelectionManager.Instance.IsSelected(this))
            SelectionManager.Instance.Deselect(this);
    }

    public void MoveTo(Vector3 position)
    {
        // Workers own their movement command so they can cancel work properly
        if (TryGetComponent<WorkerUnit>(out var worker))
        {
            worker.CommandMove(position);
            return;
        }

        // Non-workers: only move if we actually have an agent (buildings will no-op)
        if (agent == null) return;

        if (agent.isStopped) agent.isStopped = false;
        agent.stoppingDistance = 0.1f;

        // Optional: snap to navmesh
        if (NavMesh.SamplePosition(position, out var hit, 0.6f, NavMesh.AllAreas))
            position = hit.position;

        agent.SetDestination(position);
    }

    public void OnSelected()
    {
        if (selected) return;
        selected = true;
        if (selectionSprite) selectionSprite.gameObject.SetActive(true);
    }

    public void OnDeselected()
    {
        if (!selected) return;
        selected = false;
        if (selectionSprite) selectionSprite.gameObject.SetActive(false);
    }
}

// Your existing registry stays the same.
public static class RTSUnitRegistry
{
    private static readonly System.Collections.Generic.List<SelectableUnit> _units = new();
    public static System.Collections.Generic.IReadOnlyList<SelectableUnit> Units => _units;

    public static void Register(SelectableUnit u) { if (!_units.Contains(u)) _units.Add(u); }
    public static void Unregister(SelectableUnit u) { _units.Remove(u); }
}

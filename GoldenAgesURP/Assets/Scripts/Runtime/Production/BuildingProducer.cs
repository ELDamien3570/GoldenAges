using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class BuildingProducer : MonoBehaviour, IActionProvider
{
    [Header("Ownership")]
    [SerializeField] private PlayerContext owner; // assign the player's context

    [Header("Economy")]
    [SerializeField] private TownEconomy economy; // can be pulled from owner, kept separate here
    public TownEconomy Economy => economy;

    [Header("Production")]
    [SerializeField] private QueueableRecipe[] availableRecipes;
    [SerializeField] private int maxQueue = 5;

    [Header("Spawn")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform rallyPoint; // optional

    private readonly List<QueuedItem> queue = new List<QueuedItem>(8);
    private QueuedItem current;
    private float timer;

    [System.Serializable]
    private struct QueuedItem
    {
        public QueueableRecipe recipe;
        public float progress;
    }

    void Awake()
    {
        if (!economy && owner) economy = owner.economy;
    }

    void Update()
    {
        if (current.recipe == null && queue.Count > 0) BeginNext();
        if (current.recipe == null) return;

        timer += Time.deltaTime;
        current.progress = Mathf.Clamp01(timer / current.recipe.buildTimeSeconds);

        if (timer >= current.recipe.buildTimeSeconds)
        {
            CompleteCurrent();
        }
    }

    // IActionProvider
    public void GetActions(PlayerContext ctx, List<ActionDesc> outActions)
    {
        // Only show production actions if this building belongs to ctx
        if (ctx != owner) return;

        for (int i = 0; i < availableRecipes.Length; i++)
        {
            var r = availableRecipes[i];
            if (!r) continue;

            var a = new ActionDesc
            {
                id = r.name,
                displayName = string.IsNullOrEmpty(r.displayName) ? r.name : r.displayName,
                icon = r.icon,
                hotkey = KeyCode.None,
                category = ActionCategory.Production,
                kind = ActionKind.Queued,
                costs = MapCosts(r.costs),
                isEnabled = HasResources(r) && r.CanStart(owner),
                disabledReason = BuildDisabledReason(r)
            };

            a.executeInstant = () => TryEnqueue(r);

            outActions.Add(a);
        }
    }

    // Public API used by recipes
    public void SpawnUnit(GameObject prefab)
    {
        if (!prefab) return;

        Vector3 pos = spawnPoint ? spawnPoint.position : transform.position + transform.forward * 1.5f;
        Quaternion rot = spawnPoint ? spawnPoint.rotation : transform.rotation;

        var go = Instantiate(prefab, pos, rot);

        var agent = go.GetComponentInParent<NavMeshAgent>() ?? go.GetComponent<NavMeshAgent>();
        if (agent && rallyPoint)
        {
            Vector3 rally = rallyPoint.position;
            if (NavMesh.SamplePosition(rally, out var hit, 0.8f, NavMesh.AllAreas))
                rally = hit.position;
            agent.SetDestination(rally);
        }
    }

    // Queue management
    public bool TryEnqueue(QueueableRecipe r)
    {
        if (r == null) return false;
        if (queue.Count >= maxQueue) return false;
        if (!HasResources(r)) return false;
        if (!r.CanStart(owner)) return false;

        Pay(r);
        r.OnQueued(owner);

        queue.Add(new QueuedItem { recipe = r, progress = 0f });
        return true;
    }

    public bool CancelAt(int index)
    {
        if (index < 0 || index >= queue.Count) return false;
        var q = queue[index];
        Refund(q.recipe);
        q.recipe.OnCancel(owner);
        queue.RemoveAt(index);
        return true;
    }

    private void BeginNext()
    {
        current = queue[0];
        queue.RemoveAt(0);
        timer = 0f;
    }

    private void CompleteCurrent()
    {
        var done = current.recipe;
        current = default;
        timer = 0f;

        if (done != null)
            done.OnComplete(owner, this);
    }

    // Economy helpers
    private bool HasResources(QueueableRecipe r)
    {
        if (!economy) return false;
        foreach (var c in r.costs)
        {
            switch (c.type)
            {
                case ResourceType.Wood: if (economy.Wood < c.amount) return false; break;
                case ResourceType.Food: if (economy.Food < c.amount) return false; break;
                case ResourceType.Stone: if (economy.Stone < c.amount) return false; break;
            }
        }
        return true;
    }

    private void Pay(QueueableRecipe r)
    {
        foreach (var c in r.costs)
        {
            switch (c.type)
            {
                case ResourceType.Wood: economy.Wood -= c.amount; break;
                case ResourceType.Food: economy.Food -= c.amount; break;
                case ResourceType.Stone: economy.Stone -= c.amount; break;
            }
        }
    }

    private void Refund(QueueableRecipe r)
    {
        foreach (var c in r.costs)
        {
            switch (c.type)
            {
                case ResourceType.Wood: economy.Wood += c.amount; break;
                case ResourceType.Food: economy.Food += c.amount; break;
                case ResourceType.Stone: economy.Stone += c.amount; break;
            }
        }
    }

    private static (ResourceType, int)[] MapCosts(QueueableRecipe.Cost[] costs)
    {
        if (costs == null || costs.Length == 0) return System.Array.Empty<(ResourceType, int)>();
        var arr = new (ResourceType, int)[costs.Length];
        for (int i = 0; i < costs.Length; i++) arr[i] = (costs[i].type, costs[i].amount);
        return arr;
    }

    private string BuildDisabledReason(QueueableRecipe r)
    {
        if (economy == null) return "No economy reference";
        if (!r.CanStart(owner)) return "Requirements not met";
        // Add detailed reasons if you want
        return "";
    }

    // Expose owner so you can set at runtime if needed
    public void SetOwner(PlayerContext ctx)
    {
        owner = ctx;
        if (!economy && owner) economy = owner.economy;
    }
}

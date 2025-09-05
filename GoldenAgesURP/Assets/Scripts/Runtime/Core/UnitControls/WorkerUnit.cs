using System.Collections;
using UnityEngine;
using UnityEngine.AI;

//[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class WorkerUnit : MonoBehaviour
{
    [Header("Arrival / Facing")]
    [Tooltip("How close (m) to the reserved spot counts as 'arrived'.")]
    public float arriveTolerance = 0.15f;

    [Tooltip("Degrees per second while turning to face the resource.")]
    public float faceTurnSpeed = 540f;

    [Tooltip("Small delay after arrival before starting the work loop.")]
    public float startWorkDelay = 0.1f;

    [Header("Animator Driving")]
    [Tooltip("Below this (m/s), snap Speed to 0 to avoid jitter.")]
    public float idleThreshold = 0.05f;

    [Tooltip("Damping seconds for Speed param.")]
    public float speedDampTime = 0.12f;

    [Header("Inventory")]
    public int carryCapacity = 10;

    [HideInInspector] public ResourceType? carryingType = null;
    [HideInInspector] public int carryingAmount = 0;

    // Components
    private NavMeshAgent agent;
    private Animator anim;

    // Animator parameter hashes
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashWorking = Animator.StringToHash("Working");
    private static readonly int HashWorkType = Animator.StringToHash("WorkType");

    // Current job state
    private IWorkProvider currentNode;
    private Transform reservedSpot;
    private Coroutine workRoutine;

    // Drop-off state
    private Transform reservedDropSpot;
    private IDropOff currentDrop;

    // Auto-resume after deposit
    private IGatherable pendingReturnNode;

    public bool IsWorking => currentNode != null;

    void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        anim.applyRootMotion = false;
        agent.updateRotation = true;
    }

    void Update()
    {
        float speed = agent.velocity.magnitude;
        if (speed < idleThreshold) speed = 0f;
        anim.SetFloat(HashSpeed, speed, speedDampTime, Time.deltaTime);
    }

    // ---------------- Commands ----------------

    public void CommandMove(Vector3 worldPoint)
    {
        StopWorking();                 // clear work/deposit state
        pendingReturnNode = null;      // manual move cancels auto-return

        agent.isStopped = false;
        agent.stoppingDistance = 0.1f;

        if (NavMesh.SamplePosition(worldPoint, out var hit, 0.6f, NavMesh.AllAreas))
            worldPoint = hit.position;

        agent.SetDestination(worldPoint);
    }

    public void AddToCarry(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        if (carryingType.HasValue && carryingType.Value != type) return;
        carryingType ??= type;
        carryingAmount = Mathf.Min(carryingAmount + amount, carryCapacity);
    }

    public void CommandWork(IWorkProvider node)
    {
        if (node == null) return;

        if (currentNode != null && currentNode != node)
            StopWorking();

        pendingReturnNode = null; // new job -> forget previous plan to return

        if (!node.TryReserve(this, out reservedSpot))
        {
            CommandMove((node as Component) ? ((Component)node).transform.position : transform.position);
            return;
        }

        currentNode = node;

        anim.SetBool(HashWorking, false);
        agent.isStopped = false;

        if (workRoutine != null) StopCoroutine(workRoutine);
        workRoutine = StartCoroutine(WorkFlow());
    }

    public void CommandDeposit(IDropOff drop)
    {
        if (drop == null) return;

        // If we’re currently working on a gatherable node, remember it to return after deposit
        if (currentNode is IGatherable g) pendingReturnNode = g;

        StopWorkingButRememberReturn(); // clear spot/anim but keep pendingReturnNode

        currentDrop = drop;
        var spot = drop.GetDropSpot(this);
        if (spot == null)
        {
            // No spot free — just move near the building as fallback
            CommandMove(((Component)drop).transform.position);
            return;
        }

        reservedDropSpot = spot;

        if (workRoutine != null) StopCoroutine(workRoutine);
        workRoutine = StartCoroutine(DepositFlow());
    }

    public void CancelWorkIfTarget(IWorkProvider node)
    {
        if (currentNode == node) StopWorking();
        if (pendingReturnNode == node) pendingReturnNode = null;
    }

    // ---------------- Work / Gather loop ----------------

    private IEnumerator WorkFlow()
    {
        anim.SetBool(HashWorking, false);
        agent.isStopped = false;
        agent.stoppingDistance = 0.05f;

        // Navigate to reserved spot
        Vector3 target = reservedSpot ? reservedSpot.position : transform.position;
        if (NavMesh.SamplePosition(target, out var navHit, 0.4f, NavMesh.AllAreas))
            target = navHit.position;

        agent.SetDestination(target);

        // Wait until arrived (with timeout)
        float t0 = Time.time, timeout = 6f;
        while (true)
        {
            if (!agent.pathPending)
            {
                float stop = Mathf.Max(agent.stoppingDistance, arriveTolerance);
                if (agent.remainingDistance <= stop) break;
            }
            if (Time.time - t0 > timeout) break;
            yield return null;
        }

        // Face node and start working
        yield return new WaitForSeconds(startWorkDelay);
        agent.isStopped = true;

        Vector3 lookTarget = currentNode != null ? currentNode.GetLookAt(reservedSpot) : transform.position;
        yield return FaceTowards(lookTarget);

        if (currentNode != null)
        {
            // Animator WorkType stays from the node's type (Farm/Chop/Mine)
            anim.SetInteger(HashWorkType, (int)currentNode.Type);
            anim.SetBool(HashWorking, true);
        }

        // GATHER TICK (only if this node is gatherable)
        IGatherable gather = currentNode as IGatherable;
        float tickTimer = 0f;

        while (currentNode != null)
        {
            // Keep softly facing the node
            lookTarget = currentNode.GetLookAt(reservedSpot);
            FaceStep(lookTarget);

            if (gather != null)
            {
                tickTimer += Time.deltaTime;
                if (tickTimer >= gather.TickInterval)
                {
                    tickTimer = 0f;

                    // Ensure carryingType matches node type
                    if (!carryingType.HasValue) carryingType = gather.YieldsType;

                    if (carryingType.Value == gather.YieldsType && carryingAmount < carryCapacity)
                    {
                        int want = Mathf.Min(gather.YieldPerTick, carryCapacity - carryingAmount);
                        if (gather.TryConsume(want, out int got) && got > 0)
                        {
                            carryingAmount += got;
                        }
                    }

                    // Full or depleted -> go deposit
                    if (carryingAmount >= carryCapacity || gather.IsDepleted)
                    {
                        var drop = DropOffFinder.FindNearest(transform.position, carryingType.Value);

                        // release our node spot; mark for return only if not depleted
                        pendingReturnNode = gather.IsDepleted ? null : gather;

                        ReleaseReservation();
                        currentNode = null;
                        anim.SetBool(HashWorking, false);

                        // Head to deposit
                        CommandDeposit(drop);
                        yield break;
                    }
                }
            }

            yield return null;
        }
    }

    // ---------------- Deposit flow ----------------

    private IEnumerator DepositFlow()
    {
        anim.SetBool(HashWorking, false); // not working while traveling
        agent.isStopped = false;
        agent.stoppingDistance = 0.05f;

        Vector3 target = reservedDropSpot ? reservedDropSpot.position : ((Component)currentDrop).transform.position;
        if (NavMesh.SamplePosition(target, out var navHit, 0.4f, NavMesh.AllAreas))
            target = navHit.position;

        agent.SetDestination(target);

        float t0 = Time.time, timeout = 6f;
        while (true)
        {
            if (!agent.pathPending)
            {
                float stop = Mathf.Max(agent.stoppingDistance, arriveTolerance);
                if (agent.remainingDistance <= stop) break;
            }
            if (Time.time - t0 > timeout) break;
            yield return null;
        }

        // Face the building
        yield return new WaitForSeconds(0.05f);
        agent.isStopped = true;
        Vector3 look = ((Component)currentDrop).transform.position;
        yield return FaceTowards(look);

        // Deposit into economy
        if (currentDrop != null && currentDrop.Economy != null && carryingAmount > 0 && carryingType.HasValue)
        {
            currentDrop.Economy.Add(carryingType.Value, carryingAmount);
            carryingAmount = 0;
            carryingType = null;
        }

        // Release the spot and clear state
        ReleaseDropReservation();
        currentDrop = null;
        workRoutine = null;

        // Auto-return if we had a node and it's still valid
        if (pendingReturnNode != null && !pendingReturnNode.IsDepleted)
        {
            var back = pendingReturnNode;
            pendingReturnNode = null;
            CommandWork(back); // reserve a fresh spot and resume
        }
        else
        {
            pendingReturnNode = null; // idle
            agent.isStopped = false;
        }
    }

    // ---------------- Facing helpers ----------------

    private IEnumerator FaceTowards(Vector3 worldPoint)
    {
        while (true)
        {
            if (FaceStep(worldPoint)) yield break;
            yield return null;
        }
    }

    private bool FaceStep(Vector3 worldPoint)
    {
        Vector3 to = worldPoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return true;

        Quaternion target = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, faceTurnSpeed * Time.deltaTime);

        return Quaternion.Angle(transform.rotation, target) < 1.5f;
    }

    // ---------------- State clear ----------------

    private void StopWorking()
    {
        if (workRoutine != null)
        {
            StopCoroutine(workRoutine);
            workRoutine = null;
        }

        if (currentNode != null)
        {
            ReleaseReservation();
            currentNode = null;
        }

        ReleaseDropReservation();
        currentDrop = null;

        anim.SetBool(HashWorking, false);
        agent.isStopped = false;
    }

    // clear work, keep pendingReturnNode (used by manual CommandDeposit)
    private void StopWorkingButRememberReturn()
    {
        if (workRoutine != null)
        {
            StopCoroutine(workRoutine);
            workRoutine = null;
        }
        if (currentNode != null)
        {
            ReleaseReservation();
            currentNode = null;
        }
        anim.SetBool(HashWorking, false);
        agent.isStopped = false;
    }

    private void ReleaseReservation()
    {
        currentNode?.Release(this);
        reservedSpot = null;
    }

    private void ReleaseDropReservation()
    {
        if (currentDrop is DropOffBuilding building)
            building.Release(this);
        reservedDropSpot = null;
    }

    void OnDisable()
    {
        StopWorking();
        pendingReturnNode = null;
    }
}

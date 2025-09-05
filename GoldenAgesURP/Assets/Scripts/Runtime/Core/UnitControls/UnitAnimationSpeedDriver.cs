using UnityEngine;
using UnityEngine.AI;

//[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class UnitAnimatorSpeedDriver : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Animator float parameter that drives your Idle/Walk/Run blend tree.")]
    public string speedParam = "Speed";
    [Tooltip("Damping time when writing Speed, for smoother starts/stops.")]
    public float speedDampTime = 0.12f;
    [Tooltip("Below this (m/s), treat as idle to avoid flicker near zero.")]
    public float idleThreshold = 0.05f;

    private NavMeshAgent agent;
    private Animator anim;
    private int speedHash;

    void Awake()
    {
        agent = GetComponentInParent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        speedHash = Animator.StringToHash(speedParam);

        // Locomotion should be in-place for NavMeshAgent
        if (anim) anim.applyRootMotion = false;
    }

    void Update()
    {
        if (!agent || !anim) return;

        float speed = agent.velocity.magnitude;
        if (speed < idleThreshold) speed = 0f;

        // Animator damping overload: SetFloat(hash, value, dampTime, deltaTime)
        anim.SetFloat(speedHash, speed, speedDampTime, Time.deltaTime);
    }
}

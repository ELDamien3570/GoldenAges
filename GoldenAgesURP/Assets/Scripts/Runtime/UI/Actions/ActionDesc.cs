using System;
using UnityEngine;

public enum ActionKind { Instant, TargetPoint, TargetEntity, Queued }
public enum ActionCategory { Production, Economy, Military, Utility }

public sealed class ActionDesc
{
    public string id;
    public string displayName;
    public Sprite icon;
    public KeyCode hotkey;
    public ActionCategory category;
    public ActionKind kind;
    public bool isEnabled = true;
    public string disabledReason;
    public (ResourceType type, int amount)[] costs;

    public Action executeInstant;
    public Action<Vector3> executePoint;
    public Action<UnityEngine.Object> executeEntity;
}
using UnityEngine;

public abstract class QueueableRecipe : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public float buildTimeSeconds = 10f;

    [System.Serializable]
    public struct Cost { public ResourceType type; public int amount; }
    public Cost[] costs;

    public virtual bool CanStart(PlayerContext ctx) { return true; }
    public virtual void OnQueued(PlayerContext ctx) { }
    public virtual void OnCancel(PlayerContext ctx) { }
    public abstract void OnComplete(PlayerContext ctx, BuildingProducer producer);
}

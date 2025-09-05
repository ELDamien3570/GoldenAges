using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Recipe/Unit")]
public class UnitRecipe : QueueableRecipe
{
    public GameObject prefab;
    public int popUsed = 1; // for later

    public override bool CanStart(PlayerContext ctx)
    {
        // Add population or tech checks later
        return prefab != null;
    }

    public override void OnComplete(PlayerContext ctx, BuildingProducer producer)
    {
        producer.SpawnUnit(prefab);
    }
}

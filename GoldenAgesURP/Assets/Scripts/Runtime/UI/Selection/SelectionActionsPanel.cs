using System.Collections.Generic;
using UnityEngine;

public class SelectionActionsPanel : MonoBehaviour
{
    [SerializeField] private ActionPalette palette;
    [SerializeField] private PlayerContext localPlayer; // the player viewing this UI

    private readonly List<ActionDesc> _buffer = new List<ActionDesc>(32);
    private object _lastSelectionToken;

    void Update()
    {
        // Replace this with your own selection system hook if you have events.
        var token = ComputeSelectionToken();
        if (!Equals(token, _lastSelectionToken))
        {
            _lastSelectionToken = token;
            Rebuild();
        }

        // If you want live enable/disable updates, you can Rebuild() periodically.
    }

    private void Rebuild()
    {
        _buffer.Clear();

        // Basic policy: if exactly one object selected and it has providers, show those.
        // Replace SelectionManager.Instance with your own if named differently.
        var set = SelectionManager.Instance.SelectedUnits;
        if (set.Count == 1)
        {
            foreach (var sel in set)
            {
                if (!sel) continue;
                var providers = sel.GetComponentsInParent<IActionProvider>(true);
                bool any = false;
                for (int i = 0; i < providers.Length; i++)
                {
                    providers[i].GetActions(localPlayer, _buffer);
                    any = true;
                }
                if (any)
                {
                    palette.ShowActions(sel.name, _buffer);
                    return;
                }
            }
        }

        // Fallback: hide when nothing to show.
        palette.Hide();
    }

    private object ComputeSelectionToken()
    {
        // Simple version: count + first name. Good enough to refresh the panel.
        int count = SelectionManager.Instance.SelectedUnits.Count;
        string name = "";
        foreach (var s in SelectionManager.Instance.SelectedUnits) { name = s ? s.name : ""; break; }
        return (count, name);
    }
}

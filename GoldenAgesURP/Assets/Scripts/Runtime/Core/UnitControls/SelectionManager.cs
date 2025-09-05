using System.Collections.Generic;
using UnityEngine;

public class SelectionManager
{
    private static SelectionManager _instance;
    public static SelectionManager Instance => _instance ??= new SelectionManager();

    public readonly HashSet<SelectableUnit> SelectedUnits = new HashSet<SelectableUnit>();

    private SelectionManager() { }

    public bool Select(SelectableUnit unit)
    {
        if (unit == null || SelectedUnits.Contains(unit)) return false;
        SelectedUnits.Add(unit);
        unit.OnSelected();
        return true;
    }

    public bool Deselect(SelectableUnit unit)
    {
        if (unit == null || !SelectedUnits.Contains(unit)) return false;
        SelectedUnits.Remove(unit);
        unit.OnDeselected();
        return true;
    }

    public void ReplaceSelection(HashSet<SelectableUnit> newSelection)
    {
        // Deselect removed
        foreach (var u in SelectedUnits)
            if (!newSelection.Contains(u)) u.OnDeselected();
        // Select added
        foreach (var u in newSelection)
            if (!SelectedUnits.Contains(u)) u.OnSelected();

        SelectedUnits.Clear();
        foreach (var u in newSelection) SelectedUnits.Add(u);
    }

    public void DeselectAll()
    {
        if (SelectedUnits.Count == 0) return;
        foreach (var unit in SelectedUnits) unit.OnDeselected();
        SelectedUnits.Clear();
    }

    public bool IsSelected(SelectableUnit unit) => SelectedUnits.Contains(unit);
}

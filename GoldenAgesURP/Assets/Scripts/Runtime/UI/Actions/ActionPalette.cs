using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionPalette : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform buttonsParent;
    [SerializeField] private GameObject buttonPrefab; // MUST contain a UnityEngine.UI.Button somewhere (root or child)
    [SerializeField] private Text titleLabel;         // optional (legacy UI). Use TextMeshProUGUI if you prefer.

    // cache of spawned buttons so we can clear
    private readonly List<GameObject> _spawned = new List<GameObject>(16);

    public void ShowActions(string title, List<ActionDesc> actions)
    {
        if (titleLabel) titleLabel.text = title ?? "";

        // clear old
        for (int i = 0; i < _spawned.Count; i++) if (_spawned[i]) Destroy(_spawned[i]);
        _spawned.Clear();

        if (actions == null || actions.Count == 0)
        {
            Debug.Log("[ActionPalette] No actions to show.");
            gameObject.SetActive(true);
            return;
        }

        // build new
        for (int i = 0; i < actions.Count; i++)
        {
            var a = actions[i];
            var go = Instantiate(buttonPrefab, buttonsParent ? buttonsParent : transform);
            _spawned.Add(go);
            go.name = $"Action_{(string.IsNullOrEmpty(a.displayName) ? a.id : a.displayName)}";

            // Ensure there is a Button on the prefab (root or child)
            var btn = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
            if (!btn)
            {
                Debug.LogError($"[ActionPalette] Prefab '{buttonPrefab.name}' has no Button component (root or children).");
                continue;
            }

            // Optional visuals
            var img = go.GetComponentInChildren<Image>(true);
            if (img) img.sprite = a.icon;
            var txt = go.GetComponentInChildren<Text>(true);
            if (txt) txt.text = string.IsNullOrEmpty(a.displayName) ? a.id : a.displayName;

            // Ensure our world-input blocker exists
            if (!go.TryGetComponent<UIBlockWorldClick>(out _))
                go.AddComponent<UIBlockWorldClick>();

            // Interactable state
            btn.interactable = a.isEnabled;

            // Capture local for the lambda
            var local = a;

            // WIRE THE CLICK (this is the important part)
            btn.onClick.AddListener(() =>
            {
                Debug.Log($"[ActionPalette] CLICK '{(string.IsNullOrEmpty(local.displayName) ? local.id : local.displayName)}'");

                // Belt & suspenders: explicitly block world input for this frame
                PlayerInput.BlockWorldPointerThisFrame(0.1f);

                switch (local.kind)
                {
                    case ActionKind.Instant:
                    case ActionKind.Queued:
                        if (local.executeInstant != null)
                        {
                            Debug.Log("[ActionPalette] executeInstant()");
                            local.executeInstant.Invoke();
                        }
                        else
                        {
                            Debug.LogWarning("[ActionPalette] executeInstant is null for this action.");
                        }
                        break;

                    case ActionKind.TargetPoint:
                        if (local.executePoint != null)
                        {
                            Debug.Log("[ActionPalette] executePoint(Vector3.zero) (placeholder)");
                            local.executePoint.Invoke(Vector3.zero); // replace with targeting flow later
                        }
                        else
                        {
                            Debug.LogWarning("[ActionPalette] executePoint is null for this action.");
                        }
                        break;

                    case ActionKind.TargetEntity:
                        if (local.executeEntity != null)
                        {
                            Debug.Log("[ActionPalette] executeEntity(null) (placeholder)");
                            local.executeEntity.Invoke(null); // replace with targeting flow later
                        }
                        else
                        {
                            Debug.LogWarning("[ActionPalette] executeEntity is null for this action.");
                        }
                        break;

                    default:
                        Debug.LogWarning($"[ActionPalette] Unknown ActionKind: {local.kind}");
                        break;
                }
            });

            // Optional tooltip hook
           // var tip = go.GetComponentInChildren<TooltipHook>(true);
           // if (tip) tip.text = BuildTooltip(local);
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private string BuildTooltip(ActionDesc a)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
        sb.AppendLine(string.IsNullOrEmpty(a.displayName) ? a.id : a.displayName);
        if (a.costs != null && a.costs.Length > 0)
        {
            sb.Append("Cost: ");
            for (int i = 0; i < a.costs.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(a.costs[i].amount).Append(" ").Append(a.costs[i].type);
            }
            sb.AppendLine();
        }
        if (!a.isEnabled && !string.IsNullOrEmpty(a.disabledReason))
            sb.AppendLine(a.disabledReason);
        return sb.ToString();
    }
}

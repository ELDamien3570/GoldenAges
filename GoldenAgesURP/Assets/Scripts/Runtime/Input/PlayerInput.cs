using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private RectTransform selectionBox;

    [Header("Layers")]
    [Tooltip("Include ONLY the child 'SelectableOnly' layer used by SelectionCollider")]
    [SerializeField] private LayerMask unitLayers;   // child colliders layer
    [SerializeField] private LayerMask floorLayer;   // ground/terrain for move commands

    [Header("Click / Drag Settings")]
    [SerializeField] private float dragDelaySeconds = 0.08f;  // time threshold for drag
    [SerializeField] private float dragThresholdPixels = 6f;  // distance threshold for drag

    [Header("Picking Settings")]
    [Tooltip("Radius (world units) for SphereCast picking. 0.25–0.5 is typical.")]
    [SerializeField] private float spherePickRadius = 0.35f;
    [Tooltip("Should clicks hit trigger colliders (true if your selection colliders are triggers).")]
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Picking Mode")]
    [Tooltip("false = behave like drag box (ignore props/foliage); true = block on non-unit colliders")]
    [SerializeField] private bool occlusionAwareClicks = false;

    [Header("Screen-space Click")]
    [Tooltip("Use a screen-space radius pick first (most consistent).")]
    [SerializeField] private bool useScreenSpaceClick = true;
    [SerializeField] private float screenPickRadiusPx = 16f;
    [SerializeField] private bool screenPickOcclusionCheck = true;

    [Header("Click Behavior")]
    [SerializeField] private bool clickTogglesWhenAlreadySelected = true;

    private float mouseDownTime;
    private Vector2 mouseDownPos;
    private bool dragging;

    // reuse sets to avoid GC during drags
    private readonly HashSet<SelectableUnit> _dragSet = new();
    private readonly HashSet<SelectableUnit> _tmpSet = new();

    // --- UI click suppression (prevents world from handling the same click) ---
    private static int _uiBlockFrame = -1;
    private static float _uiBlockUntilTime = -1f;

    public static void BlockWorldPointerThisFrame(float extraSeconds = 0.08f)
    {
        _uiBlockFrame = Time.frameCount;
        _uiBlockUntilTime = Time.unscaledTime + Mathf.Max(0f, extraSeconds);
    }

    private static bool IsBlockedByUINow()
    {
        return Time.frameCount == _uiBlockFrame || Time.unscaledTime <= _uiBlockUntilTime;
    }

    // Cancels any in-progress drag and hides the selection box
    private void CancelDragUI()
    {
        dragging = false;
        if (selectionBox)
        {
            selectionBox.gameObject.SetActive(false);
            selectionBox.sizeDelta = Vector2.zero;
        }
    }
    // -------------------------------------------------------------------------

    void Reset()
    {
        cam = Camera.main;
    }

    void Awake()
    {
        if (unitLayers.value == 0)
        {
            Debug.LogWarning("PlayerInput.unitLayers is 0. Ensure it includes ONLY your SelectableOnly layer.");
        }
    }

    void Update()
    {
        HandleSelectionInputs();
        HandleMovementInputs();
    }

    // ---------- Movement / Work (RMB) ----------
    private void HandleMovementInputs()
    {
        if (IsBlockedByUINow()) return;                    // UI suppression
        if (!Input.GetMouseButtonUp(1)) return;
        if (SelectionManager.Instance.SelectedUnits.Count == 0) return;
        if (IsPointerOverUI()) return;                     // hard UI hit test

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, triggerInteraction);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3? firstGroundPoint = null;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (!h.collider) continue;

                var drop = h.collider.GetComponentInParent<DropOffBuilding>();
                if (drop != null)
                {
                    IssueDepositCommand(drop);
                    return;
                }

                var node = h.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    IssueWorkCommand(node);
                    return;
                }

                int hitLayerBit = 1 << h.collider.gameObject.layer;
                if ((floorLayer.value & hitLayerBit) != 0 && firstGroundPoint == null)
                    firstGroundPoint = h.point;
            }

            if (firstGroundPoint.HasValue)
            {
                IssueMoveCommand(firstGroundPoint.Value);
                return;
            }
        }

        if (Physics.Raycast(ray, out var groundHit, Mathf.Infinity, floorLayer, triggerInteraction))
        {
            IssueMoveCommand(groundHit.point);
        }
    }

    private void IssueDepositCommand(IDropOff drop)
    {
        foreach (var sel in SelectionManager.Instance.SelectedUnits)
        {
            if (!sel) continue;

            var worker = sel.GetComponent<WorkerUnit>()
                       ?? sel.GetComponentInParent<WorkerUnit>()
                       ?? sel.GetComponentInChildren<WorkerUnit>(true);

            if (worker != null)
                worker.CommandDeposit(drop);
            else
                sel.MoveTo(((Component)drop).transform.position);
        }
    }

    private void IssueWorkCommand(ResourceNode node)
    {
        foreach (var sel in SelectionManager.Instance.SelectedUnits)
        {
            if (!sel) continue;

            WorkerUnit worker = sel.GetComponent<WorkerUnit>()
                               ?? sel.GetComponentInParent<WorkerUnit>()
                               ?? sel.GetComponentInChildren<WorkerUnit>(true);

            if (worker)
            {
                worker.CommandWork(node);
            }
            else
            {
                sel.MoveTo(node.transform.position);
            }
        }
    }

    private void IssueMoveCommand(Vector3 point)
    {
        foreach (var sel in SelectionManager.Instance.SelectedUnits)
        {
            if (!sel) continue;

            var worker = sel.GetComponent<WorkerUnit>()
                       ?? sel.GetComponentInParent<WorkerUnit>()
                       ?? sel.GetComponentInChildren<WorkerUnit>(true);

            if (worker != null)
            {
                worker.CommandMove(point);
            }
            else
            {
                sel.MoveTo(point);
            }
        }
    }

    // ---------- Selection (LMB) ----------
    private void HandleSelectionInputs()
    {
        // Global UI suppression
        if (IsBlockedByUINow())
        {
            CancelDragUI();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // If pointer is over UI at press, cancel any drag/box and bail
            if (IsPointerOverUI())
            {
                CancelDragUI();
                // Also extend the block a tad in case Update order makes world see this frame
                BlockWorldPointerThisFrame(0.08f);
                return;
            }

            mouseDownTime = Time.time;
            mouseDownPos = Input.mousePosition;
            dragging = false;

            if (selectionBox)
            {
                selectionBox.gameObject.SetActive(false);
                selectionBox.sizeDelta = Vector2.zero;
            }
        }

        if (Input.GetMouseButton(0))
        {
            // If we move into UI while dragging, cancel immediately
            if (dragging && IsPointerOverUI())
            {
                CancelDragUI();
                BlockWorldPointerThisFrame(0.08f);
                return;
            }

            if (!dragging)
            {
                bool timeOK = (Time.time - mouseDownTime) >= dragDelaySeconds;
                bool distOK = (Input.mousePosition - (Vector3)mouseDownPos).sqrMagnitude >= (dragThresholdPixels * dragThresholdPixels);
                if (timeOK || distOK)
                {
                    dragging = true;
                    if (selectionBox) selectionBox.gameObject.SetActive(true);
                }
            }

            if (dragging)
            {
                UpdateSelectionBox(mouseDownPos, Input.mousePosition);
                BuildDragSet(mouseDownPos, Input.mousePosition, _dragSet);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            // If UI owns the release, hide and bail
            if (IsPointerOverUI() || IsBlockedByUINow())
            {
                CancelDragUI();
                return;
            }

            if (selectionBox)
            {
                selectionBox.gameObject.SetActive(false);
                selectionBox.sizeDelta = Vector2.zero;
            }

            if (dragging)
            {
                CommitDragSelection(IsAdditive());
                _dragSet.Clear();
            }
            else
            {
                ClickSelect();
            }
        }
    }

    private bool IsAdditive()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private void ClickSelect()
    {
        if (IsPointerOverUI() || IsBlockedByUINow()) return;

        SelectableUnit unit = null;

        // 1) Screen-space pick like drag (most consistent)
        if (useScreenSpaceClick &&
            TryPickByScreenRadius(cam, Input.mousePosition, screenPickRadiusPx, screenPickOcclusionCheck, out var ssUnit))
        {
            unit = ssUnit;
        }
        else
        {
            // 2) Fallback to physics pipeline
            TryPickUnitUnitFirst(
                cam,
                Input.mousePosition,
                unitLayers,               // ONLY your SelectableOnly child colliders
                spherePickRadius,
                triggerInteraction,
                occlusionAwareClicks,
                out unit);
        }

        if (unit != null)
        {
            bool additive = IsAdditive();

            if (clickTogglesWhenAlreadySelected && !additive && SelectionManager.Instance.IsSelected(unit))
            {
                SelectionManager.Instance.Deselect(unit);
                return;
            }

            if (additive)
            {
                if (SelectionManager.Instance.IsSelected(unit))
                    SelectionManager.Instance.Deselect(unit);
                else
                    SelectionManager.Instance.Select(unit);
            }
            else
            {
                _tmpSet.Clear();
                _tmpSet.Add(unit);
                SelectionManager.Instance.ReplaceSelection(_tmpSet);
            }
            return;
        }

        // Missed any unit -> clear on ground (non-additive)
        if (!IsAdditive() &&
            Physics.Raycast(
                cam.ScreenPointToRay(Input.mousePosition),
                out var groundHit,
                Mathf.Infinity,
                floorLayer,
                triggerInteraction))
        {
            SelectionManager.Instance.DeselectAll();
            return;
        }

        if (!IsAdditive())
            SelectionManager.Instance.DeselectAll();
    }

    // ---------- Screen-space picker ----------
    private static bool TryPickByScreenRadius(Camera cam,
                                              Vector2 mousePos,
                                              float pxRadius,
                                              bool occlusionCheck,
                                              out SelectableUnit picked)
    {
        picked = null;
        var units = RTSUnitRegistry.Units;
        if (units == null || units.Count == 0) return false;

        float best = float.MaxValue;
        SelectableUnit bestUnit = null;

        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (!u) continue;

            Vector3 sp = cam.WorldToScreenPoint(u.transform.position);
            if (sp.z < 0f) continue; // behind camera

            float d = Vector2.Distance(mousePos, (Vector2)sp);
            if (d > pxRadius) continue;
            if (d >= best) continue;

            if (occlusionCheck && IsOccluded(cam.transform.position, u.transform.position, u.transform.root))
                continue;

            best = d;
            bestUnit = u;
        }

        picked = bestUnit;
        return picked != null;
    }

    /// <summary>
    /// Unit-first picking:
    /// A) Tiny OverlapSphere near the ray origin to catch "inside trigger" cases.
    /// B) Precise Raycast against unit layer only.
    /// C) SphereCast against unit layer only for forgiveness.
    /// Each candidate passes an occlusion check that ignores the candidate's own root.
    /// </summary>
    private static bool TryPickUnitUnitFirst(
        Camera cam,
        Vector2 mousePos,
        LayerMask unitMask,
        float spherePickRadius,
        QueryTriggerInteraction qti,
        bool occlusionAware,
        out SelectableUnit picked)
    {
        picked = null;
        Ray ray = cam.ScreenPointToRay(mousePos);

        // A) Tiny overlap just past near plane
        {
            Vector3 start = ray.origin + ray.direction * 0.05f;
            Collider[] overlaps = Physics.OverlapSphere(start, 0.1f, unitMask, qti);
            for (int i = 0; i < overlaps.Length; i++)
            {
                var u = overlaps[i] ? overlaps[i].GetComponentInParent<SelectableUnit>() : null;
                if (!u) continue;

                if (!occlusionAware || IsNotOccluded(cam.transform.position, u.transform.position, u.transform.root))
                {
                    picked = u;
                    return true;
                }
            }
        }

        // B) Thin ray against unit layer
        if (Physics.Raycast(ray, out var hitUnit, Mathf.Infinity, unitMask, qti))
        {
            var u = hitUnit.collider ? hitUnit.collider.GetComponentInParent<SelectableUnit>() : null;
            if (u)
            {
                if (!occlusionAware || IsNotOccluded(cam.transform.position, u.transform.position, u.transform.root))
                {
                    picked = u;
                    return true;
                }
            }
        }

        // C) Forgiving spherecast against unit layer
        var sphereHits = Physics.SphereCastAll(ray, spherePickRadius, Mathf.Infinity, unitMask, qti);
        if (sphereHits != null && sphereHits.Length > 0)
        {
            System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < sphereHits.Length; i++)
            {
                var col = sphereHits[i].collider;
                if (!col) continue;

                var u = col.GetComponentInParent<SelectableUnit>();
                if (!u) continue;

                if (!occlusionAware || IsNotOccluded(cam.transform.position, u.transform.position, u.transform.root))
                {
                    picked = u;
                    return true;
                }
            }
        }

        return false;
    }

    // ---------- Occlusion helpers ----------
    private static bool IsNotOccluded(Vector3 from, Vector3 to, Transform allowedRoot)
    {
        return !IsOccluded(from, to, allowedRoot);
    }

    // Ignores the candidate's own colliders and other SelectableUnits.
    private static bool IsOccluded(Vector3 from, Vector3 to, Transform allowedRoot)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return false;
        dir /= Mathf.Max(dist, 0.0001f);

        if (Physics.Raycast(from, dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider) return false;

            if (allowedRoot && hit.collider.transform.root == allowedRoot)
                return false;

            if (hit.collider.GetComponentInParent<SelectableUnit>() != null)
                return false;

            return true;
        }
        return false;
    }

    // ---------- Drag helpers ----------
    private void UpdateSelectionBox(Vector2 start, Vector2 end)
    {
        if (!selectionBox) return;

        Vector2 min = Vector2.Min(start, end);
        Vector2 max = Vector2.Max(start, end);
        Vector2 size = max - min;
        Vector2 center = (min + max) * 0.5f;

        selectionBox.position = center;
        selectionBox.sizeDelta = size;
    }

    private void BuildDragSet(Vector2 start, Vector2 end, HashSet<SelectableUnit> outSet)
    {
        outSet.Clear();
        Rect r = GetScreenRect(start, end);

        var units = RTSUnitRegistry.Units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (!u) continue;

            Vector3 sp = cam.WorldToScreenPoint(u.transform.position);
            if (sp.z < 0f) continue;
            if (r.Contains(sp)) outSet.Add(u);
        }
    }

    private void CommitDragSelection(bool additive)
    {
        if (additive)
        {
            foreach (var u in _dragSet) SelectionManager.Instance.Select(u);
        }
        else
        {
            SelectionManager.Instance.ReplaceSelection(_dragSet);
        }
    }

    private static Rect GetScreenRect(Vector2 a, Vector2 b)
    {
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // ---------- UI hit test ----------
    private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>(8);
    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        var ev = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        _uiHits.Clear();
        EventSystem.current.RaycastAll(ev, _uiHits);
        return _uiHits.Count > 0;
    }
}

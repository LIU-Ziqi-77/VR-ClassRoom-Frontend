using UnityEngine;

/// <summary>
/// Adds a world-space overhead label to a student avatar showing:
///   • The student's display name
///   • The current behavior (when one is active)
///   • A colored selection ring / highlight arrow when this student
///     is selected in BehaviorDemoController
///
/// Relies on Unity's built-in OnGUI drawing in world space via
/// Camera.WorldToScreenPoint — no external UI prefab required.
///
/// Attach to the student's root GameObject alongside
/// ProceduralBehaviorAnimator.
/// </summary>
[RequireComponent(typeof(ProceduralBehaviorAnimator))]
public class StudentBehaviorVisuals : MonoBehaviour
{
    [Header("Display")]
    [Tooltip("Name shown above the avatar. Leave blank to use GameObject name.")]
    public string displayName = "";

    [Tooltip("World-space height offset above the avatar root for the label.")]
    public float labelHeightOffset = 2.2f;

    [Header("Colors")]
    public Color idleColor      = new Color(0.85f, 0.85f, 0.85f, 0.7f);
    public Color selectedColor  = new Color(0.4f,  0.9f,  0.4f,  1f);
    public Color behaviorColor  = new Color(1f,    0.85f, 0.2f,  1f);
    public Color speakingColor  = new Color(0.4f,  0.75f, 1f,    1f);

    // ─── Runtime ─────────────────────────────────────────────

    ProceduralBehaviorAnimator _pba;
    BehaviorDemoController     _demo;

    // Cached GUI styles (created once in OnGUI to avoid GC allocs every frame)
    GUIStyle _nameStyle;
    GUIStyle _behaviorStyle;
    GUIStyle _arrowStyle;
    bool     _stylesReady;

    void Awake()
    {
        _pba = GetComponent<ProceduralBehaviorAnimator>();
        if (string.IsNullOrEmpty(displayName))
            displayName = gameObject.name;
    }

    void Start()
    {
        // BehaviorDemoController might not be present at Awake time (added by Setup)
        _demo = FindObjectOfType<BehaviorDemoController>();
    }

    void Update()
    {
        // Keep a fresh reference in case the demo controller was created after us
        if (_demo == null)
            _demo = FindObjectOfType<BehaviorDemoController>();
    }

    // ─── Rendering ───────────────────────────────────────────

    void OnGUI()
    {
        if (Camera.main == null) return;

        EnsureStyles();

        Vector3 worldPos = transform.position + Vector3.up * labelHeightOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        // Cull if behind camera or far away
        if (screenPos.z < 0) return;
        float distSq = (Camera.main.transform.position - transform.position).sqrMagnitude;
        if (distSq > 225f) return; // 15 m cutoff

        // Flip Y: Unity GUI origin is top-left, Camera.WorldToScreenPoint is bottom-left
        float sx = screenPos.x;
        float sy = Screen.height - screenPos.y;

        bool isSelected = IsSelected();
        bool hasBehavior = _pba != null && _pba.IsBehaviorActive;
        string behaviorName = _pba != null ? _pba.CurrentBehaviorName : "";

        // ── Selection arrow ──
        if (isSelected)
        {
            _arrowStyle.normal.textColor = selectedColor;
            float arrowY = sy - 28f;
            GUI.Label(new Rect(sx - 20f, arrowY, 40f, 24f), "▼", _arrowStyle);
        }

        // ── Behavior tag (only when active) ──
        if (hasBehavior && !string.IsNullOrEmpty(behaviorName))
        {
            _behaviorStyle.normal.textColor = BehaviorTagColor(behaviorName);
            Vector2 bSize = _behaviorStyle.CalcSize(new GUIContent(behaviorName));
            GUI.Label(new Rect(sx - bSize.x * 0.5f, sy - 14f, bSize.x + 6f, 20f),
                      behaviorName, _behaviorStyle);
        }

        // ── Name label ──
        Color nameCol = isSelected ? selectedColor : (hasBehavior ? behaviorColor : idleColor);
        _nameStyle.normal.textColor = nameCol;
        Vector2 nSize = _nameStyle.CalcSize(new GUIContent(displayName));
        GUI.Label(new Rect(sx - nSize.x * 0.5f, sy + 4f, nSize.x + 6f, 20f),
                  displayName, _nameStyle);
    }

    // ─── Helpers ─────────────────────────────────────────────

    bool IsSelected()
    {
        if (_demo == null) return false;
        var students = _demo.students;
        if (students == null || students.Count == 0) return false;
        int idx = Mathf.Clamp(_demo.SelectedIndex, 0, students.Count - 1);
        return students[idx] != null && students[idx].gameObject == gameObject;
    }

    Color BehaviorTagColor(string name)
    {
        if (name.Contains("说话") || name.Contains("提问")) return speakingColor;
        return behaviorColor;
    }

    void EnsureStyles()
    {
        if (_stylesReady) return;

        _nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        _behaviorStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
        };

        _arrowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        _stylesReady = true;
    }
}

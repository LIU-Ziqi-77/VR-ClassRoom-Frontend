using UnityEngine;

/// <summary>
/// Runtime-visible marker for the child currently selected for DTT.
/// Uses OnGUI to avoid requiring a UI prefab.
/// </summary>
public class DTTTargetStudentMarker : MonoBehaviour
{
    public bool isSelected;
    public float arrowHeightOffset = 2.05f;
    public Color arrowColor = new Color(0.2f, 1f, 0.25f, 1f);
    public string arrowText = "▼";

    private GUIStyle arrowStyle;

    void Awake()
    {
        EnsureRaycastCollider();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
    }

    void OnGUI()
    {
        if (!isSelected || Camera.main == null) return;
        if (HasStudentBehaviorVisuals()) return;

        if (arrowStyle == null)
        {
            arrowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        Vector3 worldPos = transform.position + Vector3.up * arrowHeightOffset;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        if (screenPos.z < 0f) return;

        arrowStyle.normal.textColor = arrowColor;
        float sx = screenPos.x;
        float sy = Screen.height - screenPos.y;
        GUI.Label(new Rect(sx - 30f, sy - 24f, 60f, 36f), arrowText, arrowStyle);
    }

    private void EnsureRaycastCollider()
    {
        Collider existing = GetComponentInChildren<Collider>();
        if (existing != null) return;

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.height = 1.8f;
        capsule.radius = 0.35f;
    }

    private bool HasStudentBehaviorVisuals()
    {
        return GetComponentInParent<StudentBehaviorVisuals>() != null
            || GetComponentInChildren<StudentBehaviorVisuals>() != null;
    }
}

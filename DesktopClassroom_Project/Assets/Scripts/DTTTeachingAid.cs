using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum DTTTeachingAidKind
{
    Ruler,
    Rubber,
    OpenNotebook,
    Pencils
}

/// <summary>
/// Marks a classroom object as a DTT teaching aid that can be selected, held,
/// and used as the current gaze stimulus.
/// </summary>
public class DTTTeachingAid : MonoBehaviour
{
    public DTTTeachingAidKind aidKind = DTTTeachingAidKind.Ruler;
    public string displayName = "Teaching Aid";
    public Transform gazeTarget;

    [Header("Hold State")]
    public bool isSelected;
    public bool isHeld;
    public bool returnToOriginalPoseOnRelease = true;

    [Header("Visual Feedback")]
    public bool useRuntimeHighlight = true;
    public Color selectedTint = new Color(0.2f, 1f, 0.35f, 1f);
    public Color heldTint = new Color(0.1f, 0.85f, 1f, 1f);
    public float selectedScaleMultiplier = 1.04f;
    public float heldScaleMultiplier = 1.08f;

    private Rigidbody cachedRigidbody;
    private XRGrabInteractable grabInteractable;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock highlightBlock;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;

    void Awake()
    {
        if (gazeTarget == null)
        {
            gazeTarget = transform;
        }

        cachedRigidbody = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
    }

    void OnEnable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnXrGrabbed);
        }
    }

    void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnXrGrabbed);
        }
    }

    void LateUpdate()
    {
        UpdateRuntimeHighlight();
    }

    public Transform GetGazeTarget()
    {
        return gazeTarget != null ? gazeTarget : transform;
    }

    public bool TryGetClassroomItemType(out ClassroomItemType itemType)
    {
        switch (aidKind)
        {
            case DTTTeachingAidKind.Ruler:
                itemType = ClassroomItemType.Ruler;
                return true;
            case DTTTeachingAidKind.Rubber:
                itemType = ClassroomItemType.Eraser;
                return true;
            case DTTTeachingAidKind.OpenNotebook:
                itemType = ClassroomItemType.OpenNotebook;
                return true;
            case DTTTeachingAidKind.Pencils:
                itemType = ClassroomItemType.Pencil;
                return true;
            default:
                itemType = ClassroomItemType.Ruler;
                return false;
        }
    }

    public void BeginHold()
    {
        isHeld = true;

        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        if (cachedRigidbody != null)
        {
            originalUseGravity = cachedRigidbody.useGravity;
            originalIsKinematic = cachedRigidbody.isKinematic;
            cachedRigidbody.useGravity = false;
            cachedRigidbody.isKinematic = true;
        }
    }

    public void EndHold()
    {
        isHeld = false;

        if (returnToOriginalPoseOnRelease)
        {
            transform.SetPositionAndRotation(originalPosition, originalRotation);
        }

        if (cachedRigidbody != null)
        {
            cachedRigidbody.useGravity = originalUseGravity;
            cachedRigidbody.isKinematic = originalIsKinematic;
        }
    }

    private void UpdateRuntimeHighlight()
    {
        if (!useRuntimeHighlight) return;

        bool highlighted = isHeld || isSelected;
        float scaleMultiplier = isHeld ? heldScaleMultiplier : selectedScaleMultiplier;
        transform.localScale = highlighted ? originalScale * scaleMultiplier : originalScale;

        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        if (!highlighted)
        {
            foreach (Renderer renderer in cachedRenderers)
            {
                if (renderer != null) renderer.SetPropertyBlock(null);
            }
            return;
        }

        if (highlightBlock == null)
        {
            highlightBlock = new MaterialPropertyBlock();
        }

        Color tint = isHeld ? heldTint : selectedTint;
        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(highlightBlock);
            highlightBlock.SetColor("_BaseColor", tint);
            highlightBlock.SetColor("_Color", tint);
            highlightBlock.SetColor("_EmissionColor", tint * 0.35f);
            renderer.SetPropertyBlock(highlightBlock);
        }
    }

    private void OnXrGrabbed(SelectEnterEventArgs args)
    {
        if (DTTTeachingAidManager.Instance != null)
        {
            DTTTeachingAidManager.Instance.NotifyAidGrabbed(this);
        }
    }
}

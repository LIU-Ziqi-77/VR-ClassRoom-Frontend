using UnityEngine;
using UnityEngine.Rendering;
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
    public bool preserveOriginalMaterialColors = true;
    public bool showSelectionShadow = false;
    public Color selectionShadowColor = new Color(0.1f, 0.9f, 0.28f, 0.34f);
    public Color selectedTint = new Color(0.2f, 1f, 0.35f, 1f);
    public Color heldTint = new Color(0.1f, 0.85f, 1f, 1f);
    public float selectedScaleMultiplier = 1.04f;
    public float heldScaleMultiplier = 1.08f;
    public float selectionShadowScalePadding = 1.25f;

    private Rigidbody cachedRigidbody;
    private XRGrabInteractable grabInteractable;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock highlightBlock;
    private Transform selectionShadow;
    private Material selectionShadowMaterial;
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
        if (!useRuntimeHighlight)
        {
            HideSelectionShadow();
            return;
        }

        bool highlighted = isHeld || isSelected;
        float scaleMultiplier = isHeld ? heldScaleMultiplier : selectedScaleMultiplier;
        transform.localScale = highlighted ? originalScale * scaleMultiplier : originalScale;
        UpdateSelectionShadow(highlighted);

        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        bool shouldTint = isSelected && !isHeld;
        if (!shouldTint)
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

        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(highlightBlock);
            highlightBlock.SetColor("_BaseColor", selectedTint);
            highlightBlock.SetColor("_Color", selectedTint);
            highlightBlock.SetColor("_EmissionColor", selectedTint * 0.35f);
            renderer.SetPropertyBlock(highlightBlock);
        }
    }

    private void UpdateSelectionShadow(bool highlighted)
    {
        if (!showSelectionShadow || !highlighted || cachedRenderers == null || cachedRenderers.Length == 0)
        {
            HideSelectionShadow();
            return;
        }

        EnsureSelectionShadow();
        if (selectionShadow == null) return;

        if (!TryGetRendererBounds(out Bounds bounds))
        {
            HideSelectionShadow();
            return;
        }

        selectionShadow.gameObject.SetActive(true);
        selectionShadow.position = new Vector3(bounds.center.x, bounds.min.y + 0.003f, bounds.center.z);
        selectionShadow.rotation = Quaternion.identity;

        float diameter = Mathf.Max(bounds.size.x, bounds.size.z, 0.04f) * selectionShadowScalePadding;
        Vector3 parentScale = transform.lossyScale;
        selectionShadow.localScale = new Vector3(
            diameter / Mathf.Max(Mathf.Abs(parentScale.x), 0.001f),
            0.0015f / Mathf.Max(Mathf.Abs(parentScale.y), 0.001f),
            diameter / Mathf.Max(Mathf.Abs(parentScale.z), 0.001f));
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void EnsureSelectionShadow()
    {
        if (selectionShadow != null) return;

        Transform existing = transform.Find("Selection Shadow");
        if (existing != null)
        {
            selectionShadow = existing;
        }
        else
        {
            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = "Selection Shadow";
            shadow.transform.SetParent(transform, false);
            Collider collider = shadow.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            selectionShadow = shadow.transform;
        }

        Renderer renderer = selectionShadow.GetComponent<Renderer>();
        if (renderer == null) return;

        if (selectionShadowMaterial == null)
        {
            selectionShadowMaterial = CreateTransparentMaterial(selectionShadowColor);
        }

        renderer.sharedMaterial = selectionShadowMaterial;
    }

    private void HideSelectionShadow()
    {
        if (selectionShadow == null)
        {
            selectionShadow = transform.Find("Selection Shadow");
        }

        if (selectionShadow != null)
        {
            selectionShadow.gameObject.SetActive(false);
        }
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            color = color
        };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private void OnXrGrabbed(SelectEnterEventArgs args)
    {
        if (DTTTeachingAidManager.Instance != null)
        {
            DTTTeachingAidManager.Instance.NotifyAidGrabbed(this);
        }
    }
}

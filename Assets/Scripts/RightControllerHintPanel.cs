using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

/// <summary>
/// Lightweight button-location hints that sit directly on the right Quest controller model.
/// The marker offsets are intentionally inspector-editable because official controller model
/// pivots can vary between SDK/profile versions.
/// </summary>
public class RightControllerHintPanel : MonoBehaviour
{
    public XRNode controllerNode = XRNode.RightHand;

    [Header("Layout")]
    public bool faceLabelsTowardCamera = true;
    public float labelTextSize = 0.0016f;
    public Color labelColor = new Color(0.84f, 0.88f, 0.9f, 0.72f);
    public float lineWidth = 0.00085f;
    public float idleAlpha = 0.58f;
    public float pressedAlpha = 0.95f;
    public float pulseScale = 1.28f;
    public float pulseSeconds = 0.18f;

    [Header("Model Anchors")]
    public bool useModelButtonAnchors = true;
    public string modelRootName = "Official Meta Quest 3 Touch Plus Right Controller Model";
    public string triggerAnchorName = "right_b_trigger_front";
    public string gripAnchorName = "right_b_trigger_grip";
    public string aButtonAnchorName = "b_button_a";
    [Tooltip("Offset in this hint root's local space. Negative Y moves down.")]
    public Vector3 triggerAnchorLocalOffset = new Vector3(0f, -0.004f, 0f);
    [Tooltip("Offset in this hint root's local space. Negative Z moves back.")]
    public Vector3 gripAnchorLocalOffset = new Vector3(0f, 0f, -0.005f);
    public Vector3 aButtonAnchorLocalOffset = Vector3.zero;
    public bool showCalibrationGizmos = true;

    [Header("Trigger")]
    public string triggerLabel = "选定教具";
    public Vector3 triggerMarkerLocalPosition = new Vector3(0.0157f, -0.0014f, 0.0243f);
    public Vector3 triggerMarkerLocalScale = new Vector3(0.007f, 0.0035f, 0.005f);
    public Vector3 triggerLabelLocalPosition = new Vector3(0.048f, -0.002f, 0.038f);
    public Color triggerColor = new Color(0.15f, 0.72f, 1f, 1f);

    [Header("Grip")]
    public string gripLabel = "呈现/收起教具";
    public Vector3 gripMarkerLocalPosition = new Vector3(0.0116f, -0.0214f, 0.0126f);
    public Vector3 gripMarkerLocalScale = new Vector3(0.0045f, 0.012f, 0.006f);
    public Vector3 gripLabelLocalPosition = new Vector3(-0.058f, -0.032f, 0.013f);
    public Color gripColor = new Color(1f, 0.58f, 0.16f, 1f);

    [Header("A Button")]
    public string aButtonLabel = "打开/收起参考教材";
    public Vector3 aMarkerLocalPosition = new Vector3(0.0049f, 0.0044f, -0.0097f);
    public Vector3 aMarkerLocalScale = new Vector3(0.005f, 0.005f, 0.005f);
    public Vector3 aLabelLocalPosition = new Vector3(0.044f, 0.018f, -0.01f);
    public Color aButtonColor = new Color(0.35f, 1f, 0.48f, 1f);

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private HintVisual triggerHint;
    private HintVisual gripHint;
    private HintVisual aButtonHint;
    private readonly Dictionary<string, Transform> anchorCache = new Dictionary<string, Transform>();
    private Transform modelRoot;
    private InputDevice controllerDevice;
    private bool previousTrigger;
    private bool previousGrip;
    private bool previousAButton;

    private void Awake()
    {
        RebuildHints();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && isActiveAndEnabled)
        {
            RebuildHints();
        }
    }

    private void Update()
    {
        EnsureHints();
        UpdateInputPulse();
        ApplyHintLayout();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showCalibrationGizmos) return;

        DrawCalibrationGizmo(triggerAnchorName, triggerMarkerLocalPosition, triggerAnchorLocalOffset, triggerLabelLocalPosition, triggerColor);
        DrawCalibrationGizmo(gripAnchorName, gripMarkerLocalPosition, gripAnchorLocalOffset, gripLabelLocalPosition, gripColor);
        DrawCalibrationGizmo(aButtonAnchorName, aMarkerLocalPosition, aButtonAnchorLocalOffset, aLabelLocalPosition, aButtonColor);
    }

    private void ApplyHintLayout()
    {
        UpdateHint(
            triggerHint,
            ResolveMarkerLocalPosition(triggerAnchorName, triggerMarkerLocalPosition, triggerAnchorLocalOffset),
            triggerMarkerLocalScale,
            triggerLabelLocalPosition,
            triggerLabel,
            triggerColor);
        UpdateHint(
            gripHint,
            ResolveMarkerLocalPosition(gripAnchorName, gripMarkerLocalPosition, gripAnchorLocalOffset),
            gripMarkerLocalScale,
            gripLabelLocalPosition,
            gripLabel,
            gripColor);
        UpdateHint(
            aButtonHint,
            ResolveMarkerLocalPosition(aButtonAnchorName, aMarkerLocalPosition, aButtonAnchorLocalOffset),
            aMarkerLocalScale,
            aLabelLocalPosition,
            aButtonLabel,
            aButtonColor);
    }

    [ContextMenu("Rebuild Controller Hints")]
    public void RebuildHints()
    {
        ClearGeneratedChildren();
        anchorCache.Clear();
        triggerHint = CreateHint("Trigger Hint", PrimitiveType.Cube, triggerColor);
        gripHint = CreateHint("Grip Hint", PrimitiveType.Cube, gripColor);
        aButtonHint = CreateHint("A Button Hint", PrimitiveType.Sphere, aButtonColor);
        ApplyHintLayout();
    }

    private void EnsureHints()
    {
        if (triggerHint == null || triggerHint.Marker == null)
        {
            RebuildHints();
        }
    }

    private void UpdateInputPulse()
    {
        if (!TryGetControllerDevice()) return;

        bool triggerPressed = ReadButton(CommonUsages.triggerButton);
        bool gripPressed = ReadButton(CommonUsages.gripButton);
        bool aPressed = ReadButton(CommonUsages.primaryButton);

        if (triggerPressed && !previousTrigger) triggerHint.PulseRemaining = pulseSeconds;
        if (gripPressed && !previousGrip) gripHint.PulseRemaining = pulseSeconds;
        if (aPressed && !previousAButton) aButtonHint.PulseRemaining = pulseSeconds;

        previousTrigger = triggerPressed;
        previousGrip = gripPressed;
        previousAButton = aPressed;
    }

    private bool ReadButton(InputFeatureUsage<bool> usage)
    {
        return controllerDevice.isValid &&
               controllerDevice.TryGetFeatureValue(usage, out bool pressed) &&
               pressed;
    }

    private bool TryGetControllerDevice()
    {
        if (controllerDevice.isValid) return true;

        devices.Clear();
        InputDevices.GetDevicesAtXRNode(controllerNode, devices);
        if (devices.Count == 0) return false;

        controllerDevice = devices[0];
        return controllerDevice.isValid;
    }

    private void UpdateHint(
        HintVisual hint,
        Vector3 markerLocalPosition,
        Vector3 markerLocalScale,
        Vector3 labelLocalPosition,
        string label,
        Color baseColor)
    {
        if (hint == null) return;

        float pulse = 0f;
        if (hint.PulseRemaining > 0f)
        {
            hint.PulseRemaining = Mathf.Max(0f, hint.PulseRemaining - Time.deltaTime);
            pulse = hint.PulseRemaining / Mathf.Max(0.001f, pulseSeconds);
        }

        float alpha = Mathf.Lerp(idleAlpha, pressedAlpha, pulse);
        float scaleMultiplier = Mathf.Lerp(1f, pulseScale, pulse);
        Color color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        hint.Marker.localPosition = markerLocalPosition;
        hint.Marker.localScale = Vector3.Scale(markerLocalScale, hint.BaseMarkerScale) * scaleMultiplier;
        ApplyColor(hint.MarkerMaterial, color);

        hint.Label.transform.localPosition = labelLocalPosition;
        hint.Label.text = label;
        hint.Label.characterSize = labelTextSize;
        bool labelIsLeftOfMarker = labelLocalPosition.x < markerLocalPosition.x;
        hint.Label.anchor = labelIsLeftOfMarker ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        hint.Label.alignment = labelIsLeftOfMarker ? TextAlignment.Right : TextAlignment.Left;
        float labelAlpha = Mathf.Clamp01(labelColor.a * Mathf.Lerp(0.75f, 1f, pulse));
        hint.Label.color = new Color(labelColor.r, labelColor.g, labelColor.b, labelAlpha);

        if (faceLabelsTowardCamera && Camera.main != null)
        {
            Vector3 toCamera = hint.Label.transform.position - Camera.main.transform.position;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                hint.Label.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
        }
        else
        {
            hint.Label.transform.localRotation = Quaternion.identity;
        }

        hint.Line.startWidth = lineWidth;
        hint.Line.endWidth = lineWidth * 0.45f;
        hint.Line.startColor = color;
        hint.Line.endColor = new Color(color.r, color.g, color.b, color.a * 0.25f);
        hint.Line.SetPosition(0, hint.Marker.position);
        hint.Line.SetPosition(1, hint.Label.transform.position);
    }

    private Vector3 ResolveMarkerLocalPosition(string anchorName, Vector3 fallbackLocalPosition, Vector3 anchorLocalOffset)
    {
        Transform anchor = FindModelAnchor(anchorName);
        if (anchor == null) return fallbackLocalPosition + anchorLocalOffset;

        Vector3 anchorLocalPosition = transform.InverseTransformPoint(anchor.position);
        return anchorLocalPosition + anchorLocalOffset;
    }

    private Transform FindModelAnchor(string anchorName)
    {
        if (!useModelButtonAnchors || string.IsNullOrEmpty(anchorName)) return null;

        if (anchorCache.TryGetValue(anchorName, out Transform cachedAnchor) && cachedAnchor != null)
        {
            return cachedAnchor;
        }

        if (modelRoot == null || modelRoot.name != modelRootName)
        {
            modelRoot = FindDescendant(transform, modelRootName);
            anchorCache.Clear();
        }

        if (modelRoot == null) return null;

        Transform anchor = FindDescendant(modelRoot, anchorName);
        if (anchor != null)
        {
            anchorCache[anchorName] = anchor;
        }

        return anchor;
    }

    private static Transform FindDescendant(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindDescendant(child, childName);
            if (found != null) return found;
        }

        return null;
    }

    private void DrawCalibrationGizmo(
        string anchorName,
        Vector3 fallbackLocalPosition,
        Vector3 anchorLocalOffset,
        Vector3 labelLocalPosition,
        Color baseColor)
    {
        Vector3 markerLocalPosition = ResolveMarkerLocalPosition(anchorName, fallbackLocalPosition, anchorLocalOffset);
        Vector3 markerWorldPosition = transform.TransformPoint(markerLocalPosition);
        Vector3 labelWorldPosition = transform.TransformPoint(labelLocalPosition);
        Color color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.9f);

        Gizmos.color = color;
        Gizmos.DrawSphere(markerWorldPosition, 0.004f);
        Gizmos.DrawLine(markerWorldPosition, labelWorldPosition);
    }

    private HintVisual CreateHint(string name, PrimitiveType markerType, Color color)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);

        GameObject marker = GameObject.CreatePrimitive(markerType);
        marker.name = "Button Marker";
        marker.transform.SetParent(root.transform, false);
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyInEditOrPlay(collider);
        }

        Material markerMaterial = CreateTransparentMaterial(color);
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = markerMaterial;
        }

        GameObject labelObject = new GameObject("Button Label");
        labelObject.transform.SetParent(root.transform, false);
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleLeft;
        label.alignment = TextAlignment.Left;
        label.characterSize = labelTextSize;
        label.fontSize = 64;
        label.richText = false;

        GameObject lineObject = new GameObject("Leader Line");
        lineObject.transform.SetParent(root.transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.sharedMaterial = CreateTransparentMaterial(color);

        return new HintVisual
        {
            Root = root.transform,
            Marker = marker.transform,
            Label = label,
            Line = line,
            MarkerMaterial = markerMaterial,
            BaseMarkerScale = Vector3.one
        };
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.EndsWith(" Hint", StringComparison.Ordinal))
            {
                DestroyInEditOrPlay(child.gameObject);
            }
        }
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        ConfigureTransparentMaterial(material);
        ApplyColor(material, color);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null) return;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.color = color;
    }

    private static void DestroyInEditOrPlay(UnityEngine.Object target)
    {
        if (target == null) return;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private class HintVisual
    {
        public Transform Root;
        public Transform Marker;
        public TextMesh Label;
        public LineRenderer Line;
        public Material MarkerMaterial;
        public Vector3 BaseMarkerScale;
        public float PulseRemaining;
    }
}

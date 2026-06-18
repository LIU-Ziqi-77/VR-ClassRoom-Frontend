using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Tracks an XR controller pose and renders a visible physics ray for lightweight
/// Quest classroom interactions.
/// </summary>
public class QuestControllerRayVisual : MonoBehaviour
{
    public XRNode controllerNode = XRNode.RightHand;
    public float rayDistance = 12f;
    public LayerMask raycastMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    public float minimumHitDistance = 0.08f;
    public bool ignoreTriggerColliders = true;
    public bool ignoreOwnHierarchy = true;

    [Header("Visuals")]
    public bool showRay = true;
    public bool showOnlyWhenControllerPose = true;
    public float lineWidth = 0.012f;
    public Color idleColor = new Color(0.25f, 1f, 0.35f, 0.95f);
    public Color hitColor = new Color(0.25f, 1f, 0.35f, 1f);
    public float reticleSize = 0.035f;

    [Header("Editor Fallback")]
    public bool fallbackToCameraWhenNoController = true;
    public Vector3 cameraLocalPosition = new Vector3(0.18f, -0.22f, 0.45f);
    public Vector3 cameraLocalEuler = new Vector3(8f, 0f, 0f);

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private static readonly RaycastHit[] raycastHits = new RaycastHit[64];
    private LineRenderer lineRenderer;
    private Transform reticle;
    private Material lineMaterial;
    private Material reticleMaterial;

    void Awake()
    {
        EnsureVisuals();
    }

    void Update()
    {
        bool hasControllerPose = TryApplyControllerPose();
        if (!hasControllerPose && fallbackToCameraWhenNoController)
        {
            ApplyCameraFallbackPose();
        }

        UpdateRayVisual(hasControllerPose || !showOnlyWhenControllerPose);
    }

    private bool TryApplyControllerPose()
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(controllerNode, devices);
        if (devices.Count == 0) return false;

        InputDevice device = devices[0];
        if (!device.isValid) return false;

        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);
        if (!hasPosition || !hasRotation) return false;

        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        return true;
    }

    private void ApplyCameraFallbackPose()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Transform cameraTransform = mainCamera.transform;
        transform.SetPositionAndRotation(
            cameraTransform.TransformPoint(cameraLocalPosition),
            cameraTransform.rotation * Quaternion.Euler(cameraLocalEuler));
    }

    private void UpdateRayVisual(bool canShowRay)
    {
        EnsureVisuals();

        bool hitSomething = TryGetFirstVisibleRayHit(out RaycastHit hit);

        Vector3 endPoint = hitSomething ? hit.point : transform.position + transform.forward * rayDistance;
        Color color = hitSomething ? hitColor : idleColor;
        ApplyColor(lineMaterial, color);
        ApplyColor(reticleMaterial, hitColor);

        if (lineRenderer != null)
        {
            lineRenderer.enabled = showRay && canShowRay;
            lineRenderer.startColor = color;
            lineRenderer.endColor = new Color(color.r, color.g, color.b, color.a * 0.18f);
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth * 0.35f;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, endPoint);
        }

        if (reticle != null)
        {
            reticle.gameObject.SetActive(showRay && canShowRay && hitSomething);
            if (hitSomething)
            {
                reticle.position = hit.point;
                reticle.localScale = Vector3.one * reticleSize;
            }
        }
    }

    private bool TryGetFirstVisibleRayHit(out RaycastHit selectedHit)
    {
        selectedHit = default;

        int hitCount = Physics.RaycastNonAlloc(
            transform.position,
            transform.forward,
            raycastHits,
            rayDistance,
            raycastMask,
            triggerInteraction);

        if (hitCount == 0) return false;

        System.Array.Sort(raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            if (ShouldIgnoreHit(hit)) continue;

            selectedHit = hit;
            return true;
        }

        return false;
    }

    private bool ShouldIgnoreHit(RaycastHit hit)
    {
        if (hit.collider == null) return true;
        if (hit.distance < minimumHitDistance) return true;
        if (ignoreTriggerColliders && hit.collider.isTrigger) return true;

        Transform hitTransform = hit.collider.transform;
        if (ignoreOwnHierarchy && (hitTransform == transform || hitTransform.IsChildOf(transform)))
        {
            return true;
        }

        if (hit.collider.GetComponentInParent<RightControllerHintPanel>() != null)
        {
            return true;
        }

        return false;
    }

    private void EnsureVisuals()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
            lineRenderer.numCapVertices = 6;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureMode = LineTextureMode.Stretch;

            if (lineMaterial == null)
            {
                lineMaterial = CreateUnlitMaterial(idleColor);
            }

            lineRenderer.sharedMaterial = lineMaterial;
        }

        if (reticle == null)
        {
            Transform existing = transform.Find("Ray Hit Reticle");
            if (existing != null)
            {
                reticle = existing;
            }
            else
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Ray Hit Reticle";
                sphere.transform.SetParent(transform, false);
                Destroy(sphere.GetComponent<Collider>());
                reticle = sphere.transform;
            }

            Renderer renderer = reticle.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (reticleMaterial == null)
                {
                    reticleMaterial = CreateUnlitMaterial(hitColor);
                }

                renderer.sharedMaterial = reticleMaterial;
            }
        }
    }

    private static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            color = color
        };

        ApplyColor(material, color);
        return material;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.color = color;
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }
}

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
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Visuals")]
    public bool showRay = true;
    public bool showOnlyWhenControllerPose = true;
    public float lineWidth = 0.012f;
    public Color idleColor = new Color(0.15f, 0.95f, 1f, 0.95f);
    public Color hitColor = new Color(0.25f, 1f, 0.35f, 1f);
    public float reticleSize = 0.035f;

    [Header("Editor Fallback")]
    public bool fallbackToCameraWhenNoController = true;
    public Vector3 cameraLocalPosition = new Vector3(0.18f, -0.22f, 0.45f);
    public Vector3 cameraLocalEuler = new Vector3(8f, 0f, 0f);

    private readonly List<InputDevice> devices = new List<InputDevice>();
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

        bool hitSomething = Physics.Raycast(
            transform.position,
            transform.forward,
            out RaycastHit hit,
            rayDistance,
            raycastMask,
            triggerInteraction);

        Vector3 endPoint = hitSomething ? hit.point : transform.position + transform.forward * rayDistance;
        Color color = hitSomething ? hitColor : idleColor;

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

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }
}

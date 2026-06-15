using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;

/// <summary>
/// Minimal VR/desktop interaction bridge for DTT:
/// - point at a teaching aid or student and press select
/// - press the hold button once to pick up the selected aid, press again to return it
/// </summary>
public class DTTTeacherInteractionController : MonoBehaviour
{
    [Header("References")]
    public DTTTeachingAidManager manager;
    public DTTWorkflowController workflowController;
    public Transform rayOriginOverride;
    public Transform holdAnchorOverride;

    [Header("Raycast")]
    public float rayDistance = 12f;
    public LayerMask raycastMask = ~0;
    public bool drawDebugRay = true;
    public bool selectThroughNonDTTObjects = true;
    public bool selectThroughNonDTTObjectsForDesktopTesting = true;

    [Header("XR Input")]
    public XRNode controllerNode = XRNode.RightHand;
    public bool useXRInput = true;
    public bool triggerSelects = true;
    [FormerlySerializedAs("gripHoldsAid")]
    public bool gripTogglesAidHold = true;
    public bool triggerCanReturnKekeLeaveSeat = true;

    [Header("Keyboard Fallback")]
    public bool keyboardFallback = true;
    public KeyCode selectKey = KeyCode.J;
    public KeyCode holdKey = KeyCode.K;

    [Header("Desktop Test UI")]
    public bool showDesktopReticle = true;
    public bool showDesktopSelectionHint = true;
    public Color reticleColor = new Color(0.1f, 0.95f, 1f, 0.95f);

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private InputDevice controllerDevice;
    private Transform runtimeControllerAnchor;
    private bool previousTrigger;
    private bool previousHold;
    private bool usingDesktopRayOrigin;
    private bool hasControllerPoseThisFrame;
    private string currentAimLabel = "Aim: none";
    private GUIStyle hintStyle;

    void Awake()
    {
        if (manager == null)
        {
            manager = FindObjectOfType<DTTTeachingAidManager>();
        }

        if (workflowController == null)
        {
            workflowController = FindObjectOfType<DTTWorkflowController>();
        }
    }

    void Update()
    {
        if (manager == null)
        {
            manager = FindObjectOfType<DTTTeachingAidManager>();
            if (manager == null) return;
        }

        if (workflowController == null)
        {
            workflowController = FindObjectOfType<DTTWorkflowController>();
        }

        hasControllerPoseThisFrame = UpdateRuntimeControllerAnchor();

        Transform rayOrigin = GetRayOrigin();
        if (rayOrigin == null) return;

        usingDesktopRayOrigin = Camera.main != null && rayOrigin == Camera.main.transform;
        UpdateAimLabel(rayOrigin);

        if (drawDebugRay)
        {
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * rayDistance, Color.cyan);
        }

        bool selectDown = GetSelectDown();
        bool holdToggleDown = GetHoldToggleDown();

        if (selectDown)
        {
            TrySelectFromRay(rayOrigin);
        }

        if (holdToggleDown)
        {
            if (manager.heldAid != null)
            {
                manager.ReleaseHeldAid();
            }
            else
            {
                manager.BeginHoldSelectedAid();
            }
        }

        if (manager.heldAid != null)
        {
            manager.UpdateHeldAid(GetHoldAnchor(rayOrigin));
        }
    }

    private void TrySelectFromRay(Transform rayOrigin)
    {
        RaycastHit hit;
        DTTTeachingAid aid;
        DTTTargetStudentMarker student;
        if (!TryFindDTTTarget(rayOrigin, out aid, out student, out hit))
        {
            Debug.Log("[DTT] Select ray hit nothing.");
            return;
        }

        if (aid != null)
        {
            manager.SelectAid(aid);
            return;
        }

        if (student != null)
        {
            if (triggerCanReturnKekeLeaveSeat &&
                workflowController != null &&
                workflowController.TryHandleKekeReturnPointer(student))
            {
                Debug.Log($"[DTT] Select ray requested Keke return-to-seat: {student.gameObject.name}");
                return;
            }

            Debug.Log($"[DTT] Student ray selection disabled; use voice to select students: {student.gameObject.name}");
            return;
        }

        Debug.Log($"[DTT] Select ray hit non-DTT object: {hit.collider.name}");
    }

    private void UpdateAimLabel(Transform rayOrigin)
    {
        RaycastHit hit;
        DTTTeachingAid aid;
        DTTTargetStudentMarker student;
        if (TryFindDTTTarget(rayOrigin, out aid, out student, out hit))
        {
            if (aid != null)
            {
                currentAimLabel = $"Aim: teaching aid - {aid.displayName}";
            }
            else if (student != null)
            {
                currentAimLabel = $"Aim: student - {student.gameObject.name} (voice select only)";
            }
            return;
        }

        if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out hit, rayDistance, raycastMask, QueryTriggerInteraction.Collide))
        {
            currentAimLabel = $"Aim: {hit.collider.name} (not DTT target)";
        }
        else
        {
            currentAimLabel = "Aim: none";
        }
    }

    private bool TryFindDTTTarget(
        Transform rayOrigin,
        out DTTTeachingAid aid,
        out DTTTargetStudentMarker student,
        out RaycastHit selectedHit)
    {
        aid = null;
        student = null;
        selectedHit = default;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, rayDistance, raycastMask, QueryTriggerInteraction.Collide);
        if (hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            DTTTeachingAid hitAid = hit.collider.GetComponentInParent<DTTTeachingAid>();
            if (hitAid != null)
            {
                aid = hitAid;
                selectedHit = hit;
                return true;
            }

            DTTTargetStudentMarker hitStudent = hit.collider.GetComponentInParent<DTTTargetStudentMarker>();
            if (hitStudent != null)
            {
                student = hitStudent;
                selectedHit = hit;
                return true;
            }

            bool canKeepSearching =
                selectThroughNonDTTObjects ||
                (selectThroughNonDTTObjectsForDesktopTesting && usingDesktopRayOrigin);

            if (!canKeepSearching)
            {
                selectedHit = hit;
                return false;
            }
        }

        return false;
    }

    private Transform GetRayOrigin()
    {
        if (ShouldUseXrControllerRay())
        {
            if (rayOriginOverride != null)
            {
                return rayOriginOverride;
            }

            if (runtimeControllerAnchor != null)
            {
                return runtimeControllerAnchor;
            }
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    private Transform GetHoldAnchor(Transform fallbackRayOrigin)
    {
        if (ShouldUseXrControllerRay() && holdAnchorOverride != null)
        {
            return holdAnchorOverride;
        }

        return ShouldUseXrControllerRay() && runtimeControllerAnchor != null
            ? runtimeControllerAnchor
            : fallbackRayOrigin;
    }

    private bool ShouldUseXrControllerRay()
    {
        return useXRInput && hasControllerPoseThisFrame;
    }

    private bool GetSelectDown()
    {
        bool pressed = false;

        if (useXRInput && triggerSelects && TryGetControllerDevice())
        {
            controllerDevice.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);
        }

        if (keyboardFallback && DesktopInputBridge.GetKeyDown(selectKey))
        {
            pressed = true;
        }

        bool down = pressed && !previousTrigger;
        previousTrigger = pressed;
        return down;
    }

    private bool GetHoldToggleDown()
    {
        bool pressed = false;

        if (useXRInput && gripTogglesAidHold && TryGetControllerDevice())
        {
            controllerDevice.TryGetFeatureValue(CommonUsages.gripButton, out pressed);
        }

        if (keyboardFallback && DesktopInputBridge.GetKeyDown(holdKey))
        {
            pressed = true;
        }

        bool down = pressed && !previousHold;
        previousHold = pressed;
        return down;
    }

    void OnGUI()
    {
        if (!usingDesktopRayOrigin) return;

        if (showDesktopReticle)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color previousColor = GUI.color;
            GUI.color = reticleColor;
            GUI.DrawTexture(new Rect(cx - 9f, cy - 1f, 18f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 9f, 2f, 18f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        if (!showDesktopSelectionHint) return;

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6)
            };
            hintStyle.normal.textColor = Color.white;
        }

        string selectedStudent = manager != null && manager.selectedStudent != null
            ? manager.selectedStudent.gameObject.name
            : "none";
        string selectedAid = manager != null && manager.selectedAid != null
            ? manager.selectedAid.displayName
            : "none";
        string heldAid = manager != null && manager.heldAid != null
            ? manager.heldAid.displayName
            : "none";
        string hint = $"{currentAimLabel}\nJ select aid/Keke return | K toggle pick up/return aid | student selection by voice\nSelected student: {selectedStudent} | Selected aid: {selectedAid} | Held aid: {heldAid}";

        GUI.Box(new Rect(16f, Screen.height - 92f, 620f, 76f), hint, hintStyle);
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

    private bool UpdateRuntimeControllerAnchor()
    {
        if (!useXRInput || !TryGetControllerDevice()) return false;

        Vector3 position;
        Quaternion rotation;
        bool hasPosition = controllerDevice.TryGetFeatureValue(CommonUsages.devicePosition, out position);
        bool hasRotation = controllerDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
        if (!hasPosition || !hasRotation) return false;

        if (runtimeControllerAnchor == null)
        {
            GameObject anchor = new GameObject("DTT Runtime Right Hand Anchor");
            anchor.hideFlags = HideFlags.HideInHierarchy;
            runtimeControllerAnchor = anchor.transform;
        }

        runtimeControllerAnchor.SetPositionAndRotation(position, rotation);
        return true;
    }
}

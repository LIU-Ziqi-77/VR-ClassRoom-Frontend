using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Direct Quest locomotion fallback that does not require XR Interaction Toolkit input actions.
/// Attach to XR Origin. Left stick moves, right stick snap-turns.
/// On Quest left controller, Y moves up and X moves down.
/// </summary>
public class QuestThumbstickLocomotion : MonoBehaviour
{
    public XROrigin xrOrigin;
    public XRNode moveHand = XRNode.LeftHand;
    public XRNode turnHand = XRNode.RightHand;
    [Tooltip("Quest left hand: primaryButton is X, secondaryButton is Y.")]
    public XRNode verticalMoveHand = XRNode.LeftHand;
    public float moveSpeed = 1.8f;
    public float verticalMoveSpeed = 1.2f;
    public float deadzone = 0.18f;
    public float snapTurnDegrees = 45f;
    public float snapTurnCooldown = 0.35f;
    public bool enableVerticalMoveButtons = true;

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private float nextTurnTime;

    void Awake()
    {
        if (xrOrigin == null)
        {
            xrOrigin = GetComponent<XROrigin>();
        }
    }

    void Update()
    {
        if (!ShouldUseXrInput()) return;

        Vector2 moveAxis = ReadPrimary2DAxis(moveHand);
        if (moveAxis.magnitude > deadzone)
        {
            Move(moveAxis);
        }

        if (enableVerticalMoveButtons)
        {
            MoveVertically(ReadVerticalMoveDirection());
        }

        Vector2 turnAxis = ReadPrimary2DAxis(turnHand);
        if (Mathf.Abs(turnAxis.x) > 0.75f && Time.time >= nextTurnTime)
        {
            SnapTurn(Mathf.Sign(turnAxis.x));
            nextTurnTime = Time.time + snapTurnCooldown;
        }
    }

    private void Move(Vector2 axis)
    {
        Transform head = GetHeadTransform();
        if (head == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        Vector3 delta = (forward * axis.y + right * axis.x) * (moveSpeed * Time.deltaTime);
        transform.position += delta;
    }

    private void MoveVertically(float direction)
    {
        if (Mathf.Approximately(direction, 0f)) return;

        transform.position += Vector3.up * (direction * verticalMoveSpeed * Time.deltaTime);
    }

    private void SnapTurn(float direction)
    {
        Transform head = GetHeadTransform();
        if (head == null) return;

        transform.RotateAround(head.position, Vector3.up, direction * snapTurnDegrees);
    }

    private Transform GetHeadTransform()
    {
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            return xrOrigin.Camera.transform;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    private Vector2 ReadPrimary2DAxis(XRNode node)
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);
        if (devices.Count == 0) return Vector2.zero;

        InputDevice device = devices[0];
        return device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis)
            ? axis
            : Vector2.zero;
    }

    private float ReadVerticalMoveDirection()
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(verticalMoveHand, devices);
        if (devices.Count == 0) return 0f;

        InputDevice device = devices[0];
        if (!device.isValid) return 0f;

        bool xPressed = device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed) && primaryPressed;
        bool yPressed = device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryPressed) && secondaryPressed;

        if (yPressed == xPressed) return 0f;
        return yPressed ? 1f : -1f;
    }

    private static bool ShouldUseXrInput()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return HasTrackedHeadPose(XRNode.CenterEye) || HasTrackedHeadPose(XRNode.Head);
#endif
    }

    private static bool HasTrackedHeadPose(XRNode node)
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);
        if (devices.Count == 0) return false;

        InputDevice device = devices[0];
        return device.isValid &&
               (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 _) ||
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion _));
    }
}

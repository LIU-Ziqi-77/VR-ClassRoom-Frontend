using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Editor-friendly demo camera.  Only moves/rotates while the RIGHT mouse
/// button is held, so keyboard shortcuts (Q/W/E/…) still trigger behaviors.
///
/// Controls (while holding right mouse button):
///   Mouse     Look around
///   WASD      Move horizontally
///   Q / E     Move down / up
///   Shift     Move faster
///
/// When right mouse button is released, the cursor is free for UI clicks
/// and keyboard goes back to behavior shortcuts.
/// </summary>
public class DemoCameraController : MonoBehaviour
{
    [Header("Speed")]
    public float moveSpeed = 4f;
    public float lookSpeed = 3f;
    public float boostMultiplier = 2.5f;
    public float shortcutSuppressionAfterControl = 0.2f;

    public static bool IsCameraControlActive { get; private set; }
    public static float LastCameraControlTime { get; private set; } = -999f;

    float _yaw, _pitch;

    void Awake()
    {
        DisableDesktopPoseDrivers();
    }

    void Start()
    {
        ResetLookFromCurrentTransform();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResetLookFromCurrentTransform()
    {
        Vector3 euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
        if (_pitch > 180f) _pitch -= 360f;
    }

    void OnDisable()
    {
        if (IsCameraControlActive)
        {
            IsCameraControlActive = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void Update()
    {
        if (DesktopInputBridge.GetMouseButtonDown(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (DesktopInputBridge.GetMouseButtonUp(1))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void LateUpdate()
    {
        IsCameraControlActive = DesktopInputBridge.GetMouseButton(1);

        if (!IsCameraControlActive) return;
        LastCameraControlTime = Time.unscaledTime;

        // Look
        Vector2 mouseDelta = DesktopInputBridge.GetMouseDelta();
        _yaw += mouseDelta.x * lookSpeed;
        _pitch -= mouseDelta.y * lookSpeed;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);

        // Move
        float speed = moveSpeed * (DesktopInputBridge.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);
        Vector3 move = Vector3.zero;
        if (DesktopInputBridge.GetKey(KeyCode.W)) move += transform.forward;
        if (DesktopInputBridge.GetKey(KeyCode.S)) move -= transform.forward;
        if (DesktopInputBridge.GetKey(KeyCode.A)) move -= transform.right;
        if (DesktopInputBridge.GetKey(KeyCode.D)) move += transform.right;
        if (DesktopInputBridge.GetKey(KeyCode.E)) move += Vector3.up;
        if (DesktopInputBridge.GetKey(KeyCode.Q)) move -= Vector3.up;

        transform.position += move.normalized * speed * Time.deltaTime;
    }

    public bool IsSuppressingBehaviorShortcuts()
    {
        return IsCameraControlActive ||
               Time.unscaledTime - LastCameraControlTime <= shortcutSuppressionAfterControl;
    }

    public static bool AnyCameraSuppressesBehaviorShortcuts(float fallbackGraceSeconds = 0.2f)
    {
        return IsCameraControlActive ||
               Time.unscaledTime - LastCameraControlTime <= fallbackGraceSeconds;
    }

    private void DisableDesktopPoseDrivers()
    {
        if (HasTrackedHeadPose(XRNode.CenterEye) || HasTrackedHeadPose(XRNode.Head)) return;

        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
        {
            if (component == null || component == this) continue;

            string typeName = component.GetType().Name;
            if (typeName.Contains("TrackedPoseDriver"))
            {
                component.enabled = false;
            }
        }
    }

    private static bool HasTrackedHeadPose(XRNode node)
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);
        if (devices.Count == 0) return false;

        InputDevice device = devices[0];
        return device.isValid &&
               (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 _) ||
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion _));
    }
}

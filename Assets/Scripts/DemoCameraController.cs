using UnityEngine;

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

    float _yaw, _pitch;

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

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (Input.GetMouseButtonUp(1))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (!Input.GetMouseButton(1)) return;

        // Look
        _yaw += Input.GetAxis("Mouse X") * lookSpeed;
        _pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
        _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);

        // Move
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        transform.position += move.normalized * speed * Time.deltaTime;
    }
}

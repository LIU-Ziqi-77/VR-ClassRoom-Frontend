using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class FreeWalkCamera : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float lookSpeed = 12.0f;
    public float boostMultiplier = 2.0f;
    public float gravity = 20.0f;
    public float jumpHeight = 2.0f;

    private CharacterController characterController;
    private readonly List<InputDevice> xrDevices = new List<InputDevice>();
    private float rotationX = 0.0f;
    private float rotationY = 0.0f;
    private Vector3 moveDirection = Vector3.zero;
    private bool isGrounded;

    void Awake()
    {
        if (ShouldUseXrHeadTracking())
        {
            return;
        }

        // 在Awake中添加CharacterController组件，确保它在Start和Update之前初始化
        if (GetComponent<CharacterController>() == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            // 设置适当的碰撞器参数
            characterController.height = 2.0f;
            characterController.radius = 0.3f;
            characterController.stepOffset = 0.3f;
        }
        else
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    [Tooltip("Set to false for demo mode — keeps cursor visible and unlocked")]
    public bool lockCursor = true;

    void Start()
    {
        if (ShouldUseXrHeadTracking())
        {
            return;
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

	void Update()
	{
        if (ShouldUseXrHeadTracking())
        {
            ApplyXrHeadPose();
            return;
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null) return;
        }

	    // 先做射线检测
	    float rayDist = characterController.height * 0.5f + 0.1f; 
    bool hitGround = Physics.Raycast(transform.position, Vector3.down, rayDist);
    isGrounded = hitGround;

    // 确保在地面时给一个微小下压力，保持贴地
    if (isGrounded && moveDirection.y < 0)
        moveDirection.y = -1f;

    // 水平输入不再依赖 isGrounded
    Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
    input = transform.TransformDirection(input);
    float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1);
    moveDirection.x = input.x * speed;
    moveDirection.z = input.z * speed;

    // 跳跃保持不变
    if (isGrounded && Input.GetButton("Jump"))
        moveDirection.y = Mathf.Sqrt(jumpHeight * 2f * gravity);

    // 重力照常
    moveDirection.y -= gravity * Time.deltaTime;

	    characterController.Move(moveDirection * Time.deltaTime);
	}

    private bool ApplyXrHeadPose()
    {
        return TryApplyXrNodePose(XRNode.CenterEye) || TryApplyXrNodePose(XRNode.Head);
    }

    private bool TryApplyXrNodePose(XRNode node)
    {
        xrDevices.Clear();
        InputDevices.GetDevicesAtXRNode(node, xrDevices);
        if (xrDevices.Count == 0) return false;

        InputDevice device = xrDevices[0];
        if (!device.isValid) return false;

        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);
        if (!hasPosition && !hasRotation) return false;

        if (hasPosition) transform.localPosition = localPosition;
        if (hasRotation) transform.localRotation = localRotation;
        return true;
    }

    private static bool ShouldUseXrHeadTracking()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return HasTrackedHeadPose(XRNode.CenterEye) || HasTrackedHeadPose(XRNode.Head);
#endif
    }

    private static bool HasTrackedHeadPose(XRNode node)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);
        if (devices.Count == 0) return false;

        InputDevice device = devices[0];
        return device.isValid &&
               (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 _) ||
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion _));
    }
}  

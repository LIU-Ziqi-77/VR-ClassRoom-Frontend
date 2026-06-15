using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using System.Collections.Generic;
using VRM;

/// <summary>
/// One-click scene setup for the behavior demo.
/// Add to an empty GameObject in "University Classroom.unity", then hit Play.
///
/// Setup flow:
///   0. Replace flower-head avatars with clones of good-looking avatars
///   1. Find all VRM avatars via VRMBlendShapeProxy
///   2. Add ProceduralBehaviorAnimator + FallbackSpeechService + StudentBehaviorVisuals to each
///   3. Create BehaviorDemoController for keyboard/GUI input
///   4. Disable old conflicting scripts
///   5a. Desktop mode: add DemoCameraController to Camera.main (right-mouse-drag + WASD)
///   5b. VR mode: add XRClassroomLocomotion to XR Origin (joystick locomotion + snap turn)
/// </summary>
public class BehaviorDemoSetup : MonoBehaviour
{
    static readonly Vector3 DesktopFallbackCameraPosition = new Vector3(468.951f, 1.047f, 189.15f);
    static readonly Quaternion DesktopFallbackCameraRotation = Quaternion.Euler(0f, 160f, 0f);
    static readonly Vector3 DesktopHeadLocalPosition = new Vector3(0f, 1.59f, 0f);

    [Header("Options")]
    public bool includeInactive = false;
    public bool disableOldTestControllers = true;
    public bool randomizePitch = true;
    public AnimationClip clappingClip;
    public AnimationClip leaveSeatLayingClip;
    public AnimationClip leaveSeatGettingUpClip;
    [Tooltip("Fallback runtime names if a student is not one of the Ele_student prefabs.")]
    public string[] studentDisplayNames = { "可可", "李奥", "安娜" };

    [Header("Avatar Appearance")]
    [Tooltip("Replace flower-head avatars with clones of good-looking ones at runtime")]
    public bool replaceFlowerAvatars = true;
    [Tooltip("Names of avatars that have flower/non-human accessories")]
    public string[] badAvatarNames = { "avatar_man2", "avatar_woman1" };
    [Tooltip("Names of good-looking avatars to clone from (same order as above)")]
    public string[] goodDonorNames = { "avatar_man1", "avatar_woman2" };

    void Start()
    {
        Debug.Log("[BehaviorDemoSetup] ====== SETUP START ======");
        SetupScene();
    }

    [ContextMenu("Run Setup Now")]
    public void SetupScene()
    {
        // Step 0: Replace flower-head avatars
        if (replaceFlowerAvatars)
            ReplaceFlowerAvatars();

        // Step 1: Find VRM avatars
        var students = new List<ProceduralBehaviorAnimator>();
        var proxies = new List<VRMBlendShapeProxy>(FindObjectsOfType<VRMBlendShapeProxy>(includeInactive));
        proxies.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
        Debug.Log($"[BehaviorDemoSetup] Step 1: Found {proxies.Count} VRMBlendShapeProxy in scene");

        if (proxies.Count == 0)
        {
            Debug.LogError("[BehaviorDemoSetup] FATAL: No VRMBlendShapeProxy found!");
            return;
        }

        // Step 2: Configure each avatar
        for (int idx = 0; idx < proxies.Count; idx++)
        {
            var proxy = proxies[idx];
            if (proxy == null) continue;
            GameObject go = proxy.gameObject;
            string originalName = go.name;
            Debug.Log($"[BehaviorDemoSetup] ── Configuring [{idx}]: {originalName} ──");

            Animator humanoidAnim = FindHumanoidAnimator(go);
            if (humanoidAnim == null)
            {
                Debug.LogError($"[BehaviorDemoSetup]   {go.name}: NO Animator. Skipping.");
                continue;
            }
            Debug.Log($"[BehaviorDemoSetup]   Animator isHuman={humanoidAnim.isHuman}");

            var pba = go.GetComponent<ProceduralBehaviorAnimator>();
            if (pba == null)
                pba = go.AddComponent<ProceduralBehaviorAnimator>();
            pba.animator = humanoidAnim;

            var audio = go.GetComponent<AudioSource>();
            if (audio == null)
            {
                audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
            }

            var fss = go.GetComponent<FallbackSpeechService>();
            if (fss == null)
                fss = go.AddComponent<FallbackSpeechService>();
            fss.blendShapeProxy = proxy;
            fss.proceduralAnimator = pba;
            fss.audioSource = audio;

            if (randomizePitch)
                fss.baseFrequency = Random.Range(140f, 260f);

            string displayName = GetStudentDisplayName(originalName, students.Count);
            go.name = displayName;

            // Visual overlay — overhead label + selection indicator
            var visuals = go.GetComponent<StudentBehaviorVisuals>();
            if (visuals == null)
                visuals = go.AddComponent<StudentBehaviorVisuals>();
            visuals.displayName = displayName;

            students.Add(pba);
            Debug.Log($"[BehaviorDemoSetup]   ✓ {displayName} ready");
        }

        // Step 3: BehaviorDemoController
        var demo = FindObjectOfType<BehaviorDemoController>();
        if (demo == null)
            demo = gameObject.AddComponent<BehaviorDemoController>();
        demo.students = students;
        if (clappingClip != null)
        {
            demo.clappingClip = clappingClip;
        }
        demo.leaveSeatLayingClip = leaveSeatLayingClip != null
            ? leaveSeatLayingClip
            : LoadResourceAnimationClip("DTTAnimationClips/Laying Shaking Head");
        demo.leaveSeatGettingUpClip = leaveSeatGettingUpClip != null
            ? leaveSeatGettingUpClip
            : LoadResourceAnimationClip("DTTAnimationClips/Getting Up");
        Debug.Log($"[BehaviorDemoSetup] Step 3: {students.Count} students assigned to controller");

        // Step 4: Disable old controllers
        if (disableOldTestControllers)
        {
            DisableIfExists<EditorTestController>();
            DisableIfExists<SimpleTestController>();
            DisableIfExists<QuickTestSetup>();
            DisableIfExists<StudentTestController>();
        }

        // Step 5: Camera setup - replace walk/fly cameras with demo camera
        SetupDemoCamera();

        Debug.Log($"[BehaviorDemoSetup] ====== SETUP COMPLETE: {students.Count} student(s) ready ======");
    }

    // ─── Avatar Replacement ──────────────────────────────────

    void ReplaceFlowerAvatars()
    {
        Debug.Log("[BehaviorDemoSetup] Step 0: Replacing flower-head avatars...");

        if (badAvatarNames.Length != goodDonorNames.Length)
        {
            Debug.LogError("[BehaviorDemoSetup] badAvatarNames and goodDonorNames must have the same length!");
            return;
        }

        // Build lookup of all current VRM avatars by name
        var allProxies = FindObjectsOfType<VRMBlendShapeProxy>(includeInactive);
        var byName = new Dictionary<string, GameObject>();
        foreach (var p in allProxies)
        {
            string key = p.gameObject.name.ToLower();
            byName[key] = p.gameObject;
        }

        for (int i = 0; i < badAvatarNames.Length; i++)
        {
            string badKey = badAvatarNames[i].ToLower();
            string goodKey = goodDonorNames[i].ToLower();

            if (!byName.TryGetValue(badKey, out GameObject badGO))
            {
                Debug.LogWarning($"[BehaviorDemoSetup]   Bad avatar '{badAvatarNames[i]}' not found in scene. Skipping.");
                continue;
            }
            if (!byName.TryGetValue(goodKey, out GameObject goodGO))
            {
                Debug.LogWarning($"[BehaviorDemoSetup]   Donor avatar '{goodDonorNames[i]}' not found in scene. Skipping.");
                continue;
            }

            // Clone the good avatar
            Vector3 pos = badGO.transform.position;
            Quaternion rot = badGO.transform.rotation;
            Vector3 scale = badGO.transform.localScale;
            Transform parent = badGO.transform.parent;

            GameObject clone = Instantiate(goodGO, pos, rot, parent);
            clone.transform.localScale = scale;
            clone.name = badAvatarNames[i]; // keep the original name for consistency

            // Deactivate the flower-head original
            badGO.SetActive(false);
            badGO.name = badGO.name + "_disabled";

            Debug.Log($"[BehaviorDemoSetup]   Replaced '{badAvatarNames[i]}' with clone of '{goodDonorNames[i]}' at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
        }
    }

    // ─── Camera / Locomotion ─────────────────────────────────

    void SetupDemoCamera()
    {
        // Always disable the old walk/fly cameras that lock the cursor
        foreach (var cam in FindObjectsOfType<FreeWalkCamera>())
        {
            cam.lockCursor = false;
            cam.enabled = false;
            Debug.Log($"[BehaviorDemoSetup] Disabled FreeWalkCamera on '{cam.gameObject.name}'");
        }
        foreach (var cam in FindObjectsOfType<FreeFlyCamera>())
        {
            cam.lockCursor = false;
            cam.enabled = false;
            Debug.Log($"[BehaviorDemoSetup] Disabled FreeFlyCamera on '{cam.gameObject.name}'");
        }

        bool xrActive = ShouldUseXrMode();

        if (xrActive)
        {
            SetupXRLocomotion();
        }
        else
        {
            SetupDesktopCamera();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[BehaviorDemoSetup] Cursor unlocked and visible");
    }

    /// Desktop demo camera: right-click to look + WASD to move.
    void SetupDesktopCamera()
    {
        var mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[BehaviorDemoSetup] No Camera.main found for desktop camera setup.");
            return;
        }

        // Keep the camera under XR Origin in desktop mode. That makes the editor
        // start from the same teacher spawn point used by the Quest build.
        GameObject xrOriginGO = FindXROrigin();
        bool cameraInsideXrOrigin = xrOriginGO != null && IsInsideXROrigin(mainCam.transform);
        if (xrOriginGO != null)
        {
            xrOriginGO.SetActive(true);
        }

        DisableTrackingComponents(mainCam);

        if (!cameraInsideXrOrigin && xrOriginGO != null)
        {
            ParentCameraToXrOrigin(mainCam, xrOriginGO);
            cameraInsideXrOrigin = true;
        }

        if (cameraInsideXrOrigin)
        {
            NormalizeDesktopCameraLocalPose(mainCam);
            Debug.Log("[BehaviorDemoSetup] Desktop camera kept on XR Origin spawn.");
        }
        else
        {
            Pose desktopStartPose = GetDesktopCameraStartPose();
            mainCam.transform.SetPositionAndRotation(desktopStartPose.position, desktopStartPose.rotation);
            Debug.Log("[BehaviorDemoSetup] No XR Origin found; using fallback desktop camera pose.");
        }

        var existing = mainCam.GetComponent<DemoCameraController>();
        if (existing == null)
        {
            existing = mainCam.gameObject.AddComponent<DemoCameraController>();
            Debug.Log($"[BehaviorDemoSetup] Added DemoCameraController to '{mainCam.gameObject.name}'");
        }

        existing.ResetLookFromCurrentTransform();

        // Remove any leftover CharacterController from old walk cameras
        var cc = mainCam.GetComponent<CharacterController>();
        if (cc != null)
        {
            Destroy(cc);
            Debug.Log("[BehaviorDemoSetup] Removed leftover CharacterController from camera");
        }

        Debug.Log("[BehaviorDemoSetup] Desktop camera ready. Right-click + WASD to move.");
    }

    static void DisableTrackingComponents(Camera mainCam)
    {
        foreach (var comp in mainCam.GetComponents<MonoBehaviour>())
        {
            if (comp == null) continue;

            string typeName = comp.GetType().Name;
            if (typeName.Contains("TrackedPoseDriver") || typeName.Contains("XRController"))
            {
                comp.enabled = false;
                Debug.Log($"[BehaviorDemoSetup] Disabled {typeName} on camera for desktop mode.");
            }
        }
    }

    static void ParentCameraToXrOrigin(Camera mainCam, GameObject xrOriginGO)
    {
        var origin = xrOriginGO.GetComponent<XROrigin>();
        Transform cameraParent = origin != null && origin.CameraFloorOffsetObject != null
            ? origin.CameraFloorOffsetObject.transform
            : xrOriginGO.transform.Find("Camera Offset");

        if (cameraParent == null)
        {
            cameraParent = xrOriginGO.transform;
        }

        mainCam.transform.SetParent(cameraParent, false);
        if (origin != null)
        {
            origin.Camera = mainCam;
        }
    }

    static void NormalizeDesktopCameraLocalPose(Camera mainCam)
    {
        Transform t = mainCam.transform;
        t.localPosition = DesktopHeadLocalPosition;
        t.localRotation = Quaternion.identity;
    }

    static Pose GetDesktopCameraStartPose()
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.gameObject.name == "High School Asset Camera (disabled)")
            {
                Transform t = camera.transform;
                return new Pose(t.position, t.rotation);
            }
        }

        return new Pose(DesktopFallbackCameraPosition, DesktopFallbackCameraRotation);
    }

    string GetStudentDisplayName(string originalName, int fallbackIndex)
    {
        if (!string.IsNullOrEmpty(originalName))
        {
            if (originalName.Contains("Ele_student1")) return "可可";
            if (originalName.Contains("Ele_student2")) return "李奥";
            if (originalName.Contains("Ele_student3")) return "安娜";
        }

        if (studentDisplayNames != null &&
            fallbackIndex >= 0 &&
            fallbackIndex < studentDisplayNames.Length &&
            !string.IsNullOrWhiteSpace(studentDisplayNames[fallbackIndex]))
        {
            return studentDisplayNames[fallbackIndex];
        }

        return originalName;
    }

    /// VR mode: add XRClassroomLocomotion to the XR Origin.
    void SetupXRLocomotion()
    {
        // Try to find the XR Origin by its common component type
        GameObject xrOrigin = FindXROrigin();
        if (xrOrigin == null)
        {
            Debug.LogWarning("[BehaviorDemoSetup] XR device active but no XR Origin found in scene. " +
                             "VR locomotion could not be configured automatically.");
            return;
        }

        var locomotion = xrOrigin.GetComponent<XRClassroomLocomotion>();
        if (locomotion == null)
            locomotion = xrOrigin.AddComponent<XRClassroomLocomotion>();

        Debug.Log($"[BehaviorDemoSetup] VR mode: XRClassroomLocomotion added to '{xrOrigin.name}'. " +
                  "Ensure Input Action References are assigned in Inspector.");
    }

    // ─── XR Helpers ──────────────────────────────────────────

    /// Returns true if the given transform is anywhere inside an XR Origin hierarchy.
    static bool IsInsideXROrigin(Transform t)
    {
        var xrOriginGO = FindXROrigin();
        if (xrOriginGO == null) return false;
        Transform check = t;
        while (check != null)
        {
            if (check.gameObject == xrOriginGO) return true;
            check = check.parent;
        }
        return false;
    }

    /// Finds the XR Origin GameObject using known component names (XRIT 3.x).
    static GameObject FindXROrigin()
    {
        var xrOriginComp = FindObjectOfType<XROrigin>();
        if (xrOriginComp != null) return xrOriginComp.gameObject;

        // Fallback: search by common GameObject name
        var go = GameObject.Find("XR Origin (VR)") ?? GameObject.Find("XR Origin") ?? GameObject.Find("XROrigin");
        return go;
    }

    static bool ShouldUseXrMode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return HasTrackedHeadPose(XRNode.CenterEye) || HasTrackedHeadPose(XRNode.Head);
#endif
    }

    static bool HasTrackedHeadPose(XRNode node)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);
        if (devices.Count == 0) return false;

        InputDevice device = devices[0];
        return device.isValid &&
               (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 _) ||
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion _));
    }

    // ─── Utilities ───────────────────────────────────────────

    static Animator FindHumanoidAnimator(GameObject go)
    {
        var animators = go.GetComponents<Animator>();
        foreach (var a in animators)
            if (a.isHuman) return a;
        return animators.Length > 0 ? animators[0] : null;
    }

    void DisableIfExists<T>() where T : MonoBehaviour
    {
        foreach (var inst in FindObjectsOfType<T>())
        {
            inst.enabled = false;
            Debug.Log($"[BehaviorDemoSetup] Disabled {typeof(T).Name} on '{inst.gameObject.name}'");
        }
    }

    static AnimationClip LoadResourceAnimationClip(string resourcePath)
    {
        AnimationClip[] clips = Resources.LoadAll<AnimationClip>(resourcePath);
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"[BehaviorDemoSetup] No AnimationClip found in Resources at '{resourcePath}'.");
            return null;
        }

        string fileName = resourcePath.Substring(resourcePath.LastIndexOf('/') + 1);
        foreach (AnimationClip clip in clips)
        {
            if (clip != null && clip.length > 0f && clip.name == fileName)
                return clip;
        }

        foreach (AnimationClip clip in clips)
        {
            if (clip != null && clip.length > 0f && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return clips[0];
    }
}

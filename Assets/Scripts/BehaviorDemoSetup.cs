using UnityEngine;
using System.Collections.Generic;
using VRM;

/// <summary>
/// One-click scene setup for the behavior demo.
/// Add to an empty GameObject in "University Classroom.unity", then hit Play.
///
/// Flow:
///   1. Finds all VRM avatars by VRMBlendShapeProxy component
///   2. Finds the correct Humanoid Animator on each (isHuman=true)
///   3. Adds ProceduralBehaviorAnimator + FallbackSpeechService + AudioSource
///   4. Creates BehaviorDemoController for keyboard/GUI input
///   5. Disables old conflicting test controllers
///   6. Scans for and reports Missing Script components on avatar GameObjects
///
/// Each VRM avatar root has TWO Animators:
///   • Prefab Animator  — has Humanoid Avatar (isHuman=true), no Controller
///   • Scene Animator   — has Avatar-Controller (Sit anim), no Humanoid Avatar
/// We use the Humanoid one for GetBoneTransform() in procedural animation.
/// </summary>
public class BehaviorDemoSetup : MonoBehaviour
{
    [Header("Options")]
    public bool includeInactive = false;
    public bool disableOldTestControllers = true;
    public bool randomizePitch = true;

    [Header("Avatar Appearance")]
    [Tooltip("Hide the head_Transparent mesh on specific avatars to remove flower/accessory visuals")]
    public bool hideFlowerAccessories = true;
    [Tooltip("Avatar names that should have their transparent head mesh hidden (e.g. flower-head avatars)")]
    public string[] avatarsToClean = { "avatar_man2", "avatar_woman2" };

    void Start()
    {
        Debug.Log("[BehaviorDemoSetup] ====== SETUP START ======");
        SetupScene();
    }

    [ContextMenu("Run Setup Now")]
    public void SetupScene()
    {
        var students = new List<ProceduralBehaviorAnimator>();

        // Step 1: Find VRM avatars
        var proxies = FindObjectsOfType<VRMBlendShapeProxy>(includeInactive);
        Debug.Log($"[BehaviorDemoSetup] Step 1: Found {proxies.Length} VRMBlendShapeProxy in scene");

        if (proxies.Length == 0)
        {
            Debug.LogError("[BehaviorDemoSetup] FATAL: No VRMBlendShapeProxy found! VRM avatars may not be in the scene, or VRM package failed to load.");
            ScanForMissingScriptsGlobal();
            return;
        }

        // Step 2: Configure each avatar
        for (int idx = 0; idx < proxies.Length; idx++)
        {
            var proxy = proxies[idx];
            if (proxy == null) continue;
            GameObject go = proxy.gameObject;
            Debug.Log($"[BehaviorDemoSetup] ── Configuring [{idx}]: {go.name} ──");

            ScanForMissingScripts(go);

            // Find Humanoid Animator
            Animator humanoidAnim = null;
            var animators = go.GetComponents<Animator>();
            Debug.Log($"[BehaviorDemoSetup]   Animators on root: {animators.Length}");
            for (int a = 0; a < animators.Length; a++)
            {
                var anim = animators[a];
                bool human = anim.isHuman;
                bool hasCtrl = anim.runtimeAnimatorController != null;
                string avatarName = anim.avatar != null ? anim.avatar.name : "null";
                Debug.Log($"[BehaviorDemoSetup]   Animator[{a}]: isHuman={human} hasController={hasCtrl} avatar={avatarName}");
                if (human && humanoidAnim == null)
                    humanoidAnim = anim;
            }

            if (humanoidAnim == null && animators.Length > 0)
            {
                humanoidAnim = animators[0];
                Debug.LogWarning($"[BehaviorDemoSetup]   No humanoid Animator found — falling back to Animator[0] (isHuman={humanoidAnim.isHuman})");
            }
            if (humanoidAnim == null)
            {
                Debug.LogError($"[BehaviorDemoSetup]   {go.name}: NO Animator at all. Skipping.");
                continue;
            }

            // Add ProceduralBehaviorAnimator
            var pba = go.GetComponent<ProceduralBehaviorAnimator>();
            if (pba == null)
            {
                pba = go.AddComponent<ProceduralBehaviorAnimator>();
                Debug.Log($"[BehaviorDemoSetup]   Added ProceduralBehaviorAnimator");
            }
            else
            {
                Debug.Log($"[BehaviorDemoSetup]   ProceduralBehaviorAnimator already present");
            }
            pba.animator = humanoidAnim;

            // Add AudioSource (before FallbackSpeechService, which needs it)
            var audio = go.GetComponent<AudioSource>();
            if (audio == null)
            {
                audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                Debug.Log($"[BehaviorDemoSetup]   Added AudioSource");
            }

            // Add FallbackSpeechService
            var fss = go.GetComponent<FallbackSpeechService>();
            if (fss == null)
            {
                fss = go.AddComponent<FallbackSpeechService>();
                Debug.Log($"[BehaviorDemoSetup]   Added FallbackSpeechService");
            }
            fss.blendShapeProxy = proxy;
            fss.proceduralAnimator = pba;
            fss.audioSource = audio;

            if (randomizePitch)
                fss.baseFrequency = Random.Range(140f, 260f);

            students.Add(pba);
            Debug.Log($"[BehaviorDemoSetup]   ✓ {go.name} fully configured");
        }

        // Step 3: Create/find BehaviorDemoController
        var demo = FindObjectOfType<BehaviorDemoController>();
        if (demo == null)
        {
            demo = gameObject.AddComponent<BehaviorDemoController>();
            Debug.Log($"[BehaviorDemoSetup] Step 3: Created BehaviorDemoController on '{gameObject.name}'");
        }
        else
        {
            Debug.Log($"[BehaviorDemoSetup] Step 3: Found existing BehaviorDemoController on '{demo.gameObject.name}'");
        }
        demo.students = students;
        Debug.Log($"[BehaviorDemoSetup] Assigned {students.Count} students to BehaviorDemoController");

        // Step 4: Disable old test controllers
        if (disableOldTestControllers)
        {
            DisableIfExists<EditorTestController>();
            DisableIfExists<SimpleTestController>();
            DisableIfExists<QuickTestSetup>();
            DisableIfExists<StudentTestController>();
        }

        // Step 5: Clean up avatar appearance (hide flower/accessory meshes)
        if (hideFlowerAccessories)
        {
            foreach (var pba in students)
                CleanAvatarAppearance(pba.gameObject);
        }

        // Step 6: Unlock cursor for demo usability
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("[BehaviorDemoSetup] Step 5: Cursor unlocked and visible");

        // Disable FreeWalkCamera / FreeFlyCamera to prevent re-locking
        DisableCursorLockers();

        Debug.Log($"[BehaviorDemoSetup] ====== SETUP COMPLETE: {students.Count} student(s) ready ======");
    }

    void CleanAvatarAppearance(GameObject avatarGO)
    {
        string name = avatarGO.name.ToLower();
        bool shouldClean = false;
        foreach (var pattern in avatarsToClean)
        {
            if (name.Contains(pattern.ToLower()))
            {
                shouldClean = true;
                break;
            }
        }
        if (!shouldClean) return;

        // The "flower" or unusual accessory visuals are typically baked into
        // head_Transparent_Material_Meshes_Mesh.  Disabling this renderer
        // removes the transparent accessory while preserving the opaque face/head.
        var renderers = avatarGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in renderers)
        {
            if (r.gameObject.name.Contains("head_Transparent"))
            {
                r.enabled = false;
                Debug.Log($"[BehaviorDemoSetup] Hidden '{r.gameObject.name}' on {avatarGO.name} (flower/accessory cleanup)");
            }
        }
    }

    void DisableCursorLockers()
    {
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
    }

    void ScanForMissingScripts(GameObject go)
    {
        var components = go.GetComponents<Component>();
        int missing = 0;
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                missing++;
            }
        }
        if (missing > 0)
            Debug.LogWarning($"[BehaviorDemoSetup]   ⚠ {go.name} has {missing} Missing Script component(s). These are harmless orphan references but may produce console warnings.");

        // Also scan immediate children (VRM avatars have child objects with springs etc.)
        for (int c = 0; c < go.transform.childCount; c++)
        {
            var child = go.transform.GetChild(c);
            var childComps = child.GetComponents<Component>();
            int childMissing = 0;
            for (int i = 0; i < childComps.Length; i++)
            {
                if (childComps[i] == null) childMissing++;
            }
            if (childMissing > 0)
                Debug.LogWarning($"[BehaviorDemoSetup]   ⚠ {go.name}/{child.name} has {childMissing} Missing Script(s)");
        }
    }

    void ScanForMissingScriptsGlobal()
    {
        Debug.Log("[BehaviorDemoSetup] Scanning all root GameObjects for missing scripts...");
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            var comps = root.GetComponents<Component>();
            int missing = 0;
            for (int i = 0; i < comps.Length; i++)
                if (comps[i] == null) missing++;
            if (missing > 0)
                Debug.LogWarning($"[BehaviorDemoSetup] Root GO '{root.name}' has {missing} Missing Script(s)");
        }
    }

    void DisableIfExists<T>() where T : MonoBehaviour
    {
        foreach (var inst in FindObjectsOfType<T>())
        {
            inst.enabled = false;
            Debug.Log($"[BehaviorDemoSetup] Disabled {typeof(T).Name} on '{inst.gameObject.name}'");
        }
    }
}

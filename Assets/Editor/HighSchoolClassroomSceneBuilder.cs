using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HighSchoolClassroomSceneBuilder
{
    const string SourceScenePath = "Assets/High school classroom/Demo.unity";
    const string TargetScenePath = "Assets/Scenes/HighSchoolClassroom_Demo.unity";
    const string MixamoSittingIdleFbxPath = "Assets/Animation/Mixamo/Sitting Idle.fbx";
    const string MixamoSittingIdleControllerPath = "Assets/Animation/Ele_student_MixamoSittingIdle.controller";
    const string FallbackStudentAnimatorControllerPath = "Assets/Animation/Ele_student.controller";

    static readonly Vector3 StudentGroupDeskCenter = new Vector3(468.05f, 0.35f, 188.33f);

    static readonly string[] AvatarPrefabPaths =
    {
        "Assets/Elementary_Students/Ele_student1.prefab",
        "Assets/Elementary_Students/Ele_student2.prefab",
        "Assets/Elementary_Students/Ele_student3.prefab",
    };

    [MenuItem("Tools/VR Classroom/Build High School Classroom Scene")]
    public static void BuildScene()
    {
        BuildSceneInternal(false);
    }

    public static void BuildSceneBatch()
    {
        try
        {
            BuildSceneInternal(true);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void BuildSceneInternal(bool exitOnComplete)
    {
        if (!File.Exists(SourceScenePath))
        {
            throw new FileNotFoundException("High school classroom source scene was not found.", SourceScenePath);
        }

        EnsureFolder("Assets", "Scenes");

        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

        DisableImportedDemoCamera();
        GameObject xrOrigin = EnsureXrOrigin();
        Camera mainCamera = EnsureMainCamera(xrOrigin);
        ConfigureCameraForClassroom(mainCamera);

        bool waitingForVrmImport = EnsureElementaryStudentPrefabs();
        if (waitingForVrmImport)
        {
            Debug.Log("Waiting for UniVRM delayed prefab creation before placing students.");
            EditorApplication.delayCall += () => FinishSceneBuild(scene, exitOnComplete);
            return;
        }

        FinishSceneBuild(scene, exitOnComplete);
    }

    static void FinishSceneBuild(Scene scene, bool exitOnComplete)
    {
        AssetDatabase.Refresh();
        ConvertElementaryStudentMaterialsToUniUnlit();
        EnsureMixamoSittingIdleController();
        List<GameObject> students = PlaceStudentAvatars();
        EnsureBehaviorDemoSetup();
        EnsurePptProjector();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TargetScenePath);
        ConfigureBuildSettings(TargetScenePath);

        Debug.Log($"High school classroom integration scene built at {TargetScenePath}. Students placed: {students.Count}");

        if (exitOnComplete)
        {
            EditorApplication.Exit(0);
        }
    }

    static void EnsureFolder(string parent, string folder)
    {
        string path = $"{parent}/{folder}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    static void DisableImportedDemoCamera()
    {
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            GameObject go = camera.gameObject;
            go.tag = "Untagged";

            if (go.name == "Camera")
            {
                go.name = "High School Asset Camera (disabled)";
                go.SetActive(false);
            }
        }
    }

    static GameObject EnsureXrOrigin()
    {
        var existing = Object.FindFirstObjectByType<XROrigin>();
        if (existing != null)
        {
            existing.gameObject.name = "XR Origin (VR)";
            return existing.gameObject;
        }

        GameObject xrOrigin = new GameObject("XR Origin (VR)");
        xrOrigin.transform.SetPositionAndRotation(new Vector3(470.2f, 0.05f, 186.8f), Quaternion.Euler(0f, 0f, 0f));
        xrOrigin.AddComponent<XROrigin>();

        GameObject cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(xrOrigin.transform, false);
        cameraOffset.transform.localPosition = Vector3.zero;

        var origin = xrOrigin.GetComponent<XROrigin>();
        origin.CameraFloorOffsetObject = cameraOffset;

        return xrOrigin;
    }

    static Camera EnsureMainCamera(GameObject xrOrigin)
    {
        Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
        if (cameraOffset == null)
        {
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(xrOrigin.transform, false);
            cameraOffset = offset.transform;
        }

        Camera camera = Camera.main;
        if (camera == null || camera.gameObject.name.StartsWith("High School Asset Camera"))
        {
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(cameraOffset, false);
            camera = cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            cameraGo.tag = "MainCamera";
        }
        else
        {
            camera.gameObject.name = "Main Camera";
            camera.gameObject.tag = "MainCamera";
            camera.transform.SetParent(cameraOffset, false);
        }

        var origin = xrOrigin.GetComponent<XROrigin>();
        origin.Camera = camera;

        return camera;
    }

    static void ConfigureCameraForClassroom(Camera camera)
    {
        camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        camera.transform.localRotation = Quaternion.identity;
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 1000f;

        if (camera.GetComponent<DemoCameraController>() == null)
        {
            camera.gameObject.AddComponent<DemoCameraController>();
        }
    }

    static List<GameObject> PlaceStudentAvatars()
    {
        Transform parent = GetOrCreateRoot("VR Classroom Students").transform;
        List<Pose> seats = GetStudentSeatPoses();
        List<GameObject> students = new List<GameObject>();

        for (int i = 0; i < AvatarPrefabPaths.Length && i < seats.Count; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPaths[i]);
            if (prefab == null)
            {
                Debug.LogWarning($"Avatar prefab not found: {AvatarPrefabPaths[i]}");
                continue;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) continue;

            instance.name = GetStudentName(i);
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(seats[i].position, seats[i].rotation);
            instance.transform.localScale = Vector3.one;
            ConfigureSeatedStudentAnimator(instance);
            students.Add(instance);
        }

        return students;
    }

    static List<Pose> GetStudentSeatPoses()
    {
        return new List<Pose>
        {
            new Pose(new Vector3(469.195f, 0.167f, 187.906f), Quaternion.Euler(0f, 279.54f, 0f)),
            new Pose(new Vector3(468.26f, 0.167f, 187.15f), Quaternion.Euler(0f, 99.54f, 0f)),
            new Pose(new Vector3(468.978f, 0.167f, 187.04f), Quaternion.Euler(0f, 279.54f, 0f)),
        };
    }

    static Quaternion FacePointOnY(Vector3 position, Vector3 target)
    {
        Vector3 direction = target - position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    static void ConfigureSeatedStudentAnimator(GameObject student)
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MixamoSittingIdleControllerPath);
        if (controller == null)
        {
            controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackStudentAnimatorControllerPath);
        }

        if (controller == null)
        {
            Debug.LogWarning("No seated student animator controller was found.");
            return;
        }

        Animator animator = student.GetComponent<Animator>();
        if (animator == null)
        {
            animator = student.GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning($"No Animator found on {student.name}. Sit controller was not assigned.");
            return;
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
    }

    static void EnsureMixamoSittingIdleController()
    {
        if (!File.Exists(MixamoSittingIdleFbxPath))
        {
            Debug.LogWarning($"Mixamo sitting idle FBX not found: {MixamoSittingIdleFbxPath}. Falling back to {FallbackStudentAnimatorControllerPath}.");
            return;
        }

        ConfigureMixamoModelImporter();

        AnimationClip clip = AssetDatabase.LoadAllAssetRepresentationsAtPath(MixamoSittingIdleFbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase));

        if (clip == null)
        {
            Debug.LogWarning($"No animation clip found in {MixamoSittingIdleFbxPath}. Falling back to {FallbackStudentAnimatorControllerPath}.");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MixamoSittingIdleControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(MixamoSittingIdleControllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(s => s.name == "Sitting Idle");

        if (state == null)
        {
            state = stateMachine.AddState("Sitting Idle");
        }

        state.motion = clip;
        state.speed = 1f;
        state.iKOnFeet = true;
        stateMachine.defaultState = state;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void ConfigureMixamoModelImporter()
    {
        var importer = AssetImporter.GetAtPath(MixamoSittingIdleFbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"ModelImporter not found for {MixamoSittingIdleFbxPath}.");
            return;
        }

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = "Sitting Idle";
                clips[i].loopTime = true;
                clips[i].loopPose = true;
                clips[i].lockRootRotation = true;
                clips[i].keepOriginalOrientation = false;
                clips[i].rotationOffset = 0f;
                clips[i].lockRootHeightY = true;
                clips[i].keepOriginalPositionY = false;
                clips[i].heightFromFeet = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalPositionXZ = false;
            }

            importer.clipAnimations = clips;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    static string GetStudentName(int index)
    {
        switch (index)
        {
            case 0: return "Ele_student1";
            case 1: return "Ele_student2";
            case 2: return "Ele_student3";
            default: return $"student_{index + 1}";
        }
    }

    static GameObject GetOrCreateRoot(string name)
    {
        GameObject existing = GameObject.Find(name);
        return existing != null ? existing : new GameObject(name);
    }

    static void EnsureBehaviorDemoSetup()
    {
        var existing = Object.FindFirstObjectByType<BehaviorDemoSetup>();
        GameObject go = existing != null ? existing.gameObject : new GameObject("BehaviorDemoSetup");
        var setup = go.GetComponent<BehaviorDemoSetup>();
        if (setup == null) setup = go.AddComponent<BehaviorDemoSetup>();

        setup.includeInactive = false;
        setup.disableOldTestControllers = true;
        setup.randomizePitch = true;
        setup.replaceFlowerAvatars = false;
        setup.badAvatarNames = System.Array.Empty<string>();
        setup.goodDonorNames = System.Array.Empty<string>();
    }

    static bool EnsureElementaryStudentPrefabs()
    {
        bool importTriggered = false;

        foreach (string prefabPath in AvatarPrefabPaths)
        {
            if (File.Exists(prefabPath)) continue;

            string vrmAssetPath = Path.ChangeExtension(prefabPath, ".vrm");
            if (!File.Exists(vrmAssetPath))
            {
                Debug.LogWarning($"Elementary student VRM not found: {vrmAssetPath}");
                continue;
            }

            ImportVrmAndCreatePrefab(vrmAssetPath, prefabPath);
            importTriggered = true;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return importTriggered;
    }

    static void ImportVrmAndCreatePrefab(string vrmAssetPath, string prefabPath)
    {
        System.Type importerType = System.Type.GetType("VRM.vrmAssetPostprocessor, UniVRM.Editor");
        System.Type unityPathType = System.Type.GetType("UniGLTF.UnityPath, UniGLTF");

        if (importerType == null || unityPathType == null)
        {
            Debug.LogWarning("UniVRM editor importer types were not found. VRM prefab creation skipped.");
            return;
        }

        var fromFullpath = unityPathType.GetMethod("FromFullpath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var importMethod = importerType.GetMethod("ImportVrmAndCreatePrefab", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (fromFullpath == null || importMethod == null)
        {
            Debug.LogWarning("UniVRM editor importer methods were not found. VRM prefab creation skipped.");
            return;
        }

        object prefabUnityPath = fromFullpath.Invoke(null, new object[] { Path.GetFullPath(prefabPath) });
        importMethod.Invoke(null, new object[] { Path.GetFullPath(vrmAssetPath), prefabUnityPath });
    }

    static void EnsurePptProjector()
    {
        GameObject screen = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.ToLowerInvariant().Contains("projector screen") || t.name.ToLowerInvariant().Contains("blackboard"))
            .Select(t => t.gameObject)
            .FirstOrDefault(go => go.GetComponentInChildren<Renderer>() != null);

        if (screen == null)
        {
            Debug.LogWarning("No projector screen or blackboard renderer found for PPTProjector.");
            return;
        }

        Renderer renderer = screen.GetComponentInChildren<Renderer>();
        var projector = renderer.gameObject.GetComponent<PPTProjector>();
        if (projector == null) projector = renderer.gameObject.AddComponent<PPTProjector>();

        projector.targetRenderer = renderer;
        projector.projectionMaterial = null;
        projector.pptFolderPath = "PPTSlides";
        projector.preserveAspectRatio = true;
        projector.resizeScreen = true;
        projector.slideScale = 1f;
    }

    static void ConvertElementaryStudentMaterialsToUniUnlit()
    {
        Shader shader = Shader.Find("UniGLTF/UniUnlit");
        if (shader == null)
        {
            Debug.LogWarning("UniGLTF/UniUnlit shader was not found. Elementary student material conversion skipped.");
            return;
        }

        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[]
        {
            "Assets/Elementary_Students/Ele_student2.Materials",
            "Assets/Elementary_Students/Ele_student3.Materials",
        });

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;

            Texture mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;

            material.shader = shader;

            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", mainTexture);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", cutoff);
            if (material.HasProperty("_BlendMode")) material.SetFloat("_BlendMode", 1f);
            if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);

            material.renderQueue = 2450;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
    }

    static void ConfigureBuildSettings(string scenePath)
    {
        string guid = AssetDatabase.AssetPathToGUID(scenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(s => s.path == scenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));

        for (int i = 1; i < scenes.Count; i++)
        {
            if (scenes[i].path == "Assets/University Classroom/Scene/University Classroom.unity")
            {
                scenes[i].enabled = false;
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Build Settings updated. Primary scene guid: {guid}");
    }
}

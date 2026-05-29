#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.XR.CoreUtils;
using Unity.XR.Oculus;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.SpatialTracking;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

public static class Quest3InteractionSetup
{
    private const string TargetScenePath = "Assets/Scenes/HighSchoolClassroom_Demo.unity";

    [MenuItem("Tools/VR Classroom/Setup Quest 3 Interaction")]
    public static void SetupQuest3Interaction()
    {
        ConfigureQuestProjectSettings();
        ConfigureScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Quest3InteractionSetup] Quest 3 project and scene interaction setup complete.");
    }

    public static void SetupQuest3InteractionBatch()
    {
        try
        {
            SetupQuest3Interaction();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigureQuestProjectSettings()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

        ConfigureXrManagement();
        ConfigureOpenXrFeatures();
        ConfigureOculusSettings();
    }

    private static void ConfigureXrManagement()
    {
        XRGeneralSettings generalSettings =
            XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (generalSettings == null)
        {
            Debug.LogWarning("[Quest3InteractionSetup] Android XR General Settings not found.");
            return;
        }

        generalSettings.InitManagerOnStart = true;
        XRManagerSettings managerSettings = generalSettings.AssignedSettings;
        if (managerSettings == null)
        {
            Debug.LogWarning("[Quest3InteractionSetup] Android XR Manager Settings not found.");
            EditorUtility.SetDirty(generalSettings);
            return;
        }

        managerSettings.automaticLoading = true;
        managerSettings.automaticRunning = true;
        EditorUtility.SetDirty(generalSettings);
        EditorUtility.SetDirty(managerSettings);
    }

    private static void ConfigureOpenXrFeatures()
    {
        OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
        {
            Debug.LogWarning("[Quest3InteractionSetup] Android OpenXR settings not found.");
            return;
        }

        SetFeature(settings, typeof(MetaQuestFeature), true);
        SetFeature(settings, typeof(MetaQuestTouchPlusControllerProfile), true);
        SetFeature(settings, typeof(OculusTouchControllerProfile), true);

        DisableViveOpenXrFeatures(settings);
        DisableKnownViveYamlEntries(AssetDatabase.GetAssetPath(settings));

        EditorUtility.SetDirty(settings);
    }

    private static void DisableViveOpenXrFeatures(OpenXRSettings settings)
    {
        string settingsPath = AssetDatabase.GetAssetPath(settings);
        OpenXRFeature[] allFeatures = settings.GetFeatures()
            .Concat(AssetDatabase.LoadAllAssetsAtPath(settingsPath).OfType<OpenXRFeature>())
            .Distinct()
            .ToArray();

        foreach (OpenXRFeature feature in allFeatures)
        {
            SerializedObject serializedFeature = new SerializedObject(feature);
            string typeName = feature.GetType().FullName ?? string.Empty;
            string displayName = GetFeatureDisplayName(feature);
            string assetName = feature.name ?? string.Empty;
            string serializedName = GetSerializedString(serializedFeature, "m_Name");
            string serializedDisplayName = GetSerializedString(serializedFeature, "nameUi");
            string featureId = GetSerializedString(serializedFeature, "featureIdInternal");
            string company = GetSerializedString(serializedFeature, "company");
            bool isViveFeature =
                typeName.IndexOf("Vive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetName.IndexOf("Vive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetName.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                serializedName.IndexOf("Vive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                serializedName.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                serializedDisplayName.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                featureId.IndexOf("vive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                company.IndexOf("HTC", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isViveFeature) continue;

            feature.enabled = false;
            SerializedProperty openXrEnabled = serializedFeature.FindProperty("m_enabled");
            SerializedProperty unityEnabled = serializedFeature.FindProperty("m_Enabled");
            if (openXrEnabled != null) openXrEnabled.boolValue = false;
            if (unityEnabled != null) unityEnabled.boolValue = false;
            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }
    }

    private static string GetSerializedString(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.String
            ? property.stringValue
            : string.Empty;
    }

    private static void DisableKnownViveYamlEntries(string settingsPath)
    {
        if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath)) return;

        string yaml = File.ReadAllText(settingsPath);
        string updatedYaml = yaml;
        updatedYaml = DisableYamlFeatureById(updatedYaml, "com.unity.openxr.feature.vivefocus3");
        updatedYaml = DisableYamlFeatureById(updatedYaml, "vive.openxr.feature.enterprise.command");
        updatedYaml = DisableYamlFeatureById(updatedYaml, "vive.openxr.feature.focus3controller");

        if (updatedYaml == yaml) return;

        File.WriteAllText(settingsPath, updatedYaml);
        AssetDatabase.ImportAsset(settingsPath);
    }

    private static string DisableYamlFeatureById(string yaml, string featureId)
    {
        int featureIdIndex = yaml.IndexOf("featureIdInternal: " + featureId, StringComparison.Ordinal);
        if (featureIdIndex < 0) return yaml;

        int blockStart = yaml.LastIndexOf("--- !u!", featureIdIndex, StringComparison.Ordinal);
        int blockEnd = yaml.IndexOf("\n--- !u!", featureIdIndex, StringComparison.Ordinal);
        if (blockStart < 0) blockStart = 0;
        if (blockEnd < 0) blockEnd = yaml.Length;

        string block = yaml.Substring(blockStart, blockEnd - blockStart);
        string updatedBlock = Regex.Replace(block, @"(?m)^  m_enabled: 1$", "  m_enabled: 0", RegexOptions.None, TimeSpan.FromSeconds(1));
        return updatedBlock == block
            ? yaml
            : yaml.Substring(0, blockStart) + updatedBlock + yaml.Substring(blockEnd);
    }

    private static string GetFeatureDisplayName(OpenXRFeature feature)
    {
        var field = typeof(OpenXRFeature).GetField(
            "nameUi",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(feature) as string ?? string.Empty;
    }

    private static void SetFeature(OpenXRSettings settings, Type featureType, bool enabled)
    {
        OpenXRFeature feature = settings.GetFeature(featureType);
        if (feature == null)
        {
            Debug.LogWarning($"[Quest3InteractionSetup] OpenXR feature not found: {featureType.Name}");
            return;
        }

        feature.enabled = enabled;
        EditorUtility.SetDirty(feature);
    }

    private static void ConfigureOculusSettings()
    {
        OculusSettings settings = AssetDatabase.LoadAssetAtPath<OculusSettings>("Assets/XR/Settings/OculusSettings.asset");
        if (settings == null)
        {
            Debug.LogWarning("[Quest3InteractionSetup] Oculus settings asset not found.");
            return;
        }

        settings.TargetQuest2 = true;
        settings.TargetQuest3 = true;
        settings.TargetQuest3S = true;
        EditorUtility.SetDirty(settings);
    }

    private static void ConfigureScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        XROrigin origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
        if (origin == null)
        {
            throw new InvalidOperationException("No XROrigin found in the classroom scene.");
        }

        Transform cameraOffset = origin.CameraFloorOffsetObject != null
            ? origin.CameraFloorOffsetObject.transform
            : origin.transform;

        ConfigureXrCamera(origin);
        ConfigureQuestLocomotion(origin);
        EnsureInteractionManager();

        Transform leftAnchor = EnsureControllerAnchor(cameraOffset, "Left Controller Anchor", XRNode.LeftHand, false);
        Transform rightAnchor = EnsureControllerAnchor(cameraOffset, "Right Controller Anchor", XRNode.RightHand, true);

        ConfigureDttController(rightAnchor);
        ConfigureXriProbeInteractors(leftAnchor, rightAnchor);
        ConfigureTeachingAids();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureXrCamera(XROrigin origin)
    {
        Camera xrCamera = origin.Camera != null ? origin.Camera : Camera.main;
        if (xrCamera == null)
        {
            Debug.LogWarning("[Quest3InteractionSetup] No XR camera found for head tracking setup.");
            return;
        }

        TrackedPoseDriver poseDriver = xrCamera.GetComponent<TrackedPoseDriver>();
        if (poseDriver == null)
        {
            poseDriver = xrCamera.gameObject.AddComponent<TrackedPoseDriver>();
        }

        poseDriver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
        poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        poseDriver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

        EditorUtility.SetDirty(xrCamera.gameObject);
    }

    private static void ConfigureQuestLocomotion(XROrigin origin)
    {
        QuestThumbstickLocomotion locomotion = origin.GetComponent<QuestThumbstickLocomotion>();
        if (locomotion == null)
        {
            locomotion = origin.gameObject.AddComponent<QuestThumbstickLocomotion>();
        }

        locomotion.xrOrigin = origin;
        locomotion.moveHand = XRNode.LeftHand;
        locomotion.turnHand = XRNode.RightHand;
        locomotion.moveSpeed = 1.8f;
        locomotion.deadzone = 0.18f;
        locomotion.snapTurnDegrees = 45f;
        locomotion.snapTurnCooldown = 0.35f;
        EditorUtility.SetDirty(origin.gameObject);
    }

    private static void EnsureInteractionManager()
    {
        XRInteractionManager manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
        if (manager != null) return;

        GameObject go = new GameObject("XR Interaction Manager");
        go.AddComponent<XRInteractionManager>();
    }

    private static Transform EnsureControllerAnchor(Transform parent, string name, XRNode node, bool visibleRay)
    {
        Transform anchor = parent.Find(name);
        if (anchor == null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            anchor = go.transform;
        }

        anchor.localPosition = node == XRNode.RightHand
            ? new Vector3(0.24f, 1.2f, 0.35f)
            : new Vector3(-0.24f, 1.2f, 0.35f);
        anchor.localRotation = Quaternion.identity;

        QuestControllerRayVisual visual = anchor.GetComponent<QuestControllerRayVisual>();
        if (visual == null)
        {
            visual = anchor.gameObject.AddComponent<QuestControllerRayVisual>();
        }

        visual.controllerNode = node;
        visual.showRay = visibleRay;
        visual.showOnlyWhenControllerPose = true;
        visual.rayDistance = 12f;
        visual.raycastMask = ~0;
        visual.fallbackToCameraWhenNoController = true;
        visual.cameraLocalPosition = node == XRNode.RightHand
            ? new Vector3(0.18f, -0.22f, 0.45f)
            : new Vector3(-0.18f, -0.22f, 0.45f);

        return anchor;
    }

    private static void ConfigureDttController(Transform rightAnchor)
    {
        DTTTeacherInteractionController controller = UnityEngine.Object.FindFirstObjectByType<DTTTeacherInteractionController>();
        if (controller == null)
        {
            GameObject go = new GameObject("DTT Interaction Manager");
            controller = go.AddComponent<DTTTeacherInteractionController>();
        }

        DTTTeachingAidManager manager = UnityEngine.Object.FindFirstObjectByType<DTTTeachingAidManager>();
        if (manager == null)
        {
            manager = controller.gameObject.GetComponent<DTTTeachingAidManager>();
            if (manager == null)
            {
                manager = controller.gameObject.AddComponent<DTTTeachingAidManager>();
            }
        }

        controller.manager = manager;
        controller.rayOriginOverride = rightAnchor;
        controller.holdAnchorOverride = rightAnchor;
        controller.controllerNode = XRNode.RightHand;
        controller.useXRInput = true;
        controller.triggerSelects = true;
        controller.gripTogglesAidHold = true;
        controller.keyboardFallback = true;
        controller.drawDebugRay = true;
        controller.rayDistance = 12f;

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(manager);
    }

    private static void ConfigureXriProbeInteractors(Transform leftAnchor, Transform rightAnchor)
    {
        ConfigureInteractorPair(leftAnchor.gameObject, false);
        ConfigureInteractorPair(rightAnchor.gameObject, true);
    }

    private static void ConfigureInteractorPair(GameObject anchor, bool enableRay)
    {
        XRRayInteractor ray = anchor.GetComponent<XRRayInteractor>();
        if (ray == null)
        {
            ray = anchor.AddComponent<XRRayInteractor>();
        }

        ray.enabled = enableRay;
        ray.maxRaycastDistance = 12f;
        ray.raycastMask = ~0;
        ray.hitDetectionType = XRRayInteractor.HitDetectionType.Raycast;
        ray.lineType = XRRayInteractor.LineType.StraightLine;
        ray.enableUIInteraction = false;
        ray.allowHoveredActivate = false;
        ray.useForceGrab = true;

        Transform directTransform = anchor.transform.Find("Direct Interactor Probe");
        if (directTransform == null)
        {
            GameObject directGo = new GameObject("Direct Interactor Probe");
            directGo.transform.SetParent(anchor.transform, false);
            directTransform = directGo.transform;
        }

        XRDirectInteractor direct = directTransform.GetComponent<XRDirectInteractor>();
        if (direct == null)
        {
            direct = directTransform.gameObject.AddComponent<XRDirectInteractor>();
        }

        direct.enabled = false;

        SphereCollider sphere = directTransform.GetComponent<SphereCollider>();
        if (sphere == null)
        {
            sphere = directTransform.gameObject.AddComponent<SphereCollider>();
        }

        sphere.isTrigger = true;
        sphere.radius = 0.08f;

        EditorUtility.SetDirty(anchor);
        EditorUtility.SetDirty(directTransform.gameObject);
    }

    private static void ConfigureTeachingAids()
    {
        DTTTeachingAid[] aids = UnityEngine.Object.FindObjectsByType<DTTTeachingAid>(FindObjectsSortMode.None);
        foreach (DTTTeachingAid aid in aids)
        {
            if (aid == null) continue;

            if (aid.GetComponentInChildren<Collider>() == null)
            {
                BoxCollider box = aid.gameObject.AddComponent<BoxCollider>();
                box.size = Vector3.one * 0.1f;
            }

            Rigidbody body = aid.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = aid.gameObject.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (aid.GetComponent<XRGrabInteractable>() == null)
            {
                aid.gameObject.AddComponent<XRGrabInteractable>();
            }

            EditorUtility.SetDirty(aid.gameObject);
        }
    }
}
#endif

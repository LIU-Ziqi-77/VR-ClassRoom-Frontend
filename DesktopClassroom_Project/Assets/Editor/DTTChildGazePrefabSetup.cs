using UnityEditor;
using UnityEngine;
using VRM;

public static class DTTChildGazePrefabSetup
{
    private static readonly string[] StudentPrefabPaths =
    {
        "Assets/Elementary_Students/Ele_student1.prefab",
        "Assets/Elementary_Students/Ele_student2.prefab",
        "Assets/Elementary_Students/Ele_student3.prefab"
    };

    [MenuItem("Tools/DTT/Add Child Gaze Simulator To Student Prefabs")]
    public static void AddChildGazeSimulatorToStudentPrefabs()
    {
        int changedCount = 0;

        foreach (string prefabPath in StudentPrefabPaths)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"DTT gaze setup skipped. Prefab not found: {prefabPath}");
                continue;
            }

            try
            {
                bool changed = ConfigurePrefab(prefabRoot, prefabPath);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    changedCount++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"DTT gaze prefab setup complete. Changed prefabs: {changedCount}");
    }

    private static bool ConfigurePrefab(GameObject prefabRoot, string prefabPath)
    {
        bool changed = false;

        Animator animator = prefabRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = prefabRoot.GetComponentInChildren<Animator>(true);
        }

        VRMBlendShapeProxy blendShapeProxy = prefabRoot.GetComponent<VRMBlendShapeProxy>();
        if (blendShapeProxy == null)
        {
            blendShapeProxy = prefabRoot.GetComponentInChildren<VRMBlendShapeProxy>(true);
        }

        VRMLookAtHead vrmLookAtHead = prefabRoot.GetComponent<VRMLookAtHead>();
        if (vrmLookAtHead == null)
        {
            vrmLookAtHead = prefabRoot.GetComponentInChildren<VRMLookAtHead>(true);
        }

        EyeController eyeController = prefabRoot.GetComponent<EyeController>();
        if (eyeController == null)
        {
            eyeController = prefabRoot.AddComponent<EyeController>();
            changed = true;
        }

        if (eyeController.humanoidAnimator == null && animator != null)
        {
            eyeController.humanoidAnimator = animator;
            changed = true;
        }

        if (eyeController.blendShapeProxy == null && blendShapeProxy != null)
        {
            eyeController.blendShapeProxy = blendShapeProxy;
            changed = true;
        }

        if (eyeController.vrmLookAtHead == null && vrmLookAtHead != null)
        {
            eyeController.vrmLookAtHead = vrmLookAtHead;
            changed = true;
        }

        DTTChildGazeSimulator simulator = prefabRoot.GetComponent<DTTChildGazeSimulator>();
        if (simulator == null)
        {
            simulator = prefabRoot.AddComponent<DTTChildGazeSimulator>();
            changed = true;
        }

        if (simulator.eyeController == null)
        {
            simulator.eyeController = eyeController;
            changed = true;
        }

        // Default to visible demo behavior; scene-specific targets can be assigned later.
        if (!simulator.simulateOnStart)
        {
            simulator.simulateOnStart = true;
            changed = true;
        }

        changed |= SetIfDifferent(ref simulator.keyboardTesting, true);
        changed |= SetIfDifferent(ref simulator.logKeyboardPhaseChanges, true);
        changed |= SetIfDifferent(ref simulator.keyboardControlsOnlySelectedChild, true);
        changed |= SetIfDifferent(ref simulator.returnDeselectedChildToIdle, true);
        changed |= SetIfDifferent(ref simulator.useMainCameraAsTeacher, true);
        changed |= SetIfDifferent(ref simulator.gazeHoldDurationRange, new Vector2(1.1f, 2.8f));
        changed |= SetIfDifferent(ref simulator.offTaskGazeHoldDurationRange, new Vector2(0.75f, 1.8f));
        changed |= SetIfDifferent(ref simulator.responseLatencyRange, new Vector2(0.25f, 0.75f));

        DTTUpperBodyGazeFollower upperBodyFollower = prefabRoot.GetComponent<DTTUpperBodyGazeFollower>();
        if (upperBodyFollower == null)
        {
            upperBodyFollower = prefabRoot.AddComponent<DTTUpperBodyGazeFollower>();
            changed = true;
        }

        if (upperBodyFollower.humanoidAnimator == null && animator != null)
        {
            upperBodyFollower.humanoidAnimator = animator;
            changed = true;
        }

        if (upperBodyFollower.eyeController == null)
        {
            upperBodyFollower.eyeController = eyeController;
            changed = true;
        }

        changed |= SetIfDifferent(ref upperBodyFollower.followEnabled, true);
        changed |= SetIfDifferent(ref upperBodyFollower.useEyeControllerTarget, true);
        changed |= SetIfDifferent(ref upperBodyFollower.headYawWeight, 0.45f);
        changed |= SetIfDifferent(ref upperBodyFollower.neckYawWeight, 0.25f);
        changed |= SetIfDifferent(ref upperBodyFollower.chestYawWeight, 0.14f);
        changed |= SetIfDifferent(ref upperBodyFollower.spineYawWeight, 0.06f);
        changed |= SetIfDifferent(ref upperBodyFollower.headPitchWeight, 0.36f);
        changed |= SetIfDifferent(ref upperBodyFollower.neckPitchWeight, 0.2f);
        changed |= SetIfDifferent(ref upperBodyFollower.chestPitchWeight, 0.08f);
        changed |= SetIfDifferent(ref upperBodyFollower.spinePitchWeight, 0.03f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxHeadYaw, 24f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxNeckYaw, 16f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxChestYaw, 10f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxSpineYaw, 5f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxHeadPitch, 14f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxNeckPitch, 8f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxChestPitch, 5f);
        changed |= SetIfDifferent(ref upperBodyFollower.maxSpinePitch, 3f);
        changed |= SetIfDifferent(ref upperBodyFollower.followSmoothTime, 0.32f);
        changed |= SetIfDifferent(ref upperBodyFollower.returnSmoothTime, 0.45f);

        EditorUtility.SetDirty(prefabRoot);
        Debug.Log($"DTT gaze setup checked: {prefabPath}");
        return changed;
    }

    private static bool SetIfDifferent(ref bool field, bool value)
    {
        if (field == value) return false;

        field = value;
        return true;
    }

    private static bool SetIfDifferent(ref float field, float value)
    {
        if (Mathf.Approximately(field, value)) return false;

        field = value;
        return true;
    }

    private static bool SetIfDifferent(ref Vector2 field, Vector2 value)
    {
        if (Mathf.Approximately(field.x, value.x) && Mathf.Approximately(field.y, value.y)) return false;

        field = value;
        return true;
    }
}

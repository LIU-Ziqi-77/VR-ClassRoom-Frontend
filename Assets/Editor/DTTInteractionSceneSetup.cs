using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class DTTInteractionSceneSetup
{
    private const string TargetScenePath = "Assets/Scenes/HighSchoolClassroom_Demo.unity";
    private static readonly string[] StudentPrefabPaths =
    {
        "Assets/Elementary_Students/Ele_student1.prefab",
        "Assets/Elementary_Students/Ele_student2.prefab",
        "Assets/Elementary_Students/Ele_student3.prefab"
    };

    [MenuItem("Tools/DTT/Setup Teaching Aid Interaction")]
    public static void SetupTeachingAidInteraction()
    {
        SetupStudentPrefabs();
        SetupScene(TargetScenePath);
    }

    public static void SetupTeachingAidInteractionBatch()
    {
        try
        {
            SetupTeachingAidInteraction();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void SetupStudentPrefabs()
    {
        foreach (string prefabPath in StudentPrefabPaths)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"DTT student marker setup skipped. Prefab not found: {prefabPath}");
                continue;
            }

            try
            {
                DTTTargetStudentMarker marker = prefabRoot.GetComponent<DTTTargetStudentMarker>();
                if (marker == null)
                {
                    marker = prefabRoot.AddComponent<DTTTargetStudentMarker>();
                    Debug.Log($"DTT target student marker added to prefab: {prefabPath}");
                }

                marker.arrowHeightOffset = 2.05f;
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }

    private static void SetupScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        List<DTTTeachingAid> aids = new List<DTTTeachingAid>();
        AddAidIfFound(aids, "Ruler", DTTTeachingAidKind.Ruler, "Ruler");
        AddAidIfFound(aids, "Rubber", DTTTeachingAidKind.Rubber, "Rubber");
        AddAidIfFound(aids, "Open Notebook", DTTTeachingAidKind.OpenNotebook, "Open Notebook");
        AddAidIfFound(aids, "Pencils (1)", DTTTeachingAidKind.Pencils, "Pencils");

        SetupSceneStudentMarkers();
        SetupManager(aids);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"DTT interaction scene setup complete: {scenePath}. Teaching aids configured: {aids.Count}");
    }

    private static void AddAidIfFound(List<DTTTeachingAid> aids, string objectName, DTTTeachingAidKind kind, string displayName)
    {
        GameObject go = FindSceneObjectByName(objectName);
        if (go == null)
        {
            Debug.LogWarning($"DTT teaching aid not found in scene: {objectName}");
            return;
        }

        DTTTeachingAid aid = ConfigureTeachingAid(go, kind, displayName);
        if (aid != null && !aids.Contains(aid))
        {
            aids.Add(aid);
        }
    }

    private static DTTTeachingAid ConfigureTeachingAid(GameObject go, DTTTeachingAidKind kind, string displayName)
    {
        DTTTeachingAid aid = go.GetComponent<DTTTeachingAid>();
        if (aid == null)
        {
            aid = go.AddComponent<DTTTeachingAid>();
        }

        aid.aidKind = kind;
        aid.displayName = displayName;
        if (aid.gazeTarget == null)
        {
            aid.gazeTarget = go.transform;
        }

        EnsureCollider(go);
        EnsureRigidbody(go);

        if (go.GetComponent<XRGrabInteractable>() == null)
        {
            go.AddComponent<XRGrabInteractable>();
        }

        EditorUtility.SetDirty(go);
        Debug.Log($"DTT teaching aid configured: {go.name} -> {displayName}");
        return aid;
    }

    private static void SetupSceneStudentMarkers()
    {
        DTTChildGazeSimulator[] simulators = UnityEngine.Object.FindObjectsOfType<DTTChildGazeSimulator>(true);
        foreach (DTTChildGazeSimulator simulator in simulators)
        {
            DTTTargetStudentMarker marker = simulator.GetComponent<DTTTargetStudentMarker>();
            if (marker == null)
            {
                marker = simulator.gameObject.AddComponent<DTTTargetStudentMarker>();
            }

            marker.arrowHeightOffset = 2.05f;
            EditorUtility.SetDirty(simulator.gameObject);
        }
    }

    private static void SetupManager(List<DTTTeachingAid> aids)
    {
        GameObject managerGo = GameObject.Find("DTT Interaction Manager");
        if (managerGo == null)
        {
            managerGo = new GameObject("DTT Interaction Manager");
        }

        DTTTeachingAidManager manager = managerGo.GetComponent<DTTTeachingAidManager>();
        if (manager == null)
        {
            manager = managerGo.AddComponent<DTTTeachingAidManager>();
        }

        manager.teachingAids.Clear();
        manager.teachingAids.AddRange(aids);

        DTTTeacherInteractionController controller = managerGo.GetComponent<DTTTeacherInteractionController>();
        if (controller == null)
        {
            controller = managerGo.AddComponent<DTTTeacherInteractionController>();
        }

        controller.manager = manager;
        EditorUtility.SetDirty(managerGo);
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t == null || t.gameObject.scene.name == null || !t.gameObject.scene.isLoaded) continue;
            if (string.Equals(t.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return t.gameObject;
            }
        }

        return null;
    }

    private static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;

        Bounds bounds;
        if (TryGetRendererBounds(go, out bounds))
        {
            BoxCollider box = go.AddComponent<BoxCollider>();
            Vector3 scale = go.transform.lossyScale;
            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(
                SafeDivide(bounds.size.x, Mathf.Abs(scale.x)),
                SafeDivide(bounds.size.y, Mathf.Abs(scale.y)),
                SafeDivide(bounds.size.z, Mathf.Abs(scale.z)));
        }
        else
        {
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = 0.1f;
        }
    }

    private static void EnsureRigidbody(GameObject go)
    {
        Rigidbody body = go.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = go.AddComponent<Rigidbody>();
        }

        body.mass = 0.05f;
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }
}

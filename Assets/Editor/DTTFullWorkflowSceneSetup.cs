#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DTTFullWorkflowSceneSetup
{
    private const string TargetScenePath = "Assets/Scenes/HighSchoolClassroom_Demo.unity";

    [MenuItem("Tools/DTT/Setup Full Voice DTT Workflow")]
    public static void SetupFullVoiceDttWorkflow()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        DTTInteractionSceneSetup.SetupTeachingAidInteraction();
        scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        GameObject managerGo = GameObject.Find("DTT Interaction Manager");
        if (managerGo == null)
        {
            managerGo = new GameObject("DTT Interaction Manager");
        }

        DTTTeachingAidManager teachingAidManager = managerGo.GetComponent<DTTTeachingAidManager>();
        if (teachingAidManager == null)
        {
            teachingAidManager = managerGo.AddComponent<DTTTeachingAidManager>();
        }

        DTTWorkflowController workflow = managerGo.GetComponent<DTTWorkflowController>();
        if (workflow == null)
        {
            workflow = managerGo.AddComponent<DTTWorkflowController>();
        }

        TeacherVoiceIntentReceiver receiver = managerGo.GetComponent<TeacherVoiceIntentReceiver>();
        if (receiver == null)
        {
            receiver = managerGo.AddComponent<TeacherVoiceIntentReceiver>();
        }

        workflow.teachingAidManager = teachingAidManager;
        workflow.requireSpecificTeachingAid = false;
        workflow.students = BuildStudentBindings();

        receiver.workflowController = workflow;
        receiver.listenPort = 5055;

        teachingAidManager.scenarioController = null;
        EnsureAndroidNetworkPermission();

        EditorUtility.SetDirty(managerGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DTTFullWorkflowSceneSetup] Full DTT voice workflow configured.");
    }

    public static void SetupFullVoiceDttWorkflowBatch()
    {
        try
        {
            SetupFullVoiceDttWorkflow();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/DTT/Enable Quest UDP Network Permission")]
    public static void EnsureAndroidNetworkPermission()
    {
        PlayerSettings.Android.forceInternetPermission = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[DTTFullWorkflowSceneSetup] Android INTERNET permission forced for UDP voice intents.");
    }

    public static void EnsureAndroidNetworkPermissionBatch()
    {
        try
        {
            EnsureAndroidNetworkPermission();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static List<DTTStudentScenarioBinding> BuildStudentBindings()
    {
        return new List<DTTStudentScenarioBinding>
        {
            CreateBinding(
                "Ele_student1",
                "可可",
                DTTScenarioType.DirectCorrect,
                "student_01_xiaoxiao_girl",
                new [] { "可可", "学生一", "第一个学生", "一号学生", "一号", "student one", "student 1" }),
            CreateBinding(
                "Ele_student2",
                "李奥",
                DTTScenarioType.HalfPromptThenCorrect,
                "student_03_yunjian_boy",
                new [] { "李奥", "里奥", "学生二", "第二个学生", "二号学生", "二号", "student two", "student 2" }),
            CreateBinding(
                "Ele_student3",
                "安娜",
                DTTScenarioType.FullPromptAfterHalfPromptError,
                "student_02_xiaoyi_girl",
                new [] { "安娜", "学生三", "第三个学生", "三号学生", "三号", "student three", "student 3" })
        };
    }

    private static DTTStudentScenarioBinding CreateBinding(
        string objectName,
        string displayName,
        DTTScenarioType scenarioType,
        string voiceProfileId,
        IEnumerable<string> aliases)
    {
        GameObject studentObject = FindSceneObjectByName(objectName);
        DTTTargetStudentMarker marker = null;
        StudentBehaviorController controller = null;

        if (studentObject != null)
        {
            marker = studentObject.GetComponent<DTTTargetStudentMarker>();
            if (marker == null)
            {
                marker = studentObject.AddComponent<DTTTargetStudentMarker>();
            }

            controller = studentObject.GetComponent<StudentBehaviorController>();
            if (controller == null)
            {
                controller = studentObject.AddComponent<StudentBehaviorController>();
            }

            controller.studentId = objectName;
            controller.studentName = displayName;

            EnsureStudentSpeechComponents(studentObject, controller);
            EditorUtility.SetDirty(studentObject);
        }
        else
        {
            Debug.LogWarning($"[DTTFullWorkflowSceneSetup] Could not find student scene object: {objectName}");
        }

        DTTStudentScenarioBinding binding = new DTTStudentScenarioBinding
        {
            studentId = objectName,
            displayName = displayName,
            scenarioType = scenarioType,
            marker = marker,
            studentController = controller,
            voiceProfileId = voiceProfileId,
            voiceSelectionAliases = new List<string>(aliases)
        };

        return binding;
    }

    private static void EnsureStudentSpeechComponents(GameObject studentObject, StudentBehaviorController controller)
    {
        if (studentObject.GetComponent<AudioSource>() == null)
        {
            AudioSource audioSource = studentObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }

        LipSyncController lipSync = studentObject.GetComponent<LipSyncController>();
        if (lipSync == null)
        {
            lipSync = studentObject.AddComponent<LipSyncController>();
        }

        if (studentObject.GetComponent<FallbackSpeechService>() == null)
        {
            studentObject.AddComponent<FallbackSpeechService>();
        }

        controller.lipSyncController = lipSync;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform transform in allTransforms)
        {
            if (transform == null) continue;
            if (!transform.gameObject.scene.isLoaded) continue;
            if (string.Equals(transform.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return transform.gameObject;
            }
        }

        return null;
    }
}
#endif

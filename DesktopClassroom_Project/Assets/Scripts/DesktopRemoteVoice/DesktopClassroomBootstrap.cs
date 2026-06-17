using UnityEngine;

public class DesktopClassroomBootstrap : MonoBehaviour
{
    public const string RuntimeObjectName = "DesktopClassroomRuntime";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (FindFirstObjectByType<DesktopClassroomBootstrap>() != null) return;

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DesktopClassroomBootstrap bootstrap = runtimeObject.AddComponent<DesktopClassroomBootstrap>();

        if (FindFirstObjectByType<BehaviorDemoSetup>() == null)
        {
            runtimeObject.AddComponent<BehaviorDemoSetup>();
        }

        if (FindFirstObjectByType<DesktopRemoteVoiceReceiver>() == null)
        {
            DesktopRemoteVoiceReceiver receiver = runtimeObject.AddComponent<DesktopRemoteVoiceReceiver>();
            receiver.listenOnStart = true;
            receiver.listenPort = 5066;
            receiver.showStatusOverlay = false;
        }

        if (FindFirstObjectByType<DesktopPresetLineRelayClient>() == null)
        {
            DesktopPresetLineRelayClient relayClient = runtimeObject.AddComponent<DesktopPresetLineRelayClient>();
            relayClient.connectOnStart = true;
            relayClient.showStatusOverlay = false;
        }

        bootstrap.ConfigureTeacherFacingDesktopView();
        Debug.Log("[DesktopClassroom] Runtime bootstrap complete. Desktop camera, remote voice receiver, and preset relay client are enabled.");
    }

    private void Start()
    {
        Invoke(nameof(ConfigureTeacherFacingDesktopView), 0.1f);
        Invoke(nameof(ConfigureTeacherFacingDesktopView), 1f);
    }

    private void ConfigureTeacherFacingDesktopView()
    {
        foreach (BehaviorDemoController demo in FindObjectsByType<BehaviorDemoController>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            demo.showDemoOverlay = false;
        }

        foreach (DesktopRemoteVoiceReceiver receiver in FindObjectsByType<DesktopRemoteVoiceReceiver>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            receiver.showStatusOverlay = false;
        }

        foreach (DesktopPresetLineRelayClient relay in FindObjectsByType<DesktopPresetLineRelayClient>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            relay.showStatusOverlay = false;
        }

        foreach (DTTTeacherInteractionController interaction in FindObjectsByType<DTTTeacherInteractionController>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            interaction.showDesktopReticle = false;
            interaction.showDesktopSelectionHint = false;
        }

        foreach (DTTWorkflowController workflow in FindObjectsByType<DTTWorkflowController>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            workflow.showDesktopStatus = false;
        }

        foreach (StudentBehaviorVisuals visuals in FindObjectsByType<StudentBehaviorVisuals>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            visuals.useScreenSpaceLabel = false;
        }
    }
}

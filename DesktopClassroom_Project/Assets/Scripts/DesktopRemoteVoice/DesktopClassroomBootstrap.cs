using UnityEngine;

public class DesktopClassroomBootstrap : MonoBehaviour
{
    public const string RuntimeObjectName = "DesktopClassroomRuntime";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (FindFirstObjectByType<DesktopClassroomBootstrap>() != null) return;

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        runtimeObject.AddComponent<DesktopClassroomBootstrap>();

        if (FindFirstObjectByType<BehaviorDemoSetup>() == null)
        {
            runtimeObject.AddComponent<BehaviorDemoSetup>();
        }

        if (FindFirstObjectByType<DesktopRemoteVoiceReceiver>() == null)
        {
            DesktopRemoteVoiceReceiver receiver = runtimeObject.AddComponent<DesktopRemoteVoiceReceiver>();
            receiver.listenOnStart = true;
            receiver.listenPort = 5066;
            receiver.showStatusOverlay = true;
        }

        if (FindFirstObjectByType<DesktopPresetLineRelayClient>() == null)
        {
            DesktopPresetLineRelayClient relayClient = runtimeObject.AddComponent<DesktopPresetLineRelayClient>();
            relayClient.connectOnStart = true;
        }

        Debug.Log("[DesktopClassroom] Runtime bootstrap complete. Desktop camera, remote voice receiver, and preset relay client are enabled.");
    }
}

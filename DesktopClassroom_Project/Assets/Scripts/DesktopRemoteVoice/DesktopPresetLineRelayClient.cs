using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DesktopPresetLineRelayClient : MonoBehaviour
{
    [Header("Relay Server")]
    public bool connectOnStart = true;
    public string relayUrl = "wss://vr-classroom-relay.onrender.com/ws?role=unity&room=demo&token=49929d9cf29a50c34610c7d52ad4d050";
    public float reconnectSeconds = 3f;

    [Header("Student Voice Profiles")]
    public string[] voiceProfileByStudentIndex =
    {
        "student_01_xiaoxiao_girl",
        "student_03_yunjian_boy",
        "student_02_xiaoyi_girl"
    };

    [Header("Azure TTS Voices")]
    public string[] azureVoiceByStudentIndex =
    {
        "zh-CN-XiaoxiaoNeural",
        "zh-CN-YunjianNeural",
        "zh-CN-XiaoyiNeural"
    };

    [Header("Debug")]
    public bool logMessages = true;
    public bool showStatusOverlay = false;

    private readonly ConcurrentQueue<RemoteVoicePacket> pendingPackets =
        new ConcurrentQueue<RemoteVoicePacket>();
    private readonly List<RemoteStudentVoicePlayer> players = new List<RemoteStudentVoicePlayer>();

    private ClientWebSocket socket;
    private CancellationTokenSource cancellation;
    private Task receiveTask;
    private float nextReconnectTime;
    private string status = "not connected";
    private int receivedCount;

    void Awake()
    {
        string commandLineRelayUrl = GetCommandLineArg("--relay-url");
        if (!string.IsNullOrWhiteSpace(commandLineRelayUrl))
        {
            relayUrl = commandLineRelayUrl;
        }
    }

    void OnEnable()
    {
        DiscoverStudents();
        if (connectOnStart)
        {
            _ = ConnectAsync();
        }
    }

    void OnDisable()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void Update()
    {
        if (connectOnStart &&
            (socket == null || socket.State == WebSocketState.Closed || socket.State == WebSocketState.Aborted) &&
            Time.unscaledTime >= nextReconnectTime)
        {
            nextReconnectTime = Time.unscaledTime + reconnectSeconds;
            _ = ConnectAsync();
        }

        int processed = 0;
        while (processed < 64 && pendingPackets.TryDequeue(out RemoteVoicePacket packet))
        {
            ProcessPacket(packet);
            processed++;
        }
    }

    void OnGUI()
    {
        if (!showStatusOverlay) return;

        GUILayout.BeginArea(new Rect(12, 178, 430, 130), GUI.skin.box);
        GUILayout.Label("Preset Line Relay");
        GUILayout.Label($"URL: {relayUrl}");
        GUILayout.Label($"State: {(socket != null ? socket.State.ToString() : "null")} | received: {receivedCount}");
        GUILayout.Label($"Last: {status}");
        GUILayout.Label("Researcher console sends preset_line / behavior commands.");
        GUILayout.EndArea();
    }

    public async Task ConnectAsync()
    {
        if (socket != null &&
            (socket.State == WebSocketState.Open || socket.State == WebSocketState.Connecting))
        {
            return;
        }

        Disconnect();

        try
        {
            cancellation = new CancellationTokenSource();
            socket = new ClientWebSocket();
            status = "connecting";
            await socket.ConnectAsync(new Uri(relayUrl), cancellation.Token);
            status = "connected";
            Debug.Log($"[PresetRelay] Connected to {relayUrl}");
            await SendJsonAsync("{\"type\":\"hello\",\"client\":\"unity\"}");
            receiveTask = ReceiveLoopAsync(cancellation.Token);
        }
        catch (Exception e)
        {
            status = $"connect failed: {e.Message}";
            Debug.LogWarning($"[PresetRelay] Connection failed: {e.Message}");
            Disconnect();
        }
    }

    public void Disconnect()
    {
        try
        {
            cancellation?.Cancel();
        }
        catch
        {
        }

        cancellation = null;

        if (socket != null)
        {
            try
            {
                socket.Dispose();
            }
            catch
            {
            }

            socket = null;
        }

        receiveTask = null;
    }

    public void DiscoverStudents()
    {
        players.Clear();

        var existingPlayers = FindObjectsByType<RemoteStudentVoicePlayer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (RemoteStudentVoicePlayer existing in existingPlayers)
        {
            if (existing != null && !players.Contains(existing))
            {
                players.Add(existing);
            }
        }

        var studentControllers = FindObjectsByType<StudentBehaviorController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (StudentBehaviorController controller in studentControllers)
        {
            RegisterStudentObject(controller.gameObject);
        }

        var proceduralAnimators = FindObjectsByType<ProceduralBehaviorAnimator>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (ProceduralBehaviorAnimator animator in proceduralAnimators)
        {
            RegisterStudentObject(animator.gameObject);
        }

        players.Sort(CompareStudentPlayers);

        for (int i = 0; i < players.Count; i++)
        {
            RemoteStudentVoicePlayer player = players[i];
            if (player == null) continue;
            string id = !string.IsNullOrWhiteSpace(player.studentId) ? player.studentId : player.gameObject.name;
            string displayName = !string.IsNullOrWhiteSpace(player.studentDisplayName)
                ? player.studentDisplayName
                : player.gameObject.name;
            player.ConfigureIdentity(i + 1, id, displayName);
        }
    }

    private void RegisterStudentObject(GameObject studentObject)
    {
        if (studentObject == null) return;

        RemoteStudentVoicePlayer player = studentObject.GetComponent<RemoteStudentVoicePlayer>();
        if (player == null)
        {
            player = studentObject.AddComponent<RemoteStudentVoicePlayer>();
        }

        StudentBehaviorController controller = studentObject.GetComponent<StudentBehaviorController>();
        string id = controller != null && !string.IsNullOrWhiteSpace(controller.studentId)
            ? controller.studentId
            : studentObject.name;
        string displayName = controller != null && !string.IsNullOrWhiteSpace(controller.studentName)
            ? controller.studentName
            : studentObject.name;
        player.ConfigureIdentity(players.Count + 1, id, displayName);

        if (!players.Contains(player))
        {
            players.Add(player);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        byte[] buffer = new byte[8192];
        var textBuffer = new StringBuilder();

        while (!token.IsCancellationRequested &&
               socket != null &&
               socket.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        status = "closed by server";
                        return;
                    }

                    textBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                string json = textBuffer.ToString();
                textBuffer.Length = 0;

                RemoteVoicePacket packet = JsonUtility.FromJson<RemoteVoicePacket>(json);
                if (packet != null && !string.IsNullOrWhiteSpace(packet.type))
                {
                    pendingPackets.Enqueue(packet);
                    receivedCount++;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                status = $"receive failed: {e.Message}";
                Debug.LogWarning($"[PresetRelay] Receive failed: {e.Message}");
                return;
            }
        }
    }

    private async Task SendJsonAsync(string json)
    {
        if (socket == null || socket.State != WebSocketState.Open) return;
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellation != null ? cancellation.Token : CancellationToken.None);
    }

    private void ProcessPacket(RemoteVoicePacket packet)
    {
        if (packet.type == RemoteVoicePacketTypes.Refresh)
        {
            DiscoverStudents();
            status = "student list refreshed";
            return;
        }

        RemoteStudentVoicePlayer player = ResolvePlayer(packet.studentIndex);
        if (player == null)
        {
            status = $"unknown student {packet.studentIndex}";
            return;
        }

        if (logMessages)
        {
            Debug.Log($"[PresetRelay] {packet.type}: student={packet.studentIndex}, line={packet.utteranceKey}, behavior={packet.behavior}");
        }

        switch (packet.type)
        {
            case RemoteVoicePacketTypes.SelectStudent:
                SelectStudent(player);
                status = $"selected {player.studentDisplayName}";
                break;
            case RemoteVoicePacketTypes.PresetLine:
                SelectStudent(player);
                PlayPresetLine(player, packet);
                break;
            case RemoteVoicePacketTypes.CustomText:
                SelectStudent(player);
                _ = PlayCustomText(player, packet);
                break;
            case RemoteVoicePacketTypes.Behavior:
                SelectStudent(player);
                if (TryTriggerDemoBehavior(packet.behavior))
                {
                    status = $"behavior {packet.behavior} -> {player.studentDisplayName}";
                    break;
                }
                player.TriggerBehavior(packet.behavior, packet.duration);
                status = $"behavior {packet.behavior} -> {player.studentDisplayName}";
                break;
            default:
                break;
        }
    }

    private void PlayPresetLine(RemoteStudentVoicePlayer player, RemoteVoicePacket packet)
    {
        string utteranceKey = packet.utteranceKey;
        if (string.IsNullOrWhiteSpace(utteranceKey))
        {
            status = "preset line missing utteranceKey";
            return;
        }

        string profile = !string.IsNullOrWhiteSpace(packet.voiceProfileId)
            ? packet.voiceProfileId
            : GetVoiceProfile(packet.studentIndex);

        AudioClip clip = DTTStudentVoiceBank.LoadClip(profile, utteranceKey);
        if (clip == null)
        {
            status = $"missing clip {profile}/{utteranceKey}";
            Debug.LogWarning($"[PresetRelay] Missing preset clip: {profile}/{utteranceKey}");
            return;
        }

        player.PlayPresetClip(clip, $"{profile}/{utteranceKey} {packet.text}");
        status = $"preset {utteranceKey} -> {player.studentDisplayName}";
    }

    private bool TryTriggerDemoBehavior(string behavior)
    {
        string key = NormalizeBehavior(behavior);
        BehaviorDemoController demo = FindObjectOfType<BehaviorDemoController>();
        if (demo == null) return false;

        switch (key)
        {
            case "clap":
            case "clapping":
                demo.TriggerClap();
                return true;
            case "touchnose":
                demo.TriggerTouchNose();
                return true;
            default:
                return false;
        }
    }

    private async Task PlayCustomText(RemoteStudentVoicePlayer player, RemoteVoicePacket packet)
    {
        string text = packet.text;
        if (string.IsNullOrWhiteSpace(text))
        {
            status = "custom text is empty";
            return;
        }

        string voiceName = GetAzureVoiceName(packet.studentIndex);
        status = $"generating TTS -> {player.studentDisplayName}";

        try
        {
            AudioClip clip = await TTSService.Instance.GenerateSpeech(text.Trim(), voiceName);
            if (clip == null)
            {
                status = $"TTS failed -> {player.studentDisplayName}";
                return;
            }

            player.PlayPresetClip(clip, $"{voiceName}: {text}");
            status = $"custom TTS -> {player.studentDisplayName}";
        }
        catch (Exception e)
        {
            status = $"TTS error: {e.Message}";
            Debug.LogError($"[PresetRelay] Custom TTS failed for {player.studentDisplayName}: {e}");
        }
    }

    private RemoteStudentVoicePlayer ResolvePlayer(int studentIndex)
    {
        if (studentIndex <= 0)
        {
            studentIndex = 1;
        }

        int index = studentIndex - 1;
        if (index >= 0 && index < players.Count)
        {
            return players[index];
        }

        DiscoverStudents();
        if (index >= 0 && index < players.Count)
        {
            return players[index];
        }

        return null;
    }

    private void SelectStudent(RemoteStudentVoicePlayer player)
    {
        if (player == null) return;

        BehaviorDemoController demo = FindObjectOfType<BehaviorDemoController>();
        if (demo != null)
        {
            demo.SelectStudentByGameObject(player.gameObject);
        }

        DTTTeachingAidManager manager = DTTTeachingAidManager.Instance;
        if (manager != null)
        {
            DTTTargetStudentMarker marker = player.GetComponentInParent<DTTTargetStudentMarker>();
            if (marker == null)
            {
                marker = player.GetComponentInChildren<DTTTargetStudentMarker>();
            }
            if (marker == null)
            {
                marker = player.gameObject.AddComponent<DTTTargetStudentMarker>();
            }

            manager.SelectStudent(marker);
        }
    }

    private static int CompareStudentPlayers(RemoteStudentVoicePlayer a, RemoteStudentVoicePlayer b)
    {
        int orderA = PreferredStudentOrder(a);
        int orderB = PreferredStudentOrder(b);
        if (orderA != orderB) return orderA.CompareTo(orderB);

        string nameA = a != null ? a.gameObject.name : "";
        string nameB = b != null ? b.gameObject.name : "";
        return string.CompareOrdinal(nameA, nameB);
    }

    private static int PreferredStudentOrder(RemoteStudentVoicePlayer player)
    {
        if (player == null) return int.MaxValue;

        string combined = $"{player.studentDisplayName} {player.studentId} {player.gameObject.name}";
        if (combined.Contains("莉莉") || combined.Contains("Lily") || combined.Contains("Ele_student1")) return 0;
        if (combined.Contains("卢卡") || combined.Contains("Luca") || combined.Contains("Ele_student2")) return 1;
        if (combined.Contains("贝拉") || combined.Contains("Bella") || combined.Contains("Ele_student3")) return 2;
        return 100;
    }

    private string GetVoiceProfile(int studentIndex)
    {
        int index = Mathf.Clamp(studentIndex - 1, 0, voiceProfileByStudentIndex.Length - 1);
        return voiceProfileByStudentIndex[index];
    }

    private string GetAzureVoiceName(int studentIndex)
    {
        int index = Mathf.Clamp(studentIndex - 1, 0, azureVoiceByStudentIndex.Length - 1);
        return azureVoiceByStudentIndex[index];
    }

    private static string NormalizeBehavior(string behavior)
    {
        if (string.IsNullOrWhiteSpace(behavior)) return "";
        return behavior.Trim().ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
    }

    private static string GetCommandLineArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return "";
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class DesktopRemoteVoiceReceiver : MonoBehaviour
{
    [Header("UDP Remote Voice")]
    public bool listenOnStart = true;
    public int listenPort = 5066;
    public int maxPacketsProcessedPerFrame = 80;
    public float rediscoverInterval = 2f;
    public bool showStatusOverlay = true;
    public bool logPackets = false;

    private readonly ConcurrentQueue<RemoteVoicePacket> pendingPackets =
        new ConcurrentQueue<RemoteVoicePacket>();
    private readonly List<RemoteStudentVoicePlayer> players = new List<RemoteStudentVoicePlayer>();
    private readonly Dictionary<string, RemoteStudentVoicePlayer> playersByKey =
        new Dictionary<string, RemoteStudentVoicePlayer>();

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running;
    private float nextRediscoverTime;
    private string lastStatus = "not started";
    private int receivedPacketCount;
    private int malformedPacketCount;

    public IReadOnlyList<RemoteStudentVoicePlayer> Players => players;

    void OnEnable()
    {
        DiscoverStudents();

        if (listenOnStart)
        {
            StartListening();
        }
    }

    void OnDisable()
    {
        StopListening();
    }

    void OnDestroy()
    {
        StopListening();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextRediscoverTime)
        {
            DiscoverStudents();
            nextRediscoverTime = Time.unscaledTime + rediscoverInterval;
        }

        int processed = 0;
        while (processed < maxPacketsProcessedPerFrame &&
               pendingPackets.TryDequeue(out RemoteVoicePacket packet))
        {
            ProcessPacket(packet);
            processed++;
        }
    }

    void OnGUI()
    {
        if (!showStatusOverlay) return;

        const int width = 430;
        GUILayout.BeginArea(new Rect(12, 12, width, 160), GUI.skin.box);
        GUILayout.Label("Desktop Remote Voice Receiver");
        GUILayout.Label($"UDP port: {listenPort} | running: {running} | students: {players.Count}");
        GUILayout.Label($"Packets: {receivedPacketCount} | malformed: {malformedPacketCount}");
        GUILayout.Label($"Last: {lastStatus}");

        for (int i = 0; i < players.Count; i++)
        {
            RemoteStudentVoicePlayer player = players[i];
            if (player == null) continue;
            string marker = player.IsStreaming ? "SPEAKING" : "idle";
            GUILayout.Label($"{i + 1}. {player.studentDisplayName} ({player.studentId}) - {marker}, q={player.QueuedSampleCount}");
        }

        GUILayout.EndArea();
    }

    public void StartListening()
    {
        if (running) return;

        try
        {
            udpClient = new UdpClient(listenPort);
            running = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Desktop Remote Voice Receiver"
            };
            receiveThread.Start();
            lastStatus = $"listening on UDP {listenPort}";
            Debug.Log($"[RemoteVoice] Listening on UDP port {listenPort}.");
        }
        catch (Exception e)
        {
            lastStatus = $"failed to listen: {e.Message}";
            Debug.LogError($"[RemoteVoice] Failed to start UDP listener on {listenPort}: {e.Message}");
            StopListening();
        }
    }

    public void StopListening()
    {
        running = false;

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null)
        {
            receiveThread.Join(100);
            receiveThread = null;
        }
    }

    public void DiscoverStudents()
    {
        var uniqueObjects = new List<GameObject>();
        AddStudentObjects(uniqueObjects, FindObjectsByType<StudentBehaviorController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None));
        AddStudentObjects(uniqueObjects, FindObjectsByType<ProceduralBehaviorAnimator>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None));
        AddStudentObjects(uniqueObjects, FindObjectsByType<FallbackSpeechService>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None));

        uniqueObjects.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        players.Clear();
        playersByKey.Clear();

        for (int i = 0; i < uniqueObjects.Count; i++)
        {
            GameObject studentObject = uniqueObjects[i];
            if (studentObject == null || !studentObject.activeInHierarchy) continue;

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
            players.Add(player);
            RegisterKey(id, player);
            RegisterKey(displayName, player);
            RegisterKey(studentObject.name, player);
            RegisterKey((players.Count).ToString(), player);
        }
    }

    private void AddStudentObjects<T>(List<GameObject> target, T[] components) where T : Component
    {
        foreach (T component in components)
        {
            if (component == null) continue;
            GameObject obj = component.gameObject;
            if (obj == null || target.Contains(obj)) continue;
            target.Add(obj);
        }
    }

    private void RegisterKey(string key, RemoteStudentVoicePlayer player)
    {
        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized) || playersByKey.ContainsKey(normalized)) return;
        playersByKey.Add(normalized, player);
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndpoint);
                string json = Encoding.UTF8.GetString(data);
                RemoteVoicePacket packet = JsonUtility.FromJson<RemoteVoicePacket>(json);
                if (packet == null || string.IsNullOrWhiteSpace(packet.type))
                {
                    malformedPacketCount++;
                    continue;
                }

                pendingPackets.Enqueue(packet);
                receivedPacketCount++;
            }
            catch (SocketException)
            {
                if (running)
                {
                    malformedPacketCount++;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception)
            {
                malformedPacketCount++;
            }
        }
    }

    private void ProcessPacket(RemoteVoicePacket packet)
    {
        if (packet.type == RemoteVoicePacketTypes.Refresh)
        {
            DiscoverStudents();
            lastStatus = "student list refreshed";
            return;
        }

        RemoteStudentVoicePlayer player = ResolvePlayer(packet);
        if (player == null)
        {
            lastStatus = $"unknown student index={packet.studentIndex}, id={packet.studentId}";
            return;
        }

        if (logPackets)
        {
            Debug.Log($"[RemoteVoice] packet={packet.type}, student={player.studentDisplayName}, seq={packet.sequence}");
        }

        switch (packet.type)
        {
            case RemoteVoicePacketTypes.VoiceStart:
                player.BeginStream(packet.sampleRate);
                lastStatus = $"voice start: {player.studentDisplayName}";
                break;
            case RemoteVoicePacketTypes.VoiceChunk:
                byte[] pcm = Convert.FromBase64String(packet.payloadBase64);
                if (!player.IsStreaming)
                {
                    player.BeginStream(packet.sampleRate);
                }
                player.AppendPcm16(pcm, packet.channels, packet.gain);
                lastStatus = $"voice chunk: {player.studentDisplayName}";
                break;
            case RemoteVoicePacketTypes.VoiceStop:
                player.EndStream("remote stop");
                lastStatus = $"voice stop: {player.studentDisplayName}";
                break;
            case RemoteVoicePacketTypes.Behavior:
                player.TriggerBehavior(packet.behavior, packet.duration);
                lastStatus = $"behavior {packet.behavior}: {player.studentDisplayName}";
                break;
            default:
                lastStatus = $"unsupported packet type: {packet.type}";
                break;
        }
    }

    private RemoteStudentVoicePlayer ResolvePlayer(RemoteVoicePacket packet)
    {
        if (packet.studentIndex > 0)
        {
            int index = packet.studentIndex - 1;
            if (index >= 0 && index < players.Count)
            {
                return players[index];
            }
        }

        string key = NormalizeKey(packet.studentId);
        if (!string.IsNullOrEmpty(key) && playersByKey.TryGetValue(key, out RemoteStudentVoicePlayer byId))
        {
            return byId;
        }

        return null;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.Trim().ToLowerInvariant();
    }
}

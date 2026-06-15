using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[Serializable]
public class DTTWorkflowMonitorSnapshot
{
    public string type = "status";
    public string session_id;
    public string device_name;
    public string platform;
    public float unity_time;
    public string status;
    public string active_student;
    public string active_student_id;
    public string scenario;
    public int current_step_index;
    public int step_count;
    public string current_step_label;
    public string expected_event;
    public bool is_waiting;
    public bool collecting_distractors;
    public bool scenario_complete;
    public string selected_aid;
    public string held_aid;
    public string target_item_type;
    public int event_log_count;
}

[Serializable]
public class DTTMonitorEventMessage
{
    public string type;
    public string session_id;
    public string device_name;
    public string platform;
    public float unity_time;
    public string level;
    public string message;
    public string intent;
    public string text;
    public string student_id;
    public string student_name;
}

public class DTTMonitorReporter : MonoBehaviour
{
    [Header("Desktop Monitor UDP")]
    public bool sendOnStart = true;
    public string monitorHost = "255.255.255.255";
    public int monitorPort = 5060;
    public float statusIntervalSeconds = 0.5f;
    public DTTWorkflowController workflowController;

    private UdpClient udpClient;
    private IPEndPoint monitorEndpoint;
    private float nextStatusAt;
    private string sessionId;

    void Awake()
    {
        sessionId = $"{SystemInfo.deviceName}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        if (workflowController == null)
        {
            workflowController = GetComponent<DTTWorkflowController>();
        }
        if (workflowController == null)
        {
            workflowController = FindObjectOfType<DTTWorkflowController>();
        }
    }

    void OnEnable()
    {
        if (sendOnStart)
        {
            StartReporting();
        }
    }

    void OnDisable()
    {
        StopReporting();
    }

    void OnDestroy()
    {
        StopReporting();
    }

    void Update()
    {
        if (udpClient == null || workflowController == null) return;
        if (Time.unscaledTime < nextStatusAt) return;

        nextStatusAt = Time.unscaledTime + Mathf.Max(0.1f, statusIntervalSeconds);
        SendStatus();
    }

    public void StartReporting()
    {
        if (udpClient != null) return;

        try
        {
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            IPAddress address = IPAddress.Parse(monitorHost);
            monitorEndpoint = new IPEndPoint(address, monitorPort);
            nextStatusAt = 0f;
            Debug.Log($"[DTT Monitor] Reporting to UDP {monitorHost}:{monitorPort}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DTT Monitor] Failed to start reporter: {e.Message}");
            StopReporting();
        }
    }

    public void StopReporting()
    {
        if (udpClient == null) return;

        udpClient.Close();
        udpClient = null;
        monitorEndpoint = null;
    }

    public void SendStatus()
    {
        if (workflowController == null) return;
        DTTWorkflowMonitorSnapshot snapshot = workflowController.BuildMonitorSnapshot();
        snapshot.session_id = sessionId;
        snapshot.device_name = SystemInfo.deviceName;
        snapshot.platform = Application.platform.ToString();
        snapshot.unity_time = Time.time;
        SendJson(JsonUtility.ToJson(snapshot));
    }

    public void SendWorkflowLog(string message, string level = "info")
    {
        SendEvent("workflow_log", message, level, "", "", "", "");
    }

    public void SendIgnoredEvent(string eventName, string reason)
    {
        SendEvent("ignored_event", reason, "warn", eventName, "", "", "");
    }

    public void SendVoiceIntent(DTTVoiceIntentMessage message, DTTTeacherIntent parsedIntent)
    {
        if (message == null) return;
        SendEvent(
            "voice_intent",
            message.intent,
            "info",
            parsedIntent.ToString(),
            message.text,
            message.student_id,
            message.student_name);
    }

    private void SendEvent(string type, string message, string level, string intent, string text, string studentId, string studentName)
    {
        DTTMonitorEventMessage eventMessage = new DTTMonitorEventMessage
        {
            type = type,
            session_id = sessionId,
            device_name = SystemInfo.deviceName,
            platform = Application.platform.ToString(),
            unity_time = Time.time,
            level = level,
            message = message,
            intent = intent,
            text = text,
            student_id = studentId,
            student_name = studentName
        };

        SendJson(JsonUtility.ToJson(eventMessage));
    }

    private void SendJson(string json)
    {
        if (udpClient == null || monitorEndpoint == null) return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            udpClient.Send(data, data.Length, monitorEndpoint);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DTT Monitor] Failed to send monitor packet: {e.Message}");
        }
    }
}

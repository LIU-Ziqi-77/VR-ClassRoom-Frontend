using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class TeacherVoiceIntentReceiver : MonoBehaviour
{
    [Header("UDP Receiver")]
    public bool listenOnStart = true;
    public int listenPort = 5055;
    public DTTWorkflowController workflowController;

    [Header("Debug")]
    public bool logReceivedMessages = true;

    private readonly ConcurrentQueue<DTTVoiceIntentMessage> pendingMessages =
        new ConcurrentQueue<DTTVoiceIntentMessage>();

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning;

    void Awake()
    {
        if (workflowController == null)
        {
            workflowController = FindObjectOfType<DTTWorkflowController>();
        }
    }

    void OnEnable()
    {
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
        while (pendingMessages.TryDequeue(out DTTVoiceIntentMessage message))
        {
            if (workflowController == null)
            {
                workflowController = FindObjectOfType<DTTWorkflowController>();
            }

            if (logReceivedMessages)
            {
                Debug.Log($"[DTT Voice] intent={message.intent}, text=\"{message.text}\", student={message.student_id}/{message.student_name}");
            }

            if (workflowController != null)
            {
                workflowController.HandleVoiceIntent(message);
            }
        }
    }

    public void StartListening()
    {
        if (isRunning) return;

        try
        {
            udpClient = new UdpClient(listenPort);
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "DTT Voice Intent UDP Receiver"
            };
            receiveThread.Start();
            Debug.Log($"[DTT Voice] Listening for teacher intents on UDP port {listenPort}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DTT Voice] Failed to start UDP listener on port {listenPort}: {e.Message}");
            StopListening();
        }
    }

    public void StopListening()
    {
        isRunning = false;

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

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndpoint);
                string json = Encoding.UTF8.GetString(data);
                DTTVoiceIntentMessage message = JsonUtility.FromJson<DTTVoiceIntentMessage>(json);
                if (message != null && !string.IsNullOrEmpty(message.intent))
                {
                    pendingMessages.Enqueue(message);
                }
            }
            catch (SocketException)
            {
                if (isRunning)
                {
                    Debug.LogWarning("[DTT Voice] UDP receive socket interrupted.");
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DTT Voice] Ignored malformed voice intent message: {e.Message}");
            }
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text;

public class StudentNetworkManager : MonoBehaviour
{
    [Header("Network Settings")]
    public string serverUrl = "ws://localhost:8080";
    public bool autoConnect = true;
    public float reconnectInterval = 5f;
    
    [Header("Student Management")]
    public Dictionary<string, StudentBehaviorController> students = new Dictionary<string, StudentBehaviorController>();
    
    private WebSocket webSocket;
    private bool isConnected = false;
    private float lastReconnectTime;
    
    public static StudentNetworkManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        if (autoConnect)
        {
            ConnectToServer();
        }
        
        // 注册所有学生
        RegisterAllStudents();
    }
    
    void Update()
    {
        // 处理重连
        if (!isConnected && Time.time - lastReconnectTime > reconnectInterval)
        {
            ConnectToServer();
            lastReconnectTime = Time.time;
        }
    }
    
    void OnDestroy()
    {
        DisconnectFromServer();
    }
    
    public void ConnectToServer()
    {
        try
        {
            webSocket = new WebSocket(serverUrl);
            webSocket.OnMessage += OnMessageReceived;
            webSocket.OnOpen += OnConnectionOpen;
            webSocket.OnClose += OnConnectionClose;
            webSocket.OnError += OnConnectionError;
            
            webSocket.Connect();
            Debug.Log($"Connecting to server: {serverUrl}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to connect to server: {e.Message}");
        }
    }
    
    public void DisconnectFromServer()
    {
        if (webSocket != null)
        {
            webSocket.Close();
            webSocket = null;
        }
        isConnected = false;
    }
    
    private void OnConnectionOpen()
    {
        isConnected = true;
        Debug.Log("Connected to server successfully!");
        
        // 发送学生注册信息
        SendStudentRegistration();
    }
    
    private void OnConnectionClose()
    {
        isConnected = false;
        Debug.Log("Disconnected from server");
    }
    
    private void OnConnectionError(string error)
    {
        Debug.LogError($"WebSocket error: {error}");
        isConnected = false;
    }
    
    private void OnMessageReceived(string message)
    {
        try
        {
            Debug.Log($"Received message: {message}");
            
            // 解析消息
            StudentCommand command = JsonUtility.FromJson<StudentCommand>(message);
            
            if (command != null && !string.IsNullOrEmpty(command.studentId))
            {
                ProcessStudentCommand(command);
            }
            else
            {
                Debug.LogWarning("Invalid command format received");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
        }
    }
    
    private void ProcessStudentCommand(StudentCommand command)
    {
        if (students.ContainsKey(command.studentId))
        {
            students[command.studentId].ProcessCommand(command);
        }
        else
        {
            Debug.LogWarning($"Student {command.studentId} not found");
        }
    }
    
    private void RegisterAllStudents()
    {
        // 查找场景中所有的学生控制器
        StudentBehaviorController[] studentControllers = FindObjectsOfType<StudentBehaviorController>();
        
        foreach (var controller in studentControllers)
        {
            string studentId = controller.GetStudentId();
            if (!string.IsNullOrEmpty(studentId))
            {
                students[studentId] = controller;
                Debug.Log($"Registered student: {studentId} - {controller.GetStudentName()}");
            }
        }
    }
    
    public void RegisterStudent(string studentId, StudentBehaviorController controller)
    {
        students[studentId] = controller;
        Debug.Log($"Registered student: {studentId}");
    }
    
    public void UnregisterStudent(string studentId)
    {
        if (students.ContainsKey(studentId))
        {
            students.Remove(studentId);
            Debug.Log($"Unregistered student: {studentId}");
        }
    }
    
    private void SendStudentRegistration()
    {
        if (!isConnected) return;
        
        var registration = new StudentRegistration
        {
            type = "registration",
            students = new List<StudentInfo>()
        };
        
        foreach (var kvp in students)
        {
            registration.students.Add(new StudentInfo
            {
                studentId = kvp.Key,
                studentName = kvp.Value.GetStudentName()
            });
        }
        
        string message = JsonUtility.ToJson(registration);
        SendMessage(message);
    }
    
    public void SendMessage(string message)
    {
        if (webSocket != null && isConnected)
        {
            webSocket.Send(message);
        }
        else
        {
            Debug.LogWarning("Cannot send message: not connected to server");
        }
    }
    
    public void SendStudentStatus(string studentId, string status)
    {
        var statusMessage = new StudentStatus
        {
            type = "status",
            studentId = studentId,
            status = status,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
        
        string message = JsonUtility.ToJson(statusMessage);
        SendMessage(message);
    }
    
    public bool IsConnected()
    {
        return isConnected;
    }
    
    public void SendTestMessage()
    {
        var testCommand = new StudentCommand
        {
            studentId = "test_student",
            type = CommandType.Speak,
            text = "这是一个测试消息"
        };
        
        string message = JsonUtility.ToJson(testCommand);
        SendMessage(message);
    }
}

[System.Serializable]
public class StudentRegistration
{
    public string type;
    public List<StudentInfo> students;
}

[System.Serializable]
public class StudentInfo
{
    public string studentId;
    public string studentName;
}

[System.Serializable]
public class StudentStatus
{
    public string type;
    public string studentId;
    public string status;
    public string timestamp;
}

// 简化的WebSocket实现
public class WebSocket
{
    public event System.Action<string> OnMessage;
    public event System.Action OnOpen;
    public event System.Action OnClose;
    public event System.Action<string> OnError;
    
    private string url;
    private bool isConnected = false;
    
    public WebSocket(string url)
    {
        this.url = url;
    }
    
    public void Connect()
    {
        // 这里需要实现实际的WebSocket连接
        // 可以使用Unity的WebSocket库或者第三方库
        Debug.Log($"WebSocket connecting to: {url}");
        
        // 模拟连接成功
        isConnected = true;
        OnOpen?.Invoke();
    }
    
    public void Send(string message)
    {
        if (isConnected)
        {
            Debug.Log($"Sending message: {message}");
            // 实际发送消息的逻辑
        }
    }
    
    public void Close()
    {
        isConnected = false;
        OnClose?.Invoke();
    }
    
    // 模拟接收消息（用于测试）
    public void SimulateMessage(string message)
    {
        OnMessage?.Invoke(message);
    }
} 
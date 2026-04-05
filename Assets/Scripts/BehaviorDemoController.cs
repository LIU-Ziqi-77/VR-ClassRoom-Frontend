using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using VRM;

/// <summary>
/// Unified demo controller for student avatar behaviors in the classroom scene.
///
/// This version has extensive diagnostic logging to help debug setup failures.
/// Every button click, every null check, every state transition is logged.
///
/// Controls (Mac-friendly — no function keys needed):
///   1-4        Select student by index
///   Tab        Cycle to next student
///   Q          Speak
///   W          Raise Hand
///   E          Lie Down / Slump
///   R          Take Notes
///   T          Hit Desk
///   Y          Scream
///   U          Push Classmate
///   S          Stop current behavior
///   X          Stop ALL students
///   H          Print help to console
///   F9         Re-discover students
/// </summary>
public class BehaviorDemoController : MonoBehaviour
{
    [Header("Students")]
    public List<ProceduralBehaviorAnimator> students = new List<ProceduralBehaviorAnimator>();

    [Header("Demo Speech Texts")]
    public string[] sampleTexts = {
        "老师，我有一个问题想请教。",
        "这道题的答案我知道！",
        "大家好，我是学生。",
        "我觉得这个很有趣。",
        "可以再说一次吗？",
        "Thank you, teacher!",
        "I think the answer is 42.",
    };

    [Header("Behavior Durations")]
    public float raiseHandDuration = 5f;
    public float takeNotesDuration = 6f;
    public float screamDuration = 2f;
    public float hitDeskDuration = 3f;
    public float pushDuration = 2f;
    public float lieDownDuration = 8f;

    [Header("State")]
    [SerializeField] int selectedIndex;
    int _textIndex;

    string _lastAction = "none";
    string _lastDiag = "";
    int _buttonClickCount;
    bool _startCompleted;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("[BehaviorDemo] ====== START ======");
        Debug.Log($"[BehaviorDemo] students list count at Start: {students.Count}");

        PurgeNullStudents();

        if (students.Count == 0)
        {
            Debug.Log("[BehaviorDemo] students list empty at Start — running DiscoverStudents");
            DiscoverStudents();
        }

        if (students.Count == 0)
        {
            Debug.LogWarning("[BehaviorDemo] No ProceduralBehaviorAnimator found in scene. Will retry via F9 or auto-retry in 1 second.");
            Invoke(nameof(RetryDiscover), 1f);
        }
        else
        {
            LogStudentInventory();
        }

        _startCompleted = true;
    }

    void RetryDiscover()
    {
        if (students.Count > 0) return;
        Debug.Log("[BehaviorDemo] Auto-retrying student discovery...");
        DiscoverStudents();
        if (students.Count > 0)
            LogStudentInventory();
        else
            Debug.LogWarning("[BehaviorDemo] Still no students found. Press F9 to retry, or ensure BehaviorDemoSetup ran.");
    }

    void DiscoverStudents()
    {
        var found = FindObjectsOfType<ProceduralBehaviorAnimator>();
        Debug.Log($"[BehaviorDemo] FindObjectsOfType<ProceduralBehaviorAnimator> returned {found.Length} results");
        students = found.OrderBy(s => s.gameObject.name).ToList();
        PurgeNullStudents();
    }

    void PurgeNullStudents()
    {
        int before = students.Count;
        students.RemoveAll(s => s == null);
        if (before != students.Count)
            Debug.LogWarning($"[BehaviorDemo] Purged {before - students.Count} null/destroyed student entries");
    }

    void LogStudentInventory()
    {
        Debug.Log($"[BehaviorDemo] ── Student Inventory ({students.Count}) ──");
        for (int i = 0; i < students.Count; i++)
        {
            var s = students[i];
            if (s == null) { Debug.Log($"  [{i}] NULL"); continue; }
            var go = s.gameObject;
            bool hasAnimator = s.animator != null;
            bool isHuman = hasAnimator && s.animator.isHuman;
            bool hasBSP = go.GetComponent<VRMBlendShapeProxy>() != null;
            bool hasFSS = go.GetComponent<FallbackSpeechService>() != null;
            bool hasAudio = go.GetComponent<AudioSource>() != null;
            int animCount = go.GetComponents<Animator>().Length;
            Debug.Log($"  [{i}] {go.name}: Animators={animCount} humanoid={isHuman} BSP={hasBSP} FSS={hasFSS} Audio={hasAudio}");
        }
        Debug.Log($"[BehaviorDemo] Selected: [{selectedIndex}] = {CurrentName}. Press H for controls.");
        PrintHelp();
    }

    void Update()
    {
        PurgeNullStudentsThrottled();
        HandleInput();
    }

    int _purgeFrame;
    void PurgeNullStudentsThrottled()
    {
        if (Time.frameCount - _purgeFrame < 300) return;
        _purgeFrame = Time.frameCount;
        PurgeNullStudents();
    }

    void HandleInput()
    {
        if (students.Count == 0) return;

        for (int i = 0; i < Mathf.Min(9, students.Count); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedIndex = i;
                Log($"Selected [{i}]: {CurrentName}");
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            selectedIndex = (selectedIndex + 1) % students.Count;
            Log($"Tab → [{selectedIndex}]: {CurrentName}");
        }

        if (Input.GetKeyDown(KeyCode.Q)) DoTrigger("Speak", TriggerSpeak);
        if (Input.GetKeyDown(KeyCode.W)) DoTrigger("RaiseHand", TriggerRaiseHand);
        if (Input.GetKeyDown(KeyCode.E)) DoTrigger("LieDown", TriggerLieDown);
        if (Input.GetKeyDown(KeyCode.R)) DoTrigger("TakeNotes", TriggerTakeNotes);
        if (Input.GetKeyDown(KeyCode.T)) DoTrigger("HitDesk", TriggerHitDesk);
        if (Input.GetKeyDown(KeyCode.Y)) DoTrigger("Scream", TriggerScream);
        if (Input.GetKeyDown(KeyCode.U)) DoTrigger("Push", TriggerPushClassmate);

        if (Input.GetKeyDown(KeyCode.S)) { StopCurrent(); _lastAction = "Stop"; }
        if (Input.GetKeyDown(KeyCode.X)) { StopAll(); _lastAction = "StopAll"; }
        if (Input.GetKeyDown(KeyCode.H)) PrintHelp();
        if (Input.GetKeyDown(KeyCode.F9)) { DiscoverStudents(); LogStudentInventory(); }
    }

    ProceduralBehaviorAnimator CurrentPBA
    {
        get
        {
            if (students.Count == 0) return null;
            int idx = Mathf.Clamp(selectedIndex, 0, students.Count - 1);
            var s = students[idx];
            return s;
        }
    }

    string CurrentName => CurrentPBA != null ? CurrentPBA.gameObject.name : "(none)";

    // ─── Trigger Wrapper ─────────────────────────────────────

    void DoTrigger(string name, System.Action action)
    {
        _buttonClickCount++;
        Debug.Log($"[BehaviorDemo] TRIGGER '{name}' on [{selectedIndex}] {CurrentName} (click #{_buttonClickCount})");
        action();
    }

    // ─── Behavior Triggers ──────────────────────────────────

    public void TriggerSpeak()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "Speak failed: no selected student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }

        string text = sampleTexts[_textIndex % sampleTexts.Length];
        _textIndex++;

        var speech = pba.GetComponent<FallbackSpeechService>();
        if (speech != null)
        {
            speech.Speak(text);
            _lastAction = $"Speak: {text}";
            Log($"Speak: \"{text}\"");
        }
        else
        {
            pba.PlaySpeakingMotion(3f);
            _lastAction = "Speak (motion only)";
            Log("Speak (no FallbackSpeechService — head motion only)");
        }
    }

    public void TriggerRaiseHand()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "RaiseHand failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayRaiseHand(raiseHandDuration);
        _lastAction = "Raise Hand";
        Log("Raise Hand");
    }

    public void TriggerLieDown()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "LieDown failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayLieDown(lieDownDuration);
        _lastAction = "Lie Down / Slump";
        Log("Lie Down / Slump");
    }

    public void TriggerTakeNotes()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "TakeNotes failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayTakeNotes(takeNotesDuration);
        _lastAction = "Take Notes";
        Log("Take Notes");
    }

    public void TriggerHitDesk()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "HitDesk failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayHitDesk(hitDeskDuration);
        _lastAction = "Hit Desk";
        Log("Hit Desk");
    }

    public void TriggerScream()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "Scream failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayScream(screamDuration);
        _lastAction = "Scream";

        var speech = pba.GetComponent<FallbackSpeechService>();
        if (speech != null)
            speech.SetExpression(BlendShapePreset.Angry, 0.9f, screamDuration);

        Log("Scream");
    }

    public void TriggerPushClassmate()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "Push failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }

        Transform neighbor = FindNearestNeighbor(pba.transform);
        pba.PlayPushClassmate(neighbor, pushDuration);
        _lastAction = $"Push → {(neighbor != null ? neighbor.gameObject.name : "none")}";
        Log(_lastAction);

        if (neighbor != null)
        {
            var neighborAnim = neighbor.GetComponent<ProceduralBehaviorAnimator>();
            if (neighborAnim != null)
                neighborAnim.PlayReaction("pushed", 1.5f);
        }
    }

    public void StopCurrent()
    {
        var pba = CurrentPBA;
        if (pba == null) return;
        pba.StopCurrentBehavior();
        var speech = pba.GetComponent<FallbackSpeechService>();
        if (speech != null) speech.StopSpeaking();
        Log("Stopped");
    }

    public void StopAll()
    {
        foreach (var s in students)
        {
            if (s == null) continue;
            s.StopCurrentBehavior();
            var speech = s.GetComponent<FallbackSpeechService>();
            if (speech != null) speech.StopSpeaking();
        }
        Log("All students stopped");
    }

    // ─── Neighbor Detection ─────────────────────────────────

    Transform FindNearestNeighbor(Transform self)
    {
        float minDist = float.MaxValue;
        Transform nearest = null;
        foreach (var s in students)
        {
            if (s == null || s.transform == self) continue;
            float dist = Vector3.Distance(self.position, s.transform.position);
            if (dist < minDist) { minDist = dist; nearest = s.transform; }
        }
        return nearest;
    }

    // ─── UI Overlay ─────────────────────────────────────────

    void OnGUI()
    {
        GUI.enabled = true;

        float w = 300, h = 420;
        GUILayout.BeginArea(new Rect(10, 10, w, h));

        var titleStyle = new GUIStyle(GUI.skin.label) {
            richText = true, fontSize = 15, fontStyle = FontStyle.Bold
        };
        var infoStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 };
        var smallStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 };

        GUILayout.Label("VR Classroom — Behavior Demo", titleStyle);

        int count = students.Count;
        string selName = CurrentName;

        GUILayout.Label($"<b>{selName}</b>  [{selectedIndex + 1}/{Mathf.Max(1, count)}]", infoStyle);

        if (!string.IsNullOrEmpty(_lastAction))
            GUILayout.Label($"<color=#AAF>{_lastAction}</color>", smallStyle);

        GUILayout.Space(6);

        // Student selector
        if (count > 1)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Mathf.Min(count, 8); i++)
            {
                var btnStyle = new GUIStyle(GUI.skin.button) { fontStyle = i == selectedIndex ? FontStyle.Bold : FontStyle.Normal };
                string label = i == selectedIndex ? $"[{i + 1}]" : $" {i + 1} ";
                if (GUILayout.Button(label, btnStyle, GUILayout.Width(38), GUILayout.Height(24)))
                {
                    selectedIndex = i;
                    _lastDiag = "";
                    _lastAction = "";
                    Log($"Selected [{i}]: {CurrentName}");
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        // Priority 1 — large buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Q  Speak", GUILayout.Height(30)))
        { _buttonClickCount++; TriggerSpeak(); }
        if (GUILayout.Button("W  Raise Hand", GUILayout.Height(30)))
        { _buttonClickCount++; TriggerRaiseHand(); }
        if (GUILayout.Button("E  Lie Down", GUILayout.Height(30)))
        { _buttonClickCount++; TriggerLieDown(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(2);

        // Priority 2 — smaller buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("R Notes")) { _buttonClickCount++; TriggerTakeNotes(); }
        if (GUILayout.Button("T Desk")) { _buttonClickCount++; TriggerHitDesk(); }
        if (GUILayout.Button("Y Scream")) { _buttonClickCount++; TriggerScream(); }
        if (GUILayout.Button("U Push")) { _buttonClickCount++; TriggerPushClassmate(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(2);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("S  Stop")) { _buttonClickCount++; StopCurrent(); _lastAction = "Stopped"; }
        if (GUILayout.Button("X  Stop All")) { _buttonClickCount++; StopAll(); _lastAction = "All stopped"; }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Tab next | 1-4 select | H help", smallStyle);

        if (!string.IsNullOrEmpty(_lastDiag))
            GUILayout.Label($"<color=red>{_lastDiag}</color>", smallStyle);

        if (count == 0)
        {
            GUILayout.Space(4);
            GUILayout.Label("<color=red><b>No students found</b></color>", infoStyle);
            if (GUILayout.Button("Re-discover")) { DiscoverStudents(); if (students.Count > 0) LogStudentInventory(); }
        }

        GUILayout.EndArea();
    }

    // ─── Logging ────────────────────────────────────────────

    void Log(string msg)
    {
        Debug.Log($"[BehaviorDemo] [{CurrentName}] {msg}");
    }

    void PrintHelp()
    {
        Debug.Log(@"[BehaviorDemo] ═══ KEYBOARD CONTROLS (Mac-friendly) ═══
  1-4 / Tab    Select student
  Q            Speak (fallback TTS)
  W            Raise Hand
  E            Lie Down / Slump
  R            Take Notes
  T            Hit Desk
  Y            Scream
  U            Push Classmate
  S            Stop current student
  X            Stop ALL students
  F9           Re-discover students
  H            Print this help");
    }
}

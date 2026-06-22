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
///   A          Ask Question (hand up + lean forward)
///   P          Clap hands
///   N          Touch Nose
///   E          Lie Down / Slump
///   D          Distracted (look away + slouch)
///   C          Talk to Nearby Classmate
///   L          Leave Seat (or Return to Seat if already away)
///   R          Take Notes
///   T          Hit Desk
///   Y          Scream
///   U          Push Classmate
///   V          Recover from lie down / slump
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
    public float raiseHandDuration  = 5f;
    public float askQuestionDuration = 5f;
    public float takeNotesDuration  = 6f;
    public float distractedDuration = 8f;
    public float talkClassmateDuration = 6f;
    public float leaveSeatMoveDuration  = 2.5f;
    public float returnSeatMoveDuration = 2f;
    public float screamDuration     = 2f;
    public float hitDeskDuration    = 3f;
    public float pushDuration       = 2f;
    public float lieDownDuration    = 8f;
    public float touchNoseDuration  = 3f;
    public float clapDuration       = 3f;
    public float clapBlendIn        = 0.35f;
    public float clapBlendOut       = 0.45f;
    public Vector2 clapSpeedRange   = new Vector2(0.92f, 1.08f);
    public Vector2 clapDurationJitter = new Vector2(-0.25f, 0.2f);
    public float touchNoseBlendIn   = 0.25f;
    public float touchNoseBlendOut  = 0.35f;

    [Header("Imported Behavior Clips")]
    public AnimationClip clappingClip;
    [Tooltip("Optional imported humanoid FBX clip for touch-nose. If empty, the procedural pose is used.")]
    public AnimationClip touchNoseClip;
    [Tooltip("Optional full-body humanoid FBX clip held after the student leaves the seat.")]
    public AnimationClip leaveSeatLayingClip;
    [Tooltip("Optional full-body humanoid FBX clip played before returning to the seat.")]
    public AnimationClip leaveSeatGettingUpClip;

    [Tooltip("How far from the seat the student walks when leaving (world units).")]
    public float leaveSeatDistance = 2.5f;
    [Tooltip("Student whose leave-seat path is forced to local right instead of random side selection.")]
    public string fixedRightLeaveStudentName = "莉莉";
    [Tooltip("Leave-seat distance for the fixed-right student, in world units.")]
    public float fixedRightLeaveDistance = 1f;
    [Tooltip("Vertical root offset applied while holding the leave-seat laying clip. Negative values lower the avatar toward the floor.")]
    public float leaveSeatLayingRootYOffset = -0.68f;

    [Header("State")]
    [SerializeField] int selectedIndex;
    [Tooltip("Runtime debug controls for local behavior testing. Keep off for the desktop teacher-facing build.")]
    public bool showDemoOverlay = false;
    [Tooltip("Prevents WASD camera movement from also triggering W/A/D behavior shortcuts.")]
    public bool suppressKeyboardShortcutsDuringCameraControl = true;
    public float cameraShortcutSuppressionGrace = 0.2f;

    /// <summary>Exposed so StudentBehaviorVisuals can read the current selection.</summary>
    public int SelectedIndex => selectedIndex;

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

        if (ShouldSuppressKeyboardShortcutsForCameraControl()) return;

        for (int i = 0; i < Mathf.Min(9, students.Count); i++)
        {
            if (DesktopInputBridge.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectStudentIndex(i);
            }
        }

        if (DesktopInputBridge.GetKeyDown(KeyCode.Tab))
        {
            SelectStudentIndex((selectedIndex + 1) % students.Count);
        }

        if (DesktopInputBridge.GetKeyDown(KeyCode.Q)) DoTrigger("Speak",           TriggerSpeak);
        if (DesktopInputBridge.GetKeyDown(KeyCode.W)) DoTrigger("RaiseHand",       TriggerRaiseHand);
        if (DesktopInputBridge.GetKeyDown(KeyCode.A)) DoTrigger("AskQuestion",     TriggerAskQuestion);
        if (DesktopInputBridge.GetKeyDown(KeyCode.P)) DoTrigger("Clap",            TriggerClap);
        if (DesktopInputBridge.GetKeyDown(KeyCode.N)) DoTrigger("TouchNose",       TriggerTouchNose);
        if (DesktopInputBridge.GetKeyDown(KeyCode.E)) DoTrigger("LieDown",         TriggerLieDown);
        if (DesktopInputBridge.GetKeyDown(KeyCode.D)) DoTrigger("Distracted",      TriggerDistracted);
        if (DesktopInputBridge.GetKeyDown(KeyCode.C)) DoTrigger("TalkClassmate",   TriggerTalkToClassmate);
        if (DesktopInputBridge.GetKeyDown(KeyCode.L)) DoTrigger("LeaveSeat",       TriggerLeaveSeat);
        if (DesktopInputBridge.GetKeyDown(KeyCode.R)) DoTrigger("TakeNotes",       TriggerTakeNotes);
        if (DesktopInputBridge.GetKeyDown(KeyCode.T)) DoTrigger("HitDesk",         TriggerHitDesk);
        if (DesktopInputBridge.GetKeyDown(KeyCode.Y)) DoTrigger("Scream",          TriggerScream);
        if (DesktopInputBridge.GetKeyDown(KeyCode.U)) DoTrigger("Push",            TriggerPushClassmate);
        if (DesktopInputBridge.GetKeyDown(KeyCode.V)) DoTrigger("RecoverLieDown",  TriggerRecoverLieDown);

        if (DesktopInputBridge.GetKeyDown(KeyCode.S)) { StopCurrent(); _lastAction = "Stop"; }
        if (DesktopInputBridge.GetKeyDown(KeyCode.X)) { StopAll(); _lastAction = "StopAll"; }
        if (DesktopInputBridge.GetKeyDown(KeyCode.H)) PrintHelp();
        if (DesktopInputBridge.GetKeyDown(KeyCode.F9)) { DiscoverStudents(); LogStudentInventory(); }
    }

    private bool ShouldSuppressKeyboardShortcutsForCameraControl()
    {
        if (!suppressKeyboardShortcutsDuringCameraControl) return false;

        return DesktopInputBridge.GetMouseButton(1) ||
               DemoCameraController.AnyCameraSuppressesBehaviorShortcuts(cameraShortcutSuppressionGrace);
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

    public void SelectStudentIndex(int index, bool syncDTTSelection = true)
    {
        if (students == null || students.Count == 0) return;

        int clampedIndex = Mathf.Clamp(index, 0, students.Count - 1);
        bool changed = selectedIndex != clampedIndex;
        selectedIndex = clampedIndex;
        _lastDiag = "";
        _lastAction = "";
        if (changed || syncDTTSelection)
        {
            Log($"Selected [{selectedIndex}]: {CurrentName}");
        }

        if (syncDTTSelection && CurrentPBA != null)
        {
            DTTChildGazeSimulator.SelectChildByGameObject(CurrentPBA.gameObject);
        }
    }

    public void SelectStudentByGameObject(GameObject studentRoot, bool syncDTTSelection = true)
    {
        if (studentRoot == null || students == null || students.Count == 0) return;

        for (int i = 0; i < students.Count; i++)
        {
            ProceduralBehaviorAnimator student = students[i];
            if (student == null) continue;

            if (student.gameObject == studentRoot
                || studentRoot.transform.IsChildOf(student.transform)
                || student.transform.IsChildOf(studentRoot.transform))
            {
                SelectStudentIndex(i, syncDTTSelection);
                return;
            }
        }
    }

    public ProceduralBehaviorAnimator FindStudentByDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName) || students == null) return null;

        for (int i = 0; i < students.Count; i++)
        {
            ProceduralBehaviorAnimator student = students[i];
            if (student != null && student.gameObject.name == displayName)
                return student;
        }

        return null;
    }

    public bool IsLeaveSeatBehaviorActive(string displayName)
    {
        ProceduralBehaviorAnimator pba = FindStudentByDisplayName(displayName);
        return pba != null
               && pba.IsBehaviorActive
               && (pba.CurrentBehaviorName == "离座" || pba.CurrentBehaviorName == "回座位");
    }

    public bool TriggerLeaveSeatForStudent(string displayName)
    {
        ProceduralBehaviorAnimator pba = FindStudentByDisplayName(displayName);
        if (pba == null || IsLeaveSeatBehaviorActive(displayName)) return false;

        Vector3 awayDir;
        float moveDistance;
        GetLeaveSeatTarget(pba, out awayDir, out moveDistance);
        Vector3 target = pba.transform.position + awayDir * moveDistance;
        pba.PlayLeaveSeat(target, leaveSeatMoveDuration, leaveSeatLayingClip, leaveSeatLayingRootYOffset);
        Log($"Scripted leave seat: {displayName} -> {target}");
        return true;
    }

    public bool ReturnLeaveSeatStudent(string displayName)
    {
        ProceduralBehaviorAnimator pba = FindStudentByDisplayName(displayName);
        if (pba == null || !pba.IsBehaviorActive || pba.CurrentBehaviorName != "离座") return false;

        pba.PlayReturnToSeat(returnSeatMoveDuration, leaveSeatGettingUpClip);
        Log($"Scripted return to seat: {displayName}");
        return true;
    }

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
        _lastAction = "举手";
        Log("Raise Hand");
    }

    public void TriggerAskQuestion()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "AskQuestion failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayAskQuestion(askQuestionDuration);
        _lastAction = "举手提问";
        Log("Ask Question");

        // Optionally trigger speech alongside the gesture
        var speech = pba.GetComponent<FallbackSpeechService>();
        if (speech != null)
        {
            string[] questionTexts = {
                "老师，我有一个问题！",
                "这道题我不明白，可以再讲一次吗？",
                "老师，请问这个答案是对的吗？",
                "我想举手回答！"
            };
            speech.SpeakWithoutMotion(questionTexts[Random.Range(0, questionTexts.Length)], askQuestionDuration * 0.5f);
        }
    }

    public void TriggerClap()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "Clap failed: no selected student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        if (clappingClip == null) { _lastDiag = "Clap failed: clappingClip not assigned"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }

        float duration = Mathf.Max(1f, clapDuration + Random.Range(clapDurationJitter.x, clapDurationJitter.y));
        float speed = Random.Range(clapSpeedRange.x, clapSpeedRange.y);
        pba.PlayUpperBodyAnimationClip(clappingClip, duration, "拍手", clapBlendIn, clapBlendOut, speed);
        _lastAction = "拍手";
        Log($"Clap ({duration:F1}s, speed {speed:F2}x)");
    }

    public void TriggerTouchNose()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "TouchNose failed: no selected student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }

        if (touchNoseClip != null)
        {
            pba.PlayUpperBodyAnimationClip(
                touchNoseClip,
                touchNoseDuration,
                "摸鼻子",
                touchNoseBlendIn,
                touchNoseBlendOut,
                1f);
        }
        else
        {
            pba.PlayTouchNose(touchNoseDuration);
        }

        _lastAction = "摸鼻子";
        Log("Touch Nose");
    }

    public void TriggerDistracted()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "Distracted failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayDistracted(distractedDuration);
        _lastAction = "走神";
        Log("Distracted");
    }

    public void TriggerTalkToClassmate()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "TalkClassmate failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }

        Transform neighbor = FindNearestNeighbor(pba.transform);
        pba.PlayTalkToClassmate(neighbor, talkClassmateDuration);
        _lastAction = $"说话→{(neighbor != null ? neighbor.gameObject.name : "none")}";
        Log(_lastAction);

        // Neighbor faces the speaker and listens with attentive nodding
        if (neighbor != null)
        {
            var neighborPba = neighbor.GetComponent<ProceduralBehaviorAnimator>();
            if (neighborPba != null)
                neighborPba.PlayListenToClassmate(pba.transform, talkClassmateDuration);
        }

        // Trigger fallback speech on the initiator
        var speech = pba.GetComponent<FallbackSpeechService>();
        if (speech != null)
        {
            string[] chatTexts = {
                "你作业写完了吗？",
                "这道题你怎么做的？",
                "等一下一起去吃饭吧！",
                "刚才老师说啥了？"
            };
            speech.SpeakWithoutMotion(chatTexts[Random.Range(0, chatTexts.Length)], talkClassmateDuration * 0.4f);
        }
    }

    public void TriggerLeaveSeat()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "LeaveSeat failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }

        if (pba.seatPositionCaptured && pba.IsBehaviorActive && pba.CurrentBehaviorName == "离座")
        {
            // Already away — return to seat
            pba.PlayReturnToSeat(returnSeatMoveDuration, leaveSeatGettingUpClip);
            _lastAction = "回座位";
            Log("Return To Seat");
        }
        else
        {
            Vector3 awayDir;
            float moveDistance;
            GetLeaveSeatTarget(pba, out awayDir, out moveDistance);
            Vector3 target = pba.transform.position + awayDir * moveDistance;

            pba.PlayLeaveSeat(target, leaveSeatMoveDuration, leaveSeatLayingClip, leaveSeatLayingRootYOffset);
            _lastAction = "离座跑开";
            Log($"Leave Seat → {target}");
        }
    }

    void GetLeaveSeatTarget(ProceduralBehaviorAnimator pba, out Vector3 awayDir, out float moveDistance)
    {
        if (pba.gameObject.name == fixedRightLeaveStudentName)
        {
            awayDir = pba.transform.right;
            moveDistance = fixedRightLeaveDistance;
        }
        else
        {
            // Walk sideways into the aisle (avoids clipping through desk behind).
            // Students face forward; backward = into the next desk row.
            float side = Random.value > 0.5f ? 1f : -1f;
            awayDir = pba.transform.right * side + pba.transform.forward * 0.3f;
            moveDistance = leaveSeatDistance;
        }

        awayDir.y = 0;
        awayDir.Normalize();
    }

    public void TriggerLieDown()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "LieDown failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayLieDownHold();
        _lastAction = "趴桌保持";
        Log("Lie Down / Slump Hold");
    }

    public void TriggerRecoverLieDown()
    {
        var pba = CurrentPBA;
        if (pba == null) { _lastDiag = "RecoverLieDown failed: no student"; Debug.LogWarning($"[BehaviorDemo] {_lastDiag}"); return; }
        pba.PlayRecoverFromLieDown();
        _lastAction = "趴桌恢复";
        Log("Recover From Lie Down / Slump");
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
            {
                Vector3 pushDir = (neighbor.position - pba.transform.position).normalized;
                neighborAnim.PlayPushedReaction(pushDir, 1.5f);
            }
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
        if (!showDemoOverlay) return;

        GUI.enabled = true;

        float w = 320, h = 520;
        GUILayout.BeginArea(new Rect(10, 10, w, h));

        var titleStyle = new GUIStyle(GUI.skin.label) {
            richText = true, fontSize = 15, fontStyle = FontStyle.Bold
        };
        var infoStyle  = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 };
        var smallStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 };

        GUILayout.Label("VR Classroom — Behavior Demo", titleStyle);

        int count = students.Count;
        string selName = CurrentName;

        // Current behavior status
        var pba = CurrentPBA;
        string behaviorLabel = (pba != null && pba.IsBehaviorActive && !string.IsNullOrEmpty(pba.CurrentBehaviorName))
            ? $"<color=#FFD060>{pba.CurrentBehaviorName}</color>"
            : "<color=#888>idle</color>";
        GUILayout.Label($"<b>{selName}</b>  [{selectedIndex + 1}/{Mathf.Max(1, count)}]  {behaviorLabel}", infoStyle);

        if (!string.IsNullOrEmpty(_lastAction))
            GUILayout.Label($"<color=#AAF>{_lastAction}</color>", smallStyle);

        GUILayout.Space(5);

        // Student selector
        if (count > 1)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Mathf.Min(count, 8); i++)
            {
                var btnStyle = new GUIStyle(GUI.skin.button) { fontStyle = i == selectedIndex ? FontStyle.Bold : FontStyle.Normal };
                string label = i == selectedIndex ? $"[{i + 1}]" : $" {i + 1} ";
                if (GUILayout.Button(label, btnStyle, GUILayout.Width(36), GUILayout.Height(24)))
                {
                    SelectStudentIndex(i);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        // ── Row 1: attention-seeking behaviors ──
        GUILayout.Label("<color=#aef><b>注意力 / 提问</b></color>", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Q  说话",  GUILayout.Height(28))) { _buttonClickCount++; TriggerSpeak(); }
        if (GUILayout.Button("W  举手",  GUILayout.Height(28))) { _buttonClickCount++; TriggerRaiseHand(); }
        if (GUILayout.Button("A  提问",  GUILayout.Height(28))) { _buttonClickCount++; TriggerAskQuestion(); }
        if (GUILayout.Button("P  拍手",  GUILayout.Height(28))) { _buttonClickCount++; TriggerClap(); }
        if (GUILayout.Button("N  摸鼻子", GUILayout.Height(28))) { _buttonClickCount++; TriggerTouchNose(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(2);

        // ── Row 2: disruptive behaviors ──
        GUILayout.Label("<color=#fca><b>干扰行为</b></color>", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("D  走神",  GUILayout.Height(28))) { _buttonClickCount++; TriggerDistracted(); }
        if (GUILayout.Button("C  聊天",  GUILayout.Height(28))) { _buttonClickCount++; TriggerTalkToClassmate(); }
        if (GUILayout.Button("L  离座",  GUILayout.Height(28))) { _buttonClickCount++; TriggerLeaveSeat(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(2);

        // ── Row 3: extreme behaviors ──
        GUILayout.Label("<color=#faa><b>极端行为</b></color>", smallStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("E  趴桌")) { _buttonClickCount++; TriggerLieDown(); }
        if (GUILayout.Button("V  恢复")) { _buttonClickCount++; TriggerRecoverLieDown(); }
        if (GUILayout.Button("R  记笔记")) { _buttonClickCount++; TriggerTakeNotes(); }
        if (GUILayout.Button("T  拍桌")) { _buttonClickCount++; TriggerHitDesk(); }
        if (GUILayout.Button("Y  尖叫")) { _buttonClickCount++; TriggerScream(); }
        if (GUILayout.Button("U  推人")) { _buttonClickCount++; TriggerPushClassmate(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(3);

        // ── Stop ──
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("S  停止当前", GUILayout.Height(26))) { _buttonClickCount++; StopCurrent(); _lastAction = "已停止"; }
        if (GUILayout.Button("X  全部停止", GUILayout.Height(26))) { _buttonClickCount++; StopAll(); _lastAction = "全部停止"; }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Tab:下一个 | 1-4:选学生 | H:帮助", smallStyle);
        GUILayout.Label("右键拖动=视角 | 右键+WASD=移动 | Shift=加速", smallStyle);

        if (!string.IsNullOrEmpty(_lastDiag))
            GUILayout.Label($"<color=red>{_lastDiag}</color>", smallStyle);

        if (count == 0)
        {
            GUILayout.Space(4);
            GUILayout.Label("<color=red><b>未找到学生</b></color>", infoStyle);
            if (GUILayout.Button("重新搜索")) { DiscoverStudents(); if (students.Count > 0) LogStudentInventory(); }
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
        Debug.Log(@"[BehaviorDemo] ═══ CONTROLS ═══
  1-4 / Tab    Select student

  ── Attention / Question ──
  Q            说话 (Speak)
  W            举手 (Raise Hand)
  A            举手提问 (Ask Question)
  P            拍手 3 秒 (Clap)
  N            摸鼻子 (Touch Nose)

  ── Disruptive ──
  D            走神 (Distracted)
  C            和同学聊天 (Talk to Classmate)
  L            离座 / 回座位 (Leave / Return to Seat)

  ── Extreme ──
  E            趴桌并保持 (Lie Down / Slump Hold)
  V            趴桌恢复 (Recover From Slump)
  R            记笔记 (Take Notes)
  T            拍桌 (Hit Desk)
  Y            尖叫 (Scream)
  U            推同学 (Push Classmate)

  S            Stop current behavior
  X            Stop ALL

  Right-click + drag   Camera look
  Right-click + WASD   Camera move
  Right-click + Q/E    Camera up/down
  Shift                Camera boost

  F9   Re-discover students
  H    Print this help");
    }
}

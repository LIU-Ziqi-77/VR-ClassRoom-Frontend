using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DTTGazePhase
{
    TrialIdle,
    StimulusPresented,
    Prompted,
    Responding,
    Reinforced,
    OffTask,
    Avoidant
}

public enum DTTGazeTarget
{
    TeacherFace,
    TeacherHands,
    TeachingStimulus,
    Desk,
    Reinforcer,
    LeftDistractor,
    RightDistractor,
    Ground,
    AwayFromTeacher
}

/// <summary>
/// Lightweight gaze state machine for one-on-one DTT teaching demos.
/// It models stage-dependent looking, off-task scanning, and gaze avoidance
/// without requiring a backend agent or changes to avatar rigs.
/// </summary>
public class DTTChildGazeSimulator : MonoBehaviour
{
    [Header("References")]
    public EyeController eyeController;
    public Transform teacherFaceTarget;
    public Transform teacherHandsTarget;
    public Transform teachingStimulusTarget;
    public Transform deskTarget;
    public Transform reinforcerTarget;
    public Transform leftDistractorTarget;
    public Transform rightDistractorTarget;
    public Transform groundTarget;

    [Header("Teacher Camera Defaults")]
    [Tooltip("In VR, the teacher is represented by the headset camera.")]
    public bool useMainCameraAsTeacher = true;
    public Transform teacherCameraOverride;
    [Tooltip("Camera-local fallback for teacher hands when no explicit target exists.")]
    public Vector3 teacherHandsCameraOffset = new Vector3(0f, -0.45f, 0.45f);
    [Tooltip("Camera-local fallback for a card/toy/stimulus held in front of the teacher.")]
    public Vector3 stimulusCameraOffset = new Vector3(0f, -0.35f, 0.75f);
    [Tooltip("Camera-local fallback for a reinforcer held slightly to the side.")]
    public Vector3 reinforcerCameraOffset = new Vector3(0.25f, -0.25f, 0.65f);

    [Header("Simulation")]
    public bool simulateOnStart = true;
    public bool keyboardTesting = true;
    public bool logKeyboardPhaseChanges = true;
    public bool keyboardControlsOnlySelectedChild = true;
    public bool returnDeselectedChildToIdle = true;
    public DTTGazePhase currentPhase = DTTGazePhase.TrialIdle;
    public Vector2 gazeHoldDurationRange = new Vector2(1.1f, 2.8f);
    public Vector2 offTaskGazeHoldDurationRange = new Vector2(0.75f, 1.8f);
    public Vector2 responseLatencyRange = new Vector2(0.25f, 0.75f);

    [Header("Child Profile")]
    [Range(0f, 1f)]
    public float socialAttention = 0.35f;
    [Range(0f, 1f)]
    public float stimulusInterest = 0.45f;
    [Range(0f, 1f)]
    public float distractibility = 0.45f;
    [Range(0f, 1f)]
    public float gazeAvoidance = 0.25f;
    public bool randomizeProfileOnStart = true;

    [Header("Fallback Local Points")]
    public Vector3 teacherFaceFallback = new Vector3(0f, 1.4f, 2.2f);
    public Vector3 teacherHandsFallback = new Vector3(0f, 0.85f, 1.5f);
    public Vector3 stimulusFallback = new Vector3(0f, 0.75f, 0.9f);
    public Vector3 deskFallback = new Vector3(0f, 0.45f, 0.55f);
    public Vector3 reinforcerFallback = new Vector3(0.35f, 0.75f, 0.8f);
    public Vector3 leftDistractorFallback = new Vector3(-1.2f, 1.0f, 0.5f);
    public Vector3 rightDistractorFallback = new Vector3(1.2f, 1.0f, 0.5f);
    public Vector3 groundFallback = new Vector3(0.25f, -0.25f, 0.8f);
    public Vector3 awayFallback = new Vector3(1.4f, 0.85f, -0.2f);

    private Coroutine gazeRoutine;
    private DTTGazeTarget activeGazeTarget;
    private bool hasActiveGazeTarget;
    private static readonly List<DTTChildGazeSimulator> RegisteredSimulators = new List<DTTChildGazeSimulator>();
    private static int selectedChildIndex;

    void Awake()
    {
        if (eyeController == null)
        {
            eyeController = GetComponent<EyeController>();
        }
    }

    void Start()
    {
        if (randomizeProfileOnStart)
        {
            RandomizeProfile();
        }

        if (simulateOnStart)
        {
            StartSimulation();
        }
    }

    void Update()
    {
        RefreshDynamicFallbackTarget();

        if (!keyboardTesting) return;

        HandleSelectionKeys();
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SetPhaseFromKeyboard(DTTGazePhase.TrialIdle);
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) SetPhaseFromKeyboard(DTTGazePhase.StimulusPresented);
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) SetPhaseFromKeyboard(DTTGazePhase.Prompted);
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) SetPhaseFromKeyboard(DTTGazePhase.Responding);
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) SetPhaseFromKeyboard(DTTGazePhase.Reinforced);
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) SetPhaseFromKeyboard(DTTGazePhase.OffTask);
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) SetPhaseFromKeyboard(DTTGazePhase.Avoidant);
    }

    void OnEnable()
    {
        RegisterSimulator(this);

        if (simulateOnStart && Application.isPlaying && gazeRoutine == null)
        {
            StartSimulation();
        }
    }

    void OnDisable()
    {
        StopSimulation();
        UnregisterSimulator(this);
    }

    public void StartSimulation()
    {
        if (gazeRoutine != null)
        {
            StopCoroutine(gazeRoutine);
        }

        gazeRoutine = StartCoroutine(GazeLoop());
    }

    public void StopSimulation()
    {
        if (gazeRoutine != null)
        {
            StopCoroutine(gazeRoutine);
            gazeRoutine = null;
        }
    }

    public void SetPhase(DTTGazePhase phase)
    {
        currentPhase = phase;

        if (Application.isPlaying && gazeRoutine == null)
        {
            StartSimulation();
        }
    }

    private void SetPhaseFromKeyboard(DTTGazePhase phase)
    {
        if (keyboardControlsOnlySelectedChild && !IsSelectedChild())
        {
            return;
        }

        SetPhase(phase);

        if (logKeyboardPhaseChanges)
        {
            Debug.Log($"[{name}] DTT gaze phase set to {phase}. Use 1-3 to select, 4-0 for DTT gaze phases.");
        }
    }

    private void HandleSelectionKeys()
    {
        for (int i = 0; i < Mathf.Min(9, RegisteredSimulators.Count); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                SelectChild(i);
            }
        }
    }

    private bool IsSelectedChild()
    {
        RefreshRegisteredOrder();
        int index = RegisteredSimulators.IndexOf(this);
        return index >= 0 && index == selectedChildIndex;
    }

    private static void SelectChild(int index)
    {
        RefreshRegisteredOrder();
        if (RegisteredSimulators.Count == 0) return;

        int clampedIndex = Mathf.Clamp(index, 0, RegisteredSimulators.Count - 1);
        DTTChildGazeSimulator previous = selectedChildIndex >= 0 && selectedChildIndex < RegisteredSimulators.Count
            ? RegisteredSimulators[selectedChildIndex]
            : null;

        selectedChildIndex = clampedIndex;
        DTTChildGazeSimulator selected = RegisteredSimulators[selectedChildIndex];

        if (DTTTeachingAidManager.Instance != null)
        {
            DTTTeachingAidManager.Instance.SelectStudentForGazeSimulator(selected);
        }

        if (previous != null && previous != selected && previous.returnDeselectedChildToIdle)
        {
            previous.SetPhase(DTTGazePhase.TrialIdle);
        }
    }

    public static void SelectChildByGameObject(GameObject childRoot)
    {
        if (childRoot == null) return;

        RefreshRegisteredOrder();
        for (int i = 0; i < RegisteredSimulators.Count; i++)
        {
            DTTChildGazeSimulator simulator = RegisteredSimulators[i];
            if (simulator == null) continue;

            if (simulator.gameObject == childRoot || childRoot.transform.IsChildOf(simulator.transform) || simulator.transform.IsChildOf(childRoot.transform))
            {
                SelectChild(i);
                return;
            }
        }
    }

    private static void RegisterSimulator(DTTChildGazeSimulator simulator)
    {
        if (RegisteredSimulators.Contains(simulator)) return;

        RegisteredSimulators.Add(simulator);
        RefreshRegisteredOrder();
        selectedChildIndex = Mathf.Clamp(selectedChildIndex, 0, Mathf.Max(0, RegisteredSimulators.Count - 1));
    }

    private static void UnregisterSimulator(DTTChildGazeSimulator simulator)
    {
        RegisteredSimulators.Remove(simulator);
        selectedChildIndex = Mathf.Clamp(selectedChildIndex, 0, Mathf.Max(0, RegisteredSimulators.Count - 1));
    }

    private static void RefreshRegisteredOrder()
    {
        RegisteredSimulators.RemoveAll(s => s == null);
        RegisteredSimulators.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
    }

    public void SetTrialIdle()
    {
        SetPhase(DTTGazePhase.TrialIdle);
    }

    public void PresentStimulus()
    {
        SetPhase(DTTGazePhase.StimulusPresented);
    }

    public void PromptChild()
    {
        SetPhase(DTTGazePhase.Prompted);
    }

    public void ChildResponding()
    {
        SetPhase(DTTGazePhase.Responding);
    }

    public void ReinforceChild()
    {
        SetPhase(DTTGazePhase.Reinforced);
    }

    public void MarkOffTask()
    {
        SetPhase(DTTGazePhase.OffTask);
    }

    public void MarkAvoidant()
    {
        SetPhase(DTTGazePhase.Avoidant);
    }

    private IEnumerator GazeLoop()
    {
        while (true)
        {
            if (eyeController == null)
            {
                yield return null;
                continue;
            }

            DTTGazeTarget target = PickTargetForPhase(currentPhase);
            yield return new WaitForSeconds(Random.Range(responseLatencyRange.x, responseLatencyRange.y));
            LookAt(target);

            Vector2 holdRange = currentPhase == DTTGazePhase.OffTask
                ? offTaskGazeHoldDurationRange
                : gazeHoldDurationRange;
            yield return new WaitForSeconds(Random.Range(holdRange.x, holdRange.y));
        }
    }

    private void RandomizeProfile()
    {
        socialAttention = Mathf.Clamp01(socialAttention + Random.Range(-0.12f, 0.12f));
        stimulusInterest = Mathf.Clamp01(stimulusInterest + Random.Range(-0.12f, 0.12f));
        distractibility = Mathf.Clamp01(distractibility + Random.Range(-0.15f, 0.15f));
        gazeAvoidance = Mathf.Clamp01(gazeAvoidance + Random.Range(-0.12f, 0.12f));
    }

    private DTTGazeTarget PickTargetForPhase(DTTGazePhase phase)
    {
        switch (phase)
        {
            case DTTGazePhase.StimulusPresented:
                return PickWeighted(
                    DTTGazeTarget.TeachingStimulus, 0.35f + stimulusInterest * 0.4f,
                    DTTGazeTarget.TeacherFace, 0.12f + socialAttention * 0.25f,
                    DTTGazeTarget.TeacherHands, 0.18f,
                    DTTGazeTarget.Desk, 0.12f,
                    PickDistractor(), distractibility * 0.25f,
                    DTTGazeTarget.AwayFromTeacher, gazeAvoidance * 0.2f);

            case DTTGazePhase.Prompted:
                return PickWeighted(
                    DTTGazeTarget.TeacherHands, 0.25f,
                    DTTGazeTarget.TeachingStimulus, 0.3f + stimulusInterest * 0.35f,
                    DTTGazeTarget.TeacherFace, 0.12f + socialAttention * 0.2f,
                    DTTGazeTarget.Desk, 0.08f,
                    PickDistractor(), distractibility * 0.15f,
                    DTTGazeTarget.AwayFromTeacher, gazeAvoidance * 0.15f);

            case DTTGazePhase.Responding:
                return PickWeighted(
                    DTTGazeTarget.TeachingStimulus, 0.28f + stimulusInterest * 0.25f,
                    DTTGazeTarget.TeacherFace, 0.16f + socialAttention * 0.22f,
                    DTTGazeTarget.Desk, 0.18f,
                    DTTGazeTarget.TeacherHands, 0.12f,
                    PickDistractor(), distractibility * 0.18f,
                    DTTGazeTarget.AwayFromTeacher, gazeAvoidance * 0.12f);

            case DTTGazePhase.Reinforced:
                return PickWeighted(
                    DTTGazeTarget.Reinforcer, 0.45f,
                    DTTGazeTarget.TeacherHands, 0.2f,
                    DTTGazeTarget.TeacherFace, 0.1f + socialAttention * 0.2f,
                    DTTGazeTarget.TeachingStimulus, 0.08f,
                    PickDistractor(), distractibility * 0.12f,
                    DTTGazeTarget.AwayFromTeacher, gazeAvoidance * 0.1f);

            case DTTGazePhase.OffTask:
                return PickWeighted(
                    PickDistractor(), 0.38f + distractibility * 0.4f,
                    DTTGazeTarget.Desk, 0.2f,
                    DTTGazeTarget.Ground, 0.14f,
                    DTTGazeTarget.AwayFromTeacher, 0.12f + gazeAvoidance * 0.2f,
                    DTTGazeTarget.TeachingStimulus, stimulusInterest * 0.08f,
                    DTTGazeTarget.TeacherFace, socialAttention * 0.06f);

            case DTTGazePhase.Avoidant:
                return PickWeighted(
                    DTTGazeTarget.AwayFromTeacher, 0.35f + gazeAvoidance * 0.35f,
                    DTTGazeTarget.Desk, 0.25f,
                    DTTGazeTarget.Ground, 0.2f,
                    PickDistractor(), distractibility * 0.15f,
                    DTTGazeTarget.TeachingStimulus, stimulusInterest * 0.08f,
                    DTTGazeTarget.TeacherFace, socialAttention * 0.04f);

            case DTTGazePhase.TrialIdle:
            default:
                return PickWeighted(
                    DTTGazeTarget.Desk, 0.25f,
                    DTTGazeTarget.TeachingStimulus, 0.12f + stimulusInterest * 0.18f,
                    DTTGazeTarget.TeacherFace, 0.08f + socialAttention * 0.18f,
                    DTTGazeTarget.TeacherHands, 0.08f,
                    PickDistractor(), 0.12f + distractibility * 0.25f,
                    DTTGazeTarget.Ground, 0.08f,
                    DTTGazeTarget.AwayFromTeacher, gazeAvoidance * 0.14f);
        }
    }

    private DTTGazeTarget PickDistractor()
    {
        return Random.value < 0.5f ? DTTGazeTarget.LeftDistractor : DTTGazeTarget.RightDistractor;
    }

    private DTTGazeTarget PickWeighted(
        DTTGazeTarget targetA, float weightA,
        DTTGazeTarget targetB, float weightB,
        DTTGazeTarget targetC, float weightC,
        DTTGazeTarget targetD, float weightD,
        DTTGazeTarget targetE, float weightE,
        DTTGazeTarget targetF, float weightF)
    {
        float total = Mathf.Max(0f, weightA)
            + Mathf.Max(0f, weightB)
            + Mathf.Max(0f, weightC)
            + Mathf.Max(0f, weightD)
            + Mathf.Max(0f, weightE)
            + Mathf.Max(0f, weightF);

        if (total <= 0f)
        {
            return DTTGazeTarget.Desk;
        }

        float roll = Random.Range(0f, total);
        if ((roll -= Mathf.Max(0f, weightA)) <= 0f) return targetA;
        if ((roll -= Mathf.Max(0f, weightB)) <= 0f) return targetB;
        if ((roll -= Mathf.Max(0f, weightC)) <= 0f) return targetC;
        if ((roll -= Mathf.Max(0f, weightD)) <= 0f) return targetD;
        if ((roll -= Mathf.Max(0f, weightE)) <= 0f) return targetE;
        return targetF;
    }

    private DTTGazeTarget PickWeighted(
        DTTGazeTarget targetA, float weightA,
        DTTGazeTarget targetB, float weightB,
        DTTGazeTarget targetC, float weightC,
        DTTGazeTarget targetD, float weightD,
        DTTGazeTarget targetE, float weightE,
        DTTGazeTarget targetF, float weightF,
        DTTGazeTarget targetG, float weightG)
    {
        float total = Mathf.Max(0f, weightA)
            + Mathf.Max(0f, weightB)
            + Mathf.Max(0f, weightC)
            + Mathf.Max(0f, weightD)
            + Mathf.Max(0f, weightE)
            + Mathf.Max(0f, weightF)
            + Mathf.Max(0f, weightG);

        if (total <= 0f)
        {
            return DTTGazeTarget.Desk;
        }

        float roll = Random.Range(0f, total);
        if ((roll -= Mathf.Max(0f, weightA)) <= 0f) return targetA;
        if ((roll -= Mathf.Max(0f, weightB)) <= 0f) return targetB;
        if ((roll -= Mathf.Max(0f, weightC)) <= 0f) return targetC;
        if ((roll -= Mathf.Max(0f, weightD)) <= 0f) return targetD;
        if ((roll -= Mathf.Max(0f, weightE)) <= 0f) return targetE;
        if ((roll -= Mathf.Max(0f, weightF)) <= 0f) return targetF;
        return targetG;
    }

    private void LookAt(DTTGazeTarget target)
    {
        activeGazeTarget = target;
        hasActiveGazeTarget = true;

        Transform configuredTarget = GetConfiguredTarget(target);
        if (configuredTarget != null)
        {
            eyeController.LookAtTransform(configuredTarget);
            return;
        }

        eyeController.LookAtPosition(GetFallbackWorldPosition(target));
    }

    private void RefreshDynamicFallbackTarget()
    {
        if (!hasActiveGazeTarget || eyeController == null) return;
        if (GetConfiguredTarget(activeGazeTarget) != null) return;

        Vector3 cameraDerivedPosition;
        if (TryGetCameraDerivedPosition(activeGazeTarget, out cameraDerivedPosition))
        {
            eyeController.LookAtPosition(cameraDerivedPosition);
        }
    }

    private Transform GetConfiguredTarget(DTTGazeTarget target)
    {
        switch (target)
        {
            case DTTGazeTarget.TeacherFace:
                return teacherFaceTarget != null ? teacherFaceTarget : GetTeacherCameraTransform();
            case DTTGazeTarget.TeacherHands:
                return teacherHandsTarget;
            case DTTGazeTarget.TeachingStimulus:
                Transform currentAidTarget = DTTTeachingAidManager.Instance != null
                    ? DTTTeachingAidManager.Instance.GetCurrentTeachingStimulusTarget()
                    : null;
                return currentAidTarget != null ? currentAidTarget : teachingStimulusTarget;
            case DTTGazeTarget.Desk:
                return deskTarget;
            case DTTGazeTarget.Reinforcer:
                return reinforcerTarget;
            case DTTGazeTarget.LeftDistractor:
                return leftDistractorTarget;
            case DTTGazeTarget.RightDistractor:
                return rightDistractorTarget;
            case DTTGazeTarget.Ground:
                return groundTarget;
            default:
                return null;
        }
    }

    private Vector3 GetFallbackWorldPosition(DTTGazeTarget target)
    {
        Vector3 cameraDerivedPosition;
        if (TryGetCameraDerivedPosition(target, out cameraDerivedPosition))
        {
            return cameraDerivedPosition;
        }

        Vector3 localPoint;
        switch (target)
        {
            case DTTGazeTarget.TeacherFace:
                localPoint = teacherFaceFallback;
                break;
            case DTTGazeTarget.TeacherHands:
                localPoint = teacherHandsFallback;
                break;
            case DTTGazeTarget.TeachingStimulus:
                localPoint = stimulusFallback;
                break;
            case DTTGazeTarget.Desk:
                localPoint = deskFallback;
                break;
            case DTTGazeTarget.Reinforcer:
                localPoint = reinforcerFallback;
                break;
            case DTTGazeTarget.LeftDistractor:
                localPoint = leftDistractorFallback;
                break;
            case DTTGazeTarget.RightDistractor:
                localPoint = rightDistractorFallback;
                break;
            case DTTGazeTarget.Ground:
                localPoint = groundFallback;
                break;
            case DTTGazeTarget.AwayFromTeacher:
            default:
                localPoint = awayFallback;
                break;
        }

        return transform.TransformPoint(localPoint);
    }

    private Transform GetTeacherCameraTransform()
    {
        if (!useMainCameraAsTeacher)
        {
            return null;
        }

        if (teacherCameraOverride != null)
        {
            return teacherCameraOverride;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    private bool TryGetCameraDerivedPosition(DTTGazeTarget target, out Vector3 position)
    {
        Transform teacherCamera = GetTeacherCameraTransform();
        if (teacherCamera == null)
        {
            position = Vector3.zero;
            return false;
        }

        switch (target)
        {
            case DTTGazeTarget.TeacherFace:
                position = teacherCamera.position;
                return true;
            case DTTGazeTarget.TeacherHands:
                position = teacherCamera.TransformPoint(teacherHandsCameraOffset);
                return true;
            case DTTGazeTarget.TeachingStimulus:
                position = teacherCamera.TransformPoint(stimulusCameraOffset);
                return true;
            case DTTGazeTarget.Reinforcer:
                position = teacherCamera.TransformPoint(reinforcerCameraOffset);
                return true;
            default:
                position = Vector3.zero;
                return false;
        }
    }
}

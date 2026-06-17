using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public enum DTTScenarioType
{
    DirectCorrect,
    HalfPromptThenCorrect,
    FullPromptAfterHalfPromptError
}

public enum DTTWorkflowEvent
{
    SelectCorrectAid,
    HoldAid,
    ReleaseAid,
    PauseElapsed,
    WhatIsThis,
    RetryOrCorrection,
    HalfPrompt,
    FullPrompt,
    Distractor,
    PositiveReinforcement
}

public enum DTTStudentScriptedResponse
{
    None,
    Correct,
    Incorrect,
    NoResponse,
    DistractorAction
}

[Serializable]
public class DTTStudentScenarioBinding
{
    public string studentId;
    public string displayName;
    public DTTScenarioType scenarioType;
    public DTTTargetStudentMarker marker;
    public StudentBehaviorController studentController;
    public string voiceProfileId;
    public List<string> voiceSelectionAliases = new List<string>();
}

public class DTTWorkflowController : MonoBehaviour
{
    [Header("References")]
    public DTTTeachingAidManager teachingAidManager;
    public List<DTTStudentScenarioBinding> students = new List<DTTStudentScenarioBinding>();

    [Header("Teaching Aid Validation")]
    public bool requireSpecificTeachingAid = false;
    public ClassroomItemType targetItemType = ClassroomItemType.Ruler;

    [Header("Timing")]
    public float responseWaitSeconds = 2f;
    public float correctionPauseSeconds = 2f;
    public float distractorCollectSeconds = 1.2f;
    public float distractorActionSeconds = 3f;
    public float additionalDistractorWaitSeconds = 3f;
    public bool allowMultipleDistractors = true;
    public bool resetScenarioOnStudentChange = true;

    [Header("Missed Step Recovery")]
    public bool allowMissedTeacherStepRecovery = true;
    public float missedTeacherStepTimeoutSeconds = 5f;
    public float missedResponseTriggerTimeoutSeconds = 20f;
    public float missedFinalPraiseTimeoutSeconds = 5f;

    [Header("Desktop Test Keys")]
    public bool enableKeyboardTesting = true;
    public bool allowKeyboardStudentSelectionTesting = false;
    public KeyCode askKey = KeyCode.Q;
    public KeyCode correctionKey = KeyCode.W;
    public KeyCode halfPromptKey = KeyCode.E;
    public KeyCode fullPromptKey = KeyCode.R;
    public KeyCode praiseKey = KeyCode.T;
    public KeyCode clapKey = KeyCode.C;
    public KeyCode touchNoseKey = KeyCode.N;

    [Header("Debug")]
    public bool showDesktopStatus = false;
    public bool logIgnoredEvents = true;
    public List<string> eventLog = new List<string>();

    [Header("Desktop Monitor")]
    public bool autoCreateMonitorReporter = true;

    [Header("Keke Leave-Seat Disruption")]
    public bool enableKekeLeaveSeatDuringAnnaDTT = true;
    public string kekeLeaveSeatStudentName = "可可";
    public string kekeLeaveSeatAllowedActiveStudentName = "安娜";
    public float kekeLeaveSeatInitialDelaySeconds = 20f;
    public float kekeLeaveSeatIntervalSeconds = 45f;
    public float kekeReturnDelaySeconds = 1f;
    public string[] kekeReturnPhrases = { "起来", "回来", "回座位", "怎么躺下", "躺下啦", "躺下了" };

    private readonly List<DTTWorkflowStep> currentSteps = new List<DTTWorkflowStep>();
    private readonly List<DTTTeacherIntent> pendingDistractorIntents = new List<DTTTeacherIntent>();
    private readonly List<DTTTeacherIntent> completedDistractorIntents = new List<DTTTeacherIntent>();
    private DTTStudentScenarioBinding activeStudent;
    private DTTTargetStudentMarker lastManagerSelectedStudent;
    private DTTTeachingAid lastSelectedAid;
    private DTTTeachingAid lastHeldAid;
    private int currentStepIndex = -1;
    private bool isWaiting;
    private Coroutine activeRoutine;
    private string status = "Select a DTT student to begin.";
    private DTTTeacherIntent lastDistractorIntent = DTTTeacherIntent.Unknown;
    private bool collectingDistractors;
    private GUIStyle statusStyle;
    private DTTMonitorReporter monitorReporter;
    private BehaviorDemoController behaviorDemoController;
    private float nextKekeLeaveSeatCheckTime;
    private Coroutine kekeReturnRoutine;

    void Awake()
    {
        if (teachingAidManager == null)
        {
            teachingAidManager = FindObjectOfType<DTTTeachingAidManager>();
        }

        AutoBindMissingReferences();
        EnsureMonitorReporter();
        ScheduleNextKekeLeaveSeatCheck(kekeLeaveSeatInitialDelaySeconds);
    }

    void Start()
    {
        if (teachingAidManager != null && teachingAidManager.selectedStudent != null)
        {
            SelectStudentByMarker(teachingAidManager.selectedStudent, true);
        }
    }

    void Update()
    {
        PollManagerSelection();
        PollTeachingAidEvents();
        PollKeyboardTesting();
        PollKekeLeaveSeatDisruption();
    }

    public void HandleVoiceIntent(DTTVoiceIntentMessage message)
    {
        if (message == null) return;

        DTTTeacherIntent intent = DTTTeacherIntentParser.Parse(message.intent);
        if (monitorReporter != null)
        {
            monitorReporter.SendVoiceIntent(message, intent);
        }

        if (TryHandleKekeReturnVoice(message))
        {
            return;
        }

        if (intent == DTTTeacherIntent.SelectStudent || !string.IsNullOrEmpty(message.student_id) || !string.IsNullOrEmpty(message.student_name))
        {
            if (SelectStudentByVoice(message.student_id, message.student_name, message.text))
            {
                return;
            }
        }

        HandleTeacherIntent(intent, message.text);
    }

    public void HandleTeacherIntent(DTTTeacherIntent intent, string rawText = "")
    {
        if (activeStudent == null)
        {
            IgnoreEvent(intent.ToString(), "no active student");
            return;
        }

        DTTWorkflowEvent workflowEvent;
        if (!TryMapIntentToWorkflowEvent(intent, out workflowEvent))
        {
            IgnoreEvent(intent.ToString(), "unsupported intent");
            return;
        }

        if (intent == DTTTeacherIntent.ClapHands || intent == DTTTeacherIntent.TouchNose)
        {
            lastDistractorIntent = intent;
        }

        if (collectingDistractors && workflowEvent == DTTWorkflowEvent.Distractor)
        {
            AddPendingDistractor(intent);
            return;
        }

        TryAcceptEvent(workflowEvent, rawText);
    }

    public bool SelectStudentByVoice(string studentId, string studentName, string rawText)
    {
        DTTStudentScenarioBinding binding = FindStudentBinding(studentId, studentName, rawText);
        if (binding == null)
        {
            IgnoreEvent("SelectStudent", $"unknown student id/name: {studentId} {studentName} {rawText}");
            return false;
        }

        SelectStudent(binding, true);
        if (teachingAidManager != null && binding.marker != null)
        {
            teachingAidManager.SelectStudent(binding.marker);
        }

        return true;
    }

    public void SelectStudentByMarker(DTTTargetStudentMarker marker, bool resetScenario)
    {
        DTTStudentScenarioBinding binding = FindStudentBinding(marker);
        if (binding == null)
        {
            IgnoreEvent("SelectStudent", $"marker not bound: {(marker != null ? marker.name : "null")}");
            return;
        }

        SelectStudent(binding, resetScenario);
    }

    public void ResetActiveScenario()
    {
        if (activeStudent != null)
        {
            SelectStudent(activeStudent, true);
        }
    }

    private void SelectStudent(DTTStudentScenarioBinding binding, bool resetScenario)
    {
        if (binding == null) return;

        bool changedStudent = activeStudent != binding;
        activeStudent = binding;
        lastManagerSelectedStudent = binding.marker;

        if (resetScenario || changedStudent || currentSteps.Count == 0)
        {
            BuildScenario(binding.scenarioType);
            currentStepIndex = 0;
            StopActiveRoutine();
            LogEvent($"Selected {GetStudentLabel(binding)} / {binding.scenarioType}");
            UpdateStatus();
            if (IsActiveStudentNamed(kekeLeaveSeatAllowedActiveStudentName))
            {
                ScheduleNextKekeLeaveSeatCheck(kekeLeaveSeatInitialDelaySeconds);
            }
            TryAutoAdvanceCurrentStep();
        }
        else
        {
            UpdateStatus();
        }
    }

    private void BuildScenario(DTTScenarioType scenarioType)
    {
        currentSteps.Clear();

        AddStep("1. Select the correct teaching aid", DTTWorkflowEvent.SelectCorrectAid);
        AddStep("2. Pick up / present the teaching aid", DTTWorkflowEvent.HoldAid);
        AddResponseStep("3. Teacher says: 这是什么", DTTWorkflowEvent.WhatIsThis, GetInitialResponse(scenarioType), true);

        if (scenarioType == DTTScenarioType.DirectCorrect)
        {
            AddTeacherStep("4. Praise the student", DTTWorkflowEvent.PositiveReinforcement, missedFinalPraiseTimeoutSeconds);
            return;
        }

        AddTeacherStep("4. Teacher says: 不对哦 / correction", DTTWorkflowEvent.RetryOrCorrection);
        AddStep("5. Put the teaching aid away", DTTWorkflowEvent.ReleaseAid);
        AddStep("6. Wait 2 seconds", DTTWorkflowEvent.PauseElapsed);
        AddStep("7. Re-present the teaching aid", DTTWorkflowEvent.HoldAid);
        AddTeacherStep("8. Teacher says: 这是什么", DTTWorkflowEvent.WhatIsThis);

        if (scenarioType == DTTScenarioType.HalfPromptThenCorrect)
        {
            AddResponseStep("9. Immediately provide half prompt", DTTWorkflowEvent.HalfPrompt, DTTStudentScriptedResponse.Correct, true);
            AddStep("10. Put the teaching aid away without feedback", DTTWorkflowEvent.ReleaseAid);
            AddStep("11. Re-present the teaching aid", DTTWorkflowEvent.HoldAid);
            AddResponseStep("12. Teacher says: 这是什么", DTTWorkflowEvent.WhatIsThis, DTTStudentScriptedResponse.Correct, true);
            AddStep("13. Put the teaching aid away without feedback", DTTWorkflowEvent.ReleaseAid);
            AddResponseStep("14. Give distractor instruction", DTTWorkflowEvent.Distractor, DTTStudentScriptedResponse.DistractorAction, true);
            AddStep("15. Re-present the teaching aid", DTTWorkflowEvent.HoldAid);
            AddResponseStep("16. Teacher says: 这是什么", DTTWorkflowEvent.WhatIsThis, DTTStudentScriptedResponse.Correct, true);
            AddTeacherStep("17. Praise the student", DTTWorkflowEvent.PositiveReinforcement, missedFinalPraiseTimeoutSeconds);
            return;
        }

        AddResponseStep("9. Immediately provide half prompt", DTTWorkflowEvent.HalfPrompt, DTTStudentScriptedResponse.Incorrect, true);
        AddStep("10. Put the teaching aid away without feedback", DTTWorkflowEvent.ReleaseAid);
        AddStep("11. Re-present the teaching aid", DTTWorkflowEvent.HoldAid);
        AddTeacherStep("12. Teacher says: 这是什么", DTTWorkflowEvent.WhatIsThis);
        AddResponseStep("13. Immediately provide full prompt", DTTWorkflowEvent.FullPrompt, DTTStudentScriptedResponse.Correct, true);
        AddStep("14. Put the teaching aid away without feedback", DTTWorkflowEvent.ReleaseAid);
        AddStep("15. Re-present the teaching aid", DTTWorkflowEvent.HoldAid);
        AddResponseStep("16. Teacher says: 这是什么", DTTWorkflowEvent.WhatIsThis, DTTStudentScriptedResponse.Correct, true);
        AddStep("17. Put the teaching aid away without feedback", DTTWorkflowEvent.ReleaseAid);
        AddResponseStep("18. Give distractor instruction", DTTWorkflowEvent.Distractor, DTTStudentScriptedResponse.DistractorAction, true);
        AddStep("19. Re-present the teaching aid", DTTWorkflowEvent.HoldAid);
        AddResponseStep("20. Teacher says: 这是什么", DTTWorkflowEvent.WhatIsThis, DTTStudentScriptedResponse.Correct, true);
        AddTeacherStep("21. Praise the student", DTTWorkflowEvent.PositiveReinforcement, missedFinalPraiseTimeoutSeconds);
    }

    private DTTStudentScriptedResponse GetInitialResponse(DTTScenarioType scenarioType)
    {
        return scenarioType == DTTScenarioType.DirectCorrect
            ? DTTStudentScriptedResponse.Correct
            : DTTStudentScriptedResponse.Incorrect;
    }

    private void AddStep(string label, DTTWorkflowEvent expectedEvent)
    {
        currentSteps.Add(new DTTWorkflowStep(label, expectedEvent, DTTStudentScriptedResponse.None, false, 0f, true));
    }

    private void AddTeacherStep(string label, DTTWorkflowEvent expectedEvent, float autoSkipSeconds = -1f)
    {
        currentSteps.Add(new DTTWorkflowStep(label, expectedEvent, DTTStudentScriptedResponse.None, true, ResolveAutoSkipSeconds(autoSkipSeconds, false), true));
    }

    private void AddResponseStep(string label, DTTWorkflowEvent expectedEvent, DTTStudentScriptedResponse response, bool canAutoSkip = false, float autoSkipSeconds = -1f)
    {
        currentSteps.Add(new DTTWorkflowStep(label, expectedEvent, response, canAutoSkip, ResolveAutoSkipSeconds(autoSkipSeconds, response != DTTStudentScriptedResponse.None), true));
    }

    private float ResolveAutoSkipSeconds(float autoSkipSeconds, bool isResponseTrigger)
    {
        if (autoSkipSeconds > 0f) return autoSkipSeconds;
        return isResponseTrigger ? missedResponseTriggerTimeoutSeconds : missedTeacherStepTimeoutSeconds;
    }

    private void TryAcceptEvent(DTTWorkflowEvent workflowEvent, string rawText = "")
    {
        if (isWaiting)
        {
            IgnoreEvent(workflowEvent.ToString(), "workflow is waiting for timed student response");
            return;
        }

        DTTWorkflowStep step = GetCurrentStep();
        if (step == null)
        {
            IgnoreEvent(workflowEvent.ToString(), "scenario is complete");
            return;
        }

        if (allowMissedTeacherStepRecovery && step.ExpectedEvent != workflowEvent)
        {
            TrySkipMissedStepsForIncomingEvent(workflowEvent);
            step = GetCurrentStep();
            if (step == null)
            {
                IgnoreEvent(workflowEvent.ToString(), "scenario completed while recovering from missed steps");
                return;
            }
        }

        if (step.ExpectedEvent != workflowEvent)
        {
            IgnoreEvent(workflowEvent.ToString(), $"expected {step.ExpectedEvent}");
            return;
        }

        if (workflowEvent == DTTWorkflowEvent.SelectCorrectAid && !IsCurrentAidAcceptable())
        {
            IgnoreEvent(workflowEvent.ToString(), "selected teaching aid is not acceptable for this trial");
            return;
        }

        LogEvent($"Step {currentStepIndex + 1} OK: {step.Label}" + (string.IsNullOrEmpty(rawText) ? "" : $" | \"{rawText}\""));
        StopActiveRoutine();

        if (step.Response != DTTStudentScriptedResponse.None)
        {
            activeRoutine = step.Response == DTTStudentScriptedResponse.DistractorAction
                ? StartCoroutine(DistractorResponseRoutine())
                : StartCoroutine(DelayedStudentResponseRoutine(step));
            return;
        }

        AdvanceStep();
    }

    private IEnumerator DelayedStudentResponseRoutine(DTTWorkflowStep step)
    {
        isWaiting = true;
        status = $"Waiting {responseWaitSeconds:F1}s for student response: {step.Label}";
        yield return new WaitForSeconds(responseWaitSeconds);

        yield return PlayStudentResponseRoutine(step.Response);

        isWaiting = false;
        activeRoutine = null;
        AdvanceStep();
    }

    private IEnumerator PlayStudentResponseRoutine(DTTStudentScriptedResponse response)
    {
        if (activeStudent == null || activeStudent.studentController == null) yield break;

        StudentBehaviorController controller = activeStudent.studentController;
        if (response == DTTStudentScriptedResponse.NoResponse)
        {
            controller.SetBehavior(StudentBehaviorType.OffTask, 1.5f);
            LogEvent("Student response: no response");
            yield return new WaitForSeconds(1.5f);
            yield break;
        }

        string utteranceKey = GetUtteranceKey(response);
        string fallbackText = response == DTTStudentScriptedResponse.Correct
            ? DTTStudentVoiceBank.GetCorrectAnswerText(GetCurrentItemType())
            : response == DTTStudentScriptedResponse.Incorrect
                ? DTTStudentVoiceBank.GetWrongAnswerText(GetCurrentItemType())
                : DTTStudentVoiceBank.GetFallbackText(utteranceKey);

        if (response == DTTStudentScriptedResponse.DistractorAction)
        {
            PlayDistractorBehavior(controller, lastDistractorIntent);
            yield return new WaitForSeconds(distractorActionSeconds);
            LogEvent($"Student response: {response} ({lastDistractorIntent})");
            yield break;
        }

        AudioClip clip = DTTStudentVoiceBank.LoadClip(activeStudent.voiceProfileId, utteranceKey);
        if (clip != null)
        {
            Task speakTask = controller.SpeakAudioClipWithLipSync(clip, utteranceKey);
            while (!speakTask.IsCompleted)
            {
                yield return null;
            }

            if (speakTask.IsFaulted)
            {
                Debug.LogError(speakTask.Exception);
            }
        }
        else
        {
            Debug.LogWarning($"[DTT] Missing local voice clip: {activeStudent.voiceProfileId}/{utteranceKey}. Using procedural fallback.");
            controller.SpeakWithFallbackAudio(fallbackText);
            yield return new WaitForSeconds(Mathf.Max(1.5f, fallbackText.Length * 0.08f));
        }

        LogEvent($"Student response: {response} ({utteranceKey})");
    }

    private string GetUtteranceKey(DTTStudentScriptedResponse response)
    {
        switch (response)
        {
            case DTTStudentScriptedResponse.Correct:
                string key = DTTStudentVoiceBank.GetCorrectAnswerKey(GetCurrentItemType());
                return !string.IsNullOrEmpty(key) ? key : "ruler_short";
            case DTTStudentScriptedResponse.Incorrect:
                string wrongKey = DTTStudentVoiceBank.GetWrongAnswerKey(GetCurrentItemType());
                return !string.IsNullOrEmpty(wrongKey) ? wrongKey : "huh_what";
            case DTTStudentScriptedResponse.DistractorAction:
                return "okay";
            default:
                return "";
        }
    }

    private IEnumerator DistractorResponseRoutine()
    {
        if (activeStudent == null || activeStudent.studentController == null) yield break;

        pendingDistractorIntents.Clear();
        completedDistractorIntents.Clear();
        AddPendingDistractor(lastDistractorIntent);

        collectingDistractors = allowMultipleDistractors;
        status = allowMultipleDistractors
            ? $"Collecting distractor commands for {distractorCollectSeconds:F1}s..."
            : "Playing distractor action.";

        if (allowMultipleDistractors && distractorCollectSeconds > 0f)
        {
            yield return new WaitForSeconds(distractorCollectSeconds);
        }

        isWaiting = true;

        if (pendingDistractorIntents.Count == 0)
        {
            pendingDistractorIntents.Add(DTTTeacherIntent.ClapHands);
        }

        StudentBehaviorController controller = activeStudent.studentController;
        while (pendingDistractorIntents.Count > 0)
        {
            DTTTeacherIntent distractorIntent = pendingDistractorIntents[0];
            pendingDistractorIntents.RemoveAt(0);
            completedDistractorIntents.Add(distractorIntent);

            PlayDistractorBehavior(controller, distractorIntent);
            LogEvent($"Student distractor action: {distractorIntent}");
            yield return new WaitForSeconds(distractorActionSeconds);

            if (!allowMultipleDistractors)
            {
                break;
            }

            status = $"Waiting {additionalDistractorWaitSeconds:F1}s for another distractor command...";
            float elapsed = 0f;
            while (pendingDistractorIntents.Count == 0 && elapsed < additionalDistractorWaitSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        pendingDistractorIntents.Clear();
        completedDistractorIntents.Clear();
        collectingDistractors = false;
        isWaiting = false;
        activeRoutine = null;
        AdvanceStep();
    }

    private void AddPendingDistractor(DTTTeacherIntent intent)
    {
        if (intent != DTTTeacherIntent.ClapHands && intent != DTTTeacherIntent.TouchNose)
        {
            return;
        }

        if (pendingDistractorIntents.Contains(intent) || completedDistractorIntents.Contains(intent))
        {
            LogEvent($"Duplicate distractor ignored: {intent}");
            return;
        }

        pendingDistractorIntents.Add(intent);
        LogEvent($"Queued distractor: {intent}");
    }

    private void PlayDistractorBehavior(StudentBehaviorController controller, DTTTeacherIntent distractorIntent)
    {
        BehaviorDemoController demoController = FindObjectOfType<BehaviorDemoController>();
        if (demoController != null && controller != null)
        {
            demoController.SelectStudentByGameObject(controller.gameObject, false);
            if (distractorIntent == DTTTeacherIntent.ClapHands)
            {
                if (demoController.clappingClip != null)
                {
                    demoController.TriggerClap();
                    return;
                }

                Debug.LogWarning("[DTT Workflow] BehaviorDemoController has no clappingClip; using fallback behavior.");
            }

            if (distractorIntent == DTTTeacherIntent.TouchNose)
            {
                demoController.TriggerTouchNose();
                return;
            }
        }

        ProceduralBehaviorAnimator proceduralAnimator = controller != null
            ? controller.GetComponent<ProceduralBehaviorAnimator>()
            : null;

        if (distractorIntent == DTTTeacherIntent.ClapHands)
        {
            if (proceduralAnimator != null)
            {
                proceduralAnimator.PlaySpeakingMotion(1.5f);
            }
            else
            {
                controller.SetBehavior(StudentBehaviorType.Excited, 1.5f);
            }
        }
        else if (distractorIntent == DTTTeacherIntent.TouchNose)
        {
            if (proceduralAnimator != null)
            {
                proceduralAnimator.PlayTouchNose(3f);
            }
            else
            {
                controller.SetBehavior(StudentBehaviorType.Listening, 1.5f);
            }
        }
    }

    private void AdvanceStep()
    {
        currentStepIndex++;
        UpdateStatus();

        if (currentStepIndex >= currentSteps.Count)
        {
            LogEvent($"Scenario complete for {GetStudentLabel(activeStudent)}");
            status = $"Scenario complete: {GetStudentLabel(activeStudent)}";
            return;
        }

        TryAutoAdvanceCurrentStep();
    }

    private void TryAutoAdvanceCurrentStep()
    {
        DTTWorkflowStep step = GetCurrentStep();
        if (step == null || isWaiting) return;

        if (step.ExpectedEvent == DTTWorkflowEvent.PauseElapsed)
        {
            StopActiveRoutine();
            activeRoutine = StartCoroutine(PauseThenAdvanceRoutine());
            return;
        }

        if (IsCurrentPhysicalStepAlreadySatisfied(step.ExpectedEvent))
        {
            TryAcceptEvent(step.ExpectedEvent);
            return;
        }

        if (allowMissedTeacherStepRecovery && step.CanAutoSkip)
        {
            StopActiveRoutine();
            activeRoutine = StartCoroutine(AutoSkipMissedTeacherStepRoutine(currentStepIndex, step));
        }
    }

    private IEnumerator PauseThenAdvanceRoutine()
    {
        isWaiting = true;
        status = $"Waiting {correctionPauseSeconds:F1}s before re-presentation.";
        yield return new WaitForSeconds(correctionPauseSeconds);
        isWaiting = false;
        activeRoutine = null;
        TryAcceptEvent(DTTWorkflowEvent.PauseElapsed);
    }

    private IEnumerator AutoSkipMissedTeacherStepRoutine(int stepIndex, DTTWorkflowStep step)
    {
        float waitSeconds = Mathf.Max(0.1f, step.AutoSkipSeconds);
        status = $"{GetStudentLabel(activeStudent)} / {activeStudent.scenarioType}\nNext: {step.Label}\nAuto-skip if missed in {waitSeconds:F1}s";

        yield return new WaitForSeconds(waitSeconds);

        if (!allowMissedTeacherStepRecovery || isWaiting || currentStepIndex != stepIndex || GetCurrentStep() != step)
        {
            yield break;
        }

        activeRoutine = null;
        LogEvent($"Step {stepIndex + 1} MISSED / auto-skipped after {waitSeconds:F1}s: {step.Label}");
        AdvanceStep();
    }

    private void TrySkipMissedStepsForIncomingEvent(DTTWorkflowEvent incomingEvent)
    {
        while (true)
        {
            DTTWorkflowStep step = GetCurrentStep();
            if (step == null || step.ExpectedEvent == incomingEvent) return;
            if (!step.CanSkipOnLaterEvent || !HasLaterStepForEvent(incomingEvent)) return;

            StopActiveRoutine();
            LogEvent($"Step {currentStepIndex + 1} MISSED / skipped because {incomingEvent} was received: {step.Label}");
            currentStepIndex++;
            UpdateStatus();
        }
    }

    private bool HasLaterStepForEvent(DTTWorkflowEvent incomingEvent)
    {
        for (int i = currentStepIndex + 1; i < currentSteps.Count; i++)
        {
            if (currentSteps[i].ExpectedEvent == incomingEvent)
            {
                return true;
            }
        }

        return false;
    }

    private void PollManagerSelection()
    {
        if (teachingAidManager == null) return;
        if (teachingAidManager.selectedStudent == null) return;
        if (teachingAidManager.selectedStudent == lastManagerSelectedStudent) return;

        SelectStudentByMarker(teachingAidManager.selectedStudent, resetScenarioOnStudentChange);
    }

    private void PollTeachingAidEvents()
    {
        if (teachingAidManager == null || activeStudent == null) return;

        if (teachingAidManager.selectedAid != lastSelectedAid)
        {
            lastSelectedAid = teachingAidManager.selectedAid;
            if (lastSelectedAid != null)
            {
                TryAcceptEvent(DTTWorkflowEvent.SelectCorrectAid);
            }
        }

        if (teachingAidManager.heldAid != lastHeldAid)
        {
            DTTTeachingAid previousHeldAid = lastHeldAid;
            lastHeldAid = teachingAidManager.heldAid;

            if (lastHeldAid != null)
            {
                TryAcceptEvent(DTTWorkflowEvent.HoldAid);
            }
            else if (previousHeldAid != null)
            {
                TryAcceptEvent(DTTWorkflowEvent.ReleaseAid);
            }
        }
    }

    private void PollKekeLeaveSeatDisruption()
    {
        if (!enableKekeLeaveSeatDuringAnnaDTT) return;
        if (!IsActiveStudentNamed(kekeLeaveSeatAllowedActiveStudentName)) return;
        if (IsScenarioComplete()) return;
        if (IsKekeLeaveSeatActive()) return;
        if (Time.time < nextKekeLeaveSeatCheckTime) return;

        ScheduleNextKekeLeaveSeatCheck();

        BehaviorDemoController demo = GetBehaviorDemoController();
        if (demo == null)
        {
            IgnoreEvent("KekeLeaveSeat", "BehaviorDemoController not found");
            return;
        }

        if (demo.TriggerLeaveSeatForStudent(kekeLeaveSeatStudentName))
        {
            LogEvent($"{kekeLeaveSeatStudentName} timed leave-seat disruption while teaching {GetStudentLabel(activeStudent)}");
        }
    }

    private bool TryHandleKekeReturnVoice(DTTVoiceIntentMessage message)
    {
        if (!enableKekeLeaveSeatDuringAnnaDTT) return false;
        if (!IsKekeLeaveSeatActive()) return false;
        if (!MessageMentionsStudent(message, kekeLeaveSeatStudentName)) return false;

        return TryStartKekeReturnIntervention("voice");
    }

    public bool TryHandleKekeReturnPointer(DTTTargetStudentMarker marker)
    {
        if (!enableKekeLeaveSeatDuringAnnaDTT) return false;
        if (marker == null) return false;
        if (!IsKekeLeaveSeatActive()) return false;
        if (!MarkerMatchesStudentName(marker, kekeLeaveSeatStudentName)) return false;

        return TryStartKekeReturnIntervention("pointer");
    }

    private bool TryStartKekeReturnIntervention(string source)
    {
        BehaviorDemoController demo = GetBehaviorDemoController();
        if (demo == null)
        {
            IgnoreEvent("KekeReturn", "BehaviorDemoController not found");
            return true;
        }

        if (kekeReturnRoutine == null)
        {
            kekeReturnRoutine = StartCoroutine(DelayedKekeReturnRoutine(demo));
        }
        ScheduleNextKekeLeaveSeatCheck(kekeLeaveSeatInitialDelaySeconds);
        LogEvent($"{kekeLeaveSeatStudentName} return-to-seat {source} intervention intercepted; active DTT student remains {GetStudentLabel(activeStudent)}");
        UpdateStatus();
        return true;
    }

    private IEnumerator DelayedKekeReturnRoutine(BehaviorDemoController demo)
    {
        float delay = Mathf.Max(0f, kekeReturnDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (demo != null)
        {
            demo.ReturnLeaveSeatStudent(kekeLeaveSeatStudentName);
        }

        kekeReturnRoutine = null;
    }

    private bool IsKekeLeaveSeatActive()
    {
        BehaviorDemoController demo = GetBehaviorDemoController();
        return demo != null && demo.IsLeaveSeatBehaviorActive(kekeLeaveSeatStudentName);
    }

    private bool MarkerMatchesStudentName(DTTTargetStudentMarker marker, string displayName)
    {
        if (marker == null || string.IsNullOrEmpty(displayName)) return false;
        if (ContainsNormalized(marker.gameObject.name, displayName)) return true;

        DTTStudentScenarioBinding binding = FindStudentBinding(marker);
        if (binding == null) return false;

        if (Matches(binding.studentId, displayName) ||
            Matches(binding.displayName, displayName) ||
            ContainsNormalized(GetStudentLabel(binding), displayName))
            return true;

        for (int i = 0; i < binding.voiceSelectionAliases.Count; i++)
        {
            if (ContainsNormalized(binding.voiceSelectionAliases[i], displayName))
                return true;
        }

        return false;
    }

    private bool MessageMentionsStudent(DTTVoiceIntentMessage message, string displayName)
    {
        if (message == null || string.IsNullOrEmpty(displayName)) return false;
        string combined = $"{message.student_id} {message.student_name} {message.text}";
        if (ContainsNormalized(combined, displayName)) return true;

        DTTStudentScenarioBinding binding = FindStudentBinding("", displayName, "");
        if (binding == null) return false;

        if (ContainsNormalized(combined, binding.studentId) || ContainsNormalized(combined, binding.displayName))
            return true;

        for (int i = 0; i < binding.voiceSelectionAliases.Count; i++)
        {
            if (ContainsNormalized(combined, binding.voiceSelectionAliases[i]))
                return true;
        }

        return false;
    }

    private bool IsActiveStudentNamed(string displayName)
    {
        if (activeStudent == null || string.IsNullOrEmpty(displayName)) return false;
        return Matches(activeStudent.displayName, displayName)
               || Matches(activeStudent.studentId, displayName)
               || ContainsNormalized(GetStudentLabel(activeStudent), displayName);
    }

    private bool IsScenarioComplete()
    {
        return activeStudent != null && currentSteps.Count > 0 && currentStepIndex >= currentSteps.Count;
    }

    private void ScheduleNextKekeLeaveSeatCheck(float delaySeconds = -1f)
    {
        float delay = delaySeconds >= 0f
            ? delaySeconds
            : Mathf.Max(0.1f, kekeLeaveSeatIntervalSeconds);
        nextKekeLeaveSeatCheckTime = Time.time + delay;
    }

    private BehaviorDemoController GetBehaviorDemoController()
    {
        if (behaviorDemoController == null)
        {
            behaviorDemoController = FindObjectOfType<BehaviorDemoController>();
        }

        return behaviorDemoController;
    }

    private bool IsCurrentPhysicalStepAlreadySatisfied(DTTWorkflowEvent expectedEvent)
    {
        if (teachingAidManager == null) return false;

        switch (expectedEvent)
        {
            case DTTWorkflowEvent.SelectCorrectAid:
                return teachingAidManager.selectedAid != null && IsCurrentAidAcceptable();
            case DTTWorkflowEvent.HoldAid:
                return teachingAidManager.heldAid != null && IsCurrentAidAcceptable();
            case DTTWorkflowEvent.ReleaseAid:
                return teachingAidManager.heldAid == null;
            default:
                return false;
        }
    }

    private bool IsCurrentAidAcceptable()
    {
        if (teachingAidManager == null) return false;
        DTTTeachingAid aid = teachingAidManager.heldAid != null ? teachingAidManager.heldAid : teachingAidManager.selectedAid;
        if (aid == null) return false;
        if (!requireSpecificTeachingAid) return true;

        ClassroomItemType itemType;
        return aid.TryGetClassroomItemType(out itemType) && itemType == targetItemType;
    }

    private ClassroomItemType GetCurrentItemType()
    {
        if (teachingAidManager != null)
        {
            DTTTeachingAid aid = teachingAidManager.heldAid != null ? teachingAidManager.heldAid : teachingAidManager.selectedAid;
            ClassroomItemType itemType;
            if (aid != null && aid.TryGetClassroomItemType(out itemType))
            {
                return itemType;
            }
        }

        return targetItemType;
    }

    private bool TryMapIntentToWorkflowEvent(DTTTeacherIntent intent, out DTTWorkflowEvent workflowEvent)
    {
        switch (intent)
        {
            case DTTTeacherIntent.WhatIsThis:
                workflowEvent = DTTWorkflowEvent.WhatIsThis;
                return true;
            case DTTTeacherIntent.PositiveReinforcement:
                workflowEvent = DTTWorkflowEvent.PositiveReinforcement;
                return true;
            case DTTTeacherIntent.RetryOrCorrection:
                workflowEvent = DTTWorkflowEvent.RetryOrCorrection;
                return true;
            case DTTTeacherIntent.HalfPrompt:
                workflowEvent = DTTWorkflowEvent.HalfPrompt;
                return true;
            case DTTTeacherIntent.FullPrompt:
                workflowEvent = DTTWorkflowEvent.FullPrompt;
                return true;
            case DTTTeacherIntent.ClapHands:
            case DTTTeacherIntent.TouchNose:
                workflowEvent = DTTWorkflowEvent.Distractor;
                return true;
            default:
                workflowEvent = default;
                return false;
        }
    }

    private void PollKeyboardTesting()
    {
        if (!enableKeyboardTesting) return;

        if (allowKeyboardStudentSelectionTesting)
        {
            if (DesktopInputBridge.GetKeyDown(KeyCode.Alpha1)) SelectStudentByIndex(0);
            if (DesktopInputBridge.GetKeyDown(KeyCode.Alpha2)) SelectStudentByIndex(1);
            if (DesktopInputBridge.GetKeyDown(KeyCode.Alpha3)) SelectStudentByIndex(2);
        }
        if (DesktopInputBridge.GetKeyDown(askKey)) HandleTeacherIntent(DTTTeacherIntent.WhatIsThis, "keyboard");
        if (DesktopInputBridge.GetKeyDown(correctionKey)) HandleTeacherIntent(DTTTeacherIntent.RetryOrCorrection, "keyboard");
        if (DesktopInputBridge.GetKeyDown(halfPromptKey)) HandleTeacherIntent(DTTTeacherIntent.HalfPrompt, "keyboard");
        if (DesktopInputBridge.GetKeyDown(fullPromptKey)) HandleTeacherIntent(DTTTeacherIntent.FullPrompt, "keyboard");
        if (DesktopInputBridge.GetKeyDown(praiseKey)) HandleTeacherIntent(DTTTeacherIntent.PositiveReinforcement, "keyboard");
        if (DesktopInputBridge.GetKeyDown(clapKey)) HandleTeacherIntent(DTTTeacherIntent.ClapHands, "keyboard");
        if (DesktopInputBridge.GetKeyDown(touchNoseKey)) HandleTeacherIntent(DTTTeacherIntent.TouchNose, "keyboard");
    }

    private void SelectStudentByIndex(int index)
    {
        if (index < 0 || index >= students.Count) return;
        SelectStudent(students[index], true);
        if (teachingAidManager != null && students[index].marker != null)
        {
            teachingAidManager.SelectStudent(students[index].marker);
        }
    }

    private DTTWorkflowStep GetCurrentStep()
    {
        if (currentStepIndex < 0 || currentStepIndex >= currentSteps.Count) return null;
        return currentSteps[currentStepIndex];
    }

    private DTTStudentScenarioBinding FindStudentBinding(DTTTargetStudentMarker marker)
    {
        if (marker == null) return null;
        for (int i = 0; i < students.Count; i++)
        {
            if (students[i].marker == marker || marker.GetComponentInParent<StudentBehaviorController>() == students[i].studentController)
            {
                return students[i];
            }
        }

        return null;
    }

    private DTTStudentScenarioBinding FindStudentBinding(string studentId, string studentName, string rawText)
    {
        string combined = $"{studentId} {studentName} {rawText}".Trim();
        for (int i = 0; i < students.Count; i++)
        {
            DTTStudentScenarioBinding binding = students[i];
            if (Matches(binding.studentId, studentId) || Matches(binding.displayName, studentName))
            {
                return binding;
            }

            if (!string.IsNullOrEmpty(combined))
            {
                if (Matches(binding.studentId, combined) || Matches(binding.displayName, combined))
                {
                    return binding;
                }

                for (int a = 0; a < binding.voiceSelectionAliases.Count; a++)
                {
                    if (ContainsNormalized(combined, binding.voiceSelectionAliases[a]))
                    {
                        return binding;
                    }
                }
            }
        }

        return null;
    }

    private bool Matches(string expected, string actual)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual)) return false;
        return string.Equals(Normalize(expected), Normalize(actual), StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsNormalized(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return false;
        return Normalize(haystack).Contains(Normalize(needle));
    }

    private string Normalize(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace(" ", "").Replace("，", "").Replace(",", "").Replace("。", "").Replace(".", "").Trim().ToLowerInvariant();
    }

    private void AutoBindMissingReferences()
    {
        for (int i = 0; i < students.Count; i++)
        {
            DTTStudentScenarioBinding binding = students[i];
            if (binding.marker == null && binding.studentController != null)
            {
                binding.marker = binding.studentController.GetComponentInParent<DTTTargetStudentMarker>();
                if (binding.marker == null)
                {
                    binding.marker = binding.studentController.GetComponentInChildren<DTTTargetStudentMarker>();
                }
            }

            if (binding.studentController == null && binding.marker != null)
            {
                binding.studentController = binding.marker.GetComponentInParent<StudentBehaviorController>();
                if (binding.studentController == null)
                {
                    binding.studentController = binding.marker.GetComponentInChildren<StudentBehaviorController>();
                }
            }

            if (string.IsNullOrEmpty(binding.studentId) && binding.studentController != null)
            {
                binding.studentId = string.IsNullOrEmpty(binding.studentController.studentId)
                    ? binding.studentController.gameObject.name
                    : binding.studentController.studentId;
            }

            if (string.IsNullOrEmpty(binding.displayName) && binding.studentController != null)
            {
                binding.displayName = string.IsNullOrEmpty(binding.studentController.studentName)
                    ? binding.studentController.gameObject.name
                    : binding.studentController.studentName;
            }
        }
    }

    private void EnsureMonitorReporter()
    {
        if (!autoCreateMonitorReporter) return;

        monitorReporter = GetComponent<DTTMonitorReporter>();
        if (monitorReporter == null)
        {
            monitorReporter = gameObject.AddComponent<DTTMonitorReporter>();
        }

        monitorReporter.workflowController = this;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        isWaiting = false;
        collectingDistractors = false;
        pendingDistractorIntents.Clear();
    }

    private void LogEvent(string message)
    {
        string line = $"[{Time.time:F1}] {message}";
        eventLog.Add(line);
        Debug.Log("[DTT Workflow] " + line);
        if (monitorReporter != null)
        {
            monitorReporter.SendWorkflowLog(line);
        }
    }

    private void IgnoreEvent(string eventName, string reason)
    {
        if (logIgnoredEvents)
        {
            Debug.Log($"[DTT Workflow] Ignored {eventName}: {reason}");
        }
        if (monitorReporter != null)
        {
            monitorReporter.SendIgnoredEvent(eventName, reason);
        }
    }

    public DTTWorkflowMonitorSnapshot BuildMonitorSnapshot()
    {
        DTTWorkflowStep step = GetCurrentStep();
        DTTTeachingAid selectedAid = teachingAidManager != null ? teachingAidManager.selectedAid : null;
        DTTTeachingAid heldAid = teachingAidManager != null ? teachingAidManager.heldAid : null;

        return new DTTWorkflowMonitorSnapshot
        {
            type = "status",
            status = status,
            active_student = GetStudentLabel(activeStudent),
            active_student_id = activeStudent != null ? activeStudent.studentId : "",
            scenario = activeStudent != null ? activeStudent.scenarioType.ToString() : "",
            current_step_index = currentStepIndex + 1,
            step_count = currentSteps.Count,
            current_step_label = step != null ? step.Label : "",
            expected_event = step != null ? step.ExpectedEvent.ToString() : "",
            is_waiting = isWaiting,
            collecting_distractors = collectingDistractors,
            scenario_complete = activeStudent != null && currentStepIndex >= currentSteps.Count,
            selected_aid = selectedAid != null ? selectedAid.name : "",
            held_aid = heldAid != null ? heldAid.name : "",
            target_item_type = targetItemType.ToString(),
            event_log_count = eventLog.Count
        };
    }

    private void UpdateStatus()
    {
        DTTWorkflowStep step = GetCurrentStep();
        if (step == null)
        {
            status = activeStudent == null ? "Select a DTT student to begin." : $"Scenario complete: {GetStudentLabel(activeStudent)}";
            return;
        }

        status = $"{GetStudentLabel(activeStudent)} / {activeStudent.scenarioType}\nNext: {step.Label}";
    }

    private string GetStudentLabel(DTTStudentScenarioBinding binding)
    {
        if (binding == null) return "No student";
        if (!string.IsNullOrEmpty(binding.displayName)) return binding.displayName;
        if (!string.IsNullOrEmpty(binding.studentId)) return binding.studentId;
        return binding.marker != null ? binding.marker.name : "Unnamed student";
    }

    void OnGUI()
    {
        if (!showDesktopStatus) return;
        if (Camera.main == null) return;

        if (statusStyle == null)
        {
            statusStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                padding = new RectOffset(10, 10, 8, 8)
            };
            statusStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(16f, 16f, 640f, 88f), status, statusStyle);
    }

    private class DTTWorkflowStep
    {
        public readonly string Label;
        public readonly DTTWorkflowEvent ExpectedEvent;
        public readonly DTTStudentScriptedResponse Response;
        public readonly bool CanAutoSkip;
        public readonly float AutoSkipSeconds;
        public readonly bool CanSkipOnLaterEvent;

        public DTTWorkflowStep(
            string label,
            DTTWorkflowEvent expectedEvent,
            DTTStudentScriptedResponse response,
            bool canAutoSkip,
            float autoSkipSeconds,
            bool canSkipOnLaterEvent)
        {
            Label = label;
            ExpectedEvent = expectedEvent;
            Response = response;
            CanAutoSkip = canAutoSkip;
            AutoSkipSeconds = autoSkipSeconds;
            CanSkipOnLaterEvent = canSkipOnLaterEvent;
        }
    }
}

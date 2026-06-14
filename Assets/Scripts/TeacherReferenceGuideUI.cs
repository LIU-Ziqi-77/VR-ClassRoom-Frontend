using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Runtime-created VR reference guide for DTT training steps.
/// Aim the right-hand ray, then press Quest right-hand A (primaryButton) to place/show or hide.
/// While visible, use the right trigger to select tabs or drag the list vertically.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(40)]
public class TeacherReferenceGuideUI : MonoBehaviour
{
    [Header("Input")]
    public XRNode controllerNode = XRNode.RightHand;
    [Tooltip("Quest right hand: primaryButton is A.")]
    public bool usePrimaryButtonToggle = true;
    [Tooltip("Quest right hand trigger selects tabs and drags the step list.")]
    public bool useTriggerInteraction = true;
    public KeyCode desktopToggleKey = KeyCode.M;
    public KeyCode desktopNextTabKey = KeyCode.Tab;
    [Tooltip("Logs Quest controller button diagnostics briefly. Useful when a physical A button is not detected.")]
    public bool logInputDiagnostics = true;
    public int maxInputDiagnosticLogs = 6;

    [Header("Placement")]
    public Transform headOverride;
    [Tooltip("Optional world-space right-hand ray origin. If empty, the script uses QuestControllerRayVisual when present.")]
    public Transform controllerRayOverride;
    [Tooltip("Fixed distance from the teacher's eyes after pressing A.")]
    public float fixedEyeDistance = 1.5f;
    public Vector3 editorRayLocalPosition = new Vector3(0.18f, -0.22f, 0.45f);
    public Vector3 editorRayLocalEuler = new Vector3(8f, 0f, 0f);
    public float worldScale = 0.00135f;

    [Header("Look")]
    public Color cardColor = new Color(0.10f, 0.17f, 0.17f, 0.88f);
    public Color headerColor = new Color(0.11f, 0.20f, 0.22f, 0.94f);
    public Color accentColor = new Color(0.0f, 0.76f, 0.76f, 1f);
    public Color warmAccentColor = new Color(1f, 0.74f, 0.30f, 1f);
    public Color textColor = new Color(0.93f, 0.97f, 0.98f, 1f);
    public Color mutedTextColor = new Color(0.64f, 0.73f, 0.76f, 1f);

    private const int CanvasWidth = 690;
    private const int CanvasHeight = 780;
    private const float StepViewportHeight = 340f;
    private const float ScrollSpeed = 720f;
    private const float DiagnosticsInterval = 2f;

    private static readonly string[] InputSystemAButtonNames =
    {
        "primaryButton",
        "buttonSouth",
        "aButton"
    };

    private static readonly string[] InputSystemTriggerButtonNames =
    {
        "triggerPressed",
        "triggerButton"
    };

    private static readonly string[] FontCandidates =
    {
        "PingFang SC",
        "Hiragino Sans GB",
        "Noto Sans CJK SC",
        "Noto Sans CJK",
        "Source Han Sans SC",
        "Microsoft YaHei",
        "SimHei",
        "Droid Sans Fallback",
        "Arial Unicode MS"
    };

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private readonly List<Image> tabBackgrounds = new List<Image>();
    private readonly List<Text> tabLabels = new List<Text>();
    private readonly List<RectTransform> tabRects = new List<RectTransform>();

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform canvasRect;
    private RectTransform listContent;
    private Text scenarioTitle;
    private Text scenarioSummary;
    private BoxCollider panelCollider;
    private RectTransform pointerReticle;
    private QuestControllerRayVisual cachedControllerRayVisual;
    private Font uiFont;
    private bool visible;
    private bool lastPrimaryPressed;
    private bool lastTriggerPressed;
    private bool draggingSteps;
    private int currentScenario;
    private int hoveredTabIndex = -1;
    private float scrollOffset;
    private float maxScrollOffset;
    private float lastDragLocalY;
    private float nextDiagnosticsTime;
    private int diagnosticsLogCount;

    private readonly ScenarioData[] scenarios =
    {
        new ScenarioData
        {
            Tab = "情景一",
            Title = "情景一：独立正确反应",
            Summary = "目标：学生在无辅助条件下正确回答后，立即给予积极反馈。",
            Steps = new[]
            {
                "选择正确的教学材料",
                "拿起教学材料，发出指令“这是什么”，不提供任何辅助",
                "等待学生5秒作出反应",
                "若学生回答正确，表扬学生：“说对了”“非常好”“特别棒”"
            }
        },
        new ScenarioData
        {
            Tab = "情景二",
            Title = "情景二：半辅助后回到独立反应",
            Summary = "目标：错误或无反应后使用半辅助，再逐步回到无辅助测试。",
            Steps = new[]
            {
                "选择正确的教学材料",
                "拿起教学材料，发出指令“这是什么”，不提供任何辅助",
                "等待学生5秒作出反应",
                "若学生反应错误，说“不对哦”；若学生无反应，收回教学材料",
                "重新呈现教学材料，发出指令“这是什么”，并立刻提供半辅助",
                "等待学生5秒作出反应",
                "若学生回答正确，收起教学材料，不提供反馈",
                "重新呈现教学材料，发出指令“这是什么”，不提供任何辅助",
                "等待学生5秒作出反应",
                "若学生回答正确，收起教学材料，不提供反馈",
                "发布干扰指令，如“拍拍手”，等待学生拍手",
                "重新呈现教学材料，发出指令“这是什么”，不提供任何辅助",
                "等待学生5秒作出反应",
                "若学生回答正确，表扬学生：“说对了”“非常好”“特别棒”"
            }
        },
        new ScenarioData
        {
            Tab = "情景三",
            Title = "情景三：半辅助无效后提供全辅助",
            Summary = "目标：半辅助仍不成功时使用全辅助，再回到无辅助测试。",
            Steps = new[]
            {
                "选择正确的教学材料",
                "拿起教学材料，发出指令“这是什么”，不提供任何辅助",
                "等待学生5秒作出反应",
                "若学生反应错误，说“不对哦”；若学生无反应，收回教学材料",
                "重新呈现教学材料，发出指令“这是什么”，并立刻提供半辅助",
                "等待学生5秒作出反应",
                "若学生反应错误，说“不对哦”；若学生无反应，收回教学材料",
                "重新呈现教学材料，发出指令“这是什么”，提供全辅助",
                "若学生回答正确，收起教学材料，不提供反馈",
                "重新呈现教学材料，发出指令“这是什么”，不提供任何辅助",
                "等待学生5秒作出反应",
                "若学生回答正确，收起教学材料，不提供反馈",
                "发布干扰指令，如“拍拍手”，等待学生拍手",
                "重新呈现教学材料，发出指令“这是什么”，不提供任何辅助",
                "等待学生5秒作出反应",
                "若学生回答正确，表扬学生：“说对了”“非常好”“特别棒”"
            }
        }
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (UnityEngine.Object.FindFirstObjectByType<TeacherReferenceGuideUI>() != null) return;

        GameObject parent = GameObject.Find("VR System");
        GameObject go = new GameObject("Teacher Reference Guide UI");
        if (parent != null)
        {
            go.transform.SetParent(parent.transform, false);
        }

        go.AddComponent<TeacherReferenceGuideUI>();
    }

    private void Awake()
    {
        uiFont = CreateReadableFont();
        BuildUi();
        SetVisible(false, true);
        RefreshScenario();
    }

    private void Update()
    {
        HandleInput();
        AnimateVisibility();
        if (visible)
        {
            HandleScrollInput();
        }
    }

    private void HandleInput()
    {
        bool primaryPressed = usePrimaryButtonToggle && ReadQuestAButton();
        if ((primaryPressed && !lastPrimaryPressed) || DesktopInputBridge.GetKeyDown(desktopToggleKey))
        {
            SetVisible(!visible, false);
        }

        bool triggerPressed = useTriggerInteraction && ReadTriggerButton();
        LogInputDiagnostics(primaryPressed, triggerPressed);
        if (visible)
        {
            UpdateRayInteraction(triggerPressed);
        }

        if (visible && DesktopInputBridge.GetKeyDown(desktopNextTabKey))
        {
            SelectScenario((currentScenario + 1) % scenarios.Length);
        }

        if (visible)
        {
            if (DesktopInputBridge.GetKeyDown(KeyCode.Alpha1)) SelectScenario(0);
            if (DesktopInputBridge.GetKeyDown(KeyCode.Alpha2)) SelectScenario(1);
            if (DesktopInputBridge.GetKeyDown(KeyCode.Alpha3)) SelectScenario(2);
        }

        lastPrimaryPressed = primaryPressed;
        lastTriggerPressed = triggerPressed;
    }

    private void HandleScrollInput()
    {
        float scroll = 0f;
        Vector2 axis = ReadAxis(CommonUsages.primary2DAxis);
        if (Mathf.Abs(axis.y) > 0.28f)
        {
            scroll -= axis.y * ScrollSpeed * Time.deltaTime;
        }

        scroll -= DesktopInputBridge.GetMouseScrollY() * 70f;
        if (DesktopInputBridge.GetKey(KeyCode.DownArrow)) scroll += ScrollSpeed * Time.deltaTime;
        if (DesktopInputBridge.GetKey(KeyCode.UpArrow)) scroll -= ScrollSpeed * Time.deltaTime;

        if (!Mathf.Approximately(scroll, 0f))
        {
            scrollOffset = Mathf.Clamp(scrollOffset + scroll, 0f, maxScrollOffset);
            ApplyScrollOffset();
        }
    }

    private bool ReadQuestAButton()
    {
        return ReadButton(CommonUsages.primaryButton) ||
               ReadNamedBoolFeature("primaryButton") ||
               ReadInputSystemButton(InputSystemAButtonNames);
    }

    private bool ReadTriggerButton()
    {
        return ReadButton(CommonUsages.triggerButton) ||
               ReadNamedBoolFeature("triggerButton") ||
               ReadNamedBoolFeature("triggerPressed") ||
               ReadInputSystemButton(InputSystemTriggerButtonNames);
    }

    private bool ReadButton(InputFeatureUsage<bool> feature)
    {
        if (!TryGetControllerDevice(out InputDevice device)) return false;
        return device.isValid && device.TryGetFeatureValue(feature, out bool pressed) && pressed;
    }

    private Vector2 ReadAxis(InputFeatureUsage<Vector2> feature)
    {
        if (!TryGetControllerDevice(out InputDevice device)) return Vector2.zero;
        return device.isValid && device.TryGetFeatureValue(feature, out Vector2 axis) ? axis : Vector2.zero;
    }

    private bool ReadNamedBoolFeature(string featureName)
    {
        if (!TryGetControllerDevice(out InputDevice device) || !device.isValid) return false;

        List<InputFeatureUsage> featureUsages = new List<InputFeatureUsage>();
        if (!device.TryGetFeatureUsages(featureUsages)) return false;

        for (int i = 0; i < featureUsages.Count; i++)
        {
            InputFeatureUsage usage = featureUsages[i];
            if (usage.type != typeof(bool) ||
                !string.Equals(usage.name, featureName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (device.TryGetFeatureValue(usage.As<bool>(), out bool pressed) && pressed)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetControllerDevice(out InputDevice device)
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(controllerNode, devices);
        if (devices.Count > 0 && devices[0].isValid)
        {
            device = devices[0];
            return true;
        }

        devices.Clear();
        InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller;
        characteristics |= controllerNode == XRNode.RightHand
            ? InputDeviceCharacteristics.Right
            : InputDeviceCharacteristics.Left;
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);
        if (devices.Count > 0 && devices[0].isValid)
        {
            device = devices[0];
            return true;
        }

        device = default;
        return false;
    }

    private bool ReadInputSystemButton(string[] buttonNames)
    {
#if ENABLE_INPUT_SYSTEM
        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (!IsMatchingInputSystemController(device)) continue;

            for (int i = 0; i < buttonNames.Length; i++)
            {
                ButtonControl button = device.TryGetChildControl<ButtonControl>(buttonNames[i]);
                if (button != null && button.isPressed)
                {
                    return true;
                }
            }
        }
#endif

        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private bool IsMatchingInputSystemController(UnityEngine.InputSystem.InputDevice device)
    {
        string expectedUsage = controllerNode == XRNode.RightHand ? "RightHand" : "LeftHand";
        foreach (UnityEngine.InputSystem.Utilities.InternedString usage in device.usages)
        {
            if (string.Equals(usage.ToString(), expectedUsage, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        string deviceText = (device.name + " " + device.displayName + " " + device.layout).ToLowerInvariant();
        return controllerNode == XRNode.RightHand
            ? deviceText.Contains("right")
            : deviceText.Contains("left");
    }
#endif

    private void LogInputDiagnostics(bool primaryPressed, bool triggerPressed)
    {
        if (!logInputDiagnostics ||
            diagnosticsLogCount >= maxInputDiagnosticLogs ||
            Time.unscaledTime < nextDiagnosticsTime)
        {
            return;
        }

        diagnosticsLogCount++;
        nextDiagnosticsTime = Time.unscaledTime + DiagnosticsInterval;
        if (TryGetControllerDevice(out InputDevice device))
        {
            Debug.Log($"[TeacherReferenceGuideUI] Controller={device.name}, valid={device.isValid}, A={primaryPressed}, trigger={triggerPressed}, boolFeatures={DescribeBoolFeatures(device)}");
        }
        else
        {
            Debug.Log("[TeacherReferenceGuideUI] No right-hand XR controller device detected.");
        }
    }

    private string DescribeBoolFeatures(InputDevice device)
    {
        List<InputFeatureUsage> featureUsages = new List<InputFeatureUsage>();
        if (!device.TryGetFeatureUsages(featureUsages)) return "unavailable";

        string result = string.Empty;
        int count = 0;
        for (int i = 0; i < featureUsages.Count && count < 12; i++)
        {
            InputFeatureUsage usage = featureUsages[i];
            if (usage.type != typeof(bool)) continue;

            result += count == 0 ? usage.name : "," + usage.name;
            count++;
        }

        return string.IsNullOrEmpty(result) ? "none" : result;
    }

    private void UpdateRayInteraction(bool triggerPressed)
    {
        bool hasHit = TryGetPanelHitLocal(out Vector2 localPoint);
        UpdatePointerReticle(hasHit, localPoint);

        int nextHoveredTab = hasHit ? HitTestTabs(localPoint) : -1;
        if (nextHoveredTab != hoveredTabIndex)
        {
            hoveredTabIndex = nextHoveredTab;
            RefreshTabVisuals();
        }

        if (triggerPressed && !lastTriggerPressed)
        {
            if (hoveredTabIndex >= 0)
            {
                SelectScenario(hoveredTabIndex);
                draggingSteps = false;
            }
            else if (hasHit)
            {
                draggingSteps = true;
                lastDragLocalY = localPoint.y;
            }
        }
        else if (!triggerPressed)
        {
            draggingSteps = false;
        }

        if (triggerPressed && draggingSteps && hasHit)
        {
            float deltaY = localPoint.y - lastDragLocalY;
            if (Mathf.Abs(deltaY) > 0.01f)
            {
                scrollOffset = Mathf.Clamp(scrollOffset + deltaY * 1.15f, 0f, maxScrollOffset);
                ApplyScrollOffset();
                lastDragLocalY = localPoint.y;
            }
        }
    }

    private int HitTestTabs(Vector2 panelLocalPoint)
    {
        for (int i = 0; i < tabRects.Count; i++)
        {
            RectTransform tabRect = tabRects[i];
            if (tabRect == null) continue;

            Vector3 worldPoint = transform.TransformPoint(new Vector3(panelLocalPoint.x, panelLocalPoint.y, 0f));
            Vector3 tabLocalPoint = tabRect.InverseTransformPoint(worldPoint);
            if (tabRect.rect.Contains(new Vector2(tabLocalPoint.x, tabLocalPoint.y)))
            {
                return i;
            }
        }

        return -1;
    }

    private bool TryGetPanelHitLocal(out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (!TryGetControllerRay(out Ray ray)) return false;

        Plane panelPlane = new Plane(transform.forward, transform.position);
        if (!panelPlane.Raycast(ray, out float distance) || distance < 0f) return false;

        Vector3 worldPoint = ray.GetPoint(distance);
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Rect rect = new Rect(-CanvasWidth * 0.5f, -CanvasHeight * 0.5f, CanvasWidth, CanvasHeight);
        localPoint = new Vector2(local.x, local.y);
        return rect.Contains(localPoint);
    }

    private bool TryGetControllerRay(out Ray ray)
    {
        Transform controllerRay = GetWorldControllerRayTransform();
        if (controllerRay != null)
        {
            ray = new Ray(controllerRay.position, controllerRay.forward);
            return true;
        }

        if (TryGetControllerDevice(out InputDevice device))
        {
            if (device.isValid &&
                device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                ray = new Ray(position, rotation * Vector3.forward);
                return true;
            }
        }

        Transform head = GetHead();
        if (head != null)
        {
            Vector3 position = head.TransformPoint(editorRayLocalPosition);
            Quaternion rotation = head.rotation * Quaternion.Euler(editorRayLocalEuler);
            ray = new Ray(position, rotation * Vector3.forward);
            return true;
        }

        ray = default;
        return false;
    }

    private Transform GetWorldControllerRayTransform()
    {
        if (controllerRayOverride != null)
        {
            return controllerRayOverride;
        }

        if (cachedControllerRayVisual != null &&
            cachedControllerRayVisual.controllerNode == controllerNode)
        {
            return cachedControllerRayVisual.transform;
        }

        QuestControllerRayVisual[] visuals = UnityEngine.Object.FindObjectsByType<QuestControllerRayVisual>(FindObjectsSortMode.None);
        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] != null && visuals[i].controllerNode == controllerNode)
            {
                cachedControllerRayVisual = visuals[i];
                return cachedControllerRayVisual.transform;
            }
        }

        string anchorName = controllerNode == XRNode.RightHand ? "Right Controller Anchor" : "Left Controller Anchor";
        GameObject anchor = GameObject.Find(anchorName);
        return anchor != null ? anchor.transform : null;
    }

    private void SetVisible(bool show, bool immediate)
    {
        visible = show;
        if (canvasGroup == null) return;

        canvasGroup.blocksRaycasts = show;
        canvasGroup.interactable = show;

        if (show)
        {
            SetPanelColliderEnabled(false);
            PlaceAtCurrentRayDirection();
        }

        SetPanelColliderEnabled(show);
        UpdatePointerReticle(false, Vector2.zero);

        if (immediate)
        {
            canvasGroup.alpha = show ? 1f : 0f;
        }
    }

    private void AnimateVisibility()
    {
        if (canvasGroup == null) return;

        float target = visible ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.deltaTime * 9f);
    }

    private void PlaceAtCurrentRayDirection()
    {
        Transform head = GetHead();
        if (head == null) return;

        Vector3 direction = head.forward;
        if (TryGetControllerRay(out Ray ray))
        {
            Vector3 referencePoint;
            if (Physics.Raycast(ray, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                referencePoint = hit.point;
            }
            else
            {
                referencePoint = ray.origin + ray.direction * fixedEyeDistance;
            }

            Vector3 fromEye = referencePoint - head.position;
            if (fromEye.sqrMagnitude > 0.01f)
            {
                direction = fromEye.normalized;
            }
        }

        transform.position = head.position + direction * fixedEyeDistance;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        hoveredTabIndex = -1;
        draggingSteps = false;
        RefreshTabVisuals();
    }

    private Transform GetHead()
    {
        if (headOverride != null) return headOverride;
        return Camera.main != null ? Camera.main.transform : null;
    }

    private void SelectScenario(int index)
    {
        currentScenario = Mathf.Clamp(index, 0, scenarios.Length - 1);
        scrollOffset = 0f;
        RefreshScenario();
    }

    private void RefreshScenario()
    {
        if (scenarioTitle == null || scenarioSummary == null || listContent == null) return;

        ScenarioData scenario = scenarios[currentScenario];
        scenarioTitle.text = scenario.Title;
        scenarioSummary.text = scenario.Summary;

        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Destroy(listContent.GetChild(i).gameObject);
        }

        for (int i = 0; i < scenario.Steps.Length; i++)
        {
            CreateStepRow(listContent, i + 1, scenario.Steps[i]);
        }

        RefreshTabVisuals();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        maxScrollOffset = Mathf.Max(0f, listContent.rect.height - StepViewportHeight);
        ApplyScrollOffset();
    }

    private void ApplyScrollOffset()
    {
        if (listContent == null) return;

        Vector2 anchored = listContent.anchoredPosition;
        anchored.y = scrollOffset;
        listContent.anchoredPosition = anchored;
    }

    private void BuildUi()
    {
        transform.localScale = Vector3.one * worldScale;

        GameObject canvasGo = new GameObject("Reference Guide Canvas");
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;

        canvasGroup = canvasGo.AddComponent<CanvasGroup>();
        canvasGo.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 2.5f;
        scaler.referencePixelsPerUnit = 100f;

        canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        panelCollider = gameObject.AddComponent<BoxCollider>();
        panelCollider.isTrigger = true;
        panelCollider.center = new Vector3(0f, 0f, -8f);
        panelCollider.size = new Vector3(CanvasWidth, CanvasHeight, 4f);
        SetPanelColliderEnabled(false);

        Image shell = CreateImage(canvasRect, "Card Shell", cardColor, RoundedSprite(34, cardColor));
        Stretch(shell.rectTransform);

        Shadow shadow = shell.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(9f, -11f);

        Outline outline = shell.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.26f);
        outline.effectDistance = new Vector2(2f, 2f);

        RectTransform header = CreatePanel(canvasRect, "Header", new Vector2(0f, -18f), new Vector2(650f, 126f), headerColor, 26);
        AnchorTop(header);

        Text title = CreateText(header, "Title", "参考教材", 34, FontStyle.Bold, textColor, TextAnchor.UpperLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 0f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(28f, 48f);
        title.rectTransform.offsetMax = new Vector2(-28f, -18f);

        Text subtitle = CreateText(header, "Subtitle", "DTT 训练步骤速查", 18, FontStyle.Normal, mutedTextColor, TextAnchor.UpperLeft);
        subtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
        subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        subtitle.rectTransform.offsetMin = new Vector2(30f, 18f);
        subtitle.rectTransform.offsetMax = new Vector2(-28f, -72f);

        RectTransform tabBar = CreatePanel(canvasRect, "Scenario Tabs", new Vector2(0f, -158f), new Vector2(650f, 62f), new Color(1f, 1f, 1f, 0.055f), 20);
        AnchorTop(tabBar);
        HorizontalLayoutGroup tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.padding = new RectOffset(10, 10, 8, 8);
        tabLayout.spacing = 10f;
        tabLayout.childControlHeight = true;
        tabLayout.childControlWidth = true;
        tabLayout.childForceExpandHeight = true;
        tabLayout.childForceExpandWidth = true;

        for (int i = 0; i < scenarios.Length; i++)
        {
            CreateTab(tabBar, scenarios[i].Tab);
        }

        RectTransform titlePanel = CreatePanel(canvasRect, "Scenario Summary", new Vector2(0f, -238f), new Vector2(650f, 104f), new Color(0.12f, 0.22f, 0.22f, 0.70f), 20);
        AnchorTop(titlePanel);

        scenarioTitle = CreateText(titlePanel, "Scenario Title", string.Empty, 25, FontStyle.Bold, textColor, TextAnchor.UpperLeft);
        scenarioTitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        scenarioTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
        scenarioTitle.rectTransform.offsetMin = new Vector2(24f, -6f);
        scenarioTitle.rectTransform.offsetMax = new Vector2(-24f, -14f);

        scenarioSummary = CreateText(titlePanel, "Scenario Summary Text", string.Empty, 18, FontStyle.Normal, mutedTextColor, TextAnchor.UpperLeft);
        scenarioSummary.rectTransform.anchorMin = new Vector2(0f, 0f);
        scenarioSummary.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        scenarioSummary.rectTransform.offsetMin = new Vector2(24f, 8f);
        scenarioSummary.rectTransform.offsetMax = new Vector2(-24f, -2f);

        RectTransform viewport = CreatePanel(canvasRect, "Steps Viewport", new Vector2(0f, -360f), new Vector2(650f, StepViewportHeight), new Color(0.12f, 0.21f, 0.21f, 0.76f), 24);
        AnchorTop(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        GameObject contentGo = new GameObject("Steps Content");
        contentGo.transform.SetParent(viewport, false);
        listContent = contentGo.AddComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        listContent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup listLayout = contentGo.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(18, 18, 18, 18);
        listLayout.spacing = 10f;
        listLayout.childControlHeight = true;
        listLayout.childControlWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform footer = CreatePanel(canvasRect, "Footer", new Vector2(0f, 28f), new Vector2(650f, 42f), new Color(1f, 1f, 1f, 0.045f), 16);
        AnchorBottom(footer);
        Text footerText = CreateText(footer, "Footer Text", "板机点选情景 / 按住拖动滚动     A 收起", 16, FontStyle.Normal, mutedTextColor, TextAnchor.MiddleCenter);
        Stretch(footerText.rectTransform, 0f);

        CreatePointerReticle();
    }

    private void CreateTab(RectTransform parent, string label)
    {
        Image tab = CreateImage(parent, label + " Tab", new Color(1f, 1f, 1f, 0.08f), RoundedSprite(18, Color.white));
        LayoutElement layout = tab.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 46f;

        Text tabText = CreateText(tab.rectTransform, label + " Text", label, 19, FontStyle.Normal, textColor, TextAnchor.MiddleCenter);
        Stretch(tabText.rectTransform, 0f);

        tabBackgrounds.Add(tab);
        tabLabels.Add(tabText);
        tabRects.Add(tab.rectTransform);
    }

    private void RefreshTabVisuals()
    {
        for (int i = 0; i < tabBackgrounds.Count; i++)
        {
            bool active = i == currentScenario;
            bool hovered = i == hoveredTabIndex;
            if (active)
            {
                tabBackgrounds[i].color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.96f);
                tabLabels[i].color = Color.black;
                tabLabels[i].fontStyle = FontStyle.Bold;
            }
            else if (hovered)
            {
                tabBackgrounds[i].color = new Color(warmAccentColor.r, warmAccentColor.g, warmAccentColor.b, 0.30f);
                tabLabels[i].color = textColor;
                tabLabels[i].fontStyle = FontStyle.Bold;
            }
            else
            {
                tabBackgrounds[i].color = new Color(1f, 1f, 1f, 0.08f);
                tabLabels[i].color = textColor;
                tabLabels[i].fontStyle = FontStyle.Normal;
            }
        }
    }

    private void SetPanelColliderEnabled(bool enabled)
    {
        if (panelCollider != null)
        {
            panelCollider.enabled = enabled;
        }
    }

    private void CreatePointerReticle()
    {
        Image outer = CreateImage(canvasRect, "Ray Hit Reticle", new Color(0.36f, 1f, 0.62f, 0.98f), RoundedSprite(28, Color.white));
        pointerReticle = outer.rectTransform;
        pointerReticle.anchorMin = new Vector2(0.5f, 0.5f);
        pointerReticle.anchorMax = new Vector2(0.5f, 0.5f);
        pointerReticle.pivot = new Vector2(0.5f, 0.5f);
        pointerReticle.sizeDelta = new Vector2(30f, 30f);

        Outline outline = outer.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.92f);
        outline.effectDistance = new Vector2(2.5f, 2.5f);

        Shadow shadow = outer.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(3f, -3f);

        Image inner = CreateImage(pointerReticle, "Ray Hit Reticle Core", new Color(0.03f, 0.12f, 0.10f, 0.88f), RoundedSprite(12, Color.white));
        inner.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        inner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        inner.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        inner.rectTransform.sizeDelta = new Vector2(9f, 9f);
        inner.rectTransform.anchoredPosition = Vector2.zero;

        pointerReticle.gameObject.SetActive(false);
    }

    private void UpdatePointerReticle(bool hasHit, Vector2 panelLocalPoint)
    {
        if (pointerReticle == null) return;

        pointerReticle.gameObject.SetActive(visible && hasHit);
        if (!hasHit) return;

        pointerReticle.anchoredPosition = panelLocalPoint;
        pointerReticle.SetAsLastSibling();
    }

    private void CreateStepRow(RectTransform parent, int number, string text)
    {
        RectTransform row = CreatePanel(parent, "Step " + number.ToString("00"), Vector2.zero, new Vector2(0f, 48f), new Color(1f, 1f, 1f, 0.12f), 18);
        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = EstimateRowHeight(text);
        rowLayout.preferredHeight = rowLayout.minHeight;

        HorizontalLayoutGroup rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowGroup.padding = new RectOffset(12, 16, 9, 9);
        rowGroup.spacing = 13f;
        rowGroup.childAlignment = TextAnchor.MiddleLeft;
        rowGroup.childControlHeight = true;
        rowGroup.childControlWidth = true;
        rowGroup.childForceExpandHeight = true;
        rowGroup.childForceExpandWidth = false;

        RectTransform numberBadge = CreatePanel(row, "Number", Vector2.zero, new Vector2(42f, 42f), NumberColor(number), 17);
        LayoutElement numberLayout = numberBadge.gameObject.AddComponent<LayoutElement>();
        numberLayout.preferredWidth = 42f;
        numberLayout.preferredHeight = 42f;

        Text numberText = CreateText(numberBadge, "Number Text", number.ToString("00"), 18, FontStyle.Bold, Color.black, TextAnchor.MiddleCenter);
        Stretch(numberText.rectTransform, 0f);

        Text stepText = CreateText(row, "Step Text", HighlightStepText(text), 19, FontStyle.Normal, textColor, TextAnchor.MiddleLeft);
        stepText.supportRichText = true;
        stepText.horizontalOverflow = HorizontalWrapMode.Wrap;
        stepText.verticalOverflow = VerticalWrapMode.Overflow;
        LayoutElement textLayout = stepText.gameObject.AddComponent<LayoutElement>();
        textLayout.preferredWidth = 530f;
        textLayout.flexibleWidth = 1f;
    }

    private float EstimateRowHeight(string text)
    {
        if (text.Length > 42) return 76f;
        if (text.Length > 30) return 62f;
        return 52f;
    }

    private Color NumberColor(int number)
    {
        if (number == 4 || number == 7 || number == 9 || number == 12 || number == 14 || number == 16)
        {
            return warmAccentColor;
        }

        return accentColor;
    }

    private string HighlightStepText(string value)
    {
        return value
            .Replace("“这是什么”", "<b><color=#72FFF2>“这是什么”</color></b>")
            .Replace("5秒", "<b><color=#FFD36A>5秒</color></b>")
            .Replace("半辅助", "<b><color=#FFD36A>半辅助</color></b>")
            .Replace("全辅助", "<b><color=#FF9F7A>全辅助</color></b>")
            .Replace("不提供反馈", "<color=#A7B9BE>不提供反馈</color>")
            .Replace("不提供任何辅助", "<color=#A7B9BE>不提供任何辅助</color>");
    }

    private RectTransform CreatePanel(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color, int radius)
    {
        Image image = CreateImage(parent, name, color, RoundedSprite(radius, color));
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private Image CreateImage(RectTransform parent, string name, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        return image;
    }

    private Text CreateText(RectTransform parent, string name, string value, int size, FontStyle style, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Sprite RoundedSprite(int radius, Color fill)
    {
        int width = 96;
        int height = 96;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "TeacherGuideRoundedSprite"
        };

        Color transparent = new Color(fill.r, fill.g, fill.b, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(radius - x, x - (width - radius - 1), 0);
                float dy = Mathf.Max(radius - y, y - (height - radius - 1), 0);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                texture.SetPixel(x, y, Color.Lerp(transparent, Color.white, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private Font CreateReadableFont()
    {
        try
        {
            Font font = Font.CreateDynamicFontFromOSFont(FontCandidates, 24);
            if (font != null) return font;
        }
        catch
        {
            // Fall through to Unity's built-in font when OS font lookup is unavailable.
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void AnchorTop(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
    }

    private static void AnchorBottom(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
    }

    private class ScenarioData
    {
        public string Tab;
        public string Title;
        public string Summary;
        public string[] Steps;
    }
}

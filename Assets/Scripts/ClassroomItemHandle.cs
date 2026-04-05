using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 将此脚本挂在水杯/铅笔/橡皮/尺子等可抓取物体上。
/// 需要物体上已有 XRGrabInteractable 组件。
/// </summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class ClassroomItemHandle : MonoBehaviour
{
    public ClassroomItemType itemType = ClassroomItemType.Cup;
    public ClassroomScenarioController scenarioController;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;

    void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (scenarioController == null)
        {
            scenarioController = FindObjectOfType<ClassroomScenarioController>();
        }
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (scenarioController != null)
        {
            scenarioController.SetCurrentItem(itemType);
        }
    }
}


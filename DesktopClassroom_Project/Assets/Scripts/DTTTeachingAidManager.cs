using System.Collections.Generic;
using UnityEngine;

public class DTTTeachingAidManager : MonoBehaviour
{
    public static DTTTeachingAidManager Instance { get; private set; }

    [Header("Teaching Aids")]
    public List<DTTTeachingAid> teachingAids = new List<DTTTeachingAid>();
    public DTTTeachingAid selectedAid;
    public DTTTeachingAid heldAid;
    public ClassroomScenarioController scenarioController;

    [Header("Target Student")]
    public DTTTargetStudentMarker selectedStudent;

    [Header("Hold Pose")]
    public Vector3 holdLocalOffset = new Vector3(0f, -0.03f, 0.18f);
    public Vector3 holdLocalEulerOffset = new Vector3(0f, 0f, 0f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple DTTTeachingAidManager instances found. Keeping the first instance.");
            return;
        }

        Instance = this;
        if (scenarioController == null)
        {
            scenarioController = FindObjectOfType<ClassroomScenarioController>();
        }
        RefreshTeachingAids();
    }

    public void RefreshTeachingAids()
    {
        teachingAids.RemoveAll(aid => aid == null);

        DTTTeachingAid[] found = FindObjectsOfType<DTTTeachingAid>();
        foreach (DTTTeachingAid aid in found)
        {
            if (!teachingAids.Contains(aid))
            {
                teachingAids.Add(aid);
            }
        }
    }

    public Transform GetCurrentTeachingStimulusTarget()
    {
        if (heldAid != null)
        {
            return heldAid.GetGazeTarget();
        }

        if (selectedAid != null)
        {
            return selectedAid.GetGazeTarget();
        }

        return null;
    }

    public void SelectAid(DTTTeachingAid aid)
    {
        if (selectedAid != null)
        {
            selectedAid.isSelected = false;
        }

        selectedAid = aid;

        if (selectedAid != null)
        {
            selectedAid.isSelected = true;
            SyncScenarioCurrentItem(selectedAid);
            Debug.Log($"[DTT] Selected teaching aid: {selectedAid.displayName}");
        }
    }

    public void SelectStudent(DTTTargetStudentMarker student)
    {
        SetSelectedStudent(student, true);
    }

    public void SelectStudentForGazeSimulator(DTTChildGazeSimulator simulator)
    {
        if (simulator == null) return;

        DTTTargetStudentMarker marker = simulator.GetComponentInParent<DTTTargetStudentMarker>();
        if (marker == null)
        {
            marker = simulator.GetComponentInChildren<DTTTargetStudentMarker>();
        }

        if (marker == null)
        {
            marker = simulator.gameObject.AddComponent<DTTTargetStudentMarker>();
        }

        SetSelectedStudent(marker, false);
    }

    private void SetSelectedStudent(DTTTargetStudentMarker student, bool syncGazeSelection)
    {
        DeselectAllStudentMarkers();

        selectedStudent = student;

        if (selectedStudent != null)
        {
            selectedStudent.SetSelected(true);
            if (syncGazeSelection)
            {
                DTTChildGazeSimulator.SelectChildByGameObject(selectedStudent.gameObject);
            }

            BehaviorDemoController demo = FindObjectOfType<BehaviorDemoController>();
            if (demo != null)
            {
                demo.SelectStudentByGameObject(selectedStudent.gameObject, false);
            }

            Debug.Log($"[DTT] Selected target student: {selectedStudent.gameObject.name}");
        }
    }

    private void DeselectAllStudentMarkers()
    {
        DTTTargetStudentMarker[] markers = FindObjectsOfType<DTTTargetStudentMarker>(true);
        foreach (DTTTargetStudentMarker marker in markers)
        {
            if (marker != null)
            {
                marker.SetSelected(false);
            }
        }
    }

    public void BeginHoldSelectedAid()
    {
        if (selectedAid == null) return;
        if (heldAid == selectedAid) return;

        if (heldAid != null)
        {
            heldAid.EndHold();
        }

        heldAid = selectedAid;
        heldAid.BeginHold();
        SyncScenarioCurrentItem(heldAid);
        Debug.Log($"[DTT] Holding teaching aid: {heldAid.displayName}");
    }

    public void NotifyAidGrabbed(DTTTeachingAid aid)
    {
        if (aid == null) return;

        SelectAid(aid);
        SyncScenarioCurrentItem(aid);
    }

    public void UpdateHeldAid(Transform holdAnchor)
    {
        if (heldAid == null || holdAnchor == null) return;

        heldAid.transform.position = holdAnchor.TransformPoint(holdLocalOffset);
        heldAid.transform.rotation = holdAnchor.rotation * Quaternion.Euler(holdLocalEulerOffset);
    }

    public void ReleaseHeldAid()
    {
        if (heldAid == null) return;

        heldAid.EndHold();
        Debug.Log($"[DTT] Released teaching aid: {heldAid.displayName}");
        heldAid = null;
    }

    private void SyncScenarioCurrentItem(DTTTeachingAid aid)
    {
        if (aid == null) return;

        if (scenarioController == null)
        {
            scenarioController = FindObjectOfType<ClassroomScenarioController>();
        }

        if (scenarioController == null) return;

        if (aid.TryGetClassroomItemType(out ClassroomItemType itemType))
        {
            scenarioController.SetCurrentItem(itemType);
        }
    }
}

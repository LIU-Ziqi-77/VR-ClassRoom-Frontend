using UnityEngine;

public enum DTTTeachingAidKind
{
    Ruler,
    Rubber,
    OpenNotebook,
    Pencils
}

/// <summary>
/// Marks a classroom object as a DTT teaching aid that can be selected, held,
/// and used as the current gaze stimulus.
/// </summary>
public class DTTTeachingAid : MonoBehaviour
{
    public DTTTeachingAidKind aidKind = DTTTeachingAidKind.Ruler;
    public string displayName = "Teaching Aid";
    public Transform gazeTarget;

    [Header("Hold State")]
    public bool isSelected;
    public bool isHeld;
    public bool returnToOriginalPoseOnRelease = true;

    private Rigidbody cachedRigidbody;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    void Awake()
    {
        if (gazeTarget == null)
        {
            gazeTarget = transform;
        }

        cachedRigidbody = GetComponent<Rigidbody>();
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    public Transform GetGazeTarget()
    {
        return gazeTarget != null ? gazeTarget : transform;
    }

    public void BeginHold()
    {
        isHeld = true;

        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        if (cachedRigidbody != null)
        {
            originalUseGravity = cachedRigidbody.useGravity;
            originalIsKinematic = cachedRigidbody.isKinematic;
            cachedRigidbody.useGravity = false;
            cachedRigidbody.isKinematic = true;
        }
    }

    public void EndHold()
    {
        isHeld = false;

        if (returnToOriginalPoseOnRelease)
        {
            transform.SetPositionAndRotation(originalPosition, originalRotation);
        }

        if (cachedRigidbody != null)
        {
            cachedRigidbody.useGravity = originalUseGravity;
            cachedRigidbody.isKinematic = originalIsKinematic;
        }
    }
}

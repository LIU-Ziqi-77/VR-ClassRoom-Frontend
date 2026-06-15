using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// VR locomotion configurator for the classroom teacher rig.
/// Compatible with XR Interaction Toolkit 3.x.
///
/// Attach to the <b>XR Origin (VR)</b> GameObject and hit Play.
///
/// Automatically adds (if absent):
///   • XRBodyTransformer      — queues and applies body transformations
///   • LocomotionMediator     — mediates between providers and XRBodyTransformer
///   • ContinuousMoveProvider — left-stick smooth walk
///   • SnapTurnProvider       — right-stick snap turn
///   • CharacterController    — collision + gravity on the XR Origin root
///
/// ── INPUT ACTION WIRING (manual step in Inspector) ────────────────
/// The new XRIT 3.x providers use XRInputValueReader instead of
/// InputActionProperty.  After this script runs you MUST configure
/// the Input Action Bindings on each provider:
///
///   ContinuousMoveProvider  → Left Hand Move Input
///     InputActionReference: XRI LeftHand Locomotion / Move
///
///   SnapTurnProvider        → Right Hand Turn Input
///     InputActionReference: XRI RightHand Locomotion / Snap Turn
///
/// These actions ship with XR Interaction Toolkit under:
///   Packages → XR Interaction Toolkit → Samples → Starter Assets
/// Import the Starter Assets sample, then assign the actions from
///   Assets/Samples/XR Interaction Toolkit/[version]/Starter Assets/
///   XRI Default Input Actions.inputactions
/// ──────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(XROrigin))]
public class XRClassroomLocomotion : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Walking speed in metres per second (applied to ContinuousMoveProvider).")]
    public float moveSpeed = 2.5f;

    [Tooltip("Enable lateral strafe with the left stick.")]
    public bool enableStrafe = true;

    [Tooltip("Apply gravity via CharacterController when enableFly is false.")]
    public bool useGravity = true;

    [Header("Turning")]
    [Tooltip("Degrees per snap-turn event.")]
    public float snapTurnAmount = 45f;

    [Header("Collision Capsule")]
    [Tooltip("CharacterController capsule height. The CC is added to the XR Origin root " +
             "so ContinuousMoveProvider can find it for gravity / collision.")]
    public float characterHeight = 1.8f;
    public float characterRadius = 0.3f;

    // ─── Runtime ────────────────────────────────────────────

    void Start()
    {
        if (!IsXRActive())
        {
            Debug.Log("[XRLocomotion] XR device not active — skipping VR locomotion setup (desktop mode).");
            enabled = false;
            return;
        }

        EnsureLocomotionInfrastructure();
        ConfigureMoveProvider();
        ConfigureTurnProvider();
        ConfigureCharacterController();

        Debug.Log($"[XRLocomotion] VR locomotion configured on '{gameObject.name}'. " +
                  $"Speed={moveSpeed} m/s  SnapTurn={snapTurnAmount}°. " +
                  "If providers don't respond to input, assign XRInputValueReader references in Inspector.");

        enabled = false; // one-shot setup
    }

    // ─── Infrastructure ──────────────────────────────────────

    /// Ensures XRBodyTransformer and LocomotionMediator are present.
    /// LocomotionProvider.Awake() uses GetComponentInParent<LocomotionMediator>()
    /// so they must exist on this same GameObject before the providers are added.
    void EnsureLocomotionInfrastructure()
    {
        // XRBodyTransformer must exist before LocomotionMediator (it's a RequireComponent)
        if (GetComponent<XRBodyTransformer>() == null)
        {
            gameObject.AddComponent<XRBodyTransformer>();
            Debug.Log("[XRLocomotion] Added XRBodyTransformer.");
        }

        if (GetComponent<LocomotionMediator>() == null)
        {
            gameObject.AddComponent<LocomotionMediator>();
            Debug.Log("[XRLocomotion] Added LocomotionMediator.");
        }
    }

    // ─── Locomotion Providers ────────────────────────────────

    void ConfigureMoveProvider()
    {
        var provider = GetComponent<ContinuousMoveProvider>();
        if (provider == null)
        {
            provider = gameObject.AddComponent<ContinuousMoveProvider>();
            Debug.Log("[XRLocomotion] Added ContinuousMoveProvider. " +
                      "→ Assign leftHandMoveInput (XRI LeftHand Locomotion/Move) in Inspector.");
        }

        provider.moveSpeed    = moveSpeed;
        provider.enableStrafe = enableStrafe;
        provider.useGravity   = useGravity;
        provider.enableFly    = false;
    }

    void ConfigureTurnProvider()
    {
        var provider = GetComponent<SnapTurnProvider>();
        if (provider == null)
        {
            provider = gameObject.AddComponent<SnapTurnProvider>();
            Debug.Log("[XRLocomotion] Added SnapTurnProvider. " +
                      "→ Assign rightHandTurnInput (XRI RightHand Locomotion/Snap Turn) in Inspector.");
        }

        provider.turnAmount          = snapTurnAmount;
        provider.enableTurnLeftRight = true;
        provider.enableTurnAround    = true;
    }

    // ─── CharacterController ─────────────────────────────────

    /// ContinuousMoveProvider.FindCharacterController() searches the XR Origin
    /// root for a CharacterController (gravity + wall collision).
    void ConfigureCharacterController()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
            Debug.Log("[XRLocomotion] Added CharacterController to XR Origin root.");
        }

        cc.height     = characterHeight;
        cc.radius     = characterRadius;
        cc.center     = new Vector3(0, characterHeight * 0.5f, 0);
        cc.slopeLimit = 45f;
        cc.stepOffset  = 0.3f;
    }

    // ─── XR Detection ────────────────────────────────────────

    static bool IsXRActive()
    {
        // XRSettings.isDeviceActive / .enabled are the most reliable runtime checks
        // that work without a dependency on XR Management's initialization pipeline.
#pragma warning disable CS0618
        return XRSettings.isDeviceActive || XRSettings.enabled;
#pragma warning restore CS0618
    }
}

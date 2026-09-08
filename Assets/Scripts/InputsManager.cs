using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-level input facade.
///
/// Gameplay systems should ask this component for actions instead of owning
/// concrete keyboard / mouse bindings themselves. The current implementation
/// intentionally reads Unity's existing Input API so we do not disturb the
/// project's input architecture now.
///
/// Later, Steam Deck / gamepad support or a migration to Unity's newer Input
/// System can be implemented behind this class while gameplay code keeps using
/// the same action-facing API.
/// </summary>
[DisallowMultipleComponent]
public class InputsManager : MonoBehaviour
{
    public static InputsManager Instance;

    // ===========================================================
    // STICKER DRAG ROTATION
    // ===========================================================

    [Header("Sticker Drag Rotation")]

    [Tooltip(
        "Master toggle for rotating stickers while they are being dragged."
    )]
    [SerializeField]
    private bool enableStickerDragRotation =
        true;


    [Header("Sticker Rotation - Keyboard")]

    [Tooltip("Hold this key to rotate the dragged sticker to the left.")]
    [SerializeField]
    private KeyCode stickerRotateLeftKey =
        KeyCode.A;

    [Tooltip("Hold this key to rotate the dragged sticker to the right.")]
    [SerializeField]
    private KeyCode stickerRotateRightKey =
        KeyCode.D;

    [Tooltip("Continuous keyboard rotation speed in degrees per second.")]
    [Min(0f)]
    [SerializeField]
    private float stickerKeyboardRotationSpeed =
        120f;


    [Header("Sticker Rotation - Mouse Wheel")]

    [Tooltip("Allow mouse-wheel rotation while a sticker is being dragged.")]
    [SerializeField]
    private bool enableStickerMouseWheelRotation =
        true;

    [Tooltip(
        "Degrees applied for one unit of mouse-wheel input. Standard mouse " +
        "wheels usually report +/-1 per notch; trackpads may report fractions."
    )]
    [Min(0f)]
    [SerializeField]
    private float stickerMouseWheelDegreesPerUnit =
        15f;

    [Tooltip("Reverse the mouse-wheel rotation direction.")]
    [SerializeField]
    private bool invertStickerMouseWheel =
        false;


    // ===========================================================
    // MAGNIFIER
    // ===========================================================

    [Header("Magnifier")]

    [Tooltip(
        "Input action used to toggle the sticker-drag magnifier on / off. " +
        "Mouse1 is the right mouse button. Keeping this binding here lets " +
        "gameplay code remain device-agnostic for future gamepad / Steam Deck support."
    )]
    [SerializeField]
    private KeyCode magnifierToggleKey =
        KeyCode.Mouse1;


    // ===========================================================
    // PUBLIC ACTION STATE
    // ===========================================================

    public bool EnableStickerDragRotation =>
        enableStickerDragRotation;

    public float StickerKeyboardRotationSpeed =>
        Mathf.Max(
            0f,
            stickerKeyboardRotationSpeed
        );

    public float StickerMouseWheelDegreesPerUnit =>
        Mathf.Max(
            0f,
            stickerMouseWheelDegreesPerUnit
        );


    /// <summary>
    /// True while the currently authored Rotate Left binding is held.
    /// BaseSticker decides whether that action is meaningful right now.
    /// </summary>
    public bool StickerRotateLeftHeld
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return
                enableStickerDragRotation &&
                Input.GetKey(
                    stickerRotateLeftKey
                );
#else
            return false;
#endif
        }
    }


    /// <summary>
    /// True while the currently authored Rotate Right binding is held.
    /// </summary>
    public bool StickerRotateRightHeld
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return
                enableStickerDragRotation &&
                Input.GetKey(
                    stickerRotateRightKey
                );
#else
            return false;
#endif
        }
    }


    /// <summary>
    /// Raw mouse-wheel delta for the current frame, optionally inverted.
    /// Returns zero when mouse-wheel sticker rotation is disabled.
    /// </summary>
    public float StickerMouseWheelDelta
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!enableStickerDragRotation ||
                !enableStickerMouseWheelRotation)
            {
                return 0f;
            }

            float delta =
                Input.mouseScrollDelta.y;

            return
                invertStickerMouseWheel
                    ? -delta
                    : delta;
#else
            return 0f;
#endif
        }
    }


    /// <summary>
    /// True only on the frame the authored magnifier-toggle binding is pressed.
    /// MagnifierManager owns the persistent ON / OFF state; InputsManager only
    /// reports the player action.
    /// </summary>
    public bool MagnifierTogglePressed
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return
                Input.GetKeyDown(
                    magnifierToggleKey
                );
#else
            return false;
#endif
        }
    }


    // ===========================================================
    // UNITY
    // ===========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            /*
             * Do not destroy the shared GameManagers GameObject if somebody
             * accidentally adds this component twice. Only remove the duplicate
             * component.
             */
            Destroy(this);
            return;
        }

        Instance =
            this;
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance =
                null;
        }
    }
}

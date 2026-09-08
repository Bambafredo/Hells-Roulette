using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only gameplay magnifier controlled by InputsManager.
///
/// Responsibilities:
/// - listen to BaseSticker's generic drag lifecycle events;
/// - optionally remain available even when no sticker is being dragged;
/// - enable / disable one secondary camera;
/// - render that camera into a square RenderTexture;
/// - display the RenderTexture through a CIRCULAR UI Mask;
/// - follow the pointer with zero offset normally and an authored offset during drag;
/// - keep the lens inside the Canvas when requested.
///
/// This class NEVER participates in sticker placement, collision, drag input,
/// or rotation. BaseSticker remains the authority for all sticker behaviour.
/// </summary>
[DisallowMultipleComponent]
public class MagnifierManager : MonoBehaviour
{
    public static MagnifierManager Instance;

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]

    [Tooltip(
        "Canvas that contains MagnifierRoot. If left empty, the manager tries " +
        "to resolve it from MagnifierRoot."
    )]
    [SerializeField]
    private Canvas targetCanvas;


    [Tooltip(
        "Direct UI root of the complete magnifier. Recommended setup: " +
        "MagnifierRoot -> CircleMask -> View."
    )]
    [SerializeField]
    private RectTransform magnifierRoot;


    [Tooltip(
        "Image used as the circular mask. A Unity UI Mask component is added " +
        "automatically at runtime if this object does not already have one."
    )]
    [SerializeField]
    private Image circularMaskImage;


    [Tooltip(
        "RawImage inside CircleMask that displays the magnifier RenderTexture."
    )]
    [SerializeField]
    private RawImage magnifierView;


    [Tooltip(
        "Secondary camera used only by the magnifier. It renders exclusively " +
        "to the generated RenderTexture and is disabled outside sticker drag."
    )]
    [SerializeField]
    private Camera magnifierCamera;


    [Tooltip(
        "Gameplay camera used as the reference for cursor-to-world conversion, " +
        "orthographic zoom and camera settings. Camera.main is used when empty."
    )]
    [SerializeField]
    private Camera sourceCamera;

    // =========================================================
    // ACTIVATION
    // =========================================================

    [Header("Activation")]

    [Tooltip(
        "Master switch for the magnifier feature."
    )]
    [SerializeField]
    private bool enableStickerDragMagnifier =
        true;


    [Tooltip(
        "When enabled, the player can use the magnifier even when no sticker is " +
        "being dragged. Outside drag the lens is centered directly on the pointer " +
        "with zero authored offset, which makes small stickers easier to target."
    )]
    [SerializeField]
    private bool allowMagnifierOutsideStickerDrag =
        true;


    [Tooltip(
        "Initial player state for Toggle input mode. OFF by default. In Hold mode " +
        "the current physical button state always decides whether the lens is active."
    )]
    [SerializeField]
    private bool startMagnifierEnabled =
        false;

    // =========================================================
    // LENS
    // =========================================================

    [Header("Lens")]

    [Tooltip(
        "Magnification relative to the normal gameplay camera. " +
        "2.5 means the lens shows the world at 2.5x magnification."
    )]
    [Min(1f)]
    [SerializeField]
    private float zoom =
        2.5f;


    [Tooltip(
        "Visible diameter of the circular lens in Canvas units. " +
        "The manager forces MagnifierRoot to this square size at runtime so the " +
        "lens does not depend on the default 100 x 100 size of a newly-created UI object."
    )]
    [Min(32f)]
    [SerializeField]
    private float lensDiameter =
        220f;


    [Tooltip(
        "Screen-space offset used WHILE DRAGGING a sticker. Positive X = right; " +
        "positive Y = up. Outside drag the magnifier always uses zero authored offset."
    )]
    [SerializeField]
    private Vector2 cursorOffsetPixels =
        new Vector2(
            150f,
            130f
        );


    [Tooltip(
        "Keeps the complete circular lens inside the Canvas when the pointer " +
        "approaches a screen edge."
    )]
    [SerializeField]
    private bool clampLensInsideCanvas =
        true;


    [Tooltip(
        "Extra screen-edge padding used by the clamp, in pixels."
    )]
    [Min(0f)]
    [SerializeField]
    private float screenEdgePaddingPixels =
        12f;

    // =========================================================
    // RENDER TEXTURE
    // =========================================================

    [Header("Render Texture")]

    [Tooltip(
        "Square RenderTexture resolution. 512 is normally more than enough " +
        "for a small circular placement lens."
    )]
    [Range(128, 1024)]
    [SerializeField]
    private int renderTextureResolution =
        512;

    // =========================================================
    // CAMERA / LAYERS
    // =========================================================

    [Header("Camera / Layers")]

    [Tooltip(
        "When enabled, the magnifier starts from Source Camera's culling mask. " +
        "This usually makes the lens visually match the normal gameplay view."
    )]
    [SerializeField]
    private bool copySourceCameraCullingMask =
        true;


    [Tooltip(
        "Layers rendered by the magnifier when Copy Source Camera Culling Mask is OFF. " +
        "Use this to include only gameplay layers (for example Roulette / Stickers) " +
        "and exclude Enemy/UI/presentation layers."
    )]
    [SerializeField]
    private LayerMask magnifierLayers =
        ~0;


    [Tooltip(
        "Layers that the magnifier camera must NEVER render. Put world-space UI " +
        "or other presentation-only layers here if needed. Screen Space Overlay " +
        "Canvas UI is not rendered by cameras anyway."
    )]
    [SerializeField]
    private LayerMask excludedLayers;


    [Tooltip(
        "Z plane used to convert the pointer to the 2D gameplay world. " +
        "For the current 2D roulette scene this should normally remain 0."
    )]
    [SerializeField]
    private float gameplayPlaneZ =
        0f;

    // =========================================================
    // CIRCULAR MASK
    // =========================================================

    [Header("Circular Mask")]

    [Tooltip(
        "Preferred authored sprite for the circular lens mask. Assign a clean " +
        "circle sprite here (the normal Unity UI circle is ideal). When assigned, " +
        "it takes priority over the runtime-generated fallback."
    )]
    [SerializeField]
    private Sprite circularMaskSprite;


    [Tooltip(
        "Fallback only. If no authored Circular Mask Sprite is assigned and the " +
        "CircleMask Image has no sprite, generate a circular sprite at runtime."
    )]
    [SerializeField]
    private bool generateCircularMaskAtRuntime =
        true;


    [Tooltip(
        "Resolution of the fallback generated circle Sprite. Higher values make " +
        "the edge smoother if no authored circle sprite is used."
    )]
    [Range(64, 1024)]
    [SerializeField]
    private int generatedCircleTextureSize =
        512;

    // =========================================================
    // LENS BACKDROP
    // =========================================================

    [Header("Lens Backdrop")]

    [Tooltip(
        "Adds a solid UI backdrop behind the RenderTexture, still clipped by the " +
        "circular mask. This prevents transparent areas of the magnifier camera " +
        "from revealing the normal game view underneath the lens."
    )]
    [SerializeField]
    private bool enableLensBackdrop =
        true;


    [Tooltip(
        "When enabled, the backdrop uses Source Camera's background RGB. The " +
        "opacity is still controlled independently below."
    )]
    [SerializeField]
    private bool useSourceCameraBackdropColor =
        true;


    [Tooltip(
        "Backdrop color used when Use Source Camera Backdrop Color is disabled."
    )]
    [SerializeField]
    private Color lensBackdropColor =
        new Color(
            0.22f,
            0.22f,
            0.22f,
            1f
        );


    [Tooltip(
        "Opacity of the solid backdrop behind the magnified image. 1 = the normal " +
        "game view can never show through transparent pixels in the lens."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float lensBackdropOpacity =
        1f;

    // =========================================================
    // BORDER
    // =========================================================

    [Header("Lens Border")]

    [Tooltip("Draws a circular border around the magnifier.")]
    [SerializeField]
    private bool enableLensBorder =
        true;


    [Tooltip("Visible border color.")]
    [SerializeField]
    private Color lensBorderColor =
        Color.white;


    [Tooltip("Border thickness in Canvas units / pixels at Canvas scale 1.")]
    [Min(0.5f)]
    [SerializeField]
    private float lensBorderThickness =
        4f;


    [Tooltip(
        "Resolution of the generated border ring. The border is generated at a " +
        "higher resolution than the old mask so its edge stays clean."
    )]
    [Range(128, 1024)]
    [SerializeField]
    private int generatedBorderTextureSize =
        512;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private BaseSticker activeDraggedSticker;

    private RenderTexture magnifierRenderTexture;

    private Texture2D generatedCircleTexture;
    private Sprite generatedCircleSprite;

    private Image runtimeBackdropImage;
    private Image runtimeBorderImage;

    private Texture2D generatedBorderTexture;
    private Sprite generatedBorderSprite;

    private bool magnifierVisible =
        false;

    /*
     * Persistent player preference for the current run / scene.
     * This is intentionally NOT owned by InputsManager: InputsManager reports
     * actions; MagnifierManager owns what the action means for this feature.
     */
    private bool magnifierEnabledByPlayer =
        false;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }


        Instance =
            this;


        ResolveReferences();


        magnifierEnabledByPlayer =
            startMagnifierEnabled;


        ConfigureMagnifierUI();
        CreateRenderTexture();
        SetMagnifierVisible(false);
    }


    private void OnEnable()
    {
        BaseSticker.OnAnyStickerDragStarted +=
            HandleStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded +=
            HandleStickerDragEnded;
    }


    private void OnDisable()
    {
        BaseSticker.OnAnyStickerDragStarted -=
            HandleStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded -=
            HandleStickerDragEnded;


        activeDraggedSticker =
            null;

        SetMagnifierVisible(false);
    }


    private void Update()
    {
        if (!enableStickerDragMagnifier)
        {
            if (magnifierVisible)
            {
                SetMagnifierVisible(false);
            }

            return;
        }


        UpdatePlayerMagnifierStateFromInput();
        RefreshMagnifierVisibility(false);
    }


    /// <summary>
    /// Converts the global magnifier binding into this feature's current active
    /// state. InputsManager owns only the binding / input mode; this manager owns
    /// the resulting lens state.
    /// </summary>
    private void UpdatePlayerMagnifierStateFromInput()
    {
        if (InputsManager.Instance == null)
            return;


        if (InputsManager.Instance.CurrentMagnifierInputMode ==
            InputsManager.MagnifierInputMode.Hold)
        {
            /*
             * Hold mode has no persistent toggle state: the lens is active only
             * for as long as the authored binding is physically held.
             */
            magnifierEnabledByPlayer =
                InputsManager.Instance.MagnifierHeld;

            return;
        }


        /*
         * Toggle mode preserves the previous behaviour: every press flips the
         * player's persistent preference for this run / scene.
         */
        if (InputsManager.Instance.MagnifierTogglePressed)
        {
            magnifierEnabledByPlayer =
                !magnifierEnabledByPlayer;
        }
    }


    /// <summary>
    /// Re-evaluates whether the lens should currently exist on screen.
    ///
    /// When use outside drag is disabled, the old drag-only behaviour is kept.
    /// When it is enabled, the same player input can expose the lens anywhere.
    /// </summary>
    private void RefreshMagnifierVisibility(
        bool refreshPresentationWhenVisible)
    {
        bool shouldBeVisible =
            enableStickerDragMagnifier &&
            magnifierEnabledByPlayer &&
            (
                allowMagnifierOutsideStickerDrag ||
                activeDraggedSticker != null
            );


        bool visibilityChanged =
            magnifierVisible != shouldBeVisible;


        SetMagnifierVisible(
            shouldBeVisible
        );


        if (!shouldBeVisible ||
            (!visibilityChanged &&
             !refreshPresentationWhenVisible))
        {
            return;
        }


        PrimeMagnifierPresentation();
    }


    /// <summary>
    /// Positions / configures an already-visible lens immediately. This is used
    /// both when input first reveals it and when drag starts / ends, because the
    /// UI offset changes at that exact transition.
    /// </summary>
    private void PrimeMagnifierPresentation()
    {
        ResolveReferences();


        if (sourceCamera == null ||
            magnifierCamera == null ||
            magnifierRoot == null)
        {
            return;
        }


        SyncMagnifierCameraSettings();
        UpdateLensBackdropVisual();
        UpdateMagnifierCamera();
        UpdateMagnifierUIPosition();
    }

    private void LateUpdate()
    {
        if (!magnifierVisible)
        {
            return;
        }


        ResolveReferences();


        if (sourceCamera == null ||
            magnifierCamera == null ||
            magnifierRoot == null)
        {
            return;
        }


        /*
         * BaseSticker updates its drag position / rotation during normal Update.
         * Rendering here in LateUpdate guarantees the lens captures that final
         * frame state instead of relying on URP's automatic secondary-camera
         * scheduling.
         */
        UpdateLensBackdropVisual();
        UpdateMagnifierCamera();
        RenderMagnifierCamera();
        UpdateMagnifierUIPosition();
    }


    private void OnDestroy()
    {
        if (magnifierCamera != null &&
            magnifierCamera.targetTexture ==
                magnifierRenderTexture)
        {
            magnifierCamera.targetTexture =
                null;
        }


        if (magnifierView != null &&
            magnifierView.texture ==
                magnifierRenderTexture)
        {
            magnifierView.texture =
                null;
        }


        if (magnifierRenderTexture != null)
        {
            magnifierRenderTexture.Release();
            Destroy(magnifierRenderTexture);

            magnifierRenderTexture =
                null;
        }


        if (generatedCircleSprite != null)
        {
            Destroy(generatedCircleSprite);

            generatedCircleSprite =
                null;
        }


        if (generatedCircleTexture != null)
        {
            Destroy(generatedCircleTexture);

            generatedCircleTexture =
                null;
        }


        if (generatedBorderSprite != null)
        {
            Destroy(generatedBorderSprite);

            generatedBorderSprite =
                null;
        }


        if (generatedBorderTexture != null)
        {
            Destroy(generatedBorderTexture);

            generatedBorderTexture =
                null;
        }


        if (Instance == this)
        {
            Instance =
                null;
        }
    }

    // =========================================================
    // DRAG EVENTS
    // =========================================================

    private void HandleStickerDragStarted(
        BaseSticker sticker)
    {
        if (!enableStickerDragMagnifier ||
            sticker == null)
        {
            return;
        }


        /*
         * The drag state matters even if the lens is currently hidden: it decides
         * whether the drag offset is used and whether drag-only configurations are
         * allowed to show the lens.
         */
        activeDraggedSticker =
            sticker;


        /*
         * If the lens was already visible before the drag (free magnifier mode),
         * keep it visible and immediately move it to the authored drag offset.
         */
        RefreshMagnifierVisibility(true);
    }

    private void HandleStickerDragEnded(
        BaseSticker sticker)
    {
        /*
         * Only the sticker that currently owns the drag state is allowed to end
         * it. This remains defensive even though normal gameplay has one drag.
         */
        if (activeDraggedSticker != null &&
            sticker != null &&
            sticker != activeDraggedSticker)
        {
            return;
        }


        activeDraggedSticker =
            null;


        /*
         * Free magnifier mode stays visible and snaps back to zero offset.
         * Drag-only mode hides here exactly as before.
         */
        RefreshMagnifierVisibility(true);
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void ResolveReferences()
    {
        if (sourceCamera == null)
        {
            sourceCamera =
                Camera.main;
        }


        if (targetCanvas == null &&
            magnifierRoot != null)
        {
            targetCanvas =
                magnifierRoot.GetComponentInParent<Canvas>();
        }
    }

    // =========================================================
    // VISIBILITY
    // =========================================================

    private void SetMagnifierVisible(
        bool visible)
    {
        magnifierVisible =
            visible;


        if (magnifierRoot != null &&
            magnifierRoot.gameObject.activeSelf != visible)
        {
            magnifierRoot.gameObject.SetActive(
                visible
            );
        }


        if (magnifierCamera != null)
        {
            /*
             * Keep the camera disabled even while the lens is visible.
             *
             * The magnifier renders it explicitly from LateUpdate via
             * Camera.Render(). This avoids depending on URP deciding when to
             * schedule a dynamically-enabled secondary camera that targets a
             * runtime-created RenderTexture.
             */
            magnifierCamera.enabled =
                false;
        }
    }

    // =========================================================
    // CAMERA
    // =========================================================

    private void SyncMagnifierCameraSettings()
    {
        if (sourceCamera == null ||
            magnifierCamera == null)
        {
            return;
        }


        /*
         * IMPORTANT:
         * The gameplay view can be perspective, orthographic, or composed from
         * several cameras. The magnifier itself is deliberately orthographic.
         *
         * Its exact world size is calculated separately from the source camera's
         * real screen-to-world scale at the pointer. This makes "Zoom = 2.5"
         * mean an actual 2.5x magnification instead of copying a projection/FOV
         * that may not represent the final composed Game view.
         */
        magnifierCamera.orthographic =
            true;


        magnifierCamera.clearFlags =
            sourceCamera.clearFlags;

        magnifierCamera.backgroundColor =
            sourceCamera.backgroundColor;

        magnifierCamera.nearClipPlane =
            sourceCamera.nearClipPlane;

        magnifierCamera.farClipPlane =
            sourceCamera.farClipPlane;


        int baseCullingMask =
            copySourceCameraCullingMask
                ? sourceCamera.cullingMask
                : magnifierLayers.value;


        magnifierCamera.cullingMask =
            baseCullingMask &
            ~excludedLayers.value;


        magnifierCamera.transform.rotation =
            sourceCamera.transform.rotation;
    }


    private void UpdateMagnifierCamera()
    {
        if (sourceCamera == null ||
            magnifierCamera == null)
        {
            return;
        }


        SyncMagnifierCameraSettings();


        Vector2 pointerScreenPosition =
            Input.mousePosition;


        Vector3 pointerWorldPosition;


        if (!TryScreenPointOnGameplayPlane(
                pointerScreenPosition,
                out pointerWorldPosition))
        {
            return;
        }


        /*
         * Put the orthographic magnifier camera on the same viewing axis as the
         * source camera, but aim its centre exactly through the pointer position
         * on the authored 2D gameplay plane.
         */
        Vector3 sourceForward =
            sourceCamera.transform.forward;


        float sourceDistanceAlongForward =
            Vector3.Dot(
                pointerWorldPosition -
                    sourceCamera.transform.position,
                sourceForward
            );


        /*
         * Normal 2D setups produce a positive distance. Keep a defensive
         * fallback for unusual authored camera transforms.
         */
        if (sourceDistanceAlongForward <= 0.001f)
        {
            sourceDistanceAlongForward =
                Mathf.Max(
                    0.01f,
                    Vector3.Distance(
                        sourceCamera.transform.position,
                        pointerWorldPosition
                    )
                );
        }


        magnifierCamera.transform.position =
            pointerWorldPosition -
            sourceForward *
            sourceDistanceAlongForward;


        /*
         * Determine how many world units one screen pixel represents exactly at
         * the pointer/gameplay plane. This works for BOTH perspective and
         * orthographic source cameras.
         */
        const float samplePixels =
            100f;


        Vector3 sampleWorldPosition;


        if (!TryScreenPointOnGameplayPlane(
                pointerScreenPosition +
                Vector2.up * samplePixels,
                out sampleWorldPosition))
        {
            return;
        }


        float worldUnitsPerPixel =
            Vector3.Distance(
                pointerWorldPosition,
                sampleWorldPosition
            ) /
            samplePixels;


        if (worldUnitsPerPixel <= 0.000001f)
            return;


        float lensDiameterPixels =
            GetLensDiameterInScreenPixels();


        /*
         * Example:
         * - the circular lens occupies 220 screen pixels;
         * - those 220 pixels normally cover X world units;
         * - Zoom 2.5 makes the magnifier show X / 2.5 world units.
         *
         * OrthographicSize is half of the vertical world height.
         */
        float visibleWorldHeight =
            lensDiameterPixels *
            worldUnitsPerPixel /
            Mathf.Max(
                1f,
                zoom
            );


        magnifierCamera.orthographicSize =
            Mathf.Max(
                0.0001f,
                visibleWorldHeight * 0.5f
            );
    }


    /// <summary>
    /// Converts an absolute screen point to the authored 2D gameplay plane.
    ///
    /// Using a ray/plane intersection instead of ScreenToWorldPoint(distance)
    /// avoids projection-dependent offsets when the source camera is
    /// perspective or has a non-default transform.
    /// </summary>
    private bool TryScreenPointOnGameplayPlane(
        Vector2 screenPoint,
        out Vector3 worldPoint)
    {
        worldPoint =
            Vector3.zero;


        if (sourceCamera == null)
            return false;


        Ray ray =
            sourceCamera.ScreenPointToRay(
                screenPoint
            );


        Plane gameplayPlane =
            new Plane(
                Vector3.forward,
                new Vector3(
                    0f,
                    0f,
                    gameplayPlaneZ
                )
            );


        float enter;


        if (!gameplayPlane.Raycast(
                ray,
                out enter))
        {
            return false;
        }


        worldPoint =
            ray.GetPoint(
                enter
            );


        return true;
    }


    /// <summary>
    /// Returns the actual on-screen diameter of MagnifierRoot in pixels.
    /// Canvas scale is included so zoom remains stable with CanvasScaler.
    /// </summary>
    private float GetLensDiameterInScreenPixels()
    {
        if (magnifierRoot == null)
        {
            return
                Mathf.Max(
                    1f,
                    lensDiameter
                );
        }


        float canvasScale =
            targetCanvas != null
                ? Mathf.Max(
                    0.0001f,
                    targetCanvas.scaleFactor
                )
                : 1f;


        return
            Mathf.Max(
                1f,
                magnifierRoot.rect.height *
                canvasScale
            );
    }

    /// <summary>
    /// Explicitly renders the magnifier camera into its runtime RenderTexture.
    ///
    /// Camera.Render() is valid for a disabled Camera and gives us deterministic
    /// control over when the capture happens. The camera stays disabled so it is
    /// never also rendered automatically by the normal camera loop.
    /// </summary>
    private void RenderMagnifierCamera()
    {
        if (magnifierCamera == null ||
            magnifierRenderTexture == null)
        {
            return;
        }


        if (!magnifierRenderTexture.IsCreated())
        {
            magnifierRenderTexture.Create();
        }


        /*
         * Re-assert the target defensively. This is cheap and protects the lens
         * from another camera setup script changing the output at runtime.
         */
        if (magnifierCamera.targetTexture !=
            magnifierRenderTexture)
        {
            magnifierCamera.targetTexture =
                magnifierRenderTexture;
        }


        magnifierCamera.Render();
    }


    // =========================================================
    // UI POSITION
    // =========================================================

    private void UpdateMagnifierUIPosition()
    {
        if (targetCanvas == null ||
            magnifierRoot == null)
        {
            return;
        }


        RectTransform canvasRect =
            targetCanvas.transform as RectTransform;


        if (canvasRect == null)
            return;


        Vector2 currentOffsetPixels =
            activeDraggedSticker != null
                ? cursorOffsetPixels
                : Vector2.zero;


        Vector2 desiredScreenPoint =
            (Vector2)Input.mousePosition +
            currentOffsetPixels;


        Camera uiCamera =
            targetCanvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;


        Vector2 localPoint;


        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                canvasRect,
                desiredScreenPoint,
                uiCamera,
                out localPoint
            ))
        {
            return;
        }


        if (clampLensInsideCanvas)
        {
            localPoint =
                ClampLensLocalPointToCanvas(
                    canvasRect,
                    localPoint
                );
        }


        /*
         * Recommended hierarchy keeps MagnifierRoot as a direct child of the
         * target Canvas. localPosition therefore shares the same coordinate
         * space returned by ScreenPointToLocalPointInRectangle().
         */
        Vector3 currentLocalPosition =
            magnifierRoot.localPosition;


        magnifierRoot.localPosition =
            new Vector3(
                localPoint.x,
                localPoint.y,
                currentLocalPosition.z
            );
    }


    private Vector2 ClampLensLocalPointToCanvas(
        RectTransform canvasRect,
        Vector2 localPoint)
    {
        Rect canvasBounds =
            canvasRect.rect;


        Vector2 lensSize =
            magnifierRoot.rect.size;


        float canvasScale =
            targetCanvas != null
                ? Mathf.Max(
                    0.0001f,
                    targetCanvas.scaleFactor
                )
                : 1f;


        float paddingLocal =
            screenEdgePaddingPixels /
            canvasScale;


        float halfWidth =
            lensSize.x * 0.5f;

        float halfHeight =
            lensSize.y * 0.5f;


        float minX =
            canvasBounds.xMin +
            halfWidth +
            paddingLocal;

        float maxX =
            canvasBounds.xMax -
            halfWidth -
            paddingLocal;

        float minY =
            canvasBounds.yMin +
            halfHeight +
            paddingLocal;

        float maxY =
            canvasBounds.yMax -
            halfHeight -
            paddingLocal;


        /*
         * If the lens is somehow authored larger than the Canvas, do not feed
         * an inverted range into Mathf.Clamp. Centering is the safest fallback.
         */
        if (minX > maxX)
        {
            localPoint.x =
                0f;
        }
        else
        {
            localPoint.x =
                Mathf.Clamp(
                    localPoint.x,
                    minX,
                    maxX
                );
        }


        if (minY > maxY)
        {
            localPoint.y =
                0f;
        }
        else
        {
            localPoint.y =
                Mathf.Clamp(
                    localPoint.y,
                    minY,
                    maxY
                );
        }


        return
            localPoint;
    }

    // =========================================================
    // RENDER TEXTURE
    // =========================================================

    private void CreateRenderTexture()
    {
        if (magnifierCamera == null ||
            magnifierView == null)
        {
            return;
        }


        if (magnifierRenderTexture != null)
            return;


        int safeResolution =
            Mathf.Clamp(
                renderTextureResolution,
                128,
                1024
            );


        magnifierRenderTexture =
            new RenderTexture(
                safeResolution,
                safeResolution,
                16,
                RenderTextureFormat.ARGB32
            )
            {
                name =
                    "Magnifier_Runtime_RenderTexture",

                filterMode =
                    FilterMode.Bilinear,

                wrapMode =
                    TextureWrapMode.Clamp
            };


        magnifierRenderTexture.Create();


        magnifierCamera.targetTexture =
            magnifierRenderTexture;

        magnifierCamera.aspect =
            1f;

        magnifierView.texture =
            magnifierRenderTexture;
    }

    // =========================================================
    // CIRCULAR UI
    // =========================================================

    private void ConfigureMagnifierUI()
    {
        if (magnifierRoot == null)
            return;


        /*
         * A newly-created UI object defaults to 100 x 100. Force the authored
         * magnifier diameter here so the circular lens has a predictable,
         * globally editable size without requiring manual RectTransform setup.
         */
        float safeDiameter =
            Mathf.Max(
                32f,
                lensDiameter
            );


        magnifierRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            safeDiameter
        );

        magnifierRoot.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            safeDiameter
        );


        /*
         * The magnifier is presentation only. It must never steal pointer input
         * from BaseSticker while the user is dragging underneath it.
         */
        Graphic[] graphics =
            magnifierRoot.GetComponentsInChildren<Graphic>(
                true
            );


        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
                continue;

            graphic.raycastTarget =
                false;
        }


        if (circularMaskImage == null)
            return;


        Mask mask =
            circularMaskImage.GetComponent<Mask>();


        if (mask == null)
        {
            mask =
                circularMaskImage.gameObject
                    .AddComponent<Mask>();
        }


        mask.showMaskGraphic =
            false;


        /*
         * Prefer an authored circle sprite. This avoids the visibly rasterized
         * edge produced by the old 128 px runtime mask. If no explicit sprite is
         * supplied we preserve any sprite already authored on CircleMask, then
         * finally fall back to runtime generation.
         */
        if (circularMaskSprite != null)
        {
            circularMaskImage.sprite =
                circularMaskSprite;
        }
        else if (circularMaskImage.sprite == null &&
                 generateCircularMaskAtRuntime)
        {
            CreateGeneratedCircleSprite();

            if (generatedCircleSprite != null)
            {
                circularMaskImage.sprite =
                    generatedCircleSprite;
            }
        }


        circularMaskImage.type =
            Image.Type.Simple;

        circularMaskImage.preserveAspect =
            false;

        circularMaskImage.color =
            Color.white;


        /*
         * The mask fills the root. The border is a transparent ring rendered on
         * top, so the magnified image keeps the full requested diameter.
         */
        StretchRectTransform(
            circularMaskImage.rectTransform
        );


        EnsureLensBackdrop();


        if (magnifierView != null)
        {
            StretchRectTransform(
                magnifierView.rectTransform
            );

            magnifierView.raycastTarget =
                false;

            /*
             * Backdrop must always stay behind the camera image.
             */
            magnifierView.transform.SetAsLastSibling();
        }


        EnsureLensBorder();
        UpdateLensBackdropVisual();
        UpdateLensBorderVisual();
    }


    /// <summary>
    /// Creates / configures one plain Image behind the RawImage and inside the
    /// circular mask. The mask clips it automatically, so it becomes a solid
    /// circular backdrop without needing another sprite.
    /// </summary>
    private void EnsureLensBackdrop()
    {
        if (circularMaskImage == null)
            return;


        if (runtimeBackdropImage == null)
        {
            Transform existing =
                circularMaskImage.transform.Find(
                    "MagnifierBackdrop_Runtime"
                );


            if (existing != null)
            {
                runtimeBackdropImage =
                    existing.GetComponent<Image>();
            }
        }


        if (runtimeBackdropImage == null)
        {
            GameObject backdropObject =
                new GameObject(
                    "MagnifierBackdrop_Runtime",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );


            backdropObject.transform.SetParent(
                circularMaskImage.transform,
                false
            );


            runtimeBackdropImage =
                backdropObject.GetComponent<Image>();
        }


        if (runtimeBackdropImage == null)
            return;


        StretchRectTransform(
            runtimeBackdropImage.rectTransform
        );

        runtimeBackdropImage.raycastTarget =
            false;

        runtimeBackdropImage.transform.SetAsFirstSibling();
    }


    private void UpdateLensBackdropVisual()
    {
        if (runtimeBackdropImage == null)
            return;


        runtimeBackdropImage.enabled =
            enableLensBackdrop &&
            lensBackdropOpacity > 0f;


        if (!runtimeBackdropImage.enabled)
            return;


        Color backdrop =
            lensBackdropColor;


        if (useSourceCameraBackdropColor &&
            sourceCamera != null)
        {
            backdrop =
                sourceCamera.backgroundColor;
        }


        backdrop.a =
            Mathf.Clamp01(
                lensBackdropOpacity
            );


        runtimeBackdropImage.color =
            backdrop;
    }


    /// <summary>
    /// Creates a dedicated ring Image as a sibling of CircleMask. It renders on
    /// top of the lens and therefore never gets clipped by the circular Mask.
    /// </summary>
    private void EnsureLensBorder()
    {
        if (magnifierRoot == null)
            return;


        if (runtimeBorderImage == null)
        {
            Transform existing =
                magnifierRoot.Find(
                    "MagnifierBorder_Runtime"
                );


            if (existing != null)
            {
                runtimeBorderImage =
                    existing.GetComponent<Image>();
            }
        }


        if (runtimeBorderImage == null)
        {
            GameObject borderObject =
                new GameObject(
                    "MagnifierBorder_Runtime",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );


            borderObject.transform.SetParent(
                magnifierRoot,
                false
            );


            runtimeBorderImage =
                borderObject.GetComponent<Image>();
        }


        if (runtimeBorderImage == null)
            return;


        StretchRectTransform(
            runtimeBorderImage.rectTransform
        );

        runtimeBorderImage.raycastTarget =
            false;

        runtimeBorderImage.transform.SetAsLastSibling();


        CreateGeneratedBorderSprite();

        if (generatedBorderSprite != null)
        {
            runtimeBorderImage.sprite =
                generatedBorderSprite;

            runtimeBorderImage.type =
                Image.Type.Simple;

            runtimeBorderImage.preserveAspect =
                false;
        }
    }


    private void UpdateLensBorderVisual()
    {
        if (runtimeBorderImage == null)
            return;


        runtimeBorderImage.enabled =
            enableLensBorder &&
            lensBorderThickness > 0f;


        if (!runtimeBorderImage.enabled)
            return;


        runtimeBorderImage.color =
            lensBorderColor;
    }


    private void CreateGeneratedBorderSprite()
    {
        if (generatedBorderSprite != null)
            return;


        int safeSize =
            Mathf.Clamp(
                generatedBorderTextureSize,
                128,
                1024
            );


        generatedBorderTexture =
            new Texture2D(
                safeSize,
                safeSize,
                TextureFormat.RGBA32,
                false
            )
            {
                name =
                    "Magnifier_Runtime_BorderRing",

                filterMode =
                    FilterMode.Bilinear,

                wrapMode =
                    TextureWrapMode.Clamp
            };


        Color32[] pixels =
            new Color32[
                safeSize * safeSize
            ];


        float center =
            (safeSize - 1) * 0.5f;

        float outerRadius =
            safeSize * 0.5f - 1.5f;

        float thicknessInTexture =
            Mathf.Max(
                1f,
                Mathf.Max(
                    0.5f,
                    lensBorderThickness
                ) /
                Mathf.Max(
                    1f,
                    lensDiameter
                ) *
                safeSize
            );

        float innerRadius =
            Mathf.Max(
                0f,
                outerRadius -
                thicknessInTexture
            );


        for (int y = 0;
             y < safeSize;
             y++)
        {
            for (int x = 0;
                 x < safeSize;
                 x++)
            {
                float dx =
                    x - center;

                float dy =
                    y - center;

                float distance =
                    Mathf.Sqrt(
                        dx * dx +
                        dy * dy
                    );


                float outerAlpha =
                    Mathf.Clamp01(
                        outerRadius + 0.75f -
                        distance
                    );

                float innerAlpha =
                    Mathf.Clamp01(
                        distance -
                        innerRadius +
                        0.75f
                    );

                float alpha01 =
                    outerAlpha *
                    innerAlpha;


                byte alpha =
                    (byte)Mathf.RoundToInt(
                        alpha01 * 255f
                    );


                pixels[
                    y * safeSize + x
                ] =
                    new Color32(
                        255,
                        255,
                        255,
                        alpha
                    );
            }
        }


        generatedBorderTexture.SetPixels32(
            pixels
        );

        generatedBorderTexture.Apply(
            false,
            false
        );


        generatedBorderSprite =
            Sprite.Create(
                generatedBorderTexture,
                new Rect(
                    0f,
                    0f,
                    safeSize,
                    safeSize
                ),
                new Vector2(
                    0.5f,
                    0.5f
                ),
                safeSize
            );


        generatedBorderSprite.name =
            "Magnifier_Runtime_BorderRing_Sprite";
    }


    private void CreateGeneratedCircleSprite()
    {
        if (generatedCircleSprite != null)
            return;


        int safeSize =
            Mathf.Clamp(
                generatedCircleTextureSize,
                64,
                1024
            );


        generatedCircleTexture =
            new Texture2D(
                safeSize,
                safeSize,
                TextureFormat.RGBA32,
                false
            )
            {
                name =
                    "Magnifier_Runtime_CircleMask",

                filterMode =
                    FilterMode.Bilinear,

                wrapMode =
                    TextureWrapMode.Clamp
            };


        Color32[] pixels =
            new Color32[
                safeSize * safeSize
            ];


        float center =
            (safeSize - 1) * 0.5f;

        float radius =
            safeSize * 0.5f - 1f;


        for (int y = 0;
             y < safeSize;
             y++)
        {
            for (int x = 0;
                 x < safeSize;
                 x++)
            {
                float dx =
                    x - center;

                float dy =
                    y - center;

                float distance =
                    Mathf.Sqrt(
                        dx * dx +
                        dy * dy
                    );


                /*
                 * One-pixel soft edge. Unity UI Mask still gives us the desired
                 * hard circular silhouette while texture filtering stays clean.
                 */
                float alpha01 =
                    Mathf.Clamp01(
                        radius + 0.5f -
                        distance
                    );


                byte alpha =
                    (byte)Mathf.RoundToInt(
                        alpha01 * 255f
                    );


                pixels[
                    y * safeSize + x
                ] =
                    new Color32(
                        255,
                        255,
                        255,
                        alpha
                    );
            }
        }


        generatedCircleTexture.SetPixels32(
            pixels
        );

        generatedCircleTexture.Apply(
            false,
            false
        );


        generatedCircleSprite =
            Sprite.Create(
                generatedCircleTexture,
                new Rect(
                    0f,
                    0f,
                    safeSize,
                    safeSize
                ),
                new Vector2(
                    0.5f,
                    0.5f
                ),
                safeSize
            );


        generatedCircleSprite.name =
            "Magnifier_Runtime_CircleMask_Sprite";
    }


    private void StretchRectTransform(
        RectTransform rect)
    {
        if (rect == null)
            return;


        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }
}

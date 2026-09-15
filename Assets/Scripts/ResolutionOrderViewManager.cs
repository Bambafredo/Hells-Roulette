using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Optional planning overlay for the roulette's physical sticker resolution
/// order.
///
/// - Toggleable from a normal Unity UI Button via ToggleResolutionOrderView().
/// - Toggleable from InputsManager through an editable shortcut.
/// - Segment Order: numbering restarts at 1 in every segment and matches the
///   winning-segment resolution rule (priority override, then radial distance).
/// - Radial Order: one global 1..N across the wheel using pure radial distance;
///   this matches the real non-winning roulette resolution phase.
/// - The two views are mutually exclusive.
/// - Uses StickerResolutionOrderUtility, the exact same ordering code used by
///   RouletteController.
/// - While dragging, a sticker receives a prospective number only when its
///   current position is a VALID wheel placement.
///
/// This component is presentation only. It never changes placement validity,
/// collider state, sticker parenting or gameplay resolution.
/// </summary>
[DisallowMultipleComponent]
public class ResolutionOrderViewManager : MonoBehaviour
{
    public enum ViewMode
    {
        Off,
        SegmentOrder,
        RadialOrder
    }


    public static ResolutionOrderViewManager Instance;

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]

    [Tooltip(
        "Gameplay Canvas that should contain the order numbers. If empty, the " +
        "manager tries to find a root Canvas automatically."
    )]
    [SerializeField]
    private Canvas targetCanvas;


    [Tooltip(
        "Optional authored RectTransform used as the parent for all runtime order " +
        "numbers. Leave empty to create a full-canvas runtime root automatically."
    )]
    [SerializeField]
    private RectTransform labelsRoot;


    [Tooltip(
        "Camera used to convert sticker world positions to screen positions. " +
        "Camera.main is used when empty."
    )]
    [SerializeField]
    private Camera sourceCamera;


    [Tooltip(
        "Wheel centre used by the inside-out ordering rule. If empty, the manager " +
        "uses RouletteController.wheel."
    )]
    [SerializeField]
    private Transform wheelCenter;

    // =========================================================
    // ACTIVATION
    // =========================================================

    [Header("Activation")]

    [Tooltip("Master switch for the resolution-order overlay.")]
    [SerializeField]
    private bool enableResolutionOrderView =
        true;


    [Tooltip("Whether the order overlay begins visible when the scene starts.")]
    [SerializeField]
    private bool startVisible =
        false;


    [Tooltip(
        "Hide order numbers during the physical roulette spin while preserving " +
        "the player's ON/OFF choice. They reappear automatically afterwards."
    )]
    [SerializeField]
    private bool hideDuringSpin =
        true;


    [Tooltip(
        "When enabled, the dragged sticker is previewed in the order only while " +
        "its current transform is a valid roulette placement."
    )]
    [SerializeField]
    private bool previewDraggedSticker =
        true;

    // =========================================================
    // UTILITY BUTTON (OPTIONAL)
    // =========================================================

    [Header("Utility Button (Optional)")]

    [Tooltip(
        "Optional world-space Collider2D for a Utility_Area button. If you use " +
        "a normal Canvas Button instead, leave this empty and wire its Inspector " +
        "OnClick directly to ToggleResolutionOrderView()."
    )]
    [SerializeField]
    private Collider2D utilityButtonCollider;


    [Tooltip(
        "Normal Canvas Button used for the Order toggle. Assign the Button component " +
        "from ResolutionOrder_Button here. Leave empty only if you are using the " +
        "legacy world-space collider button."
    )]
    [SerializeField]
    private Button utilityCanvasButton;


    [Tooltip(
        "Optional child / icon that is active only while the order view itself is ON."
    )]
    [SerializeField]
    private GameObject utilityButtonActiveIndicator;


    [Tooltip(
        "Optional UI Graphic used as the button's colour feedback. " +
        "For a normal Canvas Button, assign its Image / target graphic here."
    )]
    [SerializeField]
    private Graphic utilityButtonColorTarget;


    [Tooltip(
        "Optional world-space Collider2D for the Radial Order button. " +
        "Leave empty when using a normal Canvas Button."
    )]
    [SerializeField]
    private Collider2D radialUtilityButtonCollider;


    [Tooltip(
        "Optional Canvas Button for Radial Order. Wire its OnClick to " +
        "ToggleRadialOrderView()."
    )]
    [SerializeField]
    private Button radialUtilityCanvasButton;


    [Tooltip(
        "Optional child / icon active only while Radial Order is ON."
    )]
    [SerializeField]
    private GameObject radialUtilityButtonActiveIndicator;


    [Tooltip(
        "Optional Graphic used as Radial Order button colour feedback."
    )]
    [SerializeField]
    private Graphic radialUtilityButtonColorTarget;


    [SerializeField]
    private Color utilityButtonOffColor =
        Color.white;


    [SerializeField]
    private Color utilityButtonOnColor =
        new Color(
            0.55f,
            1f,
            0.55f,
            1f
        );


    // =========================================================
    // LABEL STYLE
    // =========================================================

    [Header("Label Style")]

    [Tooltip(
        "Optional TMP font asset. Leave empty to use the project's TMP default."
    )]
    [SerializeField]
    private TMP_FontAsset labelFont;


    [Min(8f)]
    [SerializeField]
    private float labelFontSize =
        28f;


    [Tooltip(
        "Radial Order only: font-size multiplier used for labels with two or " +
        "more digits. Keeps 10, 11, 12... on one horizontal line."
    )]
    [Range(0.4f, 1f)]
    [SerializeField]
    private float radialMultiDigitFontScale =
        0.72f;


    [SerializeField]
    private Color labelColor =
        Color.white;


    [Tooltip(
        "Segment Order only: number colour used when a sticker has a non-zero " +
        "SpinResolutionPriority override (for example Coffee / Catapult)."
    )]
    [SerializeField]
    private Color priorityOverrideLabelColor =
        new Color(
            0.3f,
            0.9f,
            1f,
            1f
        );


    [SerializeField]
    private Color labelOutlineColor =
        Color.black;


    [Tooltip(
        "Visual outline strength. 0 = none, 1 = thick/high-contrast. " +
        "Implemented with dedicated black TMP copies around the white glyph, so " +
        "it does not depend on TMP material-outline state."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float labelOutlineWidth =
        0.65f;


    [Tooltip(
        "Square UI size reserved for each number in Canvas units."
    )]
    [Min(12f)]
    [SerializeField]
    private float labelSize =
        42f;


    [Tooltip(
        "Moves the number slightly OUTWARD from the exact closest collider point, " +
        "in screen pixels. Zero places it directly on the measured point."
    )]
    [SerializeField]
    private float radialLabelOffsetPixels =
        12f;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private ViewMode currentViewMode =
        ViewMode.Off;

    private BaseSticker activeDraggedSticker;

    private static readonly Vector2[] OutlineDirections =
    {
        new Vector2(-1f,  0f),
        new Vector2( 1f,  0f),
        new Vector2( 0f, -1f),
        new Vector2( 0f,  1f),
        new Vector2(-0.7071f, -0.7071f),
        new Vector2(-0.7071f,  0.7071f),
        new Vector2( 0.7071f, -0.7071f),
        new Vector2( 0.7071f,  0.7071f)
    };


    /*
     * Each order number uses one front TMP glyph plus eight black TMP copies
     * around it. This creates a guaranteed 360-degree outline without depending
     * on TMP material outline state or Unity UI mesh effects.
     */
    private sealed class OrderLabelVisual
    {
        public RectTransform root;
        public TextMeshProUGUI front;

        public readonly List<TextMeshProUGUI>
            outlineCopies =
                new List<TextMeshProUGUI>();
    }


    private readonly List<OrderLabelVisual>
        labelPool =
            new List<OrderLabelVisual>();


    private readonly List<BaseSticker>
        workingSegmentStickers =
            new List<BaseSticker>();


    private readonly List<BaseSticker>
        workingWheelStickers =
            new List<BaseSticker>();


    private readonly HashSet<BaseSticker>
        workingWheelStickerSet =
            new HashSet<BaseSticker>();

    // =========================================================
    // PUBLIC STATE / BUTTON API
    // =========================================================

    public bool IsResolutionOrderViewEnabled =>
        currentViewMode !=
            ViewMode.Off;


    public ViewMode CurrentViewMode =>
        currentViewMode;


    /// <summary>
    /// Existing Segment Order button / shortcut.
    /// Pressing it while Radial Order is visible switches directly to Segment.
    /// </summary>
    public void ToggleResolutionOrderView()
    {
        SetViewMode(
            currentViewMode ==
                ViewMode.SegmentOrder
                ? ViewMode.Off
                : ViewMode.SegmentOrder
        );
    }


    public void ShowResolutionOrderView()
    {
        SetViewMode(
            ViewMode.SegmentOrder
        );
    }


    public void HideResolutionOrderView()
    {
        SetViewMode(
            ViewMode.Off
        );
    }


    /// <summary>
    /// Wire the second planning button to this method.
    /// Segment Order is automatically disabled because only one ViewMode can
    /// exist at a time.
    /// </summary>
    public void ToggleRadialOrderView()
    {
        SetViewMode(
            currentViewMode ==
                ViewMode.RadialOrder
                ? ViewMode.Off
                : ViewMode.RadialOrder
        );
    }


    public void ShowRadialOrderView()
    {
        SetViewMode(
            ViewMode.RadialOrder
        );
    }


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
        EnsureLabelsRoot();
        EnsureLabelsBehindMagnifier();


        currentViewMode =
            startVisible
                ? ViewMode.SegmentOrder
                : ViewMode.Off;


        RefreshRootVisibility();


        RefreshUtilityButtonVisual();
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


        HideAllLabels();
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (enableResolutionOrderView &&
            InputsManager.Instance != null &&
            InputsManager.Instance
                .ResolutionOrderTogglePressed)
        {
            ToggleResolutionOrderView();
        }


        HandleOptionalWorldUtilityButtons();
#endif


        RefreshRootVisibility();
    }


    private void LateUpdate()
    {
        if (!ShouldRenderOverlay())
        {
            HideAllLabels();
            return;
        }


        ResolveReferences();
        EnsureLabelsRoot();
        EnsureLabelsBehindMagnifier();


        if (targetCanvas == null ||
            labelsRoot == null ||
            sourceCamera == null ||
            wheelCenter == null ||
            RouletteController.Instance == null ||
            RouletteController.Instance.generator == null)
        {
            HideAllLabels();
            return;
        }


        /*
         * The only moving physics shape we need to preview every frame is the
         * currently dragged sticker. Syncing here keeps Collider2D.ClosestPoint
         * and FindValidSegment aligned with its latest Update transform.
         */
        if (activeDraggedSticker != null)
        {
            Physics2D.SyncTransforms();
        }


        RefreshOrderLabels();
    }


    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance =
                null;
        }
    }

    // =========================================================
    // DRAG TRACKING
    // =========================================================

    private void HandleStickerDragStarted(
        BaseSticker sticker)
    {
        activeDraggedSticker =
            sticker;
    }


    private void HandleStickerDragEnded(
        BaseSticker sticker)
    {
        if (activeDraggedSticker != null &&
            sticker != null &&
            sticker != activeDraggedSticker)
        {
            return;
        }


        activeDraggedSticker =
            null;
    }

    // =========================================================
    // OPTIONAL WORLD-SPACE UTILITY BUTTON
    // =========================================================

    private void HandleOptionalWorldUtilityButtons()
    {
        if (!enableResolutionOrderView ||
            !Input.GetMouseButtonDown(0))
        {
            return;
        }


        if (RouletteController.Instance != null &&
            RouletteController.Instance.SpinInProgress)
        {
            return;
        }


        if (sourceCamera == null)
        {
            sourceCamera =
                Camera.main;
        }


        if (sourceCamera == null)
            return;


        Vector2 mouseWorld =
            sourceCamera.ScreenToWorldPoint(
                Input.mousePosition
            );


        if (utilityButtonCollider != null &&
            utilityButtonCollider.OverlapPoint(
                mouseWorld
            ))
        {
            RouletteController.Instance?
                .ConsumePointerInputThisFrame();

            ToggleResolutionOrderView();
            return;
        }


        if (radialUtilityButtonCollider != null &&
            radialUtilityButtonCollider.OverlapPoint(
                mouseWorld
            ))
        {
            RouletteController.Instance?
                .ConsumePointerInputThisFrame();

            ToggleRadialOrderView();
        }
    }


    // =========================================================
    // VIEW STATE
    // =========================================================

    private void SetViewMode(
        ViewMode mode)
    {
        currentViewMode =
            mode;


        RefreshRootVisibility();
        RefreshUtilityButtonVisual();


        if (currentViewMode ==
            ViewMode.Off)
        {
            HideAllLabels();
        }
    }


    private bool ShouldRenderOverlay()
    {
        if (!enableResolutionOrderView ||
            currentViewMode ==
                ViewMode.Off)
        {
            return false;
        }


        if (hideDuringSpin &&
            RouletteController.Instance != null &&
            RouletteController.Instance.SpinInProgress)
        {
            return false;
        }


        if (RoundManager.Instance != null &&
            RoundManager.Instance.IsGameOver)
        {
            return false;
        }


        return true;
    }


    private void RefreshRootVisibility()
    {
        if (labelsRoot == null)
            return;


        bool shouldBeActive =
            ShouldRenderOverlay();


        if (labelsRoot.gameObject.activeSelf !=
            shouldBeActive)
        {
            labelsRoot.gameObject.SetActive(
                shouldBeActive
            );
        }
    }

    // =========================================================
    // REFERENCES / ROOT
    // =========================================================

    private void ResolveReferences()
    {
        if (sourceCamera == null)
        {
            sourceCamera =
                Camera.main;
        }


        if (RouletteController.Instance != null)
        {
            if (wheelCenter == null)
            {
                wheelCenter =
                    RouletteController.Instance.wheel;
            }
        }


        if (targetCanvas == null)
        {
            Canvas[] canvases =
                FindObjectsOfType<Canvas>(true);


            foreach (Canvas candidate in canvases)
            {
                if (candidate == null ||
                    !candidate.isRootCanvas)
                {
                    continue;
                }


                targetCanvas =
                    candidate;

                break;
            }
        }
    }


    private void EnsureLabelsRoot()
    {
        if (labelsRoot != null)
        {
            EnsureLabelsBehindMagnifier();
            return;
        }


        if (targetCanvas == null)
            return;


        GameObject rootObject =
            new GameObject(
                "ResolutionOrderLabels_Runtime",
                typeof(RectTransform)
            );


        rootObject.transform.SetParent(
            targetCanvas.transform,
            false
        );


        labelsRoot =
            rootObject.GetComponent<RectTransform>();


        StretchRectTransform(
            labelsRoot
        );

        EnsureLabelsBehindMagnifier();
    }


    /// <summary>
    /// Screen-space Canvas graphics are drawn by sibling order, not camera layers.
    ///
    /// The runtime LabelsRoot is created after the authored MagnifierRoot, so it
    /// was being drawn ON TOP of the lens. That is why the numbers looked like
    /// part of the magnifier even when UI layers were excluded from the camera.
    ///
    /// Keep the labels below MagnifierRoot in the Canvas hierarchy.
    /// </summary>
    private void EnsureLabelsBehindMagnifier()
    {
        if (labelsRoot == null ||
            MagnifierManager.Instance == null)
        {
            return;
        }


        RectTransform magnifierRoot =
            MagnifierManager.Instance.MagnifierRoot;


        if (magnifierRoot == null ||
            magnifierRoot.parent != labelsRoot.parent)
        {
            return;
        }


        int magnifierIndex =
            magnifierRoot.GetSiblingIndex();

        int labelsIndex =
            labelsRoot.GetSiblingIndex();


        if (labelsIndex > magnifierIndex)
        {
            labelsRoot.SetSiblingIndex(
                magnifierIndex
            );
        }
    }


    // =========================================================
    // ORDER CALCULATION / DISPLAY
    // =========================================================

    private void RefreshOrderLabels()
    {
        switch (currentViewMode)
        {
            case ViewMode.SegmentOrder:
                RefreshSegmentOrderLabels();
                break;


            case ViewMode.RadialOrder:
                RefreshRadialOrderLabels();
                break;


            case ViewMode.Off:
            default:
                HideAllLabels();
                break;
        }
    }


    private void RefreshSegmentOrderLabels()
    {
        WheelGenerator generator =
            RouletteController.Instance.generator;


        if (generator.segments == null)
        {
            HideAllLabels();
            return;
        }


        Collider2D prospectiveDraggedSegment =
            GetProspectiveDraggedSegment();


        int usedLabels =
            0;


        for (int segmentIndex = 0;
             segmentIndex < generator.segments.Count;
             segmentIndex++)
        {
            WheelSegmentData segmentData =
                generator.segments[segmentIndex];


            if (segmentData == null ||
                segmentData.collider == null ||
                generator.IsSegmentBlocked(
                    segmentIndex
                ))
            {
                continue;
            }


            Transform segmentTransform =
                segmentData.collider.transform;


            workingSegmentStickers.Clear();


            BaseSticker[] placedStickers =
                segmentTransform
                    .GetComponentsInChildren<BaseSticker>(
                        true
                    );


            foreach (BaseSticker sticker in
                     placedStickers)
            {
                if (sticker == null ||
                    !sticker.isPlaced ||
                    sticker.currentSegment !=
                        segmentTransform)
                {
                    continue;
                }


                workingSegmentStickers.Add(
                    sticker
                );
            }


            if (activeDraggedSticker != null &&
                prospectiveDraggedSegment != null &&
                prospectiveDraggedSegment.transform ==
                    segmentTransform)
            {
                workingSegmentStickers.Add(
                    activeDraggedSticker
                );
            }


            if (workingSegmentStickers.Count <= 0)
                continue;


            List<BaseSticker> ordered =
                StickerResolutionOrderUtility
                    .BuildSegmentResolutionOrder(
                        workingSegmentStickers,
                        wheelCenter
                    );


            for (int i = 0;
                 i < ordered.Count;
                 i++)
            {
                BaseSticker sticker =
                    ordered[i];


                if (sticker == null)
                    continue;


                OrderLabelVisual label =
                    GetLabel(
                        usedLabels
                    );


                usedLabels++;


                Color numberColor =
                    StickerResolutionOrderUtility
                        .HasSpinResolutionPriorityOverride(
                            sticker
                        )
                            ? priorityOverrideLabelColor
                            : labelColor;


                ConfigureLabel(
                    label,
                    i + 1,
                    sticker,
                    numberColor,
                    false
                );
            }
        }


        HideUnusedLabels(
            usedLabels
        );
    }


    private void RefreshRadialOrderLabels()
    {
        WheelGenerator generator =
            RouletteController.Instance.generator;


        if (generator.segments == null)
        {
            HideAllLabels();
            return;
        }


        Collider2D prospectiveDraggedSegment =
            GetProspectiveDraggedSegment();


        workingWheelStickers.Clear();
        workingWheelStickerSet.Clear();


        for (int segmentIndex = 0;
             segmentIndex < generator.segments.Count;
             segmentIndex++)
        {
            WheelSegmentData segmentData =
                generator.segments[segmentIndex];


            if (segmentData == null ||
                segmentData.collider == null ||
                generator.IsSegmentBlocked(
                    segmentIndex
                ))
            {
                continue;
            }


            Transform segmentTransform =
                segmentData.collider.transform;


            BaseSticker[] placedStickers =
                segmentTransform
                    .GetComponentsInChildren<BaseSticker>(
                        true
                    );


            foreach (BaseSticker sticker in
                     placedStickers)
            {
                if (sticker == null ||
                    !sticker.isPlaced ||
                    sticker.currentSegment !=
                        segmentTransform ||
                    !workingWheelStickerSet.Add(
                        sticker
                    ))
                {
                    continue;
                }


                workingWheelStickers.Add(
                    sticker
                );
            }
        }


        if (activeDraggedSticker != null &&
            prospectiveDraggedSegment != null)
        {
            int prospectiveSegmentIndex =
                generator.GetSegmentIndex(
                    prospectiveDraggedSegment.transform
                );


            if (prospectiveSegmentIndex >= 0 &&
                !generator.IsSegmentBlocked(
                    prospectiveSegmentIndex
                ) &&
                workingWheelStickerSet.Add(
                    activeDraggedSticker
                ))
            {
                workingWheelStickers.Add(
                    activeDraggedSticker
                );
            }
        }


        List<BaseSticker> ordered =
            StickerResolutionOrderUtility
                .BuildRadialResolutionOrder(
                    workingWheelStickers,
                    wheelCenter
                );


        int usedLabels =
            0;


        for (int i = 0;
             i < ordered.Count;
             i++)
        {
            BaseSticker sticker =
                ordered[i];


            if (sticker == null)
                continue;


            OrderLabelVisual label =
                GetLabel(
                    usedLabels
                );


            usedLabels++;


            ConfigureLabel(
                label,
                i + 1,
                sticker,
                labelColor,
                true
            );
        }


        HideUnusedLabels(
            usedLabels
        );
    }


    private void HideUnusedLabels(
        int usedLabels)
    {
        for (int i = usedLabels;
             i < labelPool.Count;
             i++)
        {
            OrderLabelVisual visual =
                labelPool[i];


            if (visual != null &&
                visual.root != null)
            {
                visual.root.gameObject
                    .SetActive(false);
            }
        }
    }


    private Collider2D GetProspectiveDraggedSegment()
    {
        if (!previewDraggedSticker ||
            activeDraggedSticker == null)
        {
            return null;
        }


        if (activeDraggedSticker.StickerCollider == null)
            return null;


        return
            StickerPlacementUtility
                .FindValidSegment(
                    activeDraggedSticker,
                    activeDraggedSticker.segmentMask,
                    activeDraggedSticker.tolerance
                );
    }


    private OrderLabelVisual GetLabel(
        int index)
    {
        while (labelPool.Count <=
               index)
        {
            GameObject rootObject =
                new GameObject(
                    $"ResolutionOrder_{labelPool.Count + 1}",
                    typeof(RectTransform)
                );


            rootObject.transform.SetParent(
                labelsRoot,
                false
            );


            RectTransform root =
                rootObject.GetComponent<RectTransform>();


            OrderLabelVisual visual =
                new OrderLabelVisual
                {
                    root = root
                };


            /*
             * Create the eight black copies first so the white number is rendered
             * above them.
             */
            for (int i = 0;
                 i < 8;
                 i++)
            {
                visual.outlineCopies.Add(
                    CreateLabelText(
                        root,
                        $"Outline_{i}"
                    )
                );
            }


            visual.front =
                CreateLabelText(
                    root,
                    "Number"
                );


            labelPool.Add(
                visual
            );
        }


        OrderLabelVisual result =
            labelPool[index];


        if (result != null &&
            result.root != null &&
            !result.root.gameObject.activeSelf)
        {
            result.root.gameObject
                .SetActive(true);
        }


        return result;
    }


    private TextMeshProUGUI CreateLabelText(
        RectTransform parent,
        string objectName)
    {
        if (parent == null)
            return null;


        GameObject textObject =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );


        textObject.transform.SetParent(
            parent,
            false
        );


        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();


        if (text != null)
        {
            text.raycastTarget =
                false;

            text.enableAutoSizing =
                false;
        }


        return text;
    }


    private void ConfigureLabel(
        OrderLabelVisual visual,
        int order,
        BaseSticker sticker,
        Color numberColor,
        bool radialOrder)
    {
        if (visual == null ||
            visual.root == null ||
            visual.front == null ||
            sticker == null)
        {
            return;
        }


        visual.root.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            labelSize
        );

        visual.root.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            labelSize
        );


        string orderText =
            order.ToString();


        float resolvedFontSize =
            labelFontSize;


        if (radialOrder &&
            order >= 10)
        {
            resolvedFontSize =
                labelFontSize *
                Mathf.Clamp(
                    radialMultiDigitFontScale,
                    0.4f,
                    1f
                );
        }


        float outlineDistance =
            Mathf.Lerp(
                0f,
                4f,
                Mathf.Clamp01(
                    labelOutlineWidth
                )
            );


        for (int i = 0;
             i < visual.outlineCopies.Count;
             i++)
        {
            TextMeshProUGUI outline =
                visual.outlineCopies[i];

            if (outline == null)
                continue;


            bool showOutline =
                outlineDistance >
                0.001f;


            outline.gameObject.SetActive(
                showOutline
            );


            if (!showOutline)
                continue;


            ConfigureLabelText(
                outline,
                orderText,
                labelOutlineColor,
                resolvedFontSize
            );


            outline.rectTransform
                .anchoredPosition =
                    OutlineDirections[i] *
                    outlineDistance;
        }


        ConfigureLabelText(
            visual.front,
            orderText,
            numberColor,
            resolvedFontSize
        );


        visual.front.rectTransform
            .anchoredPosition =
                Vector2.zero;


        PositionLabelAtMeasuredPoint(
            visual.root,
            sticker
        );
    }


    private void ConfigureLabelText(
        TextMeshProUGUI text,
        string content,
        Color color,
        float fontSize)
    {
        if (text == null)
            return;


        RectTransform rect =
            text.rectTransform;


        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        rect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );


        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            labelSize
        );

        rect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            labelSize
        );


        if (labelFont != null)
        {
            text.font =
                labelFont;
        }


        text.text =
            content;

        text.fontSize =
            fontSize;

        /*
         * Order labels are atomic numbers. Never allow TMP to split a
         * multi-digit value such as 10 into two vertical lines.
         */
        text.enableWordWrapping =
            false;

        text.overflowMode =
            TextOverflowModes.Overflow;

        text.fontStyle =
            FontStyles.Bold;

        text.alignment =
            TextAlignmentOptions.Center;

        text.color =
            color;

        /*
         * The black duplicate glyphs are now the single outline authority.
         */
        text.outlineWidth =
            0f;

        text.raycastTarget =
            false;
    }


    private void PositionLabelAtMeasuredPoint(
        RectTransform labelRect,
        BaseSticker sticker)
    {
        if (labelRect == null ||
            sticker == null ||
            sourceCamera == null ||
            labelsRoot == null ||
            wheelCenter == null)
        {
            return;
        }


        Vector2 measuredWorldPoint =
            StickerResolutionOrderUtility
                .GetClosestPointToWheelCenter(
                    sticker,
                    wheelCenter
                );


        Vector2 measuredScreenPoint =
            sourceCamera.WorldToScreenPoint(
                measuredWorldPoint
            );


        Vector2 centerScreenPoint =
            sourceCamera.WorldToScreenPoint(
                wheelCenter.position
            );


        Vector2 outwardDirection =
            measuredScreenPoint -
            centerScreenPoint;


        if (outwardDirection.sqrMagnitude >
            0.0001f)
        {
            outwardDirection.Normalize();

            measuredScreenPoint +=
                outwardDirection *
                radialLabelOffsetPixels;
        }


        Camera uiCamera =
            targetCanvas != null &&
            targetCanvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas != null
                    ? targetCanvas.worldCamera
                    : null;


        Vector2 localPoint;


        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                labelsRoot,
                measuredScreenPoint,
                uiCamera,
                out localPoint))
        {
            return;
        }


        labelRect.anchoredPosition =
            localPoint;
    }

    // =========================================================
    // LABEL VISIBILITY
    // =========================================================

    private void HideAllLabels()
    {
        foreach (OrderLabelVisual visual in
                 labelPool)
        {
            if (visual == null ||
                visual.root == null)
            {
                continue;
            }


            visual.root.gameObject
                .SetActive(false);
        }
    }


    // =========================================================
    // UTILITY BUTTON VISUAL
    // =========================================================

    private void RefreshUtilityButtonVisual()
    {
        bool segmentActive =
            currentViewMode ==
                ViewMode.SegmentOrder;

        bool radialActive =
            currentViewMode ==
                ViewMode.RadialOrder;


        RefreshOneUtilityButtonVisual(
            utilityCanvasButton,
            utilityButtonActiveIndicator,
            utilityButtonColorTarget,
            segmentActive
        );


        RefreshOneUtilityButtonVisual(
            radialUtilityCanvasButton,
            radialUtilityButtonActiveIndicator,
            radialUtilityButtonColorTarget,
            radialActive
        );
    }


    private void RefreshOneUtilityButtonVisual(
        Button button,
        GameObject activeIndicator,
        Graphic explicitColorTarget,
        bool active)
    {
        if (activeIndicator != null)
        {
            activeIndicator.SetActive(
                active
            );
        }


        Color stateColor =
            active
                ? utilityButtonOnColor
                : utilityButtonOffColor;


        Graphic resolvedTarget =
            explicitColorTarget;


        if (button != null)
        {
            if (resolvedTarget == null)
            {
                resolvedTarget =
                    button.targetGraphic;
            }


            if (button.transition ==
                Selectable.Transition.ColorTint)
            {
                ColorBlock colors =
                    button.colors;


                colors.normalColor =
                    stateColor;

                colors.selectedColor =
                    stateColor;

                colors.highlightedColor =
                    Color.Lerp(
                        stateColor,
                        Color.white,
                        0.15f
                    );

                colors.pressedColor =
                    Color.Lerp(
                        stateColor,
                        Color.black,
                        0.18f
                    );


                button.colors =
                    colors;
            }
        }


        if (resolvedTarget != null)
        {
            resolvedTarget.color =
                stateColor;
        }
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

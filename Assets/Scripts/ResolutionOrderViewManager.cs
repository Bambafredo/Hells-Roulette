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
/// - Shows one number per wheel sticker; numbering restarts at 1 in every segment.
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


    [SerializeField]
    private Color labelColor =
        Color.white;


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

    private bool viewEnabled =
        false;

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

    // =========================================================
    // PUBLIC STATE / BUTTON API
    // =========================================================

    public bool IsResolutionOrderViewEnabled =>
        viewEnabled;


    /// <summary>
    /// Wire a normal Unity UI Button's Inspector OnClick to this method.
    /// </summary>
    public void ToggleResolutionOrderView()
    {
        SetResolutionOrderView(
            !viewEnabled
        );
    }


    public void ShowResolutionOrderView()
    {
        SetResolutionOrderView(true);
    }


    public void HideResolutionOrderView()
    {
        SetResolutionOrderView(false);
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


        viewEnabled =
            startVisible;


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


        HandleOptionalWorldUtilityButton();
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

    private void HandleOptionalWorldUtilityButton()
    {
        if (!enableResolutionOrderView ||
            utilityButtonCollider == null ||
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


        if (!utilityButtonCollider
            .OverlapPoint(mouseWorld))
        {
            return;
        }


        /*
         * If manual wheel drag is ever enabled again for debug / editor use,
         * consume this click so the utility button cannot also start a wheel drag.
         */
        RouletteController.Instance?
            .ConsumePointerInputThisFrame();


        ToggleResolutionOrderView();
    }


    // =========================================================
    // VIEW STATE
    // =========================================================

    private void SetResolutionOrderView(
        bool enabled)
    {
        viewEnabled =
            enabled;


        RefreshRootVisibility();


        RefreshUtilityButtonVisual();


        if (!enabled)
        {
            HideAllLabels();
        }
    }


    private bool ShouldRenderOverlay()
    {
        if (!enableResolutionOrderView ||
            !viewEnabled)
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


        foreach (var segmentData in
                 generator.segments)
        {
            if (segmentData == null ||
                segmentData.collider == null)
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
                    .BuildStableResolutionOrder(
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


                ConfigureLabel(
                    label,
                    i + 1,
                    sticker
                );
            }
        }


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
        BaseSticker sticker)
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
                labelOutlineColor
            );


            outline.rectTransform
                .anchoredPosition =
                    OutlineDirections[i] *
                    outlineDistance;
        }


        ConfigureLabelText(
            visual.front,
            orderText,
            labelColor
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
        Color color)
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
            labelFontSize;

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
        if (utilityButtonActiveIndicator != null)
        {
            utilityButtonActiveIndicator.SetActive(
                viewEnabled
            );
        }


        Color stateColor =
            viewEnabled
                ? utilityButtonOnColor
                : utilityButtonOffColor;


        Graphic resolvedTarget =
            utilityButtonColorTarget;


        /*
         * Normal Canvas Button support.
         *
         * Color Tint buttons overwrite targetGraphic.color during hover/press.
         * Update their ColorBlock AND the current graphic so the state change is
         * immediate and remains correct after pointer transitions.
         */
        if (utilityCanvasButton != null)
        {
            if (resolvedTarget == null)
            {
                resolvedTarget =
                    utilityCanvasButton.targetGraphic;
            }


            if (utilityCanvasButton.transition ==
                Selectable.Transition.ColorTint)
            {
                ColorBlock colors =
                    utilityCanvasButton.colors;


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


                utilityCanvasButton.colors =
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

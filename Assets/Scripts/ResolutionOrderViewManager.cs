using System.Collections.Generic;
using TMPro;
using UnityEngine;


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
        "Optional child / icon that is active only while the order view itself is ON."
    )]
    [SerializeField]
    private GameObject utilityButtonActiveIndicator;


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
        "Normalized outline thickness. 0 = none, 1 = strong readable outline. " +
        "The manager maps this to a safe TMP SDF outline internally."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float labelOutlineWidth =
        0.28f;


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

    /*
     * Runtime-only capture state used by the magnifier presentation hooks.
     * The order labels are hidden only for the secondary-camera Render() call,
     * then restored immediately for the normal gameplay view.
     */
    private bool labelsWereActiveBeforeMagnifierCapture =
        false;

    private readonly List<TextMeshProUGUI>
        labelPool =
            new List<TextMeshProUGUI>();

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


        viewEnabled =
            startVisible;


        RefreshRootVisibility();


        if (utilityButtonActiveIndicator != null)
        {
            utilityButtonActiveIndicator.SetActive(
                viewEnabled
            );
        }
    }


    private void OnEnable()
    {
        BaseSticker.OnAnyStickerDragStarted +=
            HandleStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded +=
            HandleStickerDragEnded;

        MagnifierManager.OnBeforeMagnifierCameraRender +=
            HandleBeforeMagnifierCameraRender;

        MagnifierManager.OnAfterMagnifierCameraRender +=
            HandleAfterMagnifierCameraRender;
    }


    private void OnDisable()
    {
        BaseSticker.OnAnyStickerDragStarted -=
            HandleStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded -=
            HandleStickerDragEnded;

        MagnifierManager.OnBeforeMagnifierCameraRender -=
            HandleBeforeMagnifierCameraRender;

        MagnifierManager.OnAfterMagnifierCameraRender -=
            HandleAfterMagnifierCameraRender;


        activeDraggedSticker =
            null;

        labelsWereActiveBeforeMagnifierCapture =
            false;


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
    // MAGNIFIER CAPTURE EXCLUSION
    // =========================================================

    private void HandleBeforeMagnifierCameraRender()
    {
        if (labelsRoot == null)
        {
            labelsWereActiveBeforeMagnifierCapture =
                false;

            return;
        }


        labelsWereActiveBeforeMagnifierCapture =
            labelsRoot.gameObject.activeSelf;


        if (labelsWereActiveBeforeMagnifierCapture)
        {
            /*
             * Hide ONLY for MagnifierCamera.Render(). MagnifierManager fires the
             * matching After event synchronously immediately after that render,
             * so the player still sees the labels in the normal gameplay view.
             *
             * This is more reliable than layer tricks because the order labels
             * are runtime Canvas graphics and the project can use different
             * Canvas render modes / camera culling masks.
             */
            labelsRoot.gameObject.SetActive(
                false
            );
        }
    }


    private void HandleAfterMagnifierCameraRender()
    {
        if (labelsRoot != null &&
            labelsWereActiveBeforeMagnifierCapture)
        {
            labelsRoot.gameObject.SetActive(
                true
            );
        }


        labelsWereActiveBeforeMagnifierCapture =
            false;
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


        if (utilityButtonActiveIndicator != null)
        {
            utilityButtonActiveIndicator.SetActive(
                viewEnabled
            );
        }


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
            return;


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


                TextMeshProUGUI label =
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
            if (labelPool[i] != null)
            {
                labelPool[i]
                    .gameObject
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


    private TextMeshProUGUI GetLabel(
        int index)
    {
        while (labelPool.Count <=
               index)
        {
            GameObject labelObject =
                new GameObject(
                    $"ResolutionOrder_{labelPool.Count + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)
                );


            labelObject.transform.SetParent(
                labelsRoot,
                false
            );


            TextMeshProUGUI label =
                labelObject
                    .GetComponent<TextMeshProUGUI>();


            label.raycastTarget =
                false;


            labelPool.Add(
                label
            );
        }


        TextMeshProUGUI result =
            labelPool[index];


        if (result != null &&
            !result.gameObject.activeSelf)
        {
            result.gameObject.SetActive(true);
        }


        return result;
    }


    private void ConfigureLabel(
        TextMeshProUGUI label,
        int order,
        BaseSticker sticker)
    {
        if (label == null ||
            sticker == null)
        {
            return;
        }


        RectTransform labelRect =
            label.rectTransform;


        labelRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            labelSize
        );

        labelRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            labelSize
        );


        if (labelFont != null)
        {
            label.font =
                labelFont;
        }


        label.text =
            order.ToString();

        label.fontSize =
            labelFontSize;

        label.fontStyle =
            FontStyles.Bold;

        label.alignment =
            TextAlignmentOptions.Center;

        label.color =
            labelColor;

        /*
         * TMP's convenient outline properties can fail to visibly update on
         * TextMeshProUGUI objects created entirely at runtime, depending on the
         * font material instance Unity/TMP has resolved for that frame.
         *
         * Force an instance material and write the SDF shader properties
         * directly. The Inspector value remains normalized 0..1; internally it
         * maps to a conservative 0..0.35 SDF outline so the glyph itself is not
         * swallowed at the maximum slider value.
         */
        float effectiveOutlineWidth =
            Mathf.Clamp01(
                labelOutlineWidth
            ) * 0.35f;


        label.outlineColor =
            labelOutlineColor;

        label.outlineWidth =
            effectiveOutlineWidth;


        Material labelMaterial =
            label.fontMaterial;


        if (labelMaterial != null)
        {
            if (labelMaterial.HasProperty(
                    ShaderUtilities.ID_OutlineColor))
            {
                labelMaterial.SetColor(
                    ShaderUtilities.ID_OutlineColor,
                    labelOutlineColor
                );
            }


            if (labelMaterial.HasProperty(
                    ShaderUtilities.ID_OutlineWidth))
            {
                labelMaterial.SetFloat(
                    ShaderUtilities.ID_OutlineWidth,
                    effectiveOutlineWidth
                );
            }
        }


        /*
         * Outline changes alter TMP's required mesh padding. Recalculate it now
         * so a thick outline is not clipped by the glyph's previous bounds.
         */
        label.UpdateMeshPadding();
        label.SetVerticesDirty();


        label.raycastTarget =
            false;


        PositionLabelAtMeasuredPoint(
            labelRect,
            sticker
        );
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
        foreach (TextMeshProUGUI label in
                 labelPool)
        {
            if (label == null)
                continue;


            label.gameObject
                .SetActive(false);
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

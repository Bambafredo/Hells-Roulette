using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SegmentBlockTooltipManager : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]

    [Tooltip(
        "WheelGenerator that owns the procedural segment state."
    )]
    public WheelGenerator generator;

    [Tooltip(
        "RouletteController used to hide the tooltip while the wheel is moving."
    )]
    public RouletteController roulette;

    [Tooltip(
        "Canvas containing this tooltip."
    )]
    public Canvas canvas;

    [Tooltip(
        "Root RectTransform of the tooltip panel."
    )]
    public RectTransform tooltipPanel;

    [Tooltip(
        "TextMeshProUGUI used for the tooltip text."
    )]
    public TMP_Text tooltipText;


    // =========================================================
    // HOVER
    // =========================================================

    [Header("Hover")]

    [Tooltip(
        "Set this to the Segment layer only."
    )]
    public LayerMask segmentMask;

    [Tooltip(
        "If true, hovering a sticker takes priority over the segment tooltip."
    )]
    public bool suppressWhenHoveringSticker =
        true;


    // =========================================================
    // POSITION
    // =========================================================

    [Header("Position")]

    [Tooltip(
        "Screen-space offset from the mouse cursor."
    )]
    public Vector2 mouseOffset =
        new Vector2(
            24f,
            18f
        );

    [Min(0f)]
    public float screenEdgePadding =
        12f;


    // =========================================================
    // SIZE
    // =========================================================

    [Header("Size")]

    [Min(100f)]
    public float maxTextWidth =
        420f;

    [Min(0f)]
    public float minPanelWidth =
        280f;

    public Vector2 textPadding =
        new Vector2(
            16f,
            12f
        );


    // =========================================================
    // COLORS
    // =========================================================

    [Header("Colors")]

    [Tooltip(
        "Color used by the 'Segment blocked' heading."
    )]
    public Color blockedTitleColor =
        new Color(
            1f,
            0.2f,
            0.2f
        );

    [Tooltip(
        "Color used by the remaining-turn countdown."
    )]
    public Color unlockCountdownColor =
        new Color(
            1f,
            0.8f,
            0.2f
        );


    // =========================================================
    // TEXT
    // =========================================================

    [Header("Text")]

    public string blockedTitle =
        "Segment blocked";

    [TextArea(2, 4)]
    public string blockedDescription =
        "Stickers in blocked segments do not activate and can't be moved until unlocked.";


    // =========================================================
    // INTERNAL
    // =========================================================

    private Camera cam;

    private RectTransform canvasRect;
    private RectTransform textRect;

    private int currentSegmentIndex =
        -1;

    private int currentTurnsRemaining =
        -1;

    private string currentTooltipText =
        "";


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        cam =
            Camera.main;
    }


    private void Start()
    {
        EnsureReferences();


        if (generator == null ||
            tooltipPanel == null ||
            tooltipText == null ||
            canvasRect == null)
        {
            Debug.LogError(
                "[SEGMENT BLOCK TOOLTIP] Missing required references."
            );

            enabled =
                false;

            return;
        }


        ConfigureTooltipRect();

        HideTooltip();
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (Input.GetMouseButton(0))
        {
            HideTooltip();
            return;
        }


        if (roulette != null &&
            roulette.SpinInProgress)
        {
            HideTooltip();
            return;
        }


        if (cam == null)
            cam = Camera.main;


        if (cam == null)
        {
            HideTooltip();
            return;
        }


        Vector2 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );


        if (suppressWhenHoveringSticker &&
            IsHoveringSticker(
                mouseWorld
            ))
        {
            HideTooltip();
            return;
        }


        int segmentIndex =
            FindHoveredBlockedSegment(
                mouseWorld
            );


        if (segmentIndex < 0)
        {
            HideTooltip();
            return;
        }


        int remaining =
            generator
                .GetSegmentBlockRemainingSpins(
                    segmentIndex
                );


        if (remaining <= 0)
        {
            HideTooltip();
            return;
        }


        string tooltip =
            BuildTooltip(
                remaining
            );


        ShowTooltip(
            segmentIndex,
            remaining,
            tooltip
        );

#endif
    }


    // =========================================================
    // REFERENCES
    // =========================================================

    private void EnsureReferences()
    {
        if (generator == null)
        {
            generator =
                FindObjectOfType<WheelGenerator>();
        }


        if (roulette == null)
        {
            roulette =
                FindObjectOfType<RouletteController>();
        }


        if (canvas == null &&
            tooltipPanel != null)
        {
            canvas =
                tooltipPanel
                    .GetComponentInParent<Canvas>();
        }


        if (canvas != null)
        {
            canvasRect =
                canvas.transform as RectTransform;
        }


        if (tooltipText != null)
        {
            textRect =
                tooltipText.rectTransform;
        }
    }


    // =========================================================
    // PANEL CONFIG
    // =========================================================

    private void ConfigureTooltipRect()
    {
        tooltipPanel.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        tooltipPanel.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        tooltipPanel.pivot =
            new Vector2(
                0f,
                0f
            );


        tooltipText.richText =
            true;

        tooltipText.enableWordWrapping =
            true;


        if (textRect != null)
        {
            textRect.anchorMin =
                Vector2.zero;

            textRect.anchorMax =
                Vector2.one;

            textRect.offsetMin =
                new Vector2(
                    textPadding.x,
                    textPadding.y
                );

            textRect.offsetMax =
                new Vector2(
                    -textPadding.x,
                    -textPadding.y
                );
        }


        /*
         * Informational only: never steal pointer input.
         */
        Graphic panelGraphic =
            tooltipPanel
                .GetComponent<Graphic>();

        if (panelGraphic != null)
        {
            panelGraphic.raycastTarget =
                false;
        }


        Graphic textGraphic =
            tooltipText as Graphic;

        if (textGraphic != null)
        {
            textGraphic.raycastTarget =
                false;
        }
    }


    // =========================================================
    // HOVER
    // =========================================================

    private int FindHoveredBlockedSegment(
        Vector2 mouseWorld)
    {
        Collider2D[] hits =
            Physics2D.OverlapPointAll(
                mouseWorld,
                segmentMask
            );


        if (hits == null ||
            hits.Length == 0)
        {
            return -1;
        }


        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                !hit.enabled)
            {
                continue;
            }


            SegmentMesh segmentMesh =
                hit.GetComponent<SegmentMesh>();


            if (segmentMesh == null ||
                !segmentMesh.IsBlocked)
            {
                continue;
            }


            int index =
                generator.GetSegmentIndex(
                    hit.transform
                );


            if (index >= 0 &&
                generator.IsSegmentBlocked(index))
            {
                return
                    index;
            }
        }


        return -1;
    }


    private bool IsHoveringSticker(
        Vector2 mouseWorld)
    {
        Collider2D[] hits =
            Physics2D.OverlapPointAll(
                mouseWorld
            );


        if (hits == null)
            return false;


        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                !hit.enabled)
            {
                continue;
            }


            BaseSticker direct =
                hit.GetComponentInParent<BaseSticker>();


            if (direct != null &&
                direct.StickerCollider == hit)
            {
                return true;
            }


            /*
             * Current sticker prefab structure can place Collider2D and
             * BaseSticker on sibling branches under StickerRoot.
             */
            Transform searchRoot =
                hit.transform;


            while (searchRoot != null)
            {
                BaseSticker[] candidates =
                    searchRoot
                        .GetComponentsInChildren<BaseSticker>(
                            true
                        );


                foreach (BaseSticker candidate in
                         candidates)
                {
                    if (candidate != null &&
                        candidate.StickerCollider == hit)
                    {
                        return true;
                    }
                }


                searchRoot =
                    searchRoot.parent;
            }
        }


        return false;
    }


    // =========================================================
    // CONTENT
    // =========================================================

    private string BuildTooltip(
        int turnsRemaining)
    {
        string titleHex =
            ColorUtility.ToHtmlStringRGB(
                blockedTitleColor
            );


        string unlockHex =
            ColorUtility.ToHtmlStringRGB(
                unlockCountdownColor
            );


        string turnWord =
            turnsRemaining == 1
                ? "turn"
                : "turns";


        return
            $"<color=#{titleHex}>{blockedTitle}</color>\n" +
            blockedDescription +
            "\n" +
            $"<color=#{unlockHex}>Unlocks in {turnsRemaining} {turnWord}</color>";
    }


    // =========================================================
    // SHOW / HIDE
    // =========================================================

    private void ShowTooltip(
        int segmentIndex,
        int turnsRemaining,
        string tooltip)
    {
        if (!tooltipPanel.gameObject.activeSelf)
        {
            tooltipPanel.gameObject
                .SetActive(true);

            tooltipPanel
                .SetAsLastSibling();
        }


        if (currentSegmentIndex !=
                segmentIndex ||
            currentTurnsRemaining !=
                turnsRemaining ||
            currentTooltipText !=
                tooltip)
        {
            currentSegmentIndex =
                segmentIndex;

            currentTurnsRemaining =
                turnsRemaining;

            currentTooltipText =
                tooltip;

            tooltipText.text =
                tooltip;

            ResizePanelToText();
        }


        UpdateTooltipPosition();
    }


    private void HideTooltip()
    {
        currentSegmentIndex =
            -1;

        currentTurnsRemaining =
            -1;

        currentTooltipText =
            "";


        if (tooltipPanel != null &&
            tooltipPanel.gameObject.activeSelf)
        {
            tooltipPanel.gameObject
                .SetActive(false);
        }
    }


    // =========================================================
    // SIZE
    // =========================================================

    private void ResizePanelToText()
    {
        float availableTextWidth =
            Mathf.Max(
                100f,
                maxTextWidth
            );


        Vector2 preferred =
            tooltipText
                .GetPreferredValues(
                    currentTooltipText,
                    availableTextWidth,
                    0f
                );


        float textWidth =
            Mathf.Clamp(
                preferred.x,
                0f,
                availableTextWidth
            );


        float panelWidth =
            Mathf.Max(
                minPanelWidth,
                textWidth +
                textPadding.x * 2f
            );


        float actualTextWidth =
            Mathf.Max(
                1f,
                panelWidth -
                textPadding.x * 2f
            );


        Vector2 wrappedPreferred =
            tooltipText
                .GetPreferredValues(
                    currentTooltipText,
                    actualTextWidth,
                    0f
                );


        float panelHeight =
            wrappedPreferred.y +
            textPadding.y * 2f;


        tooltipPanel.sizeDelta =
            new Vector2(
                panelWidth,
                panelHeight
            );


        Canvas.ForceUpdateCanvases();
    }


    // =========================================================
    // POSITION
    // =========================================================

    private void UpdateTooltipPosition()
    {
        if (canvasRect == null ||
            canvas == null)
        {
            return;
        }


        Camera uiCamera =
            canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;


        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                uiCamera,
                out Vector2 localMouse))
        {
            return;
        }


        float localOffsetX =
            ScreenPixelsToCanvasX(
                mouseOffset.x,
                uiCamera
            );

        float localOffsetY =
            ScreenPixelsToCanvasY(
                mouseOffset.y,
                uiCamera
            );


        Vector2 desiredPosition =
            localMouse +
            new Vector2(
                localOffsetX,
                localOffsetY
            );


        /*
         * Default = open to the right / above the pointer.
         * If that would hit a Canvas edge, ClampInsideCanvas keeps it visible.
         */
        ClampInsideCanvas(
            ref desiredPosition
        );


        tooltipPanel.anchoredPosition =
            desiredPosition;
    }


    private float ScreenPixelsToCanvasX(
        float pixels,
        Camera uiCamera)
    {
        if (RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    Vector2.zero,
                    uiCamera,
                    out Vector2 a) &&
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    new Vector2(
                        pixels,
                        0f
                    ),
                    uiCamera,
                    out Vector2 b))
        {
            return
                b.x - a.x;
        }


        return
            pixels;
    }


    private float ScreenPixelsToCanvasY(
        float pixels,
        Camera uiCamera)
    {
        if (RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    Vector2.zero,
                    uiCamera,
                    out Vector2 a) &&
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    new Vector2(
                        0f,
                        pixels
                    ),
                    uiCamera,
                    out Vector2 b))
        {
            return
                b.y - a.y;
        }


        return
            pixels;
    }


    private void ClampInsideCanvas(
        ref Vector2 desiredPosition)
    {
        Rect bounds =
            canvasRect.rect;


        float width =
            tooltipPanel.rect.width;

        float height =
            tooltipPanel.rect.height;


        float minX =
            bounds.xMin +
            screenEdgePadding +
            width *
            tooltipPanel.pivot.x;


        float maxX =
            bounds.xMax -
            screenEdgePadding -
            width *
            (
                1f -
                tooltipPanel.pivot.x
            );


        float minY =
            bounds.yMin +
            screenEdgePadding +
            height *
            tooltipPanel.pivot.y;


        float maxY =
            bounds.yMax -
            screenEdgePadding -
            height *
            (
                1f -
                tooltipPanel.pivot.y
            );


        desiredPosition.x =
            minX <= maxX
                ? Mathf.Clamp(
                    desiredPosition.x,
                    minX,
                    maxX
                )
                : 0f;


        desiredPosition.y =
            minY <= maxY
                ? Mathf.Clamp(
                    desiredPosition.y,
                    minY,
                    maxY
                )
                : 0f;
    }
}

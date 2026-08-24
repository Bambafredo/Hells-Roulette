using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StickerTooltipManager : MonoBehaviour
{
    public static StickerTooltipManager Instance;


    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]

    [Tooltip("Canvas that contains the tooltip.")]
    public Canvas canvas;

    [Tooltip("Root RectTransform of the tooltip panel.")]
    public RectTransform tooltipPanel;

    [Tooltip("TextMeshProUGUI used to display the tooltip.")]
    public TMP_Text tooltipText;


    // =========================================================
    // POSITION
    // =========================================================

    [Header("Position")]

    [Tooltip(
        "Horizontal gap between the sticker and the tooltip."
    )]
    [Min(0f)]
    public float horizontalOffset = 24f;

    [Tooltip(
        "Minimum distance between the tooltip and the Canvas edge."
    )]
    [Min(0f)]
    public float screenEdgePadding = 12f;


    // =========================================================
    // SIZE
    // =========================================================

    [Header("Size")]

    [Tooltip(
        "Maximum width available to the text before wrapping."
    )]
    [Min(100f)]
    public float maxTextWidth = 420f;

    [Tooltip(
        "Minimum width of the tooltip panel."
    )]
    [Min(0f)]
    public float minPanelWidth = 220f;

    [Tooltip(
        "Internal horizontal / vertical padding around the text."
    )]
    public Vector2 textPadding =
        new Vector2(
            16f,
            12f
        );


    // =========================================================
    // COLORS
    // =========================================================

    [Header("Colors")]

    public Color nameColor =
        Color.white;

    public Color winningSegmentColor =
        new Color(
            0.35f,
            1f,
            0.45f
        );

    public Color losingSegmentColor =
        new Color(
            1f,
            0.65f,
            0.2f
        );

    public Color albumColor =
        new Color(
            0.4f,
            0.75f,
            1f
        );


    // =========================================================
    // INTERNAL
    // =========================================================

    private Camera cam;

    private RectTransform canvasRect;
    private RectTransform textRect;

    private BaseSticker currentSticker;
    private string currentTooltipText = "";

    private readonly StringBuilder builder =
        new StringBuilder();


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }


        Instance =
            this;

        cam =
            Camera.main;
    }


    private void Start()
    {
        EnsureReferences();


        if (tooltipPanel == null ||
            tooltipText == null ||
            canvasRect == null)
        {
            Debug.LogError(
                "[STICKER TOOLTIP] Missing required references."
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

        /*
         * Never show the tooltip while LMB is being held.
         *
         * This covers the entire drag-and-drop interaction without adding
         * any dependency to BaseSticker's private dragging state.
         */
        if (Input.GetMouseButton(0))
        {
            HideTooltip();
            return;
        }


        BaseSticker hoveredSticker =
            FindHoveredSticker();


        if (hoveredSticker == null ||
            hoveredSticker.effect == null)
        {
            HideTooltip();
            return;
        }


        if (hoveredSticker.HasLimitedUses &&
            hoveredSticker.RemainingUses <= 0)
        {
            HideTooltip();
            return;
        }


        string tooltip =
            BuildTooltip(
                hoveredSticker
            );


        if (string.IsNullOrEmpty(tooltip))
        {
            HideTooltip();
            return;
        }


        ShowTooltip(
            hoveredSticker,
            tooltip
        );

#endif
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    // =========================================================
    // REFERENCES
    // =========================================================

    private void EnsureReferences()
    {
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


        if (cam == null)
            cam = Camera.main;
    }


    // =========================================================
    // PANEL CONFIG
    // =========================================================

    private void ConfigureTooltipRect()
    {
        /*
         * Runtime geometry only.
         *
         * The Image appearance, font, font size, etc. remain fully editable
         * in the Inspector.
         */
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
         * Tooltip is purely informational and must never steal clicks.
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

    private BaseSticker FindHoveredSticker()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            return null;


        Vector2 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );


        Collider2D[] hits =
            Physics2D.OverlapPointAll(
                mouseWorld
            );


        if (hits == null ||
            hits.Length == 0)
        {
            return null;
        }


        foreach (Collider2D hit in hits)
        {
            if (hit == null ||
                !hit.enabled)
            {
                continue;
            }


            BaseSticker sticker =
                ResolveStickerFromCollider(
                    hit
                );


            if (sticker != null)
                return sticker;
        }


        return null;
    }


    private BaseSticker ResolveStickerFromCollider(
        Collider2D hit)
    {
        if (hit == null)
            return null;


        /*
         * Old prefab structure:
         * Collider and BaseSticker share a parent chain.
         */
        BaseSticker directSticker =
            hit.GetComponentInParent<BaseSticker>();


        if (directSticker != null &&
            directSticker.StickerCollider == hit)
        {
            return directSticker;
        }


        /*
         * New prefab structure:
         *
         * StickerRoot
         * ├── Renderer/Sprite/Collider
         * └── Effect/BaseSticker
         *
         * Collider and BaseSticker can be siblings.
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


            foreach (BaseSticker candidate in candidates)
            {
                if (candidate == null)
                    continue;


                if (candidate.StickerCollider == hit)
                    return candidate;
            }


            searchRoot =
                searchRoot.parent;
        }


        return null;
    }


    // =========================================================
    // CONTENT
    // =========================================================

    private string BuildTooltip(
        BaseSticker sticker)
    {
        StickerEffect effect =
            sticker.effect;


        if (effect == null)
            return "";


        builder.Clear();


        string displayName =
            !string.IsNullOrWhiteSpace(
                effect.stickerName
            )
                ? effect.stickerName
                : sticker.gameObject.name;


        builder.Append(
            ColorText(
                $"Name: {displayName}",
                nameColor
            )
        );


        AppendLocationIfRelevant(
            sticker,
            StickerSpinLocation.WinningSegment,
            "Winning Segment",
            winningSegmentColor
        );


        AppendLocationIfRelevant(
            sticker,
            StickerSpinLocation.NonWinningSegment,
            "Losing Segment",
            losingSegmentColor
        );


        AppendLocationIfRelevant(
            sticker,
            StickerSpinLocation.Album,
            "Album",
            albumColor
        );


        return
            builder.ToString();
    }


    private void AppendLocationIfRelevant(
        BaseSticker sticker,
        StickerSpinLocation location,
        string label,
        Color labelColor)
    {
        StickerEffect effect =
            sticker.effect;


        if (effect == null ||
            !effect.HasTooltipEffect(
                sticker,
                location
            ))
        {
            return;
        }


        string description =
            effect.GetTooltipDescription(
                sticker,
                location
            );


        if (string.IsNullOrWhiteSpace(
            description))
        {
            return;
        }


        builder.Append('\n');


        string header =
            label;


        /*
         * Uses appear beside the location that actually consumes them.
         *
         * Example:
         * Winning Segment [1 use left]: Cash out $12
         *
         * Piggy Bank therefore shows uses beside Winning Segment but not
         * Losing Segment with its normal configuration.
         */
        if (sticker.HasLimitedUses &&
            effect.ShouldConsumeUseOnActivation(
                location
            ))
        {
            int uses =
                Mathf.Max(
                    0,
                    sticker.RemainingUses
                );


            header +=
                uses == 1
                    ? " [1 use left]"
                    : $" [{uses} uses left]";
        }


        builder.Append(
            ColorText(
                header,
                labelColor
            )
        );


        builder.Append(
            ": "
        );


        builder.Append(
            description
        );
    }


    // =========================================================
    // SHOW / HIDE
    // =========================================================

    private void ShowTooltip(
        BaseSticker sticker,
        string tooltip)
    {
        if (!tooltipPanel.gameObject.activeSelf)
        {
            tooltipPanel.gameObject
                .SetActive(true);

            tooltipPanel
                .SetAsLastSibling();
        }


        if (currentSticker != sticker ||
            currentTooltipText != tooltip)
        {
            currentSticker =
                sticker;

            currentTooltipText =
                tooltip;

            tooltipText.text =
                tooltip;


            ResizePanelToText();
        }


        UpdateTooltipPosition(
            sticker
        );
    }


    private void HideTooltip()
    {
        currentSticker =
            null;

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

    private void UpdateTooltipPosition(
        BaseSticker sticker)
    {
        if (sticker == null ||
            sticker.StickerCollider == null ||
            canvasRect == null ||
            canvas == null)
        {
            return;
        }


        bool stickerIsInAlbum =
            IsStickerInAlbum(
                sticker
            );


        /*
         * Album stickers open LEFT.
         * Roulette / Reward stickers open RIGHT.
         */
        float direction =
            stickerIsInAlbum
                ? -1f
                : 1f;


        tooltipPanel.pivot =
            stickerIsInAlbum
                ? new Vector2(
                    1f,
                    0.5f
                )
                : new Vector2(
                    0f,
                    0.5f
                );


        Bounds stickerBounds =
            sticker.StickerCollider.bounds;


        Vector3 worldAnchor =
            new Vector3(
                stickerIsInAlbum
                    ? stickerBounds.min.x
                    : stickerBounds.max.x,
                stickerBounds.center.y,
                stickerBounds.center.z
            );


        Vector2 screenAnchor =
            cam.WorldToScreenPoint(
                worldAnchor
            );


        Camera uiCamera =
            canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;


        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenAnchor,
                uiCamera,
                out Vector2 localAnchor))
        {
            return;
        }


        /*
         * Convert a screen-space pixel gap to Canvas-local units.
         * With a scaled Canvas this keeps the visual gap predictable.
         */
        Vector2 localMouseA;
        Vector2 localMouseB;

        float localOffset =
            horizontalOffset;


        if (RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    Vector2.zero,
                    uiCamera,
                    out localMouseA) &&
            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    new Vector2(
                        horizontalOffset,
                        0f
                    ),
                    uiCamera,
                    out localMouseB))
        {
            localOffset =
                Mathf.Abs(
                    localMouseB.x -
                    localMouseA.x
                );
        }


        Vector2 desiredPosition =
            localAnchor +
            new Vector2(
                direction *
                localOffset,
                0f
            );


        ClampInsideCanvas(
            ref desiredPosition
        );


        tooltipPanel.anchoredPosition =
            desiredPosition;
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


    private bool IsStickerInAlbum(
        BaseSticker sticker)
    {
        if (sticker == null)
            return false;


        if (sticker.currentAlbumZone != null)
            return true;


        return
            AlbumManager.Instance != null &&
            AlbumManager.Instance
                .IsStickerInAlbum(
                    sticker
                );
    }


    // =========================================================
    // COLOR
    // =========================================================

    private string ColorText(
        string text,
        Color color)
    {
        string hex =
            ColorUtility
                .ToHtmlStringRGBA(
                    color
                );


        return
            $"<color=#{hex}>" +
            text +
            "</color>";
    }
}

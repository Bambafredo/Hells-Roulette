using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyActionTooltipManager : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]

    [Tooltip(
        "Perspective camera that renders EnemyWorld."
    )]
    public Camera enemyCamera;

    [Tooltip(
        "Main UI Canvas that contains the tooltip panel."
    )]
    public Canvas canvas;

    [Tooltip(
        "Root RectTransform of the enemy action tooltip."
    )]
    public RectTransform tooltipPanel;

    [Tooltip(
        "TextMeshProUGUI used by the tooltip."
    )]
    public TMP_Text tooltipText;


    // =========================================================
    // HOVER
    // =========================================================

    [Header("Hover")]

    [Tooltip(
        "Physics2D layers checked by the enemy-camera ray. " +
        "EnemyView is fine; only Action_Icon colliders are accepted."
    )]
    public LayerMask hoverMask =
        ~0;


    // =========================================================
    // POSITION
    // =========================================================

    [Header("Position")]

    [Min(0f)]
    public float cursorOffset =
        18f;

    [Min(0f)]
    public float screenEdgePadding =
        12f;


    // =========================================================
    // SIZE
    // =========================================================

    [Header("Size")]

    [Min(100f)]
    public float maxTextWidth =
        360f;

    [Min(0f)]
    public float minPanelWidth =
        180f;

    public Vector2 textPadding =
        new Vector2(
            14f,
            10f
        );


    // =========================================================
    // COLORS
    // =========================================================

    [Header("Colors")]

    public Color actionNameColor =
        Color.white;


    // =========================================================
    // INTERNAL
    // =========================================================

    private RectTransform canvasRect;
    private RectTransform textRect;

    private BaseEnemy currentEnemy;
    private EnemyAction currentAction;
    private EnemyCurse currentCurse;

    private string currentText =
        "";


    // =========================================================
    // UNITY
    // =========================================================

    private void Start()
    {
        EnsureReferences();


        if (enemyCamera == null ||
            canvasRect == null ||
            tooltipPanel == null ||
            tooltipText == null)
        {
            Debug.LogError(
                "[ENEMY ACTION TOOLTIP] Missing required references."
            );

            enabled =
                false;

            return;
        }


        ConfigureTooltip();

        HideTooltip();
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        /*
         * No tooltip while the player is actively clicking/dragging.
         */
        if (Input.GetMouseButton(0))
        {
            HideTooltip();
            return;
        }


        if (enemyCamera == null ||
            !enemyCamera.enabled)
        {
            HideTooltip();
            return;
        }


        /*
         * Do not raycast outside the actual left-side EnemyCamera viewport.
         */
        if (!enemyCamera.pixelRect.Contains(
            Input.mousePosition))
        {
            HideTooltip();
            return;
        }


        HoverResult hover =
            FindHoveredEnemyIcon();


        if (hover.enemy == null ||
            !hover.enemy.CombatActive ||
            hover.enemy.IsDead)
        {
            HideTooltip();
            return;
        }


        // -----------------------------------------------------
        // ACTION ICON
        // -----------------------------------------------------

        if (hover.kind ==
            HoverKind.Action)
        {
            EnemyAction action =
                hover.enemy.CurrentAction;


            if (action == null)
            {
                HideTooltip();
                return;
            }


            string tooltip =
                BuildActionTooltip(
                    hover.enemy,
                    action
                );


            if (string.IsNullOrWhiteSpace(
                tooltip))
            {
                HideTooltip();
                return;
            }


            ShowActionTooltip(
                hover.enemy,
                action,
                tooltip
            );

            return;
        }


        // -----------------------------------------------------
        // CURSE ICON
        // -----------------------------------------------------

        if (hover.kind ==
            HoverKind.Curse)
        {
            EnemyCurse curse =
                hover.enemy.CurrentCurse;


            if (curse == null)
            {
                HideTooltip();
                return;
            }


            string tooltip =
                BuildCurseTooltip(
                    hover.enemy,
                    curse
                );


            if (string.IsNullOrWhiteSpace(
                tooltip))
            {
                HideTooltip();
                return;
            }


            ShowCurseTooltip(
                hover.enemy,
                curse,
                tooltip
            );

            return;
        }


        HideTooltip();

#endif
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
    }


    // =========================================================
    // CONFIG
    // =========================================================

    private void ConfigureTooltip()
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

    private enum HoverKind
    {
        None,
        Action,
        Curse
    }


    private struct HoverResult
    {
        public BaseEnemy enemy;
        public HoverKind kind;
    }


    private HoverResult FindHoveredEnemyIcon()
    {
        HoverResult result =
            new HoverResult
            {
                enemy = null,
                kind = HoverKind.None
            };


        Ray ray =
            enemyCamera.ScreenPointToRay(
                Input.mousePosition
            );


        /*
         * EnemyCamera is perspective while Action_Icon / Curse_Icon use
         * Collider2D components in EnemyWorld.
         *
         * The AlwaysOnTop shader only changes rendering. It does not change
         * physics, so the normal 2D colliders remain perfect hover targets.
         */
        RaycastHit2D[] hits =
            Physics2D.GetRayIntersectionAll(
                ray,
                Mathf.Infinity,
                hoverMask
            );


        if (hits == null ||
            hits.Length == 0)
        {
            return result;
        }


        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null ||
                !hit.collider.enabled)
            {
                continue;
            }


            BaseEnemy enemy =
                hit.collider
                    .GetComponentInParent<BaseEnemy>();


            if (enemy == null)
                continue;


            Transform hitTransform =
                hit.collider.transform;


            // -------------------------------------------------
            // ACTION ICON
            // -------------------------------------------------

            if (BelongsToRenderer(
                hitTransform,
                enemy.actionIconRenderer))
            {
                result.enemy =
                    enemy;

                result.kind =
                    HoverKind.Action;

                return result;
            }


            // -------------------------------------------------
            // CURSE ICON
            // -------------------------------------------------

            if (BelongsToRenderer(
                hitTransform,
                enemy.curseIconRenderer))
            {
                result.enemy =
                    enemy;

                result.kind =
                    HoverKind.Curse;

                return result;
            }
        }


        return result;
    }


    private bool BelongsToRenderer(
        Transform hitTransform,
        SpriteRenderer targetRenderer)
    {
        if (hitTransform == null ||
            targetRenderer == null)
        {
            return false;
        }


        Transform targetRoot =
            targetRenderer.transform;


        return
            hitTransform == targetRoot ||
            hitTransform.IsChildOf(
                targetRoot
            );
    }


    // =========================================================
    // CONTENT
    // =========================================================

    private string BuildActionTooltip(
        BaseEnemy enemy,
        EnemyAction action)
    {
        string description =
            action.GetTooltipDescription(
                enemy
            );


        string nameHex =
            ColorUtility.ToHtmlStringRGB(
                actionNameColor
            );


        if (string.IsNullOrWhiteSpace(
            description))
        {
            return
                $"<color=#{nameHex}>" +
                $"{action.ActionName}" +
                "</color>";
        }


        return
            $"<color=#{nameHex}>" +
            $"{action.ActionName}" +
            "</color>\n" +
            description;
    }


    private string BuildCurseTooltip(
        BaseEnemy enemy,
        EnemyCurse curse)
    {
        string description =
            curse.GetTooltipDescription(
                enemy,
                enemy.CurseValue
            );


        string nameHex =
            ColorUtility.ToHtmlStringRGB(
                actionNameColor
            );


        if (string.IsNullOrWhiteSpace(
            description))
        {
            return
                $"<color=#{nameHex}>" +
                $"{curse.CurseName}" +
                "</color>";
        }


        return
            $"<color=#{nameHex}>" +
            $"{curse.CurseName}" +
            "</color>\n" +
            description;
    }


    // =========================================================
    // SHOW / HIDE
    // =========================================================

    private void ShowActionTooltip(
        BaseEnemy enemy,
        EnemyAction action,
        string tooltip)
    {
        EnsureTooltipVisible();


        if (currentEnemy != enemy ||
            currentAction != action ||
            currentCurse != null ||
            currentText != tooltip)
        {
            currentEnemy =
                enemy;

            currentAction =
                action;

            currentCurse =
                null;

            currentText =
                tooltip;


            tooltipText.text =
                tooltip;


            ResizePanelToText();
        }


        UpdateTooltipPosition();
    }


    private void ShowCurseTooltip(
        BaseEnemy enemy,
        EnemyCurse curse,
        string tooltip)
    {
        EnsureTooltipVisible();


        if (currentEnemy != enemy ||
            currentCurse != curse ||
            currentAction != null ||
            currentText != tooltip)
        {
            currentEnemy =
                enemy;

            currentAction =
                null;

            currentCurse =
                curse;

            currentText =
                tooltip;


            tooltipText.text =
                tooltip;


            ResizePanelToText();
        }


        UpdateTooltipPosition();
    }


    private void EnsureTooltipVisible()
    {
        if (!tooltipPanel.gameObject.activeSelf)
        {
            tooltipPanel.gameObject
                .SetActive(true);

            tooltipPanel
                .SetAsLastSibling();
        }
    }


    private void HideTooltip()
    {
        currentEnemy =
            null;

        currentAction =
            null;

        currentCurse =
            null;

        currentText =
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
            tooltipText.GetPreferredValues(
                currentText,
                availableTextWidth,
                0f
            );


        float panelWidth =
            Mathf.Max(
                minPanelWidth,
                Mathf.Min(
                    preferred.x,
                    availableTextWidth
                ) +
                (
                    textPadding.x *
                    2f
                )
            );


        float textWidth =
            Mathf.Max(
                1f,
                panelWidth -
                (
                    textPadding.x *
                    2f
                )
            );


        preferred =
            tooltipText.GetPreferredValues(
                currentText,
                textWidth,
                0f
            );


        float panelHeight =
            preferred.y +
            (
                textPadding.y *
                2f
            );


        tooltipPanel.sizeDelta =
            new Vector2(
                panelWidth,
                panelHeight
            );
    }


    // =========================================================
    // POSITION
    // =========================================================

    private void UpdateTooltipPosition()
    {
        Camera uiCamera =
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;


        Vector2 localPoint;


        if (!RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                canvasRect,
                Input.mousePosition,
                uiCamera,
                out localPoint
            ))
        {
            return;
        }


        Vector2 size =
            tooltipPanel.rect.size;


        /*
         * Prefer the tooltip to the right and slightly above the cursor.
         */
        Vector2 desired =
            localPoint +
            new Vector2(
                cursorOffset +
                size.x * 0.5f,
                cursorOffset +
                size.y * 0.5f
            );


        Rect canvasBounds =
            canvasRect.rect;


        float halfWidth =
            size.x *
            0.5f;

        float halfHeight =
            size.y *
            0.5f;


        desired.x =
            Mathf.Clamp(
                desired.x,
                canvasBounds.xMin +
                halfWidth +
                screenEdgePadding,
                canvasBounds.xMax -
                halfWidth -
                screenEdgePadding
            );


        desired.y =
            Mathf.Clamp(
                desired.y,
                canvasBounds.yMin +
                halfHeight +
                screenEdgePadding,
                canvasBounds.yMax -
                halfHeight -
                screenEdgePadding
            );


        tooltipPanel.anchoredPosition =
            desired;
    }
}

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


        BaseEnemy hoveredEnemy =
            FindHoveredEnemyActionIcon();


        if (hoveredEnemy == null ||
            !hoveredEnemy.CombatActive ||
            hoveredEnemy.IsDead)
        {
            HideTooltip();
            return;
        }


        EnemyAction action =
            hoveredEnemy.CurrentAction;


        if (action == null)
        {
            HideTooltip();
            return;
        }


        string tooltip =
            BuildTooltip(
                hoveredEnemy,
                action
            );


        if (string.IsNullOrWhiteSpace(
            tooltip))
        {
            HideTooltip();
            return;
        }


        ShowTooltip(
            hoveredEnemy,
            action,
            tooltip
        );

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

    private BaseEnemy FindHoveredEnemyActionIcon()
    {
        Ray ray =
            enemyCamera.ScreenPointToRay(
                Input.mousePosition
            );


        /*
         * This is the key for our setup:
         *
         * - EnemyCamera is perspective.
         * - Action_Icon is a SpriteRenderer in EnemyWorld.
         * - Action_Icon uses a shader that ignores the 3D depth buffer.
         * - Hover still uses its normal BoxCollider2D.
         *
         * Physics2D.GetRayIntersectionAll lets a 3D camera ray intersect
         * Collider2D objects at their real Z depth.
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
            return null;
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


            if (enemy == null ||
                enemy.actionIconRenderer == null)
            {
                continue;
            }


            Transform iconRoot =
                enemy.actionIconRenderer
                    .transform;


            Transform hitTransform =
                hit.collider
                    .transform;


            /*
             * Accept only colliders that belong to Action_Icon itself
             * (or one of its children), not arbitrary colliders on the enemy.
             */
            if (hitTransform != iconRoot &&
                !hitTransform.IsChildOf(
                    iconRoot))
            {
                continue;
            }


            return enemy;
        }


        return null;
    }


    // =========================================================
    // CONTENT
    // =========================================================

    private string BuildTooltip(
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


    // =========================================================
    // SHOW / HIDE
    // =========================================================

    private void ShowTooltip(
        BaseEnemy enemy,
        EnemyAction action,
        string tooltip)
    {
        if (!tooltipPanel.gameObject.activeSelf)
        {
            tooltipPanel.gameObject
                .SetActive(true);

            tooltipPanel
                .SetAsLastSibling();
        }


        if (currentEnemy != enemy ||
            currentAction != action ||
            currentText != tooltip)
        {
            currentEnemy =
                enemy;

            currentAction =
                action;

            currentText =
                tooltip;


            tooltipText.text =
                tooltip;


            ResizePanelToText();
        }


        UpdateTooltipPosition();
    }


    private void HideTooltip()
    {
        currentEnemy =
            null;

        currentAction =
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

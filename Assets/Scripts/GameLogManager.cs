using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameLogManager : MonoBehaviour
{
    public static GameLogManager Instance;


    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("Panel References")]

    [Tooltip("RectTransform of the complete LogPanel.")]
    public RectTransform logPanel;

    [Tooltip("ScrollRect of the ScrollView.")]
    public ScrollRect scrollRect;

    [Tooltip("TMP UI containing the complete log history.")]
    public TMP_Text logText;


    [Header("Button")]

    [Tooltip(
        "Collider2D of the world-space button used to open/close the log."
    )]
    public Collider2D logButtonCollider;


    // =========================================================
    // PANEL ANIMATION
    // =========================================================

    [Header("Panel Animation")]

    [Tooltip("If enabled, the panel starts open.")]
    public bool startOpen = false;

    [Tooltip("Duration of the open/close slide animation.")]
    [Min(0.01f)]
    public float slideDuration = 0.2f;

    [Tooltip(
        "Extra distance used to hide the panel above the screen."
    )]
    public float hiddenExtraOffset = 10f;


    // =========================================================
    // LOG SETTINGS
    // =========================================================

    [Header("Log Settings")]

    [Tooltip("Maximum number of lines kept in the log history.")]
    [Min(10)]
    public int maxLogEntries = 250;

    [Tooltip("Adds an empty line between completed spin blocks.")]
    public bool separateSpinBlocks = true;

    [Tooltip("Mouse wheel scroll speed while the pointer is over the log.")]
    [Min(0.01f)]
    public float mouseWheelScrollSpeed = 0.12f;


    // =========================================================
    // COLORS
    // =========================================================

    [Header("Log Colors")]

    [Tooltip("Base color used by normal log text.")]
    public Color normalTextColor = Color.white;

    [Tooltip("Color used by valid-spin headers.")]
    public Color spinHeaderColor = Color.white;

    [Tooltip("Color used for sticker names.")]
    public Color stickerColor = Color.green;

    [Tooltip("Color used for Blood losses.")]
    public Color bloodColor = Color.red;

    [Tooltip("Color used for money gains.")]
    public Color moneyColor = Color.yellow;

    [Tooltip("Color used for enemy names.")]
    public Color enemyColor =
        new Color(
            1f,
            0.5f,
            0.2f
        );

    [Tooltip(
        "Fallback segment color. Real spins use the actual winning segment color."
    )]
    public Color segmentColor = Color.cyan;


    // =========================================================
    // STATE
    // =========================================================

    public bool IsOpen
    {
        get;
        private set;
    }


    public bool IsExpanded
    {
        get;
        private set;
    }


    public bool SpinBlockOpen
    {
        get;
        private set;
    }


    public int ValidSpinCount
    {
        get;
        private set;
    }


    private readonly List<string> logEntries =
        new List<string>();

    /*
     * Events from the currently resolving valid spin live here.
     * They are invisible until CommitSpinBlock().
     */
    private readonly List<string> pendingSpinEntries =
        new List<string>();


    private Camera cam;

    private Vector2 shownPosition;
    private Vector2 hiddenPosition;

    /*
     * The authored RectTransform is always our NORMAL state.
     * Expanded mode is derived from it at runtime.
     *
     * We normalize the panel pivot to its top edge without changing
     * its visible authored position. That makes height changes grow
     * downward instead of drifting up/down depending on the original pivot.
     */
    private Vector2 normalPanelPosition;

    private float normalPanelHeight;
    private float expandedPanelHeight;

    private Coroutine slideRoutine;
    private Coroutine resizeRoutine;
    private Coroutine scrollRoutine;


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


        Instance = this;

        cam = Camera.main;
    }


    private void Start()
    {
        if (logPanel == null)
        {
            Debug.LogError(
                "[GAME LOG] LogPanel reference missing."
            );

            return;
        }


        Canvas.ForceUpdateCanvases();


        /*
         * Keep the current authored rectangle exactly where it is, but
         * normalize its pivot to the TOP edge. From this point on,
         * changing only the height makes the panel grow downward.
         */
        SetTopPivotPreservingVisualRect(
            logPanel
        );


        /*
         * The RectTransform authored in the Editor is the NORMAL state.
         * We keep exactly that size and position as our baseline.
         */
        shownPosition =
            logPanel.anchoredPosition;

        normalPanelPosition =
            shownPosition;

        normalPanelHeight =
            logPanel.rect.height;


        /*
         * Convert the log's responsive children to stretch layouts while
         * preserving their exact CURRENT visible bounds.
         *
         * No Inspector values are needed:
         * - ScrollView stretches with LogPanel
         * - Viewport stretches with ScrollView
         * - VerticalScrollbar stretches with ScrollView
         * - Sliding Area stretches with the scrollbar
         *
         * This is much more robust than manually resizing each child from
         * cached sizeDelta/anchoredPosition values.
         */
        ConfigureResponsiveLogHierarchy();


        /*
         * Expanded mode is derived automatically:
         *
         * - target roughly 2x the current authored height
         * - never extend beyond the available parent/canvas space
         * - preserve the current TOP edge of the panel
         */
        expandedPanelHeight =
            CalculateExpandedPanelHeight();


        hiddenPosition =
            shownPosition +
            Vector2.up *
            (
                normalPanelHeight +
                hiddenExtraOffset
            );


        IsOpen =
            startOpen;

        IsExpanded =
            false;


        /*
         * Always begin at the authored NORMAL height.
         */
        logPanel.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            normalPanelHeight
        );


        logPanel.anchoredPosition =
            IsOpen
                ? normalPanelPosition
                : hiddenPosition;


        if (logText != null)
        {
            logText.text = "";
            logText.color = normalTextColor;
        }


        /*
         * Mouse-wheel scrolling is handled explicitly below.
         * The scrollbar can still be used as a visual position indicator.
         */
        if (scrollRect != null)
        {
            scrollRect.scrollSensitivity = 0f;
        }


        SpinBlockOpen = false;
        ValidSpinCount = 0;


        Debug.Log(
            "[GAME LOG] Initialized."
        );
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        // -----------------------------------------------------
        // MOUSE WHEEL
        // -----------------------------------------------------

        HandleMouseWheelScroll();


        // -----------------------------------------------------
        // LOG BUTTON
        // -----------------------------------------------------

        if (!Input.GetMouseButtonDown(0))
            return;


        if (logButtonCollider == null)
            return;


        if (cam == null)
            cam = Camera.main;


        if (cam == null)
            return;


        Vector2 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );


        if (logButtonCollider
            .OverlapPoint(mouseWorld))
        {
            ToggleLogPanel();
        }

#endif
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    // =========================================================
    // MOUSE WHEEL SCROLL
    // =========================================================

    private void HandleMouseWheelScroll()
    {
        if (!IsOpen)
            return;


        if (logPanel == null ||
            scrollRect == null)
        {
            return;
        }


        bool pointerInsideLog =
            RectTransformUtility
                .RectangleContainsScreenPoint(
                    logPanel,
                    Input.mousePosition,
                    null
                );


        if (!pointerInsideLog)
            return;


        float wheel =
            Input.mouseScrollDelta.y;


        if (Mathf.Abs(wheel) <
            0.01f)
        {
            return;
        }


        scrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(
                scrollRect.verticalNormalizedPosition +
                wheel *
                mouseWheelScrollSpeed
            );
    }


    // =========================================================
    // PANEL
    // =========================================================

    public void ToggleLogPanel()
    {
        /*
         * Two button behaviours:
         *
         * Start Open OFF:
         *     Closed <-> Normal
         *
         * Start Open ON:
         *     Normal <-> Expanded
         *
         * This lets Start Open behave like a permanently available
         * compact log whose button simply gives it more room.
         */
        if (startOpen)
        {
            ToggleExpandedPanel();
            return;
        }


        SetLogPanelOpen(
            !IsOpen
        );
    }


    public void OpenLogPanel()
    {
        SetLogPanelOpen(true);
    }


    public void CloseLogPanel()
    {
        SetLogPanelOpen(false);
    }


    private void SetLogPanelOpen(
        bool open)
    {
        if (logPanel == null)
            return;


        /*
         * The sliding state always uses the NORMAL authored size.
         * Expanded mode is only the alternate state used by the
         * button when Start Open is enabled.
         */
        if (IsExpanded)
        {
            if (resizeRoutine != null)
            {
                StopCoroutine(
                    resizeRoutine
                );

                resizeRoutine = null;
            }


            IsExpanded = false;

            logPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                normalPanelHeight
            );

            logPanel.anchoredPosition =
                normalPanelPosition;

            RefreshLogLayout();
        }


        IsOpen = open;


        if (slideRoutine != null)
        {
            StopCoroutine(
                slideRoutine
            );
        }


        slideRoutine =
            StartCoroutine(
                SlidePanelRoutine(
                    open
                )
            );
    }


    // =========================================================
    // EXPANDED PANEL
    // =========================================================

    private void ToggleExpandedPanel()
    {
        if (logPanel == null)
            return;


        /*
         * Start Open mode should always keep the log visible.
         * If some external call happened to close it, restore the
         * normal visible state before allowing expansion.
         */
        if (!IsOpen)
        {
            IsOpen = true;

            logPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                normalPanelHeight
            );

            logPanel.anchoredPosition =
                normalPanelPosition;

            RefreshLogLayout();
        }


        SetExpandedPanel(
            !IsExpanded
        );
    }


    private void SetExpandedPanel(
        bool expanded)
    {
        if (logPanel == null)
            return;


        IsExpanded = expanded;


        if (resizeRoutine != null)
        {
            StopCoroutine(
                resizeRoutine
            );
        }


        resizeRoutine =
            StartCoroutine(
                ResizePanelRoutine(
                    expanded
                )
            );
    }


    private IEnumerator ResizePanelRoutine(
        bool expanding)
    {
        float preservedScrollPosition =
            scrollRect != null
                ? scrollRect.verticalNormalizedPosition
                : 0f;


        float startHeight =
            logPanel.rect.height;

        float targetHeight =
            expanding
                ? expandedPanelHeight
                : normalPanelHeight;


        float elapsed = 0f;


        while (elapsed <
               slideDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    slideDuration
                );


            float smoothT =
                t *
                t *
                (
                    3f -
                    2f * t
                );


            float height =
                Mathf.Lerp(
                    startHeight,
                    targetHeight,
                    smoothT
                );


            /*
             * Because the panel pivot is normalized to Y = 1,
             * changing only the height keeps the TOP edge perfectly fixed
             * and grows/shrinks the panel downward.
             *
             * The responsive children use stretch anchors, so they follow
             * this size change automatically.
             */
            logPanel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                height
            );


            logPanel.anchoredPosition =
                normalPanelPosition;


            yield return null;
        }


        logPanel.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            targetHeight
        );

        logPanel.anchoredPosition =
            normalPanelPosition;


        RefreshLogLayout();


        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition =
                preservedScrollPosition;
        }


        resizeRoutine = null;
    }


    // =========================================================
    // RESPONSIVE LOG HIERARCHY
    // =========================================================

    private void ConfigureResponsiveLogHierarchy()
    {
        if (scrollRect == null)
            return;


        RectTransform scrollViewRect =
            scrollRect.GetComponent<RectTransform>();


        /*
         * Preserve exactly how every element currently looks, then convert
         * it to a stretch relationship with its parent.
         *
         * This means the Inspector layout remains the visual source of truth,
         * while future parent height changes become automatic.
         */
        MakeRectStretchInsideParentPreservingBounds(
            scrollViewRect
        );


        MakeRectStretchInsideParentPreservingBounds(
            scrollRect.viewport
        );


        if (scrollRect.verticalScrollbar != null)
        {
            RectTransform scrollbarRect =
                scrollRect.verticalScrollbar
                    .GetComponent<RectTransform>();


            MakeRectStretchInsideParentPreservingBounds(
                scrollbarRect
            );


            /*
             * Unity's default Scrollbar hierarchy has:
             *
             * VerticalScrollbar
             * └─ Sliding Area
             *    └─ Handle
             *
             * The Scrollbar component controls Handle itself, but Sliding Area
             * must follow the scrollbar's new height.
             */
            if (scrollRect.verticalScrollbar.handleRect != null)
            {
                RectTransform slidingArea =
                    scrollRect.verticalScrollbar
                        .handleRect.parent
                        as RectTransform;


                MakeRectStretchInsideParentPreservingBounds(
                    slidingArea
                );
            }
        }


        RefreshLogLayout();
    }


    private void MakeRectStretchInsideParentPreservingBounds(
        RectTransform rect)
    {
        if (rect == null)
            return;


        RectTransform parentRect =
            rect.parent as RectTransform;


        if (parentRect == null)
            return;


        /*
         * Capture the exact visible rectangle in the parent's local space.
         */
        Vector3[] corners =
            new Vector3[4];


        rect.GetWorldCorners(
            corners
        );


        Vector3 bottomLeft =
            parentRect.InverseTransformPoint(
                corners[0]
            );


        Vector3 topRight =
            parentRect.InverseTransformPoint(
                corners[2]
            );


        float leftMargin =
            bottomLeft.x -
            parentRect.rect.xMin;


        float rightMargin =
            parentRect.rect.xMax -
            topRight.x;


        float bottomMargin =
            bottomLeft.y -
            parentRect.rect.yMin;


        float topMargin =
            parentRect.rect.yMax -
            topRight.y;


        /*
         * Full stretch, but with the exact current margins restored.
         * Visually this should be indistinguishable at normal size.
         */
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;


        rect.offsetMin =
            new Vector2(
                leftMargin,
                bottomMargin
            );


        rect.offsetMax =
            new Vector2(
                -rightMargin,
                -topMargin
            );
    }


    private void SetTopPivotPreservingVisualRect(
        RectTransform rect)
    {
        if (rect == null)
            return;


        if (Mathf.Approximately(
                rect.pivot.y,
                1f))
        {
            return;
        }


        /*
         * Changing pivot normally moves the visible rectangle.
         * Capture its top-left world corner, change pivot, then move the
         * transform back so that exact corner remains in the same place.
         */
        Vector3[] beforeCorners =
            new Vector3[4];


        rect.GetWorldCorners(
            beforeCorners
        );


        Vector3 originalTopLeft =
            beforeCorners[1];


        rect.pivot =
            new Vector2(
                rect.pivot.x,
                1f
            );


        Vector3[] afterCorners =
            new Vector3[4];


        rect.GetWorldCorners(
            afterCorners
        );


        Vector3 correctedOffset =
            originalTopLeft -
            afterCorners[1];


        rect.position +=
            correctedOffset;
    }


    private void RefreshLogLayout()
    {
        Canvas.ForceUpdateCanvases();


        if (logPanel != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                logPanel
            );
        }


        Canvas.ForceUpdateCanvases();
    }


    // =========================================================
    // EXPANDED SIZE CALCULATION
    // =========================================================

    private float CalculateExpandedPanelHeight()
    {
        float desiredHeight =
            normalPanelHeight *
            2f;


        RectTransform parentRect =
            logPanel.parent as RectTransform;


        if (parentRect == null)
        {
            return desiredHeight;
        }


        /*
         * Work out how much vertical space exists below the
         * panel's current top edge inside its parent RectTransform.
         */
        Vector3[] panelCorners =
            new Vector3[4];

        logPanel.GetWorldCorners(
            panelCorners
        );


        Vector3 panelTopLeftInParent =
            parentRect.InverseTransformPoint(
                panelCorners[1]
            );


        float availableHeight =
            panelTopLeftInParent.y -
            parentRect.rect.yMin -
            Mathf.Max(
                0f,
                hiddenExtraOffset
            );


        /*
         * Never make "expanded" smaller than the authored state.
         */
        availableHeight =
            Mathf.Max(
                normalPanelHeight,
                availableHeight
            );


        return
            Mathf.Min(
                desiredHeight,
                availableHeight
            );
    }


    // =========================================================
    // SLIDE ANIMATION
    // =========================================================

    private IEnumerator SlidePanelRoutine(
        bool opening)
    {
        Vector2 startPosition =
            logPanel.anchoredPosition;


        Vector2 targetPosition =
            opening
                ? shownPosition
                : hiddenPosition;


        float elapsed = 0f;


        while (elapsed <
               slideDuration)
        {
            elapsed +=
                Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    elapsed /
                    slideDuration
                );


            float smoothT =
                t *
                t *
                (
                    3f -
                    2f * t
                );


            logPanel.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    smoothT
                );


            yield return null;
        }


        logPanel.anchoredPosition =
            targetPosition;


        slideRoutine = null;
    }


    // =========================================================
    // VALID SPIN BLOCK
    // =========================================================

    /// <summary>
    /// Opens a temporary block for a VALID spin.
    /// Nothing is shown in the visible log until CommitSpinBlock().
    /// </summary>
    public void BeginValidSpinBlock(
        string methodLabel,
        float power01)
    {
        /*
         * Defensive cleanup in case a previous development-time
         * spin somehow left a block open.
         */
        pendingSpinEntries.Clear();

        SpinBlockOpen = true;

        ValidSpinCount++;


        int powerPercent =
            Mathf.RoundToInt(
                Mathf.Clamp01(power01) *
                100f
            );


        string header =
            $"VALID SPIN #{ValidSpinCount}" +
            $" — {methodLabel}" +
            $" — POWER {powerPercent}%";


        AddPendingSpinLine(
            SpinHeaderText(
                header
            )
        );
    }


    /// <summary>
    /// Publishes the entire completed spin at once.
    /// </summary>
    public void CommitSpinBlock()
    {
        if (!SpinBlockOpen)
            return;


        if (pendingSpinEntries.Count == 0)
        {
            SpinBlockOpen = false;
            return;
        }


        if (separateSpinBlocks &&
            logEntries.Count > 0)
        {
            logEntries.Add("");
        }


        logEntries.AddRange(
            pendingSpinEntries
        );


        pendingSpinEntries.Clear();

        SpinBlockOpen = false;


        TrimHistory();
        RefreshLogText();
        ScrollToBottom();
    }


    /// <summary>
    /// Throws away the current unresolved block.
    /// Useful for invalid/cancelled spins if needed later.
    /// </summary>
    public void DiscardSpinBlock()
    {
        pendingSpinEntries.Clear();
        SpinBlockOpen = false;
    }


    // =========================================================
    // REAL SPIN EVENTS
    // =========================================================

    public void LogManualBrake(
        int bloodSpent)
    {
        if (bloodSpent <= 0)
            return;


        AddGameplayLine(
            "Manual brake: " +
            BloodText(
                $"-{bloodSpent} Blood"
            )
        );
    }


    public void LogFlagPinMoney(
        int moneyGained)
    {
        if (moneyGained <= 0)
            return;


        AddGameplayLine(
            "Flag Pin: " +
            MoneyText(
                $"+${moneyGained}"
            )
        );
    }


    public void LogWinningSegment(
        int segmentNumber,
        Color actualSegmentColor)
    {
        AddGameplayLine(
            "Winning segment: " +
            SegmentText(
                segmentNumber.ToString(),
                actualSegmentColor
            )
        );
    }


    public void LogWinningSegment(
        int segmentNumber)
    {
        AddGameplayLine(
            "Winning segment: " +
            SegmentText(
                segmentNumber.ToString()
            )
        );
    }


    // =========================================================
    // STICKER EVENTS
    // =========================================================

    /// <summary>
    /// Logs a sticker activation using the standard gameplay colors.
    ///
    /// The sticker name is colored with stickerColor.
    /// Positive money rewards are colored with moneyColor.
    /// effectDescription may already contain Rich Text tags.
    /// </summary>
    public void LogStickerActivation(
        string stickerName,
        string effectDescription,
        int moneyGained = 0)
    {
        string safeName =
            string.IsNullOrWhiteSpace(stickerName)
                ? "Unnamed Sticker"
                : stickerName;


        string line =
            StickerText(safeName) +
            " activates";


        bool hasDescription =
            !string.IsNullOrWhiteSpace(
                effectDescription
            );


        bool hasMoney =
            moneyGained != 0;


        if (hasDescription ||
            hasMoney)
        {
            line += ": ";
        }


        if (hasDescription)
        {
            line +=
                effectDescription.Trim();
        }


        if (hasMoney)
        {
            if (hasDescription)
                line += " ";


            string moneyLabel =
                moneyGained > 0
                    ? $"+${moneyGained}"
                    : $"-${Mathf.Abs(moneyGained)}";


            line +=
                MoneyText(
                    moneyLabel
                );
        }


        AddGameplayLine(
            line
        );
    }


    // =========================================================
    // ENEMY EVENTS
    // =========================================================

    /// <summary>
    /// Logs an enemy attack against the player.
    /// The enemy name and Blood loss use their configured log colors.
    /// </summary>
    public void LogEnemyAttack(
        string enemyName,
        int bloodLost)
    {
        string safeName =
            string.IsNullOrWhiteSpace(enemyName)
                ? "Enemy"
                : enemyName;


        string line =
            EnemyText(safeName) +
            " attacks";


        if (bloodLost > 0)
        {
            line +=
                ": " +
                BloodText(
                    $"-{bloodLost} Blood"
                );
        }


        AddGameplayLine(
            line
        );
    }


    /// <summary>
    /// Logs an enemy death at the exact moment Die() is resolved.
    /// </summary>
    public void LogEnemyDeath(
        string enemyName)
    {
        string safeName =
            string.IsNullOrWhiteSpace(enemyName)
                ? "Enemy"
                : enemyName;


        AddGameplayLine(
            EnemyText(safeName) +
            " dies"
        );
    }


    // =========================================================
    // GENERIC GAMEPLAY LINE
    // =========================================================

    /// <summary>
    /// Future stickers/enemies will use this route.
    /// During a resolving spin the line is buffered.
    /// Outside a spin it is written directly to the history.
    /// </summary>
    public void AddGameplayLine(
        string richTextLine)
    {
        if (string.IsNullOrEmpty(
            richTextLine))
        {
            return;
        }


        if (SpinBlockOpen)
        {
            AddPendingSpinLine(
                richTextLine
            );
        }
        else
        {
            AddLine(
                richTextLine
            );
        }
    }


    private void AddPendingSpinLine(
        string richTextLine)
    {
        if (string.IsNullOrEmpty(
            richTextLine))
        {
            return;
        }


        pendingSpinEntries.Add(
            richTextLine
        );
    }


    // =========================================================
    // ADD DIRECT LOG ENTRY
    // =========================================================

    public void AddLine(
        string richTextLine)
    {
        if (string.IsNullOrEmpty(
            richTextLine))
        {
            return;
        }


        logEntries.Add(
            richTextLine
        );


        TrimHistory();
        RefreshLogText();
        ScrollToBottom();
    }


    public void AddEmptyLine()
    {
        logEntries.Add("");


        TrimHistory();
        RefreshLogText();
        ScrollToBottom();
    }


    // =========================================================
    // CLEAR LOG
    // =========================================================

    public void ClearLog()
    {
        logEntries.Clear();
        pendingSpinEntries.Clear();
        SpinBlockOpen = false;


        /*
         * We deliberately do NOT reset ValidSpinCount here.
         * Clearing the visual history should not alter gameplay numbering.
         */
        RefreshLogText();
        ScrollToBottom();
    }


    // =========================================================
    // HISTORY LIMIT
    // =========================================================

    private void TrimHistory()
    {
        int limit =
            Mathf.Max(
                10,
                maxLogEntries
            );


        while (logEntries.Count >
               limit)
        {
            logEntries.RemoveAt(0);
        }
    }


    // =========================================================
    // REFRESH TMP
    // =========================================================

    private void RefreshLogText()
    {
        if (logText == null)
            return;


        logText.text =
            string.Join(
                "\n",
                logEntries
            );


        logText.color =
            normalTextColor;
    }


    // =========================================================
    // AUTO SCROLL
    // =========================================================

    private void ScrollToBottom()
    {
        if (scrollRect == null)
            return;


        if (scrollRoutine != null)
        {
            StopCoroutine(
                scrollRoutine
            );
        }


        scrollRoutine =
            StartCoroutine(
                ScrollToBottomRoutine()
            );
    }


    private IEnumerator ScrollToBottomRoutine()
    {
        yield return null;


        Canvas.ForceUpdateCanvases();


        scrollRect.verticalNormalizedPosition =
            0f;


        scrollRoutine = null;
    }


    // =========================================================
    // COLOR HELPERS
    // =========================================================

    public string StickerText(
        string text)
    {
        return ColorText(
            text,
            stickerColor
        );
    }


    public string BloodText(
        string text)
    {
        return ColorText(
            text,
            bloodColor
        );
    }


    public string MoneyText(
        string text)
    {
        return ColorText(
            text,
            moneyColor
        );
    }


    public string EnemyText(
        string text)
    {
        return ColorText(
            text,
            enemyColor
        );
    }


    public string SegmentText(
        string text,
        Color actualSegmentColor)
    {
        return ColorText(
            text,
            actualSegmentColor
        );
    }


    public string SegmentText(
        string text)
    {
        return ColorText(
            text,
            segmentColor
        );
    }


    public string SpinHeaderText(
        string text)
    {
        return ColorText(
            text,
            spinHeaderColor
        );
    }


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
            $"{text}" +
            "</color>";
    }


    // =========================================================
    // DEBUG TEST BLOCK
    // =========================================================

    [ContextMenu("DEBUG - Add Test Spin")]
    private void DebugAddTestSpin()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[GAME LOG] Enter Play Mode first."
            );

            return;
        }


        BeginValidSpinBlock(
            "MANUAL",
            0.78f
        );


        LogManualBrake(2);


        LogWinningSegment(3);


        AddGameplayLine(
            StickerText(
                "Magic Bean"
            ) +
            " activates: " +
            MoneyText(
                "+$5"
            )
        );


        AddGameplayLine(
            StickerText(
                "Sword"
            ) +
            " activates: Deal 5 damage"
        );


        AddGameplayLine(
            EnemyText(
                "Imp"
            ) +
            " dies"
        );


        AddGameplayLine(
            EnemyText(
                "Demon"
            ) +
            " attacks: " +
            BloodText(
                "-3 Blood"
            )
        );


        CommitSpinBlock();
    }


    // =========================================================
    // DEBUG TOGGLE
    // =========================================================

    [ContextMenu("DEBUG - Toggle Panel")]
    private void DebugTogglePanel()
    {
        if (!Application.isPlaying)
            return;


        ToggleLogPanel();
    }


    // =========================================================
    // DEBUG CLEAR
    // =========================================================

    [ContextMenu("DEBUG - Clear Log")]
    private void DebugClearLog()
    {
        if (!Application.isPlaying)
            return;


        ClearLog();
    }
}

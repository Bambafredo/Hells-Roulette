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

    private Coroutine slideRoutine;
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
         * The position authored in the Editor is the OPEN position.
         */
        shownPosition =
            logPanel.anchoredPosition;


        hiddenPosition =
            shownPosition +
            Vector2.up *
            (
                logPanel.rect.height +
                hiddenExtraOffset
            );


        IsOpen =
            startOpen;


        logPanel.anchoredPosition =
            IsOpen
                ? shownPosition
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

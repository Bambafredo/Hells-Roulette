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

    [Tooltip("RectTransform del LogPanel completo.")]
    public RectTransform logPanel;

    [Tooltip("ScrollRect del ScrollView.")]
    public ScrollRect scrollRect;

    [Tooltip("TMP UI que contiene todo el historial.")]
    public TMP_Text logText;


    [Header("Button")]

    [Tooltip(
        "Collider2D del botón world-space que abre/cierra el log."
    )]
    public Collider2D logButtonCollider;


    // =========================================================
    // PANEL ANIMATION
    // =========================================================

    [Header("Panel Animation")]

    [Tooltip(
        "Si está activado, el panel comienza abierto."
    )]
    public bool startOpen = false;

    [Tooltip(
        "Duración de la animación de abrir/cerrar."
    )]
    [Min(0.01f)]
    public float slideDuration = 0.2f;

    [Tooltip(
        "Margen adicional que desplazamos el panel " +
        "por encima de la pantalla al cerrarlo."
    )]
    public float hiddenExtraOffset = 10f;


    // =========================================================
    // LOG SETTINGS
    // =========================================================

    [Header("Log Settings")]

    [Tooltip(
        "Número máximo de líneas guardadas en el historial."
    )]
    [Min(10)]
    public int maxLogEntries = 250;

    [Tooltip(
        "Añade una línea vacía entre bloques de tiradas."
    )]
    public bool separateSpinBlocks = true;

    [Tooltip(
        "Velocidad del scroll con la rueda del ratón."
    )]
    [Min(0.01f)]
    public float mouseWheelScrollSpeed = 0.12f;


    // =========================================================
    // COLORS
    // =========================================================

    [Header("Log Colors")]

    [Tooltip("Color del texto normal.")]
    public Color normalTextColor = Color.white;

    [Tooltip("Color del encabezado de una tirada.")]
    public Color spinHeaderColor = Color.white;

    [Tooltip("Color utilizado para nombres de stickers.")]
    public Color stickerColor = Color.green;

    [Tooltip("Color utilizado para Blood perdida.")]
    public Color bloodColor = Color.red;

    [Tooltip("Color utilizado para dinero ganado.")]
    public Color moneyColor = Color.yellow;

    [Tooltip("Color utilizado para nombres de enemigos.")]
    public Color enemyColor =
        new Color(
            1f,
            0.5f,
            0.2f
        );

    [Tooltip(
        "Color fallback para segmentos. " +
        "Los eventos reales utilizarán el color real del segmento."
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


    private readonly List<string> logEntries =
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


        /*
         * Forzamos a Unity a calcular el layout antes
         * de leer la altura real del panel.
         */
        Canvas.ForceUpdateCanvases();


        /*
         * La posición colocada manualmente en Editor
         * es nuestra posición ABIERTA.
         */
        shownPosition =
            logPanel.anchoredPosition;


        /*
         * Cerrado = panel completo desplazado hacia arriba.
         */
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

            logText.color =
                normalTextColor;
        }


        /*
         * Nosotros controlamos explícitamente
         * la rueda del ratón.
         *
         * Esto evita que ScrollRect + GameLogManager
         * hagan scroll simultáneamente.
         *
         * Arrastrar scrollbar / handle sigue funcionando.
         */
        if (scrollRect != null)
        {
            scrollRect.scrollSensitivity = 0f;
        }


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


        /*
         * Solo hacemos scroll si el cursor está
         * realmente encima de la ventana del log.
         */
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


        /*
         * verticalNormalizedPosition:
         *
         * 1 = arriba
         * 0 = abajo
         *
         * Wheel positivo = subir.
         */
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


        IsOpen =
            open;


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


            /*
             * SmoothStep.
             */
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
    // ADD LOG ENTRY
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


    // =========================================================
    // ADD EMPTY LINE
    // =========================================================

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


        /*
         * Color base.
         *
         * Los tags Rich Text sustituyen el color
         * solamente en fragmentos concretos.
         */
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


        /*
         * Esperamos a que TMP y ContentSizeFitter
         * recalculen el Content.
         */
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


    // =========================================================
    // SEGMENT COLOR
    // =========================================================

    /*
     * ESTE será el método utilizado por la ruleta real.
     *
     * El número/nombre del segmento utiliza directamente
     * el color visual real del segmento ganador.
     */
    public string SegmentText(
        string text,
        Color actualSegmentColor)
    {
        return ColorText(
            text,
            actualSegmentColor
        );
    }


    /*
     * Fallback para debug o casos en los que
     * no tengamos un color real.
     */
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


        if (separateSpinBlocks &&
            logEntries.Count > 0)
        {
            AddEmptyLine();
        }


        AddLine(
            SpinHeaderText(
                "VALID SPIN #4 — POWER 78%"
            )
        );


        AddLine(
            "Manual brake: " +
            BloodText(
                "-2 Blood"
            )
        );


        /*
         * En el test usamos segmentColor fallback.
         *
         * Cuando conectemos RouletteController aquí
         * llegará el color REAL del segmento.
         */
        AddLine(
            "Winning segment: " +
            SegmentText(
                "3"
            )
        );


        AddLine(
            StickerText(
                "Magic Bean"
            ) +
            " activates: " +
            MoneyText(
                "+$5"
            )
        );


        AddLine(
            StickerText(
                "Sword"
            ) +
            " activates: Deal 5 damage"
        );


        AddLine(
            EnemyText(
                "Imp"
            ) +
            " dies"
        );


        AddLine(
            EnemyText(
                "Demon"
            ) +
            " attacks: " +
            BloodText(
                "-3 Blood"
            )
        );
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

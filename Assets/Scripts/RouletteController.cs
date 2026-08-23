using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RouletteController : MonoBehaviour
{
    // =========================================================
    // SPIN METHOD
    // =========================================================

    public enum SpinMethod
    {
        Manual,
        Power
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    public Transform wheel;
    public WheelGenerator generator;
    public Transform flapperTip;

    // =========================================================
    // FEEL
    // =========================================================

    [Header("Feel")]
    public float gestureToSpin = 1.0f;
    public float deceleration = 220f;

    [Range(0f, 1f)]
    public float velocitySmoothing = 0.25f;

    public float minDragRadius = 0.3f;
    public float maxSpinSpeed = 2000f;
    public float minThrowSpeed = 100f;

    [Range(0.5f, 5f)]
    public float wheelWeight = 1.0f;

    public int velocitySamples = 5;

    // =========================================================
    // POWER SPIN
    // =========================================================

    [Header("Power Spin")]

    [Tooltip(
        "Velocidad máxima que puede alcanzar una tirada realizada " +
        "con el interruptor al 100% de potencia."
    )]
    public float powerSpinMaxSpeed = 2000f;

    [Tooltip(
        "Dirección de las tiradas automáticas. " +
        "En Unity, rotación Z negativa = horario."
    )]
    public bool powerSpinClockwise = true;

    // =========================================================
    // INPUT
    // =========================================================

    [Header("Pin Interaction")]
    public LayerMask blockInputMask;
    public bool inputBlocked = false;

    // =========================================================
    // FAKE SPIN PREVENTION
    // =========================================================

    [Header("Anti-Fake Spin")]
    public float minDragDistanceToAllowSpin = 15f;

    private float accumulatedDragDistance = 0f;

    // =========================================================
    // BRAKE
    // =========================================================

    [Header("Brake System")]
    public bool enableBrake = true;
    public float extraDeceleration = 300f;
    public int bloodCostPerSecond = 1;

    private bool isBraking = false;
    private float bloodTimer = 0f;

    // Blood actually spent by manual braking during the current spin.
    // Reset on every real spin start and written to the gameplay log
    // only if the spin is later validated.
    private int brakeBloodSpentThisSpin = 0;

    // =========================================================
    // INTERNAL SPIN STATE
    // =========================================================

    private bool dragging = false;

    private float lastAngleDeg = 0f;
    private float spinSpeed = 0f;
    private float lastSampleTime = 0f;

    private bool wasMoving = false;

    private Queue<float> recentSpeeds =
        new Queue<float>();

    private int startSegmentIndex = -1;
    private int endSegmentIndex = -1;

    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool SpinInProgress { get; private set; } = false;

    /// <summary>
    /// Método utilizado para iniciar la tirada actual / última tirada.
    ///
    /// Esto nos permitirá posteriormente hacer cosas como:
    ///
    /// Manual spin → bonus
    /// Power spin  → comportamiento normal
    ///
    /// sin deducirlo de la velocidad o del input.
    /// </summary>
    public SpinMethod CurrentSpinMethod
    {
        get;
        private set;
    } = SpinMethod.Manual;

    /// <summary>
    /// Potencia visual que debe mostrar la barra.
    ///
    /// Durante un drag manual:
    ///     potencia estimada en tiempo real.
    ///
    /// Después de lanzar:
    ///     potencia final de la tirada.
    ///
    /// Rango 0 - 1.
    /// </summary>
    public float DisplayPower01
    {
        get;
        private set;
    } = 0f;

    /// <summary>
    /// Potencia con la que comenzó la tirada actual / última tirada.
    /// </summary>
    public float LastLaunchPower01
    {
        get;
        private set;
    } = 0f;

    /// <summary>
    /// Nos permite saber si el jugador está manipulando
    /// directamente la rueda.
    /// </summary>
    public bool IsDraggingWheel
    {
        get { return dragging; }
    }

    public event Action OnSpinStart;

    /*
     * IMPORTANTE:
     *
     * OnSpinEnd significa:
     *
     * "La tirada física ha terminado y los stickers
     * ganadores ya se han resuelto."
     *
     * Los enemigos escuchan este evento, por lo que
     * atacarán DESPUÉS de los stickers.
     */
    public event Action OnSpinEnd;

    public static RouletteController Instance;

    // =========================================================
    // INPUT BLOCK
    // =========================================================

    public void SetInputBlocked(bool v)
    {
        inputBlocked = v;
    }

    // =========================================================
    // UNITY
    // =========================================================

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        HandlePointer();
        ApplySpin();
    }

    // =========================================================
    // CAN START SPIN
    // =========================================================

    /// <summary>
    /// Comprobación común para inputs externos.
    ///
    /// El futuro PowerSpinController utilizará esto
    /// antes de comenzar a cargar.
    /// </summary>
    public bool CanStartNewSpin()
    {
        if (SpinInProgress)
            return false;

        if (dragging)
            return false;

        if (StickerPlacementValidator.Instance != null &&
            StickerPlacementValidator.Instance.InputBlocked)
        {
            return false;
        }

        if (RoundManager.Instance != null &&
            !RoundManager.Instance.CanStartSpin)
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // POINTER
    // =========================================================

    void HandlePointer()
    {
        // -----------------------------------------------------
        // PLACEMENT LOCK
        // -----------------------------------------------------

        if (StickerPlacementValidator.Instance != null &&
            StickerPlacementValidator.Instance.InputBlocked)
        {
            return;
        }

        // -----------------------------------------------------
        // GENERIC INPUT LOCK
        // -----------------------------------------------------

        if (inputBlocked)
            return;

        Vector2 pos;
        bool down;
        bool held;
        bool up;

        ReadPointer(
            out pos,
            out down,
            out held,
            out up
        );

        // -----------------------------------------------------
        // CANVAS UI
        // -----------------------------------------------------

        #if UNITY_EDITOR || UNITY_STANDALONE

        /*
        * Un click que empieza sobre UI no puede comenzar
        * una interacción manual con la ruleta.
        *
        * Esto permite utilizar ScrollViews, scrollbars,
        * botones y futuras utilities sin que el mismo click
        * se interprete también como un spin.
        */
        if (down &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        #endif

        // -----------------------------------------------------
        // BAG PORTALS
        // -----------------------------------------------------

        if (BagManager.Instance != null)
        {
            if (BagManager.Instance.IsPointOnBagPortal(pos) ||
                BagManager.Instance.IsPointOnGameplayPortal(pos))
            {
                return;
            }
        }

        // -----------------------------------------------------
        // SPIN ALREADY IN PROGRESS
        // -----------------------------------------------------

        if (SpinInProgress)
        {
#if UNITY_EDITOR || UNITY_STANDALONE

            if (Input.GetMouseButton(0))
            {
                Vector2 mouseWorld =
                    Camera.main.ScreenToWorldPoint(
                        Input.mousePosition
                    );

                Collider2D col =
                    wheel.GetComponent<Collider2D>();

                if (col != null &&
                    col.OverlapPoint(mouseWorld))
                {
                    isBraking = true;
                }
                else
                {
                    isBraking = false;
                }
            }
            else
            {
                isBraking = false;
            }

#endif

            return;
        }

        // -----------------------------------------------------
        // POINTER DOWN
        // -----------------------------------------------------

        if (down)
        {
            if (RoundManager.Instance != null &&
                !RoundManager.Instance.CanStartSpin)
            {
                Debug.Log(
                    "[ROULETTE] Spin blocked: no spin is currently available."
                );

                return;
            }

            Vector2 world =
                Camera.main.ScreenToWorldPoint(
                    Input.mousePosition
                );

            if (Physics2D.OverlapPoint(
                    world,
                    blockInputMask))
            {
                return;
            }

            if (Vector2.Distance(
                    pos,
                    (Vector2)wheel.position)
                >= minDragRadius)
            {
                dragging = true;

                lastAngleDeg =
                    WorldAngleFromCenter(pos);

                lastSampleTime =
                    Time.time;

                recentSpeeds.Clear();

                accumulatedDragDistance =
                    0f;

                // Nueva tirada manual: empezamos desde 0.
                DisplayPower01 =
                    0f;
            }
        }

        // -----------------------------------------------------
        // DRAGGING
        // -----------------------------------------------------

        if (held && dragging)
        {
            float currentAngle =
                WorldAngleFromCenter(pos);

            float delta =
                Mathf.DeltaAngle(
                    lastAngleDeg,
                    currentAngle
                );

            // Rotación libre.
            wheel.Rotate(
                0f,
                0f,
                delta
            );

            // Anti-fake spin.
            accumulatedDragDistance +=
                Mathf.Abs(delta);

            // Velocity sampling.
            float dt =
                Mathf.Max(
                    0.0001f,
                    Time.time -
                    lastSampleTime
                );

            float instVel =
                (delta / dt) *
                gestureToSpin;

            recentSpeeds.Enqueue(
                instVel
            );

            if (recentSpeeds.Count >
                velocitySamples)
            {
                recentSpeeds.Dequeue();
            }

            /*
             * NUEVO:
             *
             * Calculamos en tiempo real la potencia que tendría
             * la tirada si soltásemos la rueda ahora mismo.
             *
             * La barra podrá leer directamente DisplayPower01.
             */
            float previewSpeed =
                GetAverageRecentSpeed() /
                wheelWeight;

            DisplayPower01 =
                Mathf.Clamp01(
                    Mathf.Abs(previewSpeed) /
                    Mathf.Max(0.01f, maxSpinSpeed)
                );

            lastAngleDeg =
                currentAngle;

            lastSampleTime =
                Time.time;

            return;
        }

        // -----------------------------------------------------
        // RELEASE
        // -----------------------------------------------------

        if (up && dragging)
        {
            dragging = false;

            // -------------------------------------------------
            // ANTI-FAKE SPIN
            // -------------------------------------------------

            if (accumulatedDragDistance <
                minDragDistanceToAllowSpin)
            {
                accumulatedDragDistance =
                    0f;

                recentSpeeds.Clear();

                spinSpeed =
                    0f;

                DisplayPower01 =
                    0f;

                return;
            }

            accumulatedDragDistance =
                0f;

            // -------------------------------------------------
            // TRUE SPIN
            // -------------------------------------------------

            float avgSpeed =
                GetAverageRecentSpeed();

            float weightedSpeed =
                avgSpeed /
                wheelWeight;

            /*
             * La potencia manual representa la velocidad
             * efectiva después de aplicar wheelWeight.
             */
            float manualPower01 =
                Mathf.Clamp01(
                    Mathf.Abs(weightedSpeed) /
                    Mathf.Max(0.01f, maxSpinSpeed)
                );

            if (Mathf.Abs(weightedSpeed) >
                minThrowSpeed)
            {
                bool started =
                    TryBeginSpin(
                        weightedSpeed,
                        SpinMethod.Manual,
                        manualPower01
                    );

                if (!started)
                {
                    spinSpeed =
                        0f;

                    DisplayPower01 =
                        0f;
                }
            }
            else
            {
                /*
                 * Conservamos el comportamiento anterior.
                 */
                spinSpeed *=
                    0.5f;

                DisplayPower01 =
                    0f;
            }

            recentSpeeds.Clear();
        }
    }

    // =========================================================
    // POWER SPIN PUBLIC API
    // =========================================================

    /// <summary>
    /// Inicia una tirada automática con una potencia 0 - 1.
    ///
    /// El futuro interruptor llamará a este método
    /// cuando el jugador suelte el botón.
    ///
    /// Por ahora:
    ///
    /// power 0   → velocidad 0
    /// power 1   → powerSpinMaxSpeed
    ///
    /// Si la velocidad no alcanza minThrowSpeed,
    /// no se inicia una tirada real.
    /// </summary>
    public bool TryStartPowerSpin(float power01)
    {
        if (!CanStartNewSpin())
            return false;

        float normalizedPower =
            Mathf.Clamp01(power01);

        float allowedMaximum =
            Mathf.Min(
                powerSpinMaxSpeed,
                maxSpinSpeed
            );

        float requestedSpeed =
            normalizedPower *
            allowedMaximum;

        if (requestedSpeed <=
            minThrowSpeed)
        {
            return false;
        }

        float direction =
            powerSpinClockwise
                ? -1f
                : 1f;

        requestedSpeed *=
            direction;

        return
            TryBeginSpin(
                requestedSpeed,
                SpinMethod.Power,
                normalizedPower
            );
    }

    // =========================================================
    // COMMON SPIN START
    // =========================================================

    /// <summary>
    /// ÚNICA puerta de entrada para comenzar una tirada real.
    ///
    /// Tanto la tirada manual como la Power Spin terminan aquí.
    ///
    /// De esta forma no duplicamos:
    ///
    /// - RoundManager.NotifySpinStart
    /// - OnSpinStart
    /// - token/debt preparation
    /// - state initialization
    ///
    /// y futuros costes podrán añadirse antes de llegar aquí.
    /// </summary>
    private bool TryBeginSpin(
        float requestedSpeed,
        SpinMethod method,
        float displayPower01)
    {
        if (SpinInProgress)
            return false;

        if (RoundManager.Instance != null &&
            !RoundManager.Instance.CanStartSpin)
        {
            return false;
        }

        float clampedSpeed =
            Mathf.Clamp(
                requestedSpeed,
                -maxSpinSpeed,
                maxSpinSpeed
            );

        if (Mathf.Abs(clampedSpeed) <=
            minThrowSpeed)
        {
            return false;
        }

        spinSpeed =
            clampedSpeed;

        CurrentSpinMethod =
            method;

        LastLaunchPower01 =
            Mathf.Clamp01(
                displayPower01
            );

        DisplayPower01 =
            LastLaunchPower01;

        startSegmentIndex =
            GetCurrentSegmentIndex();

        SpinInProgress =
            true;

        isBraking =
            false;

        bloodTimer =
            0f;

        brakeBloodSpentThisSpin =
            0;

        /*
         * Primero RoundManager.
         *
         * Así CurrencyManager.BeginSpin() y el estado
         * de validación están preparados antes de que
         * cualquier listener procese OnSpinStart.
         */
        RoundManager.Instance?
            .NotifySpinStart();

        OnSpinStart?
            .Invoke();

        return true;
    }

    // =========================================================
    // VELOCITY SAMPLING
    // =========================================================

    private float GetAverageRecentSpeed()
    {
        if (recentSpeeds == null ||
            recentSpeeds.Count == 0)
        {
            return 0f;
        }

        float total =
            0f;

        foreach (float v in recentSpeeds)
        {
            total += v;
        }

        return
            total /
            recentSpeeds.Count;
    }

    // =========================================================
    // POINTER ABSTRACTION
    // =========================================================

    void ReadPointer(
        out Vector2 worldPos,
        out bool down,
        out bool held,
        out bool up)
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        down =
            Input.GetMouseButtonDown(0);

        held =
            Input.GetMouseButton(0);

        up =
            Input.GetMouseButtonUp(0);

        worldPos =
            Camera.main.ScreenToWorldPoint(
                Input.mousePosition
            );

#else

        down = false;
        held = false;
        up = false;

        worldPos =
            Vector2.zero;

        if (Input.touchCount > 0)
        {
            var t =
                Input.GetTouch(0);

            worldPos =
                Camera.main.ScreenToWorldPoint(
                    t.position
                );

            down =
                t.phase ==
                TouchPhase.Began;

            held =
                t.phase ==
                    TouchPhase.Moved ||
                t.phase ==
                    TouchPhase.Stationary;

            up =
                t.phase ==
                    TouchPhase.Ended ||
                t.phase ==
                    TouchPhase.Canceled;
        }

#endif
    }

    // =========================================================
    // ANGLE
    // =========================================================

    float WorldAngleFromCenter(
        Vector2 worldPos)
    {
        Vector2 dir =
            worldPos -
            (Vector2)wheel.position;

        return
            Mathf.Atan2(
                dir.y,
                dir.x
            ) *
            Mathf.Rad2Deg;
    }

    // =========================================================
    // SPIN PHYSICS
    // =========================================================

    void ApplySpin()
    {
        // -----------------------------------------------------
        // ROTATING
        // -----------------------------------------------------

        if (!dragging &&
            Mathf.Abs(spinSpeed) > 0.1f)
        {
            wheel.Rotate(
                0f,
                0f,
                spinSpeed *
                Time.deltaTime
            );

            float adjustedDecel =
                deceleration /
                wheelWeight;

            // -------------------------------------------------
            // BRAKE
            // -------------------------------------------------

            if (enableBrake &&
                isBraking &&
                BloodManager.Instance != null)
            {
                bloodTimer +=
                    Time.deltaTime;

                bool hasBlood =
                    BloodManager.Instance.currentBlood >
                    0;

                if (hasBlood)
                {
                    float interval =
                        1f /
                        bloodCostPerSecond;

                    if (bloodTimer >=
                        interval)
                    {
                        bool canBrake =
                            BloodManager.Instance
                                .ConsumeBlood(1);

                        bloodTimer =
                            0f;

                        if (!canBrake)
                        {
                            isBraking = false;
                        }
                        else
                        {
                            brakeBloodSpentThisSpin++;
                        }
                    }

                    adjustedDecel +=
                        extraDeceleration;
                }
                else
                {
                    isBraking =
                        false;
                }
            }

            // -------------------------------------------------
            // DECELERATION
            // -------------------------------------------------

            float sign =
                Mathf.Sign(spinSpeed);

            spinSpeed -=
                sign *
                adjustedDecel *
                Time.deltaTime;

            if (Mathf.Sign(spinSpeed) !=
                sign)
            {
                spinSpeed =
                    0f;
            }

            wasMoving =
                true;
        }

        // -----------------------------------------------------
        // PHYSICAL SPIN HAS STOPPED
        // -----------------------------------------------------

        else if (!dragging &&
                 wasMoving &&
                 Mathf.Abs(spinSpeed) <= 0.1f)
        {
            wasMoving =
                false;

            isBraking =
                false;

            ResolveFinishedSpin();
        }
    }

    // =========================================================
    // SPIN RESOLUTION
    // =========================================================

    private void ResolveFinishedSpin()
    {
        /*
         * 1. Primero determinamos el segmento final.
         *
         * Debemos hacerlo ANTES de ejecutar cualquier sticker,
         * porque un sticker como WheelShifter puede regenerar
         * completamente la ruleta.
         */
        endSegmentIndex =
            GetCurrentSegmentIndex();

        /*
         * 2. RoundManager decide:
         *
         * - ¿tirada válida?
         * - commit / clear pending money
         * - gastar ficha si corresponde
         *
         * Todavía NO cobra deuda.
         */
        RoundManager.Instance?
            .NotifySpinEnd();

        bool validSpin =
            RoundManager.Instance == null ||
            RoundManager.Instance.WasLastSpinValid;

        // -----------------------------------------------------
        // INVALID SPIN
        // -----------------------------------------------------

        if (!validSpin)
        {
            OnSpinEnd?
                .Invoke();

            SpinInProgress =
                false;

            Debug.Log(
                "[ROULETTE] Invalid spin resolved. " +
                "No sticker effects, enemies or tokens consumed."
            );

            return;
        }

        // -----------------------------------------------------
        // VALID SPIN
        // -----------------------------------------------------

        /*
         * 3. GAME LOG - OPEN TEMPORARY SPIN BLOCK
         *
         * Nothing becomes visible yet. GameLogManager buffers
         * these lines until CommitSpinBlock() is called after the
         * complete spin resolution. Future sticker/enemy events can
         * therefore be inserted in their real causal order.
         */
        if (GameLogManager.Instance != null)
        {
            string methodLabel =
                CurrentSpinMethod == SpinMethod.Power
                    ? "POWER SWITCH"
                    : "MANUAL";

            GameLogManager.Instance
                .BeginValidSpinBlock(
                    methodLabel,
                    LastLaunchPower01
                );

            if (brakeBloodSpentThisSpin > 0)
            {
                GameLogManager.Instance
                    .LogManualBrake(
                        brakeBloodSpentThisSpin
                    );
            }

            int displaySegmentNumber =
                endSegmentIndex + 1;

            bool hasRealSegmentColor =
                generator != null &&
                generator.segments != null &&
                endSegmentIndex >= 0 &&
                endSegmentIndex < generator.segments.Count &&
                generator.segments[endSegmentIndex] != null &&
                generator.segments[endSegmentIndex].meshComponent != null;

            if (hasRealSegmentColor)
            {
                Color winningColor =
                    generator.segments[endSegmentIndex]
                        .meshComponent.color;

                GameLogManager.Instance
                    .LogWinningSegment(
                        displaySegmentNumber,
                        winningColor
                    );
            }
            else
            {
                GameLogManager.Instance
                    .LogWinningSegment(
                        displaySegmentNumber
                    );
            }
        }

        /*
         * 4. STICKERS
         */
        TriggerStickersOnWinningSegment();

        /*
         * 5. ENEMIGOS
         */
        OnSpinEnd?
            .Invoke();

        /*
         * 6. FIN COMPLETO DE LA RESOLUCIÓN
         */
        RoundManager.Instance?
            .NotifySpinResolved();

        /*
         * 7. PUBLISH GAME LOG BLOCK
         *
         * Only now does the player see the completed spin.
         */
        GameLogManager.Instance?
            .CommitSpinBlock();

        /*
         * Mantenemos SpinInProgress = true durante toda
         * la resolución para impedir manipular stickers
         * mientras se procesan efectos.
         */
        SpinInProgress =
            false;
    }

    // =========================================================
    // CURRENT SEGMENT
    // =========================================================

    int GetCurrentSegmentIndex()
    {
        if (generator == null ||
            wheel == null ||
            flapperTip == null)
        {
            return 0;
        }

        if (generator.segments == null ||
            generator.segments.Count == 0)
        {
            return
                GetCurrentSegmentIndexByAngle();
        }

        Vector2 tip =
            flapperTip.position;

        foreach (var seg in generator.segments)
        {
            if (seg.collider != null &&
                seg.collider.OverlapPoint(tip))
            {
                return seg.index;
            }
        }

        return
            GetCurrentSegmentIndexByAngle();
    }

    int GetCurrentSegmentIndexByAngle()
    {
        int segs =
            generator.segmentCount;

        if (segs <= 0)
            return 0;

        Vector3 local =
            wheel.InverseTransformPoint(
                flapperTip.position
            );

        float ang =
            Mathf.Atan2(
                local.y,
                local.x
            ) *
            Mathf.Rad2Deg;

        if (ang < 0f)
            ang += 360f;

        ang =
            (ang - 90f + 360f) %
            360f;

        float step =
            360f /
            segs;

        int idx =
            Mathf.FloorToInt(
                ang /
                step
            );

        return
            Mathf.Clamp(
                idx,
                0,
                segs - 1
            );
    }

    // =========================================================
    // VELOCITY
    // =========================================================

    public float GetCurrentAngularVelocity()
    {
        return spinSpeed;
    }

    // =========================================================
    // WINNING STICKERS
    // =========================================================

    void TriggerStickersOnWinningSegment()
    {
        if (generator == null ||
            generator.segments == null)
        {
            return;
        }

        if (endSegmentIndex < 0 ||
            endSegmentIndex >=
            generator.segments.Count)
        {
            return;
        }

        var segData =
            generator.segments[
                endSegmentIndex
            ];

        if (segData == null ||
            segData.collider == null)
        {
            return;
        }

        Transform winningTransform =
            segData.collider.transform;

        /*
         * Guardamos primero el array completo.
         *
         * Un sticker como WheelShifter puede regenerar
         * la rueda mientras resolvemos efectos.
         */
        BaseSticker[] stickers =
            winningTransform
                .GetComponentsInChildren<BaseSticker>(
                    true
                );

        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null)
                continue;

            sticker.OnSegmentWin();
        }
    }
}
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
        Power,
        LuckyShot
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

    [Header("Sticker Physics Performance")]

    [Tooltip(
        "If enabled, colliders belonging to stickers currently placed on " +
        "the wheel are temporarily disabled during the physical spin and " +
        "restored before spin resolution. This reduces Physics2D work while " +
        "the wheel is moving without affecting placement validation."
    )]
    public bool disableWheelStickerCollidersDuringSpin =
        true;


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

    private void OnDisable()
    {
        /*
         * Editor stop, scene changes or disabling this component must never
         * leave sticker colliders disabled.
         */
        RestoreWheelStickerCollidersAfterSpin();
    }


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

    /*
     * Used by world-space utility buttons that consume the same
     * mouse-down as the roulette. This prevents a button click from
     * accidentally beginning a manual drag in the same frame.
     */
    private bool pointerInputConsumedThisFrame = false;

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
    /// Physical segment index where the current / last completed spin landed.
    ///
    /// Enemy actions execute after sticker resolution and use this value to
    /// affect the segment that produced that spin.
    /// </summary>
    public int LastResolvedSegmentIndex =>
        endSegmentIndex;

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
    /// Reward percentage attached to the current / last Lucky Shot.
    /// Zero for Manual and Power Switch spins.
    /// </summary>
    public float CurrentLuckyShotRewardPercent
    {
        get;
        private set;
    } = 0f;


    /// <summary>
    /// Up-front resource cost actually paid by the current / last Power Spin.
    /// Always zero for Manual and Lucky Shot spins.
    /// </summary>
    public int CurrentPowerSpinBloodCostPaid
    {
        get;
        private set;
    } = 0;


    public int CurrentPowerSpinCoinCostPaid
    {
        get;
        private set;
    } = 0;


    /// <summary>
    /// Whether the current physical spin allows the player to use
    /// the Blood-powered manual brake. Manual and Power spins allow
    /// it by default; Lucky Shot decides this per launch.
    /// </summary>
    public bool CurrentSpinAllowsManualBrake
    {
        get;
        private set;
    } = true;

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

    /// <summary>
    /// Consumes the roulette pointer handling for the current frame.
    /// Useful for world-space buttons that use the same mouse input.
    /// </summary>
    public void ConsumePointerInputThisFrame()
    {
        pointerInputConsumedThisFrame = true;
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
        // EXTERNAL WORLD-SPACE BUTTON
        // -----------------------------------------------------

        if (pointerInputConsumedThisFrame)
        {
            pointerInputConsumedThisFrame = false;
            return;
        }

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

            if (!CurrentSpinAllowsManualBrake)
            {
                isBraking = false;
            }
            else if (Input.GetMouseButton(0))
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
    public bool TryStartPowerSpin(
        float power01,
        int bloodCost = 0,
        int coinCost = 0)
    {
        if (!CanStartNewSpin())
            return false;


        int safeBloodCost =
            Mathf.Max(
                0,
                bloodCost
            );

        int safeCoinCost =
            Mathf.Max(
                0,
                coinCost
            );


        /*
         * Cost is checked before launch and paid only AFTER TryBeginSpin
         * succeeds. This prevents:
         *
         * - paying for a charge released below minThrowSpeed
         * - paying when the round currently blocks spins
         * - paying for any other rejected Power Spin
         */
        if (!CanAffordPowerSpinCost(
            safeBloodCost,
            safeCoinCost))
        {
            return false;
        }


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


        bool started =
            TryBeginSpin(
                requestedSpeed,
                SpinMethod.Power,
                normalizedPower
            );


        if (!started)
            return false;


        /*
         * The affordability check happened immediately before TryBeginSpin,
         * so these spends are now an atomic launch cost in normal gameplay.
         *
         * CurrencyManager.Spend does not count as positive spin earnings,
         * so Lucky Shot accounting remains untouched.
         */
        if (safeCoinCost > 0 &&
            CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance
                .Spend(
                    safeCoinCost
                );
        }


        if (safeBloodCost > 0 &&
            BloodManager.Instance != null)
        {
            BloodManager.Instance
                .ConsumeBlood(
                    safeBloodCost
                );
        }


        CurrentPowerSpinBloodCostPaid =
            safeBloodCost;

        CurrentPowerSpinCoinCostPaid =
            safeCoinCost;


        return true;
    }


    private bool CanAffordPowerSpinCost(
        int bloodCost,
        int coinCost)
    {
        if (bloodCost > 0)
        {
            if (BloodManager.Instance == null ||
                BloodManager.Instance.currentBlood <
                    bloodCost)
            {
                return false;
            }
        }


        if (coinCost > 0)
        {
            if (CurrencyManager.Instance == null ||
                !CurrencyManager.Instance.CanAfford(
                    coinCost
                ))
            {
                return false;
            }
        }


        return true;
    }


    // =========================================================
    // LUCKY SHOT PUBLIC API
    // =========================================================

    /// <summary>
    /// Starts an automatic Lucky Shot using the supplied normalized
    /// power and reward percentage.
    ///
    /// power01:
    /// 0 = 0% power
    /// 1 = 100% power
    ///
    /// rewardPercent is clamped to 0 - 500.
    /// </summary>
    public bool TryStartLuckyShot(
        float power01,
        float rewardPercent,
        bool allowManualBrake)
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
            Debug.LogWarning(
                "[LUCKY SHOT] Rolled power was below the roulette's " +
                "minimum throw speed. Increase the Lucky Shot minimum power."
            );

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
                SpinMethod.LuckyShot,
                normalizedPower,
                Mathf.Clamp(
                    rewardPercent,
                    0f,
                    500f
                ),
                allowManualBrake
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
        float displayPower01,
        float luckyShotRewardPercent = 0f,
        bool allowManualBrake = true)
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

        CurrentLuckyShotRewardPercent =
            method == SpinMethod.LuckyShot
                ? Mathf.Clamp(
                    luckyShotRewardPercent,
                    0f,
                    500f
                )
                : 0f;


        /*
         * Every real spin begins with no Power Spin cost recorded.
         * TryStartPowerSpin fills these only after the launch succeeds.
         */
        CurrentPowerSpinBloodCostPaid =
            0;

        CurrentPowerSpinCoinCostPaid =
            0;


        CurrentSpinAllowsManualBrake =
            method == SpinMethod.LuckyShot
                ? allowManualBrake
                : true;

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


        DisableWheelStickerCollidersForSpin();


        isBraking =
            false;

        bloodTimer =
            0f;

        brakeBloodSpentThisSpin =
            0;

        /*
         * Reset the Flag Pin earnings for this physical spin.
         *
         * We keep the running total in FlagPin because that is the
         * object that actually awards the pending money on every hit.
         */
        if (RoundManager.Instance != null &&
            RoundManager.Instance.flagPin != null)
        {
            RoundManager.Instance.flagPin
                .ResetSpinEarnings();
        }

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
                CurrentSpinAllowsManualBrake &&
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
    // STICKER COLLIDER PERFORMANCE
    // =========================================================

    private void DisableWheelStickerCollidersForSpin()
    {
        if (!disableWheelStickerCollidersDuringSpin)
            return;


        BaseSticker[] stickers =
            FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in
                 stickers)
        {
            if (sticker == null)
                continue;


            sticker.DisableWheelColliderForPhysicalSpin();
        }
    }


    private void RestoreWheelStickerCollidersAfterSpin()
    {
        /*
         * Restore regardless of the current Inspector toggle.
         *
         * This makes the system safe if the option is changed in the Editor
         * while a test spin is already in progress.
         */
        BaseSticker[] stickers =
            FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in
                 stickers)
        {
            if (sticker == null)
                continue;


            sticker.RestoreWheelColliderAfterPhysicalSpin();
        }


        /*
         * Placement validation uses Collider2D geometry immediately after
         * effects such as WheelShifter regenerate the wheel. Synchronizing
         * now guarantees Physics2D sees every restored collider before any
         * resolution code can query overlaps / distances / closest points.
         */
        Physics2D.SyncTransforms();
    }


    // =========================================================
    // SPIN RESOLUTION
    // =========================================================

    private void ResolveFinishedSpin()
    {
        /*
         * The physical movement is over.
         *
         * Restore sticker colliders BEFORE absolutely any spin-resolution
         * work happens. This is especially important for WheelShifter /
         * Magic Bean: their wheel regeneration ends by validating sticker
         * placement, which requires the real sticker colliders to be active.
         */
        RestoreWheelStickerCollidersAfterSpin();


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
            bool hasPaidPowerSpinCost =
                CurrentSpinMethod ==
                    SpinMethod.Power &&
                (
                    CurrentPowerSpinBloodCostPaid > 0 ||
                    CurrentPowerSpinCoinCostPaid > 0
                );


            bool refundInvalidPowerSpinCost =
                hasPaidPowerSpinCost &&
                PowerSpinController.Instance != null &&
                PowerSpinController.Instance
                    .refundCostOnInvalidSpin;


            if (refundInvalidPowerSpinCost)
            {
                /*
                 * The exact amounts paid by THIS spin are refunded.
                 *
                 * We intentionally do not recalculate the current effective
                 * cost here: a Curse may theoretically have changed between
                 * launch and resolution.
                 */
                PowerSpinController.Instance
                    .RefundPaidCost(
                        CurrentPowerSpinBloodCostPaid,
                        CurrentPowerSpinCoinCostPaid
                    );


                GameLogManager.Instance?
                    .LogInvalidPowerSpinCostRefunded(
                        CurrentPowerSpinBloodCostPaid,
                        CurrentPowerSpinCoinCostPaid
                    );
            }
            else if (hasPaidPowerSpinCost)
            {
                /*
                 * Harsher rulesets may keep the cost even on an invalid spin.
                 */
                GameLogManager.Instance?
                    .LogInvalidPowerSpinCost(
                        CurrentPowerSpinBloodCostPaid,
                        CurrentPowerSpinCoinCostPaid
                    );
            }


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
            string methodLabel;

            if (CurrentSpinMethod == SpinMethod.Power)
            {
                methodLabel =
                    "POWER SWITCH";
            }
            else if (CurrentSpinMethod == SpinMethod.LuckyShot)
            {
                methodLabel =
                    "LUCKY SHOT";
            }
            else
            {
                methodLabel =
                    "MANUAL";
            }

            GameLogManager.Instance
                .BeginValidSpinBlock(
                    methodLabel,
                    LastLaunchPower01
                );


            if (CurrentSpinMethod ==
                    SpinMethod.Power &&
                (
                    CurrentPowerSpinBloodCostPaid > 0 ||
                    CurrentPowerSpinCoinCostPaid > 0
                ))
            {
                GameLogManager.Instance
                    .LogPowerSpinCost(
                        CurrentPowerSpinBloodCostPaid,
                        CurrentPowerSpinCoinCostPaid
                    );
            }


            if (brakeBloodSpentThisSpin > 0)
            {
                GameLogManager.Instance
                    .LogManualBrake(
                        brakeBloodSpentThisSpin
                    );
            }

            /*
             * Flag Pin rewards happen during the physical spin, before
             * the final winning segment is resolved. We aggregate them
             * into a single line instead of spamming one entry per hit.
             */
            if (RoundManager.Instance != null &&
                RoundManager.Instance.flagPin != null)
            {
                int flagMoney =
                    RoundManager.Instance.flagPin
                        .MoneyEarnedThisSpin;

                if (flagMoney > 0)
                {
                    GameLogManager.Instance
                        .LogFlagPinMoney(
                            flagMoney
                        );
                }
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
         * SegmentBlock does NOT rewrite the physical winner.
         *
         * We keep the real winning segment for:
         * - spin validation
         * - logs
         * - enemy actions
         * - all systems that need to know where the wheel really stopped
         *
         * The block only suppresses sticker resolution inside that segment.
         */
        bool winningSegmentBlocked =
            generator != null &&
            generator.IsSegmentBlocked(
                endSegmentIndex
            );


        if (winningSegmentBlocked &&
            GameLogManager.Instance != null)
        {
            int displaySegmentNumber =
                endSegmentIndex + 1;

            bool hasSegmentColor =
                generator.segments != null &&
                endSegmentIndex >= 0 &&
                endSegmentIndex < generator.segments.Count &&
                generator.segments[endSegmentIndex] != null &&
                generator.segments[endSegmentIndex].meshComponent != null;


            string segmentLabel;

            if (hasSegmentColor)
            {
                segmentLabel =
                    GameLogManager.Instance
                        .SegmentText(
                            $"Segment {displaySegmentNumber}",
                            generator.segments[endSegmentIndex]
                                .meshComponent.color
                        );
            }
            else
            {
                segmentLabel =
                    GameLogManager.Instance
                        .SegmentText(
                            $"Segment {displaySegmentNumber}"
                        );
            }


            GameLogManager.Instance
                .AddGameplayLine(
                    segmentLabel +
                    " is blocked: stickers inside do not activate"
                );
        }


        /*
         * 4. STICKERS
         *
         * Snapshot where every relevant sticker was when the physical
         * spin ended, then resolve location-aware effects.
         *
         * This supports:
         * - winning-segment effects
         * - non-winning-segment effects
         * - Album effects
         *
         * without allowing an early effect such as WheelShifter to change
         * the location classification of stickers that resolve later.
         */
        ResolveStickerEffectsForSpin();

        /*
         * 5. ENEMIGOS
         */
        OnSpinEnd?
            .Invoke();

        /*
         * 6. SPIN MONEY / LUCKY SHOT
         *
         * Currency tracking stays active through:
         * - Flag Pin rewards
         * - sticker money
         * - any future money-producing enemy/effect
         *
         * Lucky Shot resolves only AFTER all those effects, but BEFORE
         * RoundManager resolves the end-of-round debt. This means the
         * bonus can legitimately help pay the debt from this spin.
         */
        ResolveSpinMoneyAndLuckyShotBonus();

        /*
         * 7. FIN COMPLETO DE LA RESOLUCIÓN
         */
        RoundManager.Instance?
            .NotifySpinResolved();

        /*
         * 8. PUBLISH GAME LOG BLOCK
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
    // LUCKY SHOT REWARD
    // =========================================================

    private void ResolveSpinMoneyAndLuckyShotBonus()
    {
        if (CurrencyManager.Instance == null)
            return;

        /*
         * End tracking BEFORE paying the Lucky Shot bonus so the bonus
         * never counts itself as part of the spin earnings.
         */
        int moneyEarnedThisSpin =
            CurrencyManager.Instance
                .EndSpinEarningsTracking();

        if (CurrentSpinMethod !=
            SpinMethod.LuckyShot)
        {
            return;
        }

        float rewardPercent =
            Mathf.Clamp(
                CurrentLuckyShotRewardPercent,
                0f,
                500f
            );

        float rawBonus =
            moneyEarnedThisSpin *
            rewardPercent /
            100f;

        /*
         * Currency is integer-based. Use normal human rounding:
         * .5 and above rounds up.
         */
        int luckyBonus =
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    rawBonus +
                    0.5f
                )
            );

        if (luckyBonus > 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    luckyBonus
                );
        }

        /*
         * This line is intentionally added after stickers and enemies.
         * Because the Game Log block is still open, it will appear as
         * the final gameplay effect of the spin.
         */
        if (GameLogManager.Instance != null)
        {
            string percentLabel =
                FormatPercent(
                    rewardPercent
                );

            GameLogManager.Instance
                .AddGameplayLine(
                    $"Lucky Shot bonus ({percentLabel}): " +
                    GameLogManager.Instance
                        .MoneyText(
                            $"+${luckyBonus}"
                        )
                );
        }

        Debug.Log(
            $"[LUCKY SHOT] Spin earned ${moneyEarnedThisSpin}. " +
            $"Bonus {rewardPercent:0.##}% = ${luckyBonus}."
        );
    }


    private string FormatPercent(
        float value)
    {
        return
            Mathf.Approximately(
                value,
                Mathf.Round(value)
            )
                ? $"{Mathf.RoundToInt(value)}%"
                : $"{value:0.##}%";
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
    // LOCATION-AWARE STICKER RESOLUTION
    // =========================================================

    private struct StickerResolutionEntry
    {
        public BaseSticker sticker;
        public StickerSpinLocation location;

        public StickerResolutionEntry(
            BaseSticker sticker,
            StickerSpinLocation location)
        {
            this.sticker = sticker;
            this.location = location;
        }
    }


    /// <summary>
    /// Resolves every sticker that has a spin-end location:
    ///
    /// 1. Winning segment stickers.
    /// 2. Non-winning roulette segment stickers.
    /// 3. Album stickers.
    ///
    /// The complete list is captured BEFORE any sticker effect executes.
    /// This is important because effects such as WheelShifter can rebuild
    /// the roulette hierarchy during resolution.
    /// </summary>
    private void ResolveStickerEffectsForSpin()
    {
        List<StickerResolutionEntry> snapshot =
            BuildStickerResolutionSnapshot();


        foreach (StickerResolutionEntry entry in snapshot)
        {
            if (entry.sticker == null)
                continue;

            entry.sticker
                .ResolveSpinLocation(
                    entry.location
                );
        }
    }


    private List<StickerResolutionEntry>
        BuildStickerResolutionSnapshot()
    {
        List<StickerResolutionEntry> snapshot =
            new List<StickerResolutionEntry>();

        HashSet<BaseSticker> alreadyAdded =
            new HashSet<BaseSticker>();


        // -----------------------------------------------------
        // 1. WINNING SEGMENT FIRST
        // -----------------------------------------------------

        bool winningSegmentBlocked =
            generator != null &&
            generator.IsSegmentBlocked(
                endSegmentIndex
            );


        if (!winningSegmentBlocked)
        {
            AddSegmentStickersToSnapshot(
                endSegmentIndex,
                StickerSpinLocation.WinningSegment,
                snapshot,
                alreadyAdded
            );
        }


        // -----------------------------------------------------
        // 2. ALL NON-WINNING SEGMENTS
        // -----------------------------------------------------

        if (generator != null &&
            generator.segments != null)
        {
            for (int i = 0;
                 i < generator.segments.Count;
                 i++)
            {
                if (i == endSegmentIndex)
                    continue;

                AddSegmentStickersToSnapshot(
                    i,
                    StickerSpinLocation.NonWinningSegment,
                    snapshot,
                    alreadyAdded
                );
            }
        }


        // -----------------------------------------------------
        // 3. ALBUM
        // -----------------------------------------------------

        AddAlbumStickersToSnapshot(
            snapshot,
            alreadyAdded
        );


        return snapshot;
    }


    private void AddSegmentStickersToSnapshot(
        int segmentIndex,
        StickerSpinLocation location,
        List<StickerResolutionEntry> snapshot,
        HashSet<BaseSticker> alreadyAdded)
    {
        if (generator == null ||
            generator.segments == null ||
            segmentIndex < 0 ||
            segmentIndex >= generator.segments.Count)
        {
            return;
        }


        var segData =
            generator.segments[
                segmentIndex
            ];

        if (segData == null ||
            segData.collider == null)
        {
            return;
        }


        BaseSticker[] stickers =
            segData.collider.transform
                .GetComponentsInChildren<BaseSticker>(
                    true
                );


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null ||
                alreadyAdded.Contains(sticker))
            {
                continue;
            }

            alreadyAdded.Add(sticker);

            snapshot.Add(
                new StickerResolutionEntry(
                    sticker,
                    location
                )
            );
        }
    }


    private void AddAlbumStickersToSnapshot(
        List<StickerResolutionEntry> snapshot,
        HashSet<BaseSticker> alreadyAdded)
    {
        if (AlbumManager.Instance == null ||
            AlbumManager.Instance.albumZone == null)
        {
            return;
        }


        Transform contentRoot =
            AlbumManager.Instance.albumZone
                .GetContentRoot();

        if (contentRoot == null)
            return;


        BaseSticker[] stickers =
            contentRoot
                .GetComponentsInChildren<BaseSticker>(
                    true
                );


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null ||
                alreadyAdded.Contains(sticker))
            {
                continue;
            }

            /*
             * Verify logical / hierarchy membership instead of assuming every
             * BaseSticker below ContentRoot is currently an Album sticker.
             */
            if (!AlbumManager.Instance
                .IsStickerInAlbum(sticker))
            {
                continue;
            }

            alreadyAdded.Add(sticker);

            snapshot.Add(
                new StickerResolutionEntry(
                    sticker,
                    StickerSpinLocation.Album
                )
            );
        }
    }

}
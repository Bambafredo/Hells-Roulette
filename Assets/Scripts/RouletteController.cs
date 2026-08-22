using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class RouletteController : MonoBehaviour
{
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

    public event Action OnSpinStart;

    /*
     * IMPORTANTE:
     *
     * OnSpinEnd ahora significa:
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
            /*
             * Antes de permitir siquiera coger la ruleta,
             * comprobamos que la ronda permita otra tirada.
             *
             * Esto será especialmente útil cuando más adelante
             * haya pantallas de recompensa / estados intermedios.
             */
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

                return;
            }

            accumulatedDragDistance =
                0f;

            // -------------------------------------------------
            // TRUE SPIN
            // -------------------------------------------------

            float avgSpeed =
                0f;

            foreach (float v in recentSpeeds)
                avgSpeed += v;

            avgSpeed /=
                Mathf.Max(
                    1,
                    recentSpeeds.Count
                );

            float weightedSpeed =
                avgSpeed /
                wheelWeight;

            if (Mathf.Abs(weightedSpeed) >
                minThrowSpeed)
            {
                /*
                 * Segunda protección.
                 *
                 * En teoría ya lo comprobamos en MouseDown,
                 * pero el estado podría haber cambiado mientras
                 * el jugador estaba haciendo el gesto.
                 */
                if (RoundManager.Instance != null &&
                    !RoundManager.Instance.CanStartSpin)
                {
                    recentSpeeds.Clear();

                    spinSpeed =
                        0f;

                    return;
                }

                spinSpeed =
                    Mathf.Clamp(
                        weightedSpeed,
                        -maxSpinSpeed,
                        maxSpinSpeed
                    );

                startSegmentIndex =
                    GetCurrentSegmentIndex();

                SpinInProgress =
                    true;

                /*
                 * Primero notificamos al RoundManager.
                 *
                 * Así CurrencyManager.BeginSpin() y el estado
                 * de validación están preparados antes de que
                 * cualquier listener externo procese OnSpinStart.
                 */
                RoundManager.Instance?
                    .NotifySpinStart();

                OnSpinStart?
                    .Invoke();

                bloodTimer =
                    0f;
            }
            else
            {
                spinSpeed *=
                    0.5f;
            }

            recentSpeeds.Clear();
        }
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
                            isBraking = false;
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
            /*
             * Ningún sticker.
             *
             * OnSpinEnd se mantiene porque otros sistemas
             * pueden necesitar saber que la tirada física
             * ha terminado.
             *
             * BaseEnemy ya consulta WasLastSpinValid,
             * por lo que no atacará.
             */
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
         * 3. STICKERS
         *
         * Esta era una de las cosas que teníamos pendientes:
         * ahora los stickers resuelven ANTES de los enemigos.
         */
        TriggerStickersOnWinningSegment();

        /*
         * 4. ENEMIGOS
         *
         * BaseEnemy está suscrito a OnSpinEnd.
         *
         * Por tanto el evento ocurre DESPUÉS de los stickers.
         */
        OnSpinEnd?
            .Invoke();

        /*
         * 5. FIN COMPLETO DE LA RESOLUCIÓN
         *
         * Aquí ya han ocurrido:
         *
         * - dinero del giro
         * - consumo de ficha
         * - stickers
         * - enemigos
         *
         * AHORA y solo ahora RoundManager puede comprobar
         * si queda alguna ficha y, en caso contrario,
         * cobrar la deuda.
         */
        RoundManager.Instance?
            .NotifySpinResolved();

        /*
         * Mantenemos SpinInProgress = true durante toda
         * la resolución para impedir que el jugador pueda
         * manipular stickers mientras se procesan efectos.
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
                ang / step
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
         * Esto es importante porque uno de esos stickers
         * puede ser WheelShifter y regenerar la rueda
         * mientras estamos resolviendo efectos.
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
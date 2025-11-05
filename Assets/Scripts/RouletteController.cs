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

    [Header("Feel")]
    public float gestureToSpin = 1.0f;
    public float deceleration = 220f;
    [Range(0f, 1f)] public float velocitySmoothing = 0.25f;
    public float minDragRadius = 0.3f;
    public float maxSpinSpeed = 2000f;
    public float minThrowSpeed = 100f;
    [Range(0.5f, 5f)] public float wheelWeight = 1.0f;
    public int velocitySamples = 5;

    [Header("Pin Interaction")]
    public LayerMask blockInputMask;
    public bool inputBlocked = false;

    [Header("Brake System")]
    public bool enableBrake = true;
    public float extraDeceleration = 300f;
    public int bloodCostPerSecond = 1; // 🩸 Sangre gastada por segundo al frenar
    private bool isBraking = false;

    // ⏱️ Temporizador para control de gasto por segundo real
    private float bloodTimer = 0f;

    // 🔔 Eventos
    public event Action OnSpinStart;
    public event Action OnSpinEnd;

    private bool dragging = false;
    private float lastAngleDeg = 0f;
    private float spinSpeed = 0f;
    private float lastSampleTime = 0f;
    private bool wasMoving = false;
    private Queue<float> recentSpeeds = new Queue<float>();

    private int startSegmentIndex = -1;
    private int endSegmentIndex = -1;

    public bool SpinInProgress { get; private set; } = false;

    public void SetInputBlocked(bool v)
    {
        inputBlocked = v;
    }

    void Update()
    {
        HandlePointer();
        ApplySpin();
    }

    void HandlePointer()
    {
        if (inputBlocked)
            return;

        Vector2 pos;
        bool down, held, up;
        ReadPointer(out pos, out down, out held, out up);

        // 🔴 Durante la tirada, solo podemos frenar
        if (SpinInProgress)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButton(0))
            {
                Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Collider2D col = wheel.GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(mouseWorld))
                    isBraking = true;
                else
                    isBraking = false;
            }
            else
            {
                isBraking = false;
            }
#endif
            return;
        }

        // 🧭 Solo podemos arrastrar para tirar si NO hay una tirada en curso
        if (down)
        {
            Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Physics2D.OverlapPoint(mouseWorld, blockInputMask))
                return;

            if (Vector2.Distance(pos, (Vector2)wheel.position) >= minDragRadius)
            {
                dragging = true;
                lastAngleDeg = WorldAngleFromCenter(pos);
                lastSampleTime = Time.time;
                recentSpeeds.Clear();
            }
        }

        if (held && dragging)
        {
            float currentAngle = WorldAngleFromCenter(pos);
            float delta = Mathf.DeltaAngle(lastAngleDeg, currentAngle);
            wheel.Rotate(0f, 0f, delta);

            float dt = Mathf.Max(0.0001f, Time.time - lastSampleTime);
            float instVel = (delta / dt) * gestureToSpin;

            recentSpeeds.Enqueue(instVel);
            if (recentSpeeds.Count > velocitySamples)
                recentSpeeds.Dequeue();

            instVel = Mathf.Clamp(instVel, -maxSpinSpeed, maxSpinSpeed);
            spinSpeed = Mathf.Lerp(spinSpeed, instVel, 1f - velocitySmoothing);

            if (Mathf.Abs(spinSpeed) > maxSpinSpeed * 0.9f)
                spinSpeed = Mathf.Lerp(spinSpeed, Mathf.Sign(spinSpeed) * maxSpinSpeed * 0.9f, 0.3f);

            lastAngleDeg = currentAngle;
            lastSampleTime = Time.time;
        }

        if (up && dragging)
        {
            dragging = false;

            float avgSpeed = 0f;
            foreach (float v in recentSpeeds) avgSpeed += v;
            avgSpeed /= Mathf.Max(1, recentSpeeds.Count);

            float weightedSpeed = avgSpeed / wheelWeight;

            if (Mathf.Abs(weightedSpeed) > minThrowSpeed)
            {
                spinSpeed = Mathf.Clamp(weightedSpeed, -maxSpinSpeed, maxSpinSpeed);

                startSegmentIndex = GetCurrentSegmentIndex();
                Debug.Log($"🎬 Empieza la tirada. Segmento inicial: {startSegmentIndex}");

                OnSpinStart?.Invoke();
                SpinInProgress = true;
                bloodTimer = 0f;
            }
            else
            {
                spinSpeed *= 0.5f;
            }

            recentSpeeds.Clear();
        }
    }

    void ReadPointer(out Vector2 worldPos, out bool down, out bool held, out bool up)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        up = Input.GetMouseButtonUp(0);
        worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
#else
        down = held = up = false;
        worldPos = Vector2.zero;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            worldPos = Camera.main.ScreenToWorldPoint(t.position);
            down = t.phase == TouchPhase.Began;
            held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
            up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
        }
#endif
    }

    float WorldAngleFromCenter(Vector2 worldPos)
    {
        Vector2 dir = worldPos - (Vector2)wheel.position;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    void ApplySpin()
    {
        if (!dragging && Mathf.Abs(spinSpeed) > 0.1f)
        {
            wheel.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            float adjustedDecel = deceleration / wheelWeight;

            // 🩸 Si mantenemos pulsado sobre la ruleta, perdemos sangre para frenarla
            if (enableBrake && isBraking && BloodManager.Instance != null)
            {
                bloodTimer += Time.deltaTime;
                bool hasBlood = BloodManager.Instance.currentBlood > 0;

                // Solo frenar si aún hay sangre
                if (hasBlood)
                {
                    float interval = 1f / bloodCostPerSecond;
                    if (bloodTimer >= interval)
                    {
                        bool canBrake = BloodManager.Instance.ConsumeBlood(1);
                        bloodTimer = 0f;

                        // Si justo ahora se queda sin sangre, corta el freno
                        if (!canBrake)
                            isBraking = false;
                    }

                    // 🔧 Solo aplica freno si sigues teniendo sangre
                    adjustedDecel += extraDeceleration;
                }
                else
                {
                    // Sin sangre => no puedes frenar
                    isBraking = false;
                }
            }

            float sign = Mathf.Sign(spinSpeed);
            spinSpeed -= sign * adjustedDecel * Time.deltaTime;
            if (Mathf.Sign(spinSpeed) != sign)
                spinSpeed = 0f;

            wasMoving = true;
        }
        else if (!dragging && wasMoving && Mathf.Abs(spinSpeed) <= 0.1f)
        {
            wasMoving = false;
            OnSpinEnd?.Invoke();
            SpinInProgress = false;

            endSegmentIndex = GetCurrentSegmentIndex();
            Debug.Log($"🏁 Tirada finalizada. Segmento ganador: {endSegmentIndex}");

            TriggerStickersOnWinningSegment();
        }
    }

    int GetCurrentSegmentIndex()
    {
        if (generator == null || wheel == null || flapperTip == null) return 0;
        if (generator.segments == null || generator.segments.Count == 0)
            return GetCurrentSegmentIndexByAngle();

        Vector2 tip = flapperTip.position;
        foreach (var seg in generator.segments)
        {
            if (seg.collider != null && seg.collider.OverlapPoint(tip))
                return seg.index;
        }

        return GetCurrentSegmentIndexByAngle();
    }

    int GetCurrentSegmentIndexByAngle()
    {
        int segs = generator.segmentCount;
        if (segs <= 0) return 0;

        Vector3 local = wheel.InverseTransformPoint(flapperTip.position);
        float ang = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;
        ang = (ang - 90f + 360f) % 360f;

        float step = 360f / segs;
        int idx = Mathf.FloorToInt(ang / step);
        return Mathf.Clamp(idx, 0, segs - 1);
    }

    public float GetCurrentAngularVelocity() => spinSpeed;

    void TriggerStickersOnWinningSegment()
    {
        if (generator == null || generator.segments == null) return;
        if (endSegmentIndex < 0 || endSegmentIndex >= generator.segments.Count) return;

        var segData = generator.segments[endSegmentIndex];
        if (segData == null || segData.collider == null) return;

        Transform winningTransform = segData.collider.transform;

        var stickers = winningTransform.GetComponentsInChildren<BaseSticker>(true);
        foreach (var sticker in stickers)
            sticker.OnSegmentWin();

        Debug.Log($"✨ {stickers.Length} stickers activados en el segmento {winningTransform.name}");
    }
}
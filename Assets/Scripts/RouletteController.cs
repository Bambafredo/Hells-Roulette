using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class RouletteController : MonoBehaviour
{
    [Header("References")]
    public Transform wheel;
    public WheelGenerator generator;
    public Transform flapperTip; // ← la punta real del flapper

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

    // 🔔 Eventos
    public event Action OnSpinStart;
    public event Action OnSpinEnd;

    private bool dragging = false;
    private float lastAngleDeg = 0f;
    private float spinSpeed = 0f;
    private float lastSampleTime = 0f;
    private bool wasMoving = false;
    private Queue<float> recentSpeeds = new Queue<float>();
    private bool spinStartedThisPress = false;

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

        if (down)
        {
            Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Physics2D.OverlapPoint(mouseWorld, blockInputMask))
                return;
        }

        if (down)
        {
            if (Vector2.Distance(pos, (Vector2)wheel.position) >= minDragRadius)
            {
                dragging = true;
                spinStartedThisPress = true;
                lastAngleDeg = WorldAngleFromCenter(pos);
                lastSampleTime = Time.time;
                recentSpeeds.Clear();
            }
        }

        if (held && dragging)
        {
            if (spinStartedThisPress)
            {
                spinStartedThisPress = false;
                OnSpinStart?.Invoke();
            }

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
                spinSpeed = Mathf.Clamp(weightedSpeed, -maxSpinSpeed, maxSpinSpeed);
            else
                spinSpeed *= 0.5f;

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

            int winner = GetCurrentSegmentIndex();
            Debug.Log($"🎯 Segmento ganador: {winner}");
        }
    }

    // ✅ Cálculo correcto del segmento, compensando la orientación del flapper (+Y)
    int GetCurrentSegmentIndex()
    {
        int segs = (generator != null) ? generator.segmentCount : 0;
        if (segs <= 0 || wheel == null || flapperTip == null)
            return 0;

        // 1️⃣ Posición del flapper en el espacio local de la rueda
        Vector3 local = wheel.InverseTransformPoint(flapperTip.position);

        // 2️⃣ Ángulo local (0° = +X)
        float ang = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        if (ang < 0f) ang += 360f;

        // 3️⃣ Compensamos porque el flapper apunta hacia +Y (90°)
        ang = (ang - 90f + 360f) % 360f;

        // 4️⃣ Tamaño del segmento
        float step = 360f / segs;

        // 5️⃣ Índice final
        int idx = Mathf.FloorToInt(ang / step);
        return Mathf.Clamp(idx, 0, segs - 1);
    }
}
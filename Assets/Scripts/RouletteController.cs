using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RouletteController : MonoBehaviour
{
    [Header("References")]
    public Transform wheel;
    public WheelGenerator generator;

    [Header("Feel")]
    [Tooltip("Fuerza con la que se traduce el gesto a velocidad angular (deg/s). 1 = directo")]
    public float gestureToSpin = 1.0f;

    [Tooltip("Frenado (deg/s^2). 150-300 suele ir bien")]
    public float deceleration = 220f;

    [Tooltip("Filtro de la velocidad durante drag (0=brusco, 1=muy suave)")]
    [Range(0f, 1f)] public float velocitySmoothing = 0.25f;

    [Tooltip("Ignora toques muy cerca del centro (en unidades de mundo)")]
    public float minDragRadius = 0.3f;

    [Tooltip("Límite de velocidad angular (deg/s) por seguridad")]
    public float maxSpinSpeed = 2000f;

    [Tooltip("Umbral de velocidad para mantener inercia al soltar")]
    public float minThrowSpeed = 100f;

    [Tooltip("A mayor peso, menos velocidad máxima real puede alcanzar la rueda (1 = ligera, 3 = muy pesada)")]
    [Range(0.5f, 5f)] public float wheelWeight = 1.0f;

    [Tooltip("Número de frames que se promedian para la velocidad media")]
    public int velocitySamples = 5;

    private bool dragging = false;
    private float lastAngleDeg = 0f;
    private float spinSpeed = 0f;
    private float lastSampleTime = 0f;
    private bool wasMoving = false;

    private Queue<float> recentSpeeds = new Queue<float>();


    void Update()
    {
        HandlePointer();
        ApplySpin();
    }

    // ---------------- INPUT ----------------
    void HandlePointer()
    {
        Vector2 pos;
        bool down, held, up;
        ReadPointer(out pos, out down, out held, out up);

        if (down)
        {
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

            // Guarda velocidad en el buffer
            recentSpeeds.Enqueue(instVel);
            if (recentSpeeds.Count > velocitySamples)
                recentSpeeds.Dequeue();

            // Aplicamos suavizado y limitación
            instVel = Mathf.Clamp(instVel, -maxSpinSpeed, maxSpinSpeed);
            spinSpeed = Mathf.Lerp(spinSpeed, instVel, 1f - velocitySmoothing);

            // Limitador suave adicional (reduce exceso sin cortar bruscamente)
            if (Mathf.Abs(spinSpeed) > maxSpinSpeed * 0.9f)
                spinSpeed = Mathf.Lerp(spinSpeed, Mathf.Sign(spinSpeed) * maxSpinSpeed * 0.9f, 0.3f);

            lastAngleDeg = currentAngle;
            lastSampleTime = Time.time;
        }

        if (up && dragging)
        {
            dragging = false;

            // Calcula media de las últimas velocidades
            float avgSpeed = 0f;
            foreach (float v in recentSpeeds) avgSpeed += v;
            avgSpeed /= Mathf.Max(1, recentSpeeds.Count);

            // 🟢 Aplica el "peso" de la rueda: cuanto más pesado, menor velocidad final
            float weightedSpeed = avgSpeed / wheelWeight;

            // 🧠 Si la media es alta, mantenemos inercia; si no, la frenamos
            if (Mathf.Abs(weightedSpeed) > minThrowSpeed)
            {
                spinSpeed = Mathf.Clamp(weightedSpeed, -maxSpinSpeed, maxSpinSpeed);
            }
            else
            {
                spinSpeed *= 0.1f;
            }

            recentSpeeds.Clear();
        }
    }

    // Lee mouse o touch y lo pasa a mundo
    void ReadPointer(out Vector2 worldPos, out bool down, out bool held, out bool up)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        up   = Input.GetMouseButtonUp(0);
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
            up   = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
        }
#endif
    }

    // Ángulo del punto respecto al centro de la rueda
    float WorldAngleFromCenter(Vector2 worldPos)
    {
        Vector2 dir = worldPos - (Vector2)wheel.position;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    // --------------- INERCIA + DETECCIÓN ---------------
    void ApplySpin()
    {
        if (!dragging && Mathf.Abs(spinSpeed) > 0.1f)
        {
            wheel.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            // Ajustamos deceleración según el peso: más peso = se frena más lento
            float adjustedDecel = deceleration / wheelWeight;

            float sign = Mathf.Sign(spinSpeed);
            spinSpeed -= sign * adjustedDecel * Time.deltaTime;
            if (Mathf.Sign(spinSpeed) != sign) spinSpeed = 0f;

            wasMoving = true;
        }
        else if (!dragging && wasMoving && Mathf.Abs(spinSpeed) <= 0.1f)
        {
            wasMoving = false;
            int winner = GetCurrentSegmentIndex();
            Debug.Log($"🎯 Segmento ganador: {winner}");
        }
    }

    int GetCurrentSegmentIndex()
    {
        if (generator == null || generator.segmentCount <= 0) return 0;

        float z = wheel.eulerAngles.z;
        float step = 360f / generator.segmentCount;
        float angle = (360f - z) % 360f;
        int index = Mathf.FloorToInt(angle / step);
        return Mathf.Clamp(index, 0, generator.segmentCount - 1);
    }
}
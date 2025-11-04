using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Round1Manager : MonoBehaviour
{
    [Header("Refs")]
    public RouletteController controller; // Arrastra el de la escena
    public FlagPin flagPin;               // Tu pin manual (uno en Round1)

    [Header("Goal")]
    public int minHits = 4;
    public int maxHits = 8;

    [Header("Spin detection")]
    [Tooltip("Velocidad mínima de giro (grados/segundo) para empezar a contar impactos")]
    public float minRealSpinSpeed = 100f;
    [Tooltip("Tiempo que debe mantenerse por encima de esa velocidad")]
    public float stableSpinTime = 0.1f;

    private int hitsThisSpin = 0;
    private bool counting = false;
    private bool spinStarted = false;
    private float spinStartTimer = 0f;

    void Awake()
    {
        if (controller != null)
        {
            controller.OnSpinStart += HandleSpinStart;
            controller.OnSpinEnd += HandleSpinEnd;
        }

        if (flagPin != null)
            flagPin.round = this; // Cablear automáticamente
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnSpinStart -= HandleSpinStart;
            controller.OnSpinEnd -= HandleSpinEnd;
        }
    }

    void Update()
    {
        // Espera a que la ruleta coja velocidad antes de empezar a contar
        if (spinStarted && !counting && controller != null && controller.wheel != null)
        {
            float absSpeed = Mathf.Abs(controller.GetCurrentAngularVelocity());
            if (absSpeed > minRealSpinSpeed)
            {
                spinStartTimer += Time.deltaTime;
                if (spinStartTimer >= stableSpinTime)
                {
                    counting = true;
                    Debug.Log("🎯 Empezamos a contar impactos (la rueda ya gira de verdad)");
                }
            }
            else
            {
                spinStartTimer = 0f;
            }
        }
    }

    void HandleSpinStart()
    {
        hitsThisSpin = 0;
        counting = false;
        spinStarted = true;
        spinStartTimer = 0f;
        Debug.Log("⏱️ Tirada iniciada, esperando a que coja velocidad...");
    }

    void HandleSpinEnd()
    {
        if (!spinStarted) return;

        spinStarted = false;
        counting = false;

        bool win = hitsThisSpin >= minHits && hitsThisSpin <= maxHits;

        if (win)
            Debug.Log($"✅ WIN: {hitsThisSpin} impactos (objetivo {minHits}-{maxHits})");
        else
            Debug.Log($"❌ LOSE: {hitsThisSpin} impactos (objetivo {minHits}-{maxHits})");
    }

    public void RegisterPinHit(FlagPin p)
    {
        if (!counting) return; // Solo cuenta durante la tirada real
        hitsThisSpin++;
        // Debug.Log($"💥 Hit #{hitsThisSpin} en {p.name}");
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Flapper : MonoBehaviour
{
    [Header("Feedback")]
    public AudioSource audioSource;
    public AudioClip tickClip;
    public float nudgeAngle = 8f;
    public float nudgeTime = 0.06f;
    public float returnTime = 0.08f;

    [Header("Detection")]
    public float cooldownPerPin = 0.03f;     // más fino, permite registrar hits consecutivos
    public float minFlagHitInterval = 0.05f; // reduce lag entre flag hits

    private float _baseLocalZ;
    private Coroutine _activeCo;
    private Dictionary<Collider2D, float> lastHitTime = new Dictionary<Collider2D, float>();
    private float lastFlagHitTime = -999f;

    // 🔥 Referencia interna al controller (no afecta nada del resto del sistema)
    private RouletteController controller;

    void Awake()
    {
        _baseLocalZ = transform.localEulerAngles.z;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        var rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        controller = FindObjectOfType<RouletteController>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 🔥🔥🔥 BLOQUEAR FALSOS TICKS — EVITA EL BUG DE ACTIVAR STICKERS ARRASTRANDO 🔥🔥🔥
        if (controller != null && !controller.SpinInProgress)
            return;

        if (!other.isTrigger) return;

        float now = Time.time;

        // previene dobles registros del mismo pin
        if (lastHitTime.ContainsKey(other) && now - lastHitTime[other] < cooldownPerPin)
            return;

        bool validHit = false;
        var flagPin = other.GetComponent<FlagPin>();

        if (flagPin != null)
        {
            if (now - lastFlagHitTime < minFlagHitInterval)
                return;

            lastFlagHitTime = now;
            validHit = true;
            flagPin.RegisterHit();
        }
        else if (other.name.ToLower().Contains("pin"))
        {
            validHit = true;
        }

        if (!validHit) return;

        lastHitTime[other] = now;

        if (tickClip && audioSource)
            audioSource.PlayOneShot(tickClip);

        StartCoroutine(NudgeRoutine());
    }

    IEnumerator NudgeRoutine()
    {
        float startZ = Mathf.DeltaAngle(0f, transform.localEulerAngles.z);
        float targetZ = _baseLocalZ - nudgeAngle;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, nudgeTime);
            float z = Mathf.Lerp(startZ, targetZ, t);
            transform.localRotation = Quaternion.Euler(0f, 0f, z);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, returnTime);
            float z = Mathf.Lerp(targetZ, _baseLocalZ, t);
            transform.localRotation = Quaternion.Euler(0f, 0f, z);
            yield return null;
        }
    }
}
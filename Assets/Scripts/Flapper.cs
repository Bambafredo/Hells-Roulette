using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Flapper : MonoBehaviour
{
    [Header("Feedback")]
    public AudioSource audioSource;   // arrastra si quieres, si no, puedes añadirlo al GameObject
    public AudioClip tickClip;        // tu clip placeholder
    [Tooltip("Grados de 'golpecito' al tocar un pin")]
    public float nudgeAngle = 8f;
    [Tooltip("Tiempo de ir hacia atrás (s)")]
    public float nudgeTime = 0.06f;
    [Tooltip("Tiempo de volver a reposo (s)")]
    public float returnTime = 0.08f;

    [Header("Limits")]
    [Tooltip("Evita doble tick en el mismo borde si vas muy lento")]
    public float cooldown = 0.03f;

    float _lastTickTime = -999f;
    float _baseLocalZ;
    Coroutine _activeCo;

    void Awake()
    {
        _baseLocalZ = transform.localEulerAngles.z;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Solo reaccionamos a pins de la rueda (que son triggers)
        if (!other.isTrigger) return;
        if (Time.time - _lastTickTime < cooldown) return;

        _lastTickTime = Time.time;

        if (tickClip != null && audioSource != null)
            audioSource.PlayOneShot(tickClip);

        DoTick();
    }

    /// <summary>
    /// Permite disparar el tick desde código (por detección angular, etc.)
    /// </summary>
    public void DoTick()
    {
        if (_activeCo != null) StopCoroutine(_activeCo);
        _activeCo = StartCoroutine(NudgeRoutine());
    }

    IEnumerator NudgeRoutine()
    {
        // Lerp hacia atrás
        float startZ = Normalize180(transform.localEulerAngles.z);
        float targetZ = _baseLocalZ - nudgeAngle;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, nudgeTime);
            float z = Mathf.Lerp(startZ, targetZ, t);
            transform.localRotation = Quaternion.Euler(0f, 0f, z);
            yield return null;
        }

        // Volver a base
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, returnTime);
            float z = Mathf.Lerp(targetZ, _baseLocalZ, t);
            transform.localRotation = Quaternion.Euler(0f, 0f, z);
            yield return null;
        }

        _activeCo = null;
    }

    // Normaliza ángulo a rango [-180, 180]
    float Normalize180(float z)
    {
        return Mathf.DeltaAngle(0f, z);
    }
}

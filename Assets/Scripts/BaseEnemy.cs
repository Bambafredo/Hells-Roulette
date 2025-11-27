using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BaseEnemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    public string enemyName = "Enemy";
    public int maxHP = 10;
    public int damageToPlayer = 1;

    [Header("References")]
    public TextMeshPro hpText;
    public SpriteRenderer sprite;

    private int currentHP;
    private bool isDead = false;
    private RouletteController roulette;

    private void Start()
    {
        currentHP = maxHP;
        UpdateHPDisplay();

        roulette = FindObjectOfType<RouletteController>();
        if (roulette != null)
            roulette.OnSpinEnd += OnSpinEnd;
    }

    private void OnDestroy()
    {
        if (roulette != null)
            roulette.OnSpinEnd -= OnSpinEnd;
    }

    private void OnSpinEnd()
    {
        if (isDead) return;

        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log($"⚠️ {enemyName} NO ataca (ronda no válida).");
            return;
        }

        StartCoroutine(AttackAnimation());

        if (BloodManager.Instance != null)
        {
            BloodManager.Instance.ConsumeBlood(damageToPlayer);
            Debug.Log($"💀 {enemyName} inflige {damageToPlayer} de daño al jugador!");
        }
    }

    public void TakeDamage(int dmg)
    {
        if (isDead) return;

        currentHP -= dmg;
        UpdateHPDisplay();
        StartCoroutine(HitFlash());

        if (currentHP <= 0)
            Die();
    }

    private IEnumerator HitFlash()
    {
        if (sprite != null)
        {
            Color original = sprite.color;
            sprite.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            sprite.color = original;
        }
    }

    private IEnumerator AttackAnimation()
    {
        Transform t = transform;

        Vector3 original = t.localScale;
        Vector3 big = original * 1.12f;

        float speed = 0.1f;

        float timer = 0f;
        while (timer < speed)
        {
            t.localScale = Vector3.Lerp(original, big, timer / speed);
            timer += Time.deltaTime;
            yield return null;
        }
        t.localScale = big;

        timer = 0f;
        while (timer < speed)
        {
            t.localScale = Vector3.Lerp(big, original, timer / speed);
            timer += Time.deltaTime;
            yield return null;
        }

        t.localScale = original;
    }

    private void Die()
    {
        isDead = true;
        Debug.Log($"☠️ {enemyName} ha muerto.");
        Destroy(gameObject, 0.1f);
    }

    private void UpdateHPDisplay()
    {
        if (hpText != null)
            hpText.text = currentHP.ToString();
    }
}

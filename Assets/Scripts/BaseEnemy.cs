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
    public TextMeshPro hpText;        // Número encima del sprite
    public SpriteRenderer sprite;     // Para efectos visuales opcionales

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

    // 🔔 Cuando termina la tirada
    private void OnSpinEnd()
    {
        if (isDead) return;

        // Si sigue vivo al terminar la tirada, daña al jugador
        if (BloodManager.Instance != null)
        {
            BloodManager.Instance.ConsumeBlood(damageToPlayer);
            Debug.Log($"💀 {enemyName} inflige {damageToPlayer} de daño al jugador!");
        }
    }

    // ⚔️ Recibir daño
    public void TakeDamage(int dmg)
    {
        if (isDead) return;

        currentHP -= dmg;
        UpdateHPDisplay();
        StartCoroutine(HitFlash());

        if (currentHP <= 0)
            Die();
    }

    // 💥 Efecto visual de golpe (opcional)
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

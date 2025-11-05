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

    private void Start()
    {
        currentHP = maxHP;
        UpdateHPDisplay();

        var roulette = FindObjectOfType<RouletteController>();
        if (roulette != null)
            roulette.OnSpinEnd += OnSpinEnd;
    }

    private void OnDestroy()
    {
        var roulette = FindObjectOfType<RouletteController>();
        if (roulette != null)
            roulette.OnSpinEnd -= OnSpinEnd;
    }

    private void OnSpinEnd()
    {
        if (isDead) return;

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

        if (currentHP <= 0)
            Die();
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

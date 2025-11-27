using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StickerSword", menuName = "Stickers/Sticker Sword")]
public class StickerSword : StickerEffect
{
    [Header("Damage")]
    public int damageAmount = 5;

    public override void ApplyEffect()
    {
        // 1. Validación de ronda
        if (RoundManager.Instance != null && !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log("❌ StickerSword no activa daño porque la ronda no fue válida.");
            return;
        }

        // 2. Encontrar el EnemyPanel dinámicamente
        EnemyPanelManager enemyPanel = Object.FindObjectOfType<EnemyPanelManager>();

        if (enemyPanel == null)
        {
            Debug.LogWarning("⚠️ StickerSword: No se encontró EnemyPanelManager en la escena.");
            return;
        }

        // 3. Buscar enemigo más a la izquierda
        BaseEnemy target = enemyPanel.GetLeftmostAliveEnemy();

        if (target == null)
        {
            Debug.Log("⚠️ StickerSword: No hay enemigos vivos a los que golpear.");
            return;
        }

        // 4. Aplicar daño
        target.TakeDamage(damageAmount);
        Debug.Log($"🗡️ StickerSword inflige {damageAmount} de daño a {target.enemyName}!");
    }
}

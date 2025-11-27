using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyPanelManager : MonoBehaviour
{
    [Header("Enemy Slots (root GameObjects)")]
    public Transform[] enemySlots;  
    // Asigna aquí Enemyslot_1, Enemyslot_2, etc.

    public BaseEnemy GetLeftmostAliveEnemy()
    {
        foreach (var slot in enemySlots)
        {
            if (slot == null) continue;

            // Buscar el enemigo dentro del slot
            var enemy = slot.GetComponentInChildren<BaseEnemy>(true);

            if (enemy != null && enemy.gameObject.activeInHierarchy)
                return enemy;
        }

        return null; // no hay enemigos
    }
}

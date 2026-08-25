using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BaseEnemy : MonoBehaviour
{
    // =========================================================
    // STATS
    // =========================================================

    [Header("Enemy Stats")]
    public string enemyName = "Enemy";
    public int maxHP = 10;
    public int damageToPlayer = 1;


    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    public TextMeshPro hpText;
    public SpriteRenderer sprite;


    // =========================================================
    // STATE
    // =========================================================

    private int currentHP;
    private bool isDead = false;
    private bool combatActive = true;
    private RouletteController roulette;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool CombatActive
    {
        get { return combatActive; }
    }


    public bool IsDead
    {
        get { return isDead; }
    }


    public int CurrentHP
    {
        get { return currentHP; }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Start()
    {
        currentHP = maxHP;

        UpdateHPDisplay();


        roulette =
            FindObjectOfType<RouletteController>();


        if (roulette != null)
        {
            roulette.OnSpinEnd +=
                OnSpinEnd;
        }
    }


    private void OnDestroy()
    {
        if (roulette != null)
        {
            roulette.OnSpinEnd -=
                OnSpinEnd;
        }
    }


    // =========================================================
    // COMBAT ACTIVATION
    // =========================================================

    public void SetCombatActive(
        bool active)
    {
        combatActive =
            active;
    }


    // =========================================================
    // ENEMY TURN
    // =========================================================

    private void OnSpinEnd()
    {
        if (isDead)
            return;


        /*
         * Enemies in Next/Future corridor rows stay fully visible and
         * initialized, but they are previews only. Event subscriptions
         * still exist, so this explicit combat gate is what prevents them
         * from attacking before they reach CurrentRow.
         */
        if (!combatActive)
            return;


        // -----------------------------------------------------
        // INVALID SPIN
        // -----------------------------------------------------

        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            Debug.Log(
                $"[ENEMY] {enemyName} does not attack because the spin was invalid."
            );

            return;
        }


        // -----------------------------------------------------
        // ATTACK ANIMATION
        // -----------------------------------------------------

        StartCoroutine(
            AttackAnimation()
        );


        // -----------------------------------------------------
        // ATTACK PLAYER
        // -----------------------------------------------------

        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                $"[ENEMY] {enemyName} cannot attack because BloodManager is missing."
            );

            return;
        }


        int bloodBefore =
            BloodManager.Instance.currentBlood;


        bool consumed =
            BloodManager.Instance
                .ConsumeBlood(
                    damageToPlayer
                );


        int bloodAfter =
            BloodManager.Instance.currentBlood;


        int actualBloodLost =
            Mathf.Max(
                0,
                bloodBefore - bloodAfter
            );


        /*
         * We log the ACTUAL Blood lost rather than blindly using
         * damageToPlayer. This remains correct if the player has
         * less Blood remaining than the enemy's nominal damage.
         */
        if (consumed)
        {
            GameLogManager.Instance?
                .LogEnemyAttack(
                    enemyName,
                    actualBloodLost
                );
        }


        Debug.Log(
            $"[ENEMY] {enemyName} attacks for {actualBloodLost} Blood."
        );
    }


    // =========================================================
    // DAMAGE
    // =========================================================

    public void TakeDamage(
        int dmg)
    {
        if (isDead)
            return;


        currentHP -= dmg;

        UpdateHPDisplay();

        StartCoroutine(
            HitFlash()
        );


        if (currentHP <= 0)
        {
            Die();
        }
    }


    // =========================================================
    // HIT FEEDBACK
    // =========================================================

    private IEnumerator HitFlash()
    {
        if (sprite != null)
        {
            Color original =
                sprite.color;


            sprite.color =
                Color.white;


            yield return
                new WaitForSeconds(
                    0.1f
                );


            sprite.color =
                original;
        }
    }


    // =========================================================
    // ATTACK FEEDBACK
    // =========================================================

    private IEnumerator AttackAnimation()
    {
        Transform t =
            transform;


        Vector3 original =
            t.localScale;


        Vector3 big =
            original * 1.12f;


        float speed =
            0.1f;


        float timer =
            0f;


        while (timer < speed)
        {
            t.localScale =
                Vector3.Lerp(
                    original,
                    big,
                    timer / speed
                );


            timer +=
                Time.deltaTime;


            yield return null;
        }


        t.localScale =
            big;


        timer =
            0f;


        while (timer < speed)
        {
            t.localScale =
                Vector3.Lerp(
                    big,
                    original,
                    timer / speed
                );


            timer +=
                Time.deltaTime;


            yield return null;
        }


        t.localScale =
            original;
    }


    // =========================================================
    // DEATH
    // =========================================================

    private void Die()
    {
        if (isDead)
            return;


        isDead = true;


        /*
         * Die() is reached synchronously from TakeDamage(), so this
         * line enters the Game Log immediately after the sticker/event
         * that caused the lethal damage.
         */
        GameLogManager.Instance?
            .LogEnemyDeath(
                enemyName
            );


        Debug.Log(
            $"[ENEMY] {enemyName} dies."
        );


        Destroy(
            gameObject,
            0.1f
        );
    }


    // =========================================================
    // UI
    // =========================================================

    private void UpdateHPDisplay()
    {
        if (hpText != null)
        {
            hpText.text =
                currentHP.ToString();
        }
    }
}

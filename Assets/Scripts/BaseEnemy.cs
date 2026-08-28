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

    public string enemyName =
        "Enemy";

    public int maxHP =
        10;


    // =========================================================
    // ACTION SEQUENCE
    // =========================================================

    public enum ActionSequenceOrderMode
    {
        Fixed,
        ShuffleOnceOnSpawn
    }


    [Header("Action Sequence")]

    [Tooltip(
        "Fixed keeps the authored order. Shuffle Once On Spawn creates an " +
        "independent random order for each enemy instance, then repeats that " +
        "same runtime pattern for extra spins."
    )]
    public ActionSequenceOrderMode actionSequenceOrderMode =
        ActionSequenceOrderMode.Fixed;


    [Tooltip(
        "Actions resolved after each VALID spin while this enemy is in " +
        "CurrentRow. The runtime sequence loops automatically."
    )]
    public EnemyAction[] actionSequence =
        new EnemyAction[3];


    // =========================================================
    // CURSE
    // =========================================================

    [Header("Curse")]

    [Tooltip(
        "Optional persistent effect. The curse is active only while this " +
        "enemy is alive AND combat-active in CurrentRow."
    )]
    public EnemyCurse curse;

    [Tooltip(
        "Generic numeric value passed to this enemy's curse. " +
        "For Debt Increase, this is the percentage."
    )]
    [Min(0)]
    public int curseValue =
        0;


    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]

    public TextMeshPro hpText;

    public SpriteRenderer sprite;

    [Tooltip(
        "SpriteRenderer used to show the enemy's CURRENT action. " +
        "It is only enabled while this enemy is combat-active in CurrentRow."
    )]
    public SpriteRenderer actionIconRenderer;

    [Tooltip(
        "SpriteRenderer used to show this enemy's Curse. " +
        "It is hidden when the enemy has no curse, is dead, or is outside " +
        "CurrentRow."
    )]
    public SpriteRenderer curseIconRenderer;


    // =========================================================
    // STATE
    // =========================================================

    private int currentHP;

    private bool isDead =
        false;

    private bool combatActive =
        true;

    private int currentActionIndex =
        0;

    private EnemyAction[] runtimeActionSequence;

    private bool curseActive =
        false;

    private RouletteController roulette;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public string EnemyName
    {
        get { return enemyName; }
    }


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


    public int CurrentActionIndex
    {
        get { return currentActionIndex; }
    }


    public EnemyAction CurrentAction
    {
        get
        {
            return
                GetActionForIndex(
                    currentActionIndex
                );
        }
    }


    public EnemyCurse CurrentCurse
    {
        get { return curse; }
    }


    public int CurseValue
    {
        get { return curseValue; }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        BuildRuntimeActionSequence();
    }


    private void Start()
    {
        currentHP =
            maxHP;

        currentActionIndex =
            0;


        UpdateHPDisplay();

        UpdateActionFeedback();

        UpdateCurseFeedback();


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
        DeactivateCurseIfNeeded();


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


        /*
         * Next/Future enemies remain visible, but their action and curse
         * feedback stay hidden. Persistent curses are gameplay-active only
         * while the enemy is in CurrentRow.
         */
        UpdateActionFeedback();

        RefreshCurseState();
    }


    // =========================================================
    // ENEMY ACTION TURN
    // =========================================================

    private void OnSpinEnd()
    {
        if (isDead)
            return;


        /*
         * Enemies in Next/Future are previews only.
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
                $"[ENEMY] {enemyName} keeps action " +
                $"{GetCurrentActionDebugName()} because the spin was invalid."
            );

            return;
        }


        // -----------------------------------------------------
        // EXECUTE CURRENT ACTION
        // -----------------------------------------------------

        EnemyAction action =
            CurrentAction;


        if (action != null)
        {
            Debug.Log(
                $"[ENEMY] {enemyName} executes action: " +
                $"{action.ActionName}."
            );


            action.Execute(
                this
            );
        }
        else
        {
            Debug.LogWarning(
                $"[ENEMY] {enemyName} has no action assigned at " +
                $"sequence index {currentActionIndex}."
            );
        }


        // -----------------------------------------------------
        // ADVANCE PATTERN
        // -----------------------------------------------------

        AdvanceActionSequence();
    }


    // =========================================================
    // ACTION SEQUENCE
    // =========================================================

    private void BuildRuntimeActionSequence()
    {
        if (actionSequence == null ||
            actionSequence.Length == 0)
        {
            runtimeActionSequence =
                new EnemyAction[0];

            return;
        }


        runtimeActionSequence =
            new EnemyAction[
                actionSequence.Length
            ];


        for (int i = 0;
             i < actionSequence.Length;
             i++)
        {
            runtimeActionSequence[i] =
                actionSequence[i];
        }


        if (actionSequenceOrderMode ==
            ActionSequenceOrderMode.ShuffleOnceOnSpawn)
        {
            ShuffleRuntimeActionSequence();
        }
    }


    private void ShuffleRuntimeActionSequence()
    {
        if (runtimeActionSequence == null)
            return;


        /*
         * Fisher-Yates shuffle.
         *
         * Example for the Imp:
         * Attack / Wait / Attack
         *
         * Each fresh Imp independently becomes one of:
         * A-W-A, W-A-A or A-A-W.
         *
         * The chosen runtime order then stays stable and loops if the
         * player gains extra spins.
         */
        for (int i =
                 runtimeActionSequence.Length - 1;
             i > 0;
             i--)
        {
            int j =
                Random.Range(
                    0,
                    i + 1
                );


            EnemyAction temp =
                runtimeActionSequence[i];

            runtimeActionSequence[i] =
                runtimeActionSequence[j];

            runtimeActionSequence[j] =
                temp;
        }
    }


    private EnemyAction GetActionForIndex(
        int index)
    {
        if (runtimeActionSequence == null)
        {
            BuildRuntimeActionSequence();
        }


        if (runtimeActionSequence == null ||
            runtimeActionSequence.Length == 0)
        {
            return null;
        }


        int safeIndex =
            index %
            runtimeActionSequence.Length;


        if (safeIndex < 0)
        {
            safeIndex +=
                runtimeActionSequence.Length;
        }


        return
            runtimeActionSequence[
                safeIndex
            ];
    }


    private void AdvanceActionSequence()
    {
        if (runtimeActionSequence == null ||
            runtimeActionSequence.Length == 0)
        {
            return;
        }


        currentActionIndex =
            (
                currentActionIndex +
                1
            ) %
            runtimeActionSequence.Length;


        /*
         * The icon immediately changes to show what will happen after the
         * NEXT valid spin.
         */
        UpdateActionFeedback();
    }


    private string GetCurrentActionDebugName()
    {
        EnemyAction action =
            CurrentAction;


        if (action == null)
            return "<none>";


        return
            action.ActionName;
    }


    // =========================================================
    // CURSE
    // =========================================================

    private void RefreshCurseState()
    {
        bool shouldBeActive =
            combatActive &&
            !isDead &&
            curse != null;


        if (shouldBeActive)
        {
            ActivateCurseIfNeeded();
        }
        else
        {
            DeactivateCurseIfNeeded();
        }


        UpdateCurseFeedback();
    }


    private void ActivateCurseIfNeeded()
    {
        if (curseActive ||
            curse == null)
        {
            return;
        }


        curseActive =
            true;


        curse.Activate(
            this,
            curseValue
        );


        Debug.Log(
            $"[ENEMY CURSE] {enemyName}: " +
            $"{curse.CurseName} activated " +
            $"(value {curseValue})."
        );
    }


    private void DeactivateCurseIfNeeded()
    {
        if (!curseActive ||
            curse == null)
        {
            curseActive =
                false;

            UpdateCurseFeedback();

            return;
        }


        curse.Deactivate(
            this,
            curseValue
        );


        curseActive =
            false;


        Debug.Log(
            $"[ENEMY CURSE] {enemyName}: " +
            $"{curse.CurseName} removed."
        );


        UpdateCurseFeedback();
    }


    private void UpdateCurseFeedback()
    {
        if (curseIconRenderer == null)
            return;


        bool shouldShow =
            combatActive &&
            !isDead &&
            curse != null &&
            curse.Icon != null;


        if (curse != null)
        {
            curseIconRenderer.sprite =
                curse.Icon;
        }


        /*
         * Same setup as Action_Icon:
         * the whole child GameObject may be disabled in the prefab.
         */
        curseIconRenderer.gameObject
            .SetActive(
                shouldShow
            );


        curseIconRenderer.enabled =
            shouldShow;
    }


    // =========================================================
    // ACTION FEEDBACK
    // =========================================================

    private void UpdateActionFeedback()
    {
        if (actionIconRenderer == null)
            return;


        EnemyAction action =
            CurrentAction;


        bool shouldShow =
            combatActive &&
            !isDead &&
            action != null &&
            action.Icon != null;


        if (action != null)
        {
            actionIconRenderer.sprite =
                action.Icon;
        }


        /*
         * The Action_Icon child may be disabled in the prefab.
         *
         * Toggling SpriteRenderer.enabled is not enough when the whole
         * GameObject is inactive, so we explicitly toggle the child GO too.
         */
        actionIconRenderer.gameObject
            .SetActive(
                shouldShow
            );


        actionIconRenderer.enabled =
            shouldShow;


    }


    // =========================================================
    // ACTION API
    // =========================================================

    /*
     * EnemyAction assets call public methods like this one.
     *
     * BaseEnemy owns the actual gameplay integrations (BloodManager,
     * GameLog, animations, etc.), while the EnemyAction asset only decides
     * WHAT the enemy does.
     *
     * This keeps actions reusable between different enemy prefabs.
     */
    public void PerformBloodAttack(
        int damage)
    {
        if (isDead ||
            damage <= 0)
        {
            return;
        }


        StartCoroutine(
            AttackAnimation()
        );


        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                $"[ENEMY] {enemyName} cannot attack because " +
                $"BloodManager is missing."
            );

            return;
        }


        int bloodBefore =
            BloodManager.Instance.currentBlood;


        bool consumed =
            BloodManager.Instance
                .ConsumeBlood(
                    damage
                );


        int bloodAfter =
            BloodManager.Instance.currentBlood;


        int actualBloodLost =
            Mathf.Max(
                0,
                bloodBefore -
                bloodAfter
            );


        if (consumed)
        {
            GameLogManager.Instance?
                .LogEnemyAttack(
                    enemyName,
                    actualBloodLost
                );
        }


        Debug.Log(
            $"[ENEMY] {enemyName} attacks for " +
            $"{actualBloodLost} Blood."
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


        currentHP -=
            dmg;


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
            original *
            1.12f;


        float speed =
            0.1f;


        float timer =
            0f;


        while (timer <
               speed)
        {
            t.localScale =
                Vector3.Lerp(
                    original,
                    big,
                    timer /
                    speed
                );


            timer +=
                Time.deltaTime;


            yield return null;
        }


        t.localScale =
            big;


        timer =
            0f;


        while (timer <
               speed)
        {
            t.localScale =
                Vector3.Lerp(
                    big,
                    original,
                    timer /
                    speed
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


        isDead =
            true;


        /*
         * A persistent curse ends immediately when its owner dies.
         * This happens before round-end debt is collected.
         */
        RefreshCurseState();

        UpdateActionFeedback();


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

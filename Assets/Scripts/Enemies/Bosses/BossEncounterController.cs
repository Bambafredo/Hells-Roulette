using UnityEngine;

/// <summary>
/// Small generic lifecycle shell for boss encounters.
///
/// Bosses remain normal BaseEnemy instances for HP, damage, death, curses,
/// action sequences and combat activation. This component only owns the
/// encounter rules that differ from a normal enemy row.
/// </summary>
[RequireComponent(typeof(BaseEnemy))]
public abstract class BossEncounterController : MonoBehaviour
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Boss Encounter")]

    [Tooltip(
        "For the current prototype, defeating the boss immediately advances " +
        "the enemy corridor by one encounter without advancing RoundManager."
    )]
    public bool advanceCorridorOnDefeat =
        true;


    // =========================================================
    // REFERENCES
    // =========================================================

    protected BaseEnemy Enemy
    {
        get;
        private set;
    }

    protected RoundManager RoundManagerRef
    {
        get;
        private set;
    }

    protected EnemyCorridorController Corridor
    {
        get;
        private set;
    }


    // =========================================================
    // STATE
    // =========================================================

    private bool encounterActive =
        false;

    private bool encounterEverActivated =
        false;

    private bool defeatHandled =
        false;


    public bool EncounterActive =>
        encounterActive;


    // =========================================================
    // UNITY
    // =========================================================

    protected virtual void Awake()
    {
        Enemy =
            GetComponent<BaseEnemy>();
    }


    protected virtual void Start()
    {
        ResolveReferences();
        Subscribe();
        RefreshEncounterState();
    }


    protected virtual void Update()
    {
        RefreshEncounterState();


        /*
         * Normal gameplay defeat is caught by
         * OnGameplaySpinResolutionCompleted. This fallback also makes debug
         * damage / kills outside a spin behave sensibly.
         */
        if (encounterEverActivated &&
            Enemy != null &&
            Enemy.IsDead &&
            !defeatHandled)
        {
            bool spinStillResolving =
                RouletteController.Instance != null &&
                RouletteController.Instance.SpinInProgress;


            if (!spinStillResolving)
            {
                HandleBossDefeated();
            }
        }
    }


    protected virtual void OnDestroy()
    {
        Unsubscribe();


        if (encounterActive &&
            !defeatHandled)
        {
            encounterActive =
                false;

            OnBossEncounterDeactivated();
        }
    }


    // =========================================================
    // REFERENCES / SUBSCRIPTIONS
    // =========================================================

    private void ResolveReferences()
    {
        RoundManagerRef =
            RoundManager.Instance != null
                ? RoundManager.Instance
                : FindObjectOfType<RoundManager>();


        if (RoundManagerRef != null &&
            RoundManagerRef.enemyCorridorController != null)
        {
            Corridor =
                RoundManagerRef.enemyCorridorController;
        }
        else
        {
            Corridor =
                FindObjectOfType<EnemyCorridorController>();
        }
    }


    private void Subscribe()
    {
        if (RoundManagerRef == null)
            return;


        RoundManagerRef.OnSpinValidated +=
            HandleSpinValidated;

        RoundManagerRef.OnGameplaySpinResolutionCompleted +=
            HandleGameplaySpinResolutionCompleted;
    }


    private void Unsubscribe()
    {
        if (RoundManagerRef == null)
            return;


        RoundManagerRef.OnSpinValidated -=
            HandleSpinValidated;

        RoundManagerRef.OnGameplaySpinResolutionCompleted -=
            HandleGameplaySpinResolutionCompleted;
    }


    // =========================================================
    // COMBAT-ACTIVE LIFECYCLE
    // =========================================================

    private void RefreshEncounterState()
    {
        bool shouldBeActive =
            Enemy != null &&
            Enemy.CombatActive &&
            !Enemy.IsDead;


        if (shouldBeActive ==
            encounterActive)
        {
            return;
        }


        encounterActive =
            shouldBeActive;


        if (encounterActive)
        {
            encounterEverActivated =
                true;

            OnBossEncounterActivated();
        }
        else if (!defeatHandled &&
                 Enemy != null &&
                 !Enemy.IsDead)
        {
            OnBossEncounterDeactivated();
        }
    }


    // =========================================================
    // ROUND HOOKS
    // =========================================================

    private void HandleSpinValidated(
        bool valid)
    {
        if (!valid ||
            !encounterActive ||
            Enemy == null ||
            Enemy.IsDead)
        {
            return;
        }


        OnBossValidSpinValidated();
    }


    private void HandleGameplaySpinResolutionCompleted()
    {
        if (!encounterEverActivated ||
            defeatHandled ||
            Enemy == null ||
            !Enemy.IsDead)
        {
            return;
        }


        HandleBossDefeated();
    }


    // =========================================================
    // DEFEAT
    // =========================================================

    private void HandleBossDefeated()
    {
        if (defeatHandled)
            return;


        defeatHandled =
            true;

        encounterActive =
            false;


        OnBossDefeated();


        if (advanceCorridorOnDefeat &&
            Corridor != null)
        {
            /*
             * This method is deliberately used instead of AdvanceEncounter().
             * Boss death is normally detected while RouletteController is still
             * finishing the current spin resolution. The corridor coroutine is
             * safe to queue at this point and blocks new spins while moving.
             *
             * IMPORTANT: RoundManager itself is NOT advanced here. The boss is
             * an encounter inserted into the current round flow.
             */
            Corridor.AdvanceEncounterForRoundTransition();
        }
    }


    // =========================================================
    // EXTENSION POINTS
    // =========================================================

    protected virtual void OnBossEncounterActivated()
    {
    }


    protected virtual void OnBossEncounterDeactivated()
    {
    }


    protected virtual void OnBossValidSpinValidated()
    {
    }


    protected virtual void OnBossDefeated()
    {
    }
}

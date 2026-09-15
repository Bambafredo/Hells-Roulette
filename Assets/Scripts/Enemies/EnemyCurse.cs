using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyCurse : ScriptableObject
{
    // =========================================================
    // PRESENTATION
    // =========================================================

    [Header("Presentation")]

    [SerializeField]
    private string curseName =
        "Curse";

    [SerializeField]
    private Sprite icon;

    [SerializeField]
    [TextArea(2, 5)]
    [Tooltip(
        "Reserved for the future Curse tooltip system. " +
        "Use {value} for the enemy-specific numeric value."
    )]
    private string tooltipDescription =
        "";


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public string CurseName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(
                curseName))
            {
                return name;
            }

            return curseName;
        }
    }


    public Sprite Icon
    {
        get { return icon; }
    }


    /// <summary>
    /// Generic targeting rules used by single-target sticker attacks.
    ///
    /// Most Curses leave both false. A Curse such as Untouchable can opt into
    /// one spin method without EnemyPanelManager needing to know about a
    /// concrete Curse type. BaseEnemy only exposes these rules while the Curse
    /// is actually active.
    /// </summary>
    public virtual bool BlocksPowerSpinSingleTargeting
    {
        get { return false; }
    }


    public virtual bool BlocksLuckyShotSingleTargeting
    {
        get { return false; }
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    public virtual string GetTooltipDescription(
        BaseEnemy enemy,
        int value)
    {
        if (string.IsNullOrWhiteSpace(
            tooltipDescription))
        {
            return "";
        }


        return
            tooltipDescription.Replace(
                "{value}",
                value.ToString()
            );
    }


    // =========================================================
    // LIFECYCLE
    // =========================================================

    public abstract void Activate(
        BaseEnemy enemy,
        int value);


    public abstract void Deactivate(
        BaseEnemy enemy,
        int value);
}

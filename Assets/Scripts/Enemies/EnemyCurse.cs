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

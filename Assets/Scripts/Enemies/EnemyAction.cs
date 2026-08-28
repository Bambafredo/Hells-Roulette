using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyAction : ScriptableObject
{
    // =========================================================
    // PRESENTATION
    // =========================================================

    [Header("Presentation")]

    [SerializeField]
    private string actionName =
        "Action";

    [SerializeField]
    private Sprite icon;

    [SerializeField]
    [TextArea(2, 5)]
    [Tooltip(
        "Text shown when hovering the action icon. " +
        "Individual action types may support dynamic tokens."
    )]
    private string tooltipDescription =
        "";


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public string ActionName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(
                actionName))
            {
                return name;
            }

            return actionName;
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
        BaseEnemy enemy)
    {
        return
            tooltipDescription;
    }


    // =========================================================
    // EXECUTION
    // =========================================================

    public abstract void Execute(
        BaseEnemy enemy);
}

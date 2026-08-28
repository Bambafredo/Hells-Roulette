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
    // EXECUTION
    // =========================================================

    public abstract void Execute(
        BaseEnemy enemy);
}

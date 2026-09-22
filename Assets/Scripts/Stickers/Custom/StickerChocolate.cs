using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerChocolate",
    menuName = "Stickers/Sticker Chocolate"
)]
public class StickerChocolate : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Chocolate")]

    [Tooltip(
        "Blood recovered when Chocolate resolves on either a winning or " +
        "non-winning segment."
    )]
    [Min(0)]
    public int bloodGain =
        10;

    [Tooltip(
        "Chance, from 0 to 100, to earn the configured Golden Ticket when " +
        "Chocolate resolves on a winning segment."
    )]
    [Range(0f, 100f)]
    public float goldenTicketChance =
        10f;

    [Tooltip(
        "Exact Golden Ticket physical sticker prefab awarded on a successful " +
        "winning roll."
    )]
    public GameObject goldenTicketPrefab;


    // =========================================================
    // EFFECT
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        if (owner == null)
        {
            Debug.LogWarning(
                "StickerChocolate: BaseSticker owner was not provided."
            );

            return;
        }


        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                ResolveWinningSegment(
                    owner
                );
                break;


            case StickerSpinLocation.NonWinningSegment:
                ResolveNonWinningSegment(
                    owner
                );
                break;
        }
    }


    // =========================================================
    // WINNING
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int safeBloodGain =
            Mathf.Max(
                0,
                bloodGain
            );


        bool goldenTicketWon =
            RollGoldenTicket();


        bool goldenTicketQueued =
            false;


        if (goldenTicketWon)
        {
            if (goldenTicketPrefab == null)
            {
                Debug.LogWarning(
                    "[CHOCOLATE] Golden Ticket roll succeeded, but no " +
                    "Golden Ticket prefab is assigned."
                );
            }
            else if (RewardManager.Instance == null)
            {
                Debug.LogWarning(
                    "[CHOCOLATE] Golden Ticket roll succeeded, but " +
                    "RewardManager is missing."
                );
            }
            else
            {
                goldenTicketQueued =
                    RewardManager.Instance
                        .RequestFreeStickerReward(
                            goldenTicketPrefab,
                            stickerName
                        );
            }
        }


        string description =
            goldenTicketQueued
                ? $"Recover {safeBloodGain} Blood. Golden Ticket won"
                : $"Recover {safeBloodGain} Blood";


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            description,
            0,
            null
        );


        RecoverBlood(
            safeBloodGain
        );


        Debug.Log(
            goldenTicketQueued
                ? $"[CHOCOLATE] Recovered {safeBloodGain} Blood and won a Golden Ticket."
                : $"[CHOCOLATE] Recovered {safeBloodGain} Blood. No Golden Ticket."
        );
    }


    // =========================================================
    // LOSING
    // =========================================================

    private void ResolveNonWinningSegment(
        BaseSticker owner)
    {
        int safeBloodGain =
            Mathf.Max(
                0,
                bloodGain
            );


        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            $"Recover {safeBloodGain} Blood",
            0,
            null
        );


        RecoverBlood(
            safeBloodGain
        );


        Debug.Log(
            $"[CHOCOLATE] Recovered {safeBloodGain} Blood."
        );
    }


    // =========================================================
    // GOLDEN TICKET
    // =========================================================

    private bool RollGoldenTicket()
    {
        float safeChance =
            Mathf.Clamp(
                goldenTicketChance,
                0f,
                100f
            );


        if (safeChance <= 0f)
            return false;


        if (safeChance >= 100f)
            return true;


        return
            Random.value <
            safeChance / 100f;
    }


    // =========================================================
    // BLOOD
    // =========================================================

    private void RecoverBlood(
        int amount)
    {
        if (amount <= 0)
            return;


        if (BloodManager.Instance == null)
        {
            Debug.LogWarning(
                "[CHOCOLATE] BloodManager missing. Blood could not be recovered."
            );

            return;
        }


        BloodManager.Instance
            .HealBlood(
                amount
            );
    }


    // =========================================================
    // TOOLTIP
    // =========================================================

    protected override bool SupportsTooltipLocation(
        StickerSpinLocation location)
    {
        return
            location ==
                StickerSpinLocation.WinningSegment ||
            location ==
                StickerSpinLocation.NonWinningSegment;
    }


    protected override string ResolveTooltipTokens(
        BaseSticker owner,
        StickerSpinLocation location,
        string template)
    {
        string resolved =
            base.ResolveTooltipTokens(
                owner,
                location,
                template
            );


        return
            resolved
                .Replace(
                    "{blood}",
                    Mathf.Max(
                        0,
                        bloodGain
                    )
                    .ToString()
                )
                .Replace(
                    "{ticketChance}",
                    Mathf.Clamp(
                        goldenTicketChance,
                        0f,
                        100f
                    )
                    .ToString("0.##")
                )
                .Replace(
                    "{goldenTicket}",
                    GetGoldenTicketName()
                );
    }


    private string GetGoldenTicketName()
    {
        if (goldenTicketPrefab == null)
            return "Golden Ticket";


        BaseSticker ticketSticker =
            goldenTicketPrefab
                .GetComponentInChildren<BaseSticker>(
                    true
                );


        if (ticketSticker != null &&
            ticketSticker.effect != null &&
            !string.IsNullOrWhiteSpace(
                ticketSticker.effect.stickerName
            ))
        {
            return
                ticketSticker.effect.stickerName;
        }


        return
            goldenTicketPrefab.name;
    }
}

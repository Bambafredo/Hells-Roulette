using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StickerPiggyBank",
    menuName = "Stickers/Sticker Piggy Bank"
)]
public class StickerPiggyBank : StickerEffect
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Piggy Bank")]

    [Tooltip(
        "Money stored inside this physical Piggy Bank every time a valid spin ends " +
        "while the sticker is on a non-winning roulette segment."
    )]
    [Min(0)]
    public int moneyStoredPerMiss = 3;


    private const string StoredMoneyKey =
        "PiggyBank.StoredMoney";


    // =========================================================
    // LOCATION-AWARE EFFECT
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
                "StickerPiggyBank: BaseSticker owner was not provided."
            );

            return;
        }


        switch (location)
        {
            case StickerSpinLocation.WinningSegment:
                ResolveWinningSegment(owner);
                break;

            case StickerSpinLocation.NonWinningSegment:
                ResolveNonWinningSegment(owner);
                break;

            case StickerSpinLocation.Album:
                /*
                 * Piggy Bank currently has no Album effect.
                 *
                 * The architecture supports Album activations already;
                 * this particular sticker simply does nothing there.
                 */
                break;
        }
    }


    // =========================================================
    // NON-WINNING SEGMENT
    // =========================================================

    private void ResolveNonWinningSegment(
        BaseSticker owner)
    {
        int amountToStore =
            Mathf.Max(
                0,
                moneyStoredPerMiss
            );

        int newStoredTotal =
            owner.AddRuntimeInt(
                StoredMoneyKey,
                amountToStore
            );


        string description =
            BuildStoreDescription(
                amountToStore,
                newStoredTotal
            );


        /*
         * This IS a genuine sticker activation, so it appears in the log.
         *
         * By default consumeUseOnNonWinningActivation is false, therefore
         * Piggy Bank banks money without losing a use.
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.NonWinningSegment,
            description,
            0,
            null
        );


        Debug.Log(
            $"[PIGGY BANK] Stored ${amountToStore}. " +
            $"Current bank: ${newStoredTotal}."
        );
    }


    // =========================================================
    // WINNING SEGMENT
    // =========================================================

    private void ResolveWinningSegment(
        BaseSticker owner)
    {
        int payout =
            Mathf.Max(
                0,
                owner.GetRuntimeInt(
                    StoredMoneyKey,
                    0
                )
            );


        /*
         * Clear the bank immediately so a Piggy Bank with multiple uses
         * starts a fresh saving cycle after every successful cash-out.
         */
        owner.SetRuntimeInt(
            StoredMoneyKey,
            0
        );


        /*
         * Winning activation uses the normal winning consumption toggle.
         * For the intended Piggy Bank setup:
         *
         * maxUses = 1
         * consumeUseOnWinningActivation = true
         * consumeUseOnNonWinningActivation = false
         */
        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            "Cash out",
            payout,
            null
        );


        if (CurrencyManager.Instance != null &&
            payout > 0)
        {
            CurrencyManager.Instance
                .AddDollar(
                    payout
                );
        }


        Debug.Log(
            $"[PIGGY BANK] Cashed out ${payout}."
        );
    }


    // =========================================================
    // LOG TEXT
    // =========================================================

    private string BuildStoreDescription(
        int added,
        int total)
    {
        if (GameLogManager.Instance != null)
        {
            return
                "Stores " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${added}"
                    ) +
                " (banked: " +
                GameLogManager.Instance
                    .MoneyText(
                        $"${total}"
                    ) +
                ")";
        }

        return
            $"Stores ${added} (banked: ${total})";
    }
}

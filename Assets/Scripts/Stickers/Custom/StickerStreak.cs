using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(
    fileName = "StickerStreak",
    menuName = "Stickers/Sticker Streak"
)]
public class StickerStreak : StickerEffect
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Streak")]

    [Tooltip(
        "Percentage removed from the current Debt while this physical Streak " +
        "sticker is in the Album. Multiple Streak stickers stack additively, " +
        "up to a maximum total discount of 100%."
    )]
    [Range(0, 100)]
    public int debtReductionPercent =
        10;


    // =========================================================
    // RUNTIME HOOKS
    // =========================================================

    private static RoundManager subscribedRoundManager;


    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad
    )]
    private static void InstallRuntimeHooks()
    {
        BaseSticker.OnAnyStickerDragEnded -=
            HandleAnyStickerDragEnded;

        BaseSticker.OnAnyStickerDragEnded +=
            HandleAnyStickerDragEnded;


        SceneManager.sceneLoaded -=
            HandleSceneLoaded;

        SceneManager.sceneLoaded +=
            HandleSceneLoaded;
    }


    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad
    )]
    private static void InitialSceneSync()
    {
        AttachRoundManager();
        SyncAllPhysicalStreakStickers();
    }


    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        AttachRoundManager();
        SyncAllPhysicalStreakStickers();
    }


    private static void AttachRoundManager()
    {
        RoundManager manager =
            RoundManager.Instance;


        if (manager == null)
        {
            manager =
                FindObjectOfType<RoundManager>();
        }


        if (subscribedRoundManager == manager)
            return;


        if (subscribedRoundManager != null)
        {
            subscribedRoundManager.OnDebtPaid -=
                HandleDebtPaid;
        }


        subscribedRoundManager =
            manager;


        if (subscribedRoundManager != null)
        {
            subscribedRoundManager.OnDebtPaid +=
                HandleDebtPaid;
        }
    }


    // =========================================================
    // LOCATION TRACKING
    // =========================================================

    private static void HandleAnyStickerDragEnded(
        BaseSticker sticker)
    {
        if (sticker == null ||
            !(sticker.effect is StickerStreak streak))
        {
            return;
        }


        /*
         * RewardStickerOffer / DraftStickerOffer may still move the sticker in
         * LateUpdate after BaseSticker fires the drag-ended event. Wait one frame
         * and inspect the final ownership/location state, matching Ladybug's
         * proven Album-registration pattern.
         */
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.StartCoroutine(
                streak.SyncAfterDrop(
                    sticker
                )
            );
        }
    }


    private IEnumerator SyncAfterDrop(
        BaseSticker sticker)
    {
        yield return null;


        SyncRegistration(
            sticker
        );
    }


    private static void SyncAllPhysicalStreakStickers()
    {
        if (!Application.isPlaying ||
            RoundManager.Instance == null)
        {
            return;
        }


        BaseSticker[] stickers =
            FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null ||
                !(sticker.effect is StickerStreak streak))
            {
                continue;
            }


            streak.SyncRegistration(
                sticker
            );
        }
    }


    private void SyncRegistration(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.effect != this ||
            RoundManager.Instance == null)
        {
            return;
        }


        bool isInAlbum =
            owner.currentAlbumZone != null;


        if (!isInAlbum &&
            AlbumManager.Instance != null)
        {
            isInAlbum =
                AlbumManager.Instance
                    .IsStickerInAlbum(
                        owner
                    );
        }


        if (isInAlbum &&
            !owner.IsConsumed &&
            !owner.IsPendingGameplayDestruction)
        {
            RoundManager.Instance
                .RegisterStickerDebtDiscount(
                    owner,
                    Mathf.Clamp(
                        debtReductionPercent,
                        0,
                        100
                    )
                );
        }
        else
        {
            RoundManager.Instance
                .UnregisterStickerDebtDiscount(
                    owner
                );
        }
    }


    // =========================================================
    // DEBT PAYMENT
    // =========================================================

    private static void HandleDebtPaid(
        int paidAmount)
    {
        if (RoundManager.Instance == null)
            return;


        /*
         * Snapshot the physical stickers first because consuming the final use
         * can destroy them immediately from gameplay.
         */
        BaseSticker[] stickers =
            FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null ||
                !(sticker.effect is StickerStreak streak) ||
                sticker.IsConsumed ||
                sticker.IsPendingGameplayDestruction)
            {
                continue;
            }


            bool isInAlbum =
                sticker.currentAlbumZone != null;


            if (!isInAlbum &&
                AlbumManager.Instance != null)
            {
                isInAlbum =
                    AlbumManager.Instance
                        .IsStickerInAlbum(
                            sticker
                        );
            }


            if (!isInAlbum)
                continue;


            /*
             * Keep RoundManager registration self-healing before consumption.
             */
            streak.SyncRegistration(
                sticker
            );


            sticker.ConsumeUseAfterActivation();


            Debug.Log(
                $"[STREAK] '{streak.stickerName}' consumed 1 use after " +
                $"Debt was paid (${paidAmount})."
            );
        }
    }


    // =========================================================
    // SPIN RESOLUTION
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        if (owner == null)
            return;


        if (RoundManager.Instance != null &&
            !RoundManager.Instance.WasLastSpinValid)
        {
            return;
        }


        /*
         * The Album effect is passive. ResolveSpinLocation is also a convenient
         * self-healing sync point for programmatic moves.
         */
        SyncRegistration(
            owner
        );


        if (location !=
            StickerSpinLocation.WinningSegment)
        {
            return;
        }


        RegisterActivation(
            owner,
            StickerSpinLocation.WinningSegment,
            "Lose 1 use",
            0,
            true
        );
    }


    public override bool ShouldConsumeUseOnActivation(
        StickerSpinLocation location)
    {
        return
            location ==
            StickerSpinLocation.WinningSegment;
    }


    // =========================================================
    // GAMEPLAY DESTRUCTION
    // =========================================================

    public override void OnDestroyedFromGameplay(
        BaseSticker owner,
        string reason)
    {
        if (owner == null ||
            RoundManager.Instance == null)
        {
            return;
        }


        RoundManager.Instance
            .UnregisterStickerDebtDiscount(
                owner
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
                StickerSpinLocation.Album;
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
            resolved.Replace(
                "{discount}",
                Mathf.Clamp(
                    debtReductionPercent,
                    0,
                    100
                )
                .ToString()
            );
    }
}

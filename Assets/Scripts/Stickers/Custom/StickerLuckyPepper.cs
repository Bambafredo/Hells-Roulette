using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(
    fileName = "StickerLuckyPepper",
    menuName = "Stickers/Sticker Lucky Pepper"
)]
public class StickerLuckyPepper : StickerEffect
{
    // =========================================================
    // CONFIGURATION
    // =========================================================

    [Header("Lucky Pepper")]

    [Tooltip(
        "Percentage points added to the Lucky Shot reward bonus while this " +
        "physical Lucky Pepper is on the roulette. Multiple Lucky Peppers stack additively."
    )]
    [Min(0f)]
    public float luckyShotBonusPercent =
        25f;


    // =========================================================
    // RUNTIME HOOKS
    // =========================================================

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
        SyncAllPhysicalLuckyPeppers();
    }


    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        SyncAllPhysicalLuckyPeppers();
    }


    private static void HandleAnyStickerDragEnded(
        BaseSticker sticker)
    {
        if (sticker == null ||
            !(sticker.effect is StickerLuckyPepper pepper))
        {
            return;
        }


        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.StartCoroutine(
                pepper.SyncAfterDrop(
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


    // =========================================================
    // LOCATION TRACKING
    // =========================================================

    private static void SyncAllPhysicalLuckyPeppers()
    {
        if (!Application.isPlaying)
            return;


        BaseSticker[] stickers =
            FindObjectsOfType<BaseSticker>(
                true
            );


        foreach (BaseSticker sticker in stickers)
        {
            if (sticker == null ||
                !(sticker.effect is StickerLuckyPepper pepper))
            {
                continue;
            }


            pepper.SyncRegistration(
                sticker
            );
        }
    }


    private void SyncRegistration(
        BaseSticker owner)
    {
        if (owner == null ||
            owner.effect != this ||
            LuckyShotController.Instance == null)
        {
            return;
        }


        bool isOnRoulette =
            owner.currentSegment != null;


        if (isOnRoulette)
        {
            LuckyShotController.Instance
                .RegisterRewardBonus(
                    owner,
                    Mathf.Max(
                        0f,
                        luckyShotBonusPercent
                    )
                );
        }
        else
        {
            LuckyShotController.Instance
                .UnregisterRewardBonus(
                    owner
                );
        }
    }


    public override void OnDestroyedFromGameplay(
        BaseSticker owner,
        string reason)
    {
        if (owner != null &&
            LuckyShotController.Instance != null)
        {
            LuckyShotController.Instance
                .UnregisterRewardBonus(
                    owner
                );
        }


        base.OnDestroyedFromGameplay(
            owner,
            reason
        );
    }


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
            return;


        /*
         * Winning and Non-Winning are passive: while Lucky Pepper is on the
         * roulette, its Lucky Shot bonus is already registered before the spin.
         *
         * In the Album it provides no Lucky Shot bonus and loses 1 use each
         * valid spin through the normal use-consumption pipeline.
         */
        if (location ==
            StickerSpinLocation.Album)
        {
            RegisterActivation(
                owner,
                StickerSpinLocation.Album,
                "Lose 1 use",
                0,
                true
            );
        }
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
                StickerSpinLocation.NonWinningSegment ||
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
                "{bonus}",
                Mathf.Max(
                    0f,
                    luckyShotBonusPercent
                ).ToString("0.##")
            );
    }
}

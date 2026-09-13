using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(
    fileName = "StickerLadybug",
    menuName = "Stickers/Sticker Ladybug"
)]
public class StickerLadybug : StickerEffect
{
    // =========================================================
    // CONFIG
    // =========================================================

    [Header("Ladybug")]

    [Tooltip(
        "Percentage added to this round's Debt while this physical Ladybug " +
        "is in the Album. Multiple Ladybugs stack additively."
    )]
    [Min(0)]
    public int interestPercent =
        5;


    // =========================================================
    // RUNTIME HOOKS
    // =========================================================

    /*
     * IMPORTANT:
     *
     * StickerEffect is a ScriptableObject. Its OnEnable() is NOT a reliable
     * place to install gameplay listeners when entering Play, especially when
     * the asset was already loaded by the Editor.
     *
     * Install the generic runtime hooks explicitly for every Play session
     * instead. Nothing Ladybug-specific is added to BaseSticker.
     */
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
        SyncAllPhysicalLadybugs();
    }


    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        SyncAllPhysicalLadybugs();
    }


    private static void HandleAnyStickerDragEnded(
        BaseSticker sticker)
    {
        if (sticker == null ||
            !(sticker.effect is StickerLadybug ladybug))
        {
            return;
        }


        /*
         * BaseSticker finishes its placement before this event fires.
         *
         * RewardStickerOffer / DraftStickerOffer, however, deliberately resolve
         * AFTER BaseSticker in LateUpdate. A rejected purchase/claim can still
         * return the sticker to its offer slot during that same frame.
         *
         * Therefore wait one frame and inspect the FINAL ownership/location
         * state instead of registering a transient Album placement.
         */
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.StartCoroutine(
                ladybug.SyncAfterDrop(
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

    private static void SyncAllPhysicalLadybugs()
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
                !(sticker.effect is StickerLadybug ladybug))
            {
                continue;
            }


            ladybug.SyncRegistration(
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


        /*
         * Hierarchy fallback for stickers authored directly inside Album.
         *
         * BaseSticker normally owns currentAlbumZone, but this also makes
         * initial scene setup independent of component Awake/Start order.
         */
        if (!isInAlbum &&
            AlbumManager.Instance != null)
        {
            isInAlbum =
                AlbumManager.Instance
                    .IsStickerInAlbum(
                        owner
                    );
        }


        if (isInAlbum)
        {
            RoundManager.Instance
                .RegisterStickerInterest(
                    owner,
                    Mathf.Max(
                        0,
                        interestPercent
                    )
                );
        }
        else
        {
            RoundManager.Instance
                .UnregisterStickerInterest(
                    owner
                );
        }
    }


    // =========================================================
    // LOCATION-AWARE EFFECT
    // =========================================================

    public override void ResolveSpinLocation(
        BaseSticker owner,
        StickerSpinLocation location)
    {
        /*
         * Ladybug is PASSIVE. It does not activate once per spin and does not
         * consume uses.
         *
         * This remains as a harmless self-healing synchronization point for
         * any future system that might move a sticker programmatically without
         * going through a manual drag.
         */
        SyncRegistration(
            owner
        );
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
            .UnregisterStickerInterest(
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
                "{interest}",
                Mathf.Max(
                    0,
                    interestPercent
                )
                .ToString()
            );
    }
}

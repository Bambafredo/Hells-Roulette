using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// Reusable presentation helper for pulsing a logical roulette segment.
///
/// This class owns only VISUAL telegraph behaviour:
/// - binds a logical segment index to its current SegmentMesh;
/// - survives WheelGenerator / WheelShifter mesh regeneration;
/// - optionally hides the telegraph while any sticker is being dragged;
/// - optionally hides the telegraph while the roulette is spinning/resolving;
/// - clears the old mesh when the target changes.
///
/// It does not know about enemies, Limbo, actions, blocked state or gameplay.
/// Callers decide WHICH segment should be telegraphed and WHEN.
/// </summary>
public sealed class SegmentTelegraphPresenter : IDisposable
{
    private WheelGenerator generator;
    private RouletteController roulette;

    private bool requestedVisible = false;
    private int requestedSegmentIndex = -1;

    private Color requestedColor = Color.white;
    private float requestedStrength = 0.18f;
    private float requestedPulseSpeed = 1.2f;
    private bool hideWhileStickerDragging = true;
    private bool hideWhileSpinInProgress = true;

    private bool stickerDragInProgress = false;

    private int boundSegmentIndex = -1;
    private SegmentMesh boundMesh;

    private bool styleDirty = true;
    private bool disposed = false;


    public SegmentTelegraphPresenter(
        WheelGenerator generator = null,
        RouletteController roulette = null)
    {
        this.generator = generator;

        BaseSticker.OnAnyStickerDragStarted +=
            HandleAnyStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded +=
            HandleAnyStickerDragEnded;

        SetRouletteController(
            roulette
        );
    }


    public void SetGenerator(
        WheelGenerator newGenerator)
    {
        if (generator == newGenerator)
            return;

        ClearBoundMesh();

        generator =
            newGenerator;

        Refresh();
    }


    public void SetRouletteController(
        RouletteController newRoulette)
    {
        if (roulette == newRoulette)
            return;

        if (roulette != null)
        {
            roulette.OnSpinStart -=
                HandleSpinStarted;

            roulette.OnSpinEnd -=
                HandleSpinEnded;
        }

        roulette =
            newRoulette;

        if (roulette != null)
        {
            roulette.OnSpinStart +=
                HandleSpinStarted;

            roulette.OnSpinEnd +=
                HandleSpinEnded;
        }

        Refresh();
    }


    /// <summary>
    /// Requests a pulsing telegraph on one logical segment.
    ///
    /// Calling Show repeatedly with the same values is safe and cheap. This is
    /// useful for systems whose target may change as their own state changes.
    /// </summary>
    public void Show(
        int segmentIndex,
        Color highlightColor,
        float strength,
        float pulseSpeed,
        bool hideDuringStickerDrag = true,
        bool hideDuringSpin = true)
    {
        if (disposed)
            return;

        Color safeColor =
            highlightColor;

        float safeStrength =
            Mathf.Clamp01(
                strength
            );

        float safePulseSpeed =
            Mathf.Max(
                0.01f,
                pulseSpeed
            );

        bool targetChanged =
            !requestedVisible ||
            requestedSegmentIndex != segmentIndex;

        bool styleChanged =
            requestedColor != safeColor ||
            !Mathf.Approximately(
                requestedStrength,
                safeStrength
            ) ||
            !Mathf.Approximately(
                requestedPulseSpeed,
                safePulseSpeed
            ) ||
            hideWhileStickerDragging !=
                hideDuringStickerDrag ||
            hideWhileSpinInProgress !=
                hideDuringSpin;

        requestedVisible =
            true;

        requestedSegmentIndex =
            segmentIndex;

        requestedColor =
            safeColor;

        requestedStrength =
            safeStrength;

        requestedPulseSpeed =
            safePulseSpeed;

        hideWhileStickerDragging =
            hideDuringStickerDrag;

        hideWhileSpinInProgress =
            hideDuringSpin;

        if (targetChanged)
        {
            ClearBoundMesh();
        }

        if (styleChanged)
        {
            styleDirty =
                true;
        }

        Refresh();
    }


    public void Hide()
    {
        requestedVisible =
            false;

        requestedSegmentIndex =
            -1;

        ClearBoundMesh();
    }


    /// <summary>
    /// Rebinds the telegraph to the current SegmentMesh for the requested index.
    ///
    /// Call this whenever the owner updates. It is what makes the telegraph
    /// automatically survive WheelShifter rebuilding the wheel hierarchy.
    /// </summary>
    public void Refresh()
    {
        if (disposed)
            return;

        bool spinInProgress =
            roulette != null &&
            roulette.SpinInProgress;

        bool shouldShow =
            requestedVisible &&
            requestedSegmentIndex >= 0 &&
            !(hideWhileStickerDragging &&
              stickerDragInProgress) &&
            !(hideWhileSpinInProgress &&
              spinInProgress);

        if (!shouldShow)
        {
            ClearBoundMesh();
            return;
        }

        if (!TryGetSegmentMesh(
                requestedSegmentIndex,
                out SegmentMesh currentMesh))
        {
            ClearBoundMesh();
            return;
        }

        bool bindingChanged =
            boundSegmentIndex !=
                requestedSegmentIndex ||
            boundMesh !=
                currentMesh;

        if (bindingChanged)
        {
            ClearBoundMesh();

            boundSegmentIndex =
                requestedSegmentIndex;

            boundMesh =
                currentMesh;

            styleDirty =
                true;
        }

        if (boundMesh == null)
            return;

        if (styleDirty)
        {
            boundMesh.ConfigureTelegraph(
                requestedColor,
                requestedStrength,
                requestedPulseSpeed
            );

            styleDirty =
                false;
        }

        if (!boundMesh.IsTelegraphed)
        {
            boundMesh.SetTelegraphed(
                true
            );
        }
    }


    public void Dispose()
    {
        if (disposed)
            return;

        BaseSticker.OnAnyStickerDragStarted -=
            HandleAnyStickerDragStarted;

        BaseSticker.OnAnyStickerDragEnded -=
            HandleAnyStickerDragEnded;

        if (roulette != null)
        {
            roulette.OnSpinStart -=
                HandleSpinStarted;

            roulette.OnSpinEnd -=
                HandleSpinEnded;
        }

        Hide();

        disposed =
            true;
    }


    private void HandleAnyStickerDragStarted(
        BaseSticker sticker)
    {
        stickerDragInProgress =
            true;

        Refresh();
    }


    private void HandleAnyStickerDragEnded(
        BaseSticker sticker)
    {
        stickerDragInProgress =
            false;

        Refresh();
    }


    private void HandleSpinStarted()
    {
        /*
         * RouletteController sets SpinInProgress before invoking OnSpinStart,
         * so Refresh() hides the telegraph immediately on the launch frame.
         */
        Refresh();
    }


    private void HandleSpinEnded()
    {
        /*
         * OnSpinEnd can fire while RouletteController is still completing spin
         * resolution. Refresh() therefore keeps respecting SpinInProgress; the
         * owner's next Refresh() shows the telegraph again after resolution.
         */
        Refresh();
    }


    private void ClearBoundMesh()
    {
        if (boundMesh != null)
        {
            boundMesh.SetTelegraphed(
                false
            );
        }

        boundSegmentIndex =
            -1;

        boundMesh =
            null;
    }


    private bool TryGetSegmentMesh(
        int segmentIndex,
        out SegmentMesh mesh)
    {
        mesh =
            null;

        if (generator == null ||
            generator.segments == null ||
            segmentIndex < 0 ||
            segmentIndex >= generator.segments.Count)
        {
            return false;
        }

        WheelSegmentData data =
            generator.segments[
                segmentIndex
            ];

        if (data == null ||
            data.meshComponent == null)
        {
            return false;
        }

        mesh =
            data.meshComponent;

        return true;
    }
}

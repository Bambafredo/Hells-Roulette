using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StickerPlacementValidator : MonoBehaviour
{
    public static StickerPlacementValidator Instance;

    [Header("UI")]
    public GameObject wrongStickerPanel;


    [Header("Invalid Sticker Flash")]

    [Tooltip("If enabled, every currently invalid sticker pulses toward the configured color.")]
    public bool flashInvalidStickers = true;

    [Tooltip("Color used to highlight invalid stickers.")]
    public Color invalidStickerFlashColor = Color.red;

    [Tooltip("Number of full color pulses per second.")]
    [Min(0.01f)]
    public float invalidStickerFlashSpeed = 2.5f;

    [Tooltip("How strongly the sticker blends toward the flash color at the peak of the pulse.")]
    [Range(0f, 1f)]
    public float invalidStickerFlashIntensity = 1f;

    // Estado global:
    // true = hay al menos un sticker de la ruleta o Album colocado incorrectamente.
    private bool hardInputLock = false;

    /*
     * Visual feedback is owned entirely by the validator.
     * We cache the original color of every SpriteRenderer we tint so it can
     * always be restored exactly when that sticker becomes valid again.
     */
    private readonly HashSet<BaseSticker> invalidStickers =
        new HashSet<BaseSticker>();

    private readonly Dictionary<SpriteRenderer, Color> originalRendererColors =
        new Dictionary<SpriteRenderer, Color>();

    private RouletteController controller;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        controller = FindObjectOfType<RouletteController>();

        if (wrongStickerPanel != null)
            wrongStickerPanel.SetActive(false);
    }

    private void Update()
    {
        UpdateInvalidStickerFlash();
    }


    private void OnDestroy()
    {
        RestoreAllInvalidStickerColors();

        if (Instance == this)
            Instance = null;
    }

    // Otros scripts pueden consultar si estamos bloqueados.
    public bool InputBlocked => hardInputLock;

    // =========================================================
    // HOOKS PÚBLICOS
    // =========================================================

    /// <summary>
    /// Llamado desde WheelGenerator cuando termina de regenerar
    /// segmentos y restaurar stickers.
    /// </summary>
    public void ValidateAfterWheelRegeneration()
    {
        ValidateAllStickers();
    }

    /// <summary>
    /// Llamado desde BaseSticker cuando el jugador suelta
    /// un sticker mientras existe un bloqueo de colocación.
    ///
    /// No intentamos validar solo ese sticker:
    /// puede haber varios inválidos tras un resize.
    /// </summary>
    public void NotifyStickerDropped(BaseSticker sticker)
    {
        ValidateAllStickers();
    }

    // =========================================================
    // VALIDACIÓN PRINCIPAL
    // =========================================================

    private void ValidateAllStickers()
    {
        /*
         * Aseguramos que cualquier cambio reciente de parent,
         * posición o collider esté reflejado en Physics2D.
         */
        Physics2D.SyncTransforms();

        BaseSticker[] allStickers =
            FindObjectsOfType<BaseSticker>(true);

        HashSet<BaseSticker> newInvalidStickers =
            new HashSet<BaseSticker>();

        foreach (BaseSticker sticker in allStickers)
        {
            if (!IsStickerValid(sticker))
            {
                newInvalidStickers.Add(
                    sticker
                );
            }
        }

        RefreshInvalidStickerSet(
            newInvalidStickers
        );

        SetBlockState(
            newInvalidStickers.Count > 0
        );
    }

    /// <summary>
    /// Un sticker es válido si:
    ///
    /// - Está fuera de la ruleta -> no nos importa.
    ///
    /// - Está colocado en la ruleta -> debe tener un segmento
    ///   lógico válido, estar realmente parentado a ese segmento
    ///   y pasar StickerPlacementUtility.
    ///
    /// - Está visualmente dentro de Segment_X pero su estado lógico
    ///   dice que NO está colocado -> lo consideramos inconsistente.
    /// </summary>
    private bool IsStickerValid(BaseSticker sticker)
    {
        if (sticker == null)
            return true;


        /*
         * A sticker destroyed through BaseSticker.DestroyFromGameplay() is
         * removed LOGICALLY immediately, but Unity destroys its GameObject only
         * at the end of the frame.
         *
         * During that tiny window:
         *
         * - isPlaced is already false
         * - currentSegment is already null
         * - its root can still physically be parented to Segment_X
         *
         * SegmentBlock unlock / block changes can trigger placement validation
         * inside that same frame. Without this guard the validator sees:
         *
         *     isPlaced == false
         *     parent == Segment_X
         *
         * and incorrectly hard-locks the roulette until some later action
         * causes another validation pass.
         *
         * Pending gameplay destruction is therefore completely irrelevant to
         * placement validation and must be ignored.
         */
        if (sticker.IsPendingGameplayDestruction)
        {
            return true;
        }


        /*
         * Destroy() is deferred until end-of-frame.
         *
         * A limited-use sticker at 0 uses has already completed its
         * lifetime and must no longer be considered by placement
         * validation. This prevents a WheelShifter / Magic Bean that
         * dies on the same activation that regenerates the wheel from
         * creating a one-frame false placement lock.
         *
         * No new BaseSticker API is introduced: these are the existing
         * HasLimitedUses / RemainingUses properties from the stable code.
         */
        if (sticker.HasLimitedUses &&
            sticker.RemainingUses <= 0)
        {
            return true;
        }

        Transform root =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;

        if (root == null)
            return true;

        // ---------------------------------------------------------
        // STICKER LÓGICAMENTE COLOCADO EN LA RULETA
        // ---------------------------------------------------------

        if (sticker.isPlaced)
        {
            /*
             * Si dice estar colocado pero no sabe en qué segmento,
             * tenemos un estado inconsistente.
             */
            if (sticker.currentSegment == null)
                return false;

            /*
             * El Transform real también debe pertenecer al segmento
             * que BaseSticker considera currentSegment.
             */
            if (root.parent != sticker.currentSegment)
                return false;

            Collider2D segmentCollider =
                sticker.currentSegment.GetComponent<Collider2D>();

            if (segmentCollider == null)
                return false;


            /*
             * A blocked segment freezes its contents.
             *
             * WheelShifter can still resize the roulette while that segment
             * is blocked. If this makes one of its frozen stickers temporarily
             * cross a boundary, we must NOT hard-lock the whole game: the
             * player is explicitly forbidden from moving that sticker.
             *
             * Once the round ends, WheelGenerator clears all blocks and
             * immediately runs this validator again. At that point normal
             * geometry rules return and the player can reposition anything
             * that no longer fits.
             */
            SegmentMesh segmentMesh =
                sticker.currentSegment
                    .GetComponent<SegmentMesh>();


            if (segmentMesh != null &&
                segmentMesh.IsBlocked)
            {
                return true;
            }


            /*
             * ÚNICA FUENTE DE VERDAD GEOMÉTRICA.
             *
             * Aquí se comprueba:
             * - collider real dentro del segmento
             * - ausencia de solape real con otros stickers
             */
            return StickerPlacementUtility.CanPlaceOnSegment(
                sticker,
                segmentCollider,
                sticker.tolerance
            );
        }

        // ---------------------------------------------------------
        // STICKER EN ALBUM
        // ---------------------------------------------------------

        /*
         * Album placement can also become invalid without a manual drag.
         * Example: a sticker is transmuted into a physically larger Zombie
         * while keeping the replaced sticker's position.
         *
         * Reuse AlbumPlacementUtility as the single geometric authority for:
         * - full collider inside Album bounds
         * - no overlap with another Album sticker
         */
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

        if (isInAlbum)
        {
            if (AlbumManager.Instance == null ||
                AlbumManager.Instance.albumZone == null)
            {
                return false;
            }

            Transform contentRoot =
                AlbumManager.Instance.albumZone
                    .GetContentRoot();

            if (contentRoot == null ||
                !root.IsChildOf(contentRoot))
            {
                return false;
            }

            return
                AlbumPlacementUtility.CanPlaceInAlbum(
                    sticker,
                    AlbumManager.Instance.albumZone
                );
        }


        // ---------------------------------------------------------
        // STICKER QUE DICE NO ESTAR EN LA RULETA
        // ---------------------------------------------------------

        /*
         * Normalmente esto significa que está:
         * - en la bolsa
         * - en un gameplay area
         * - en un slot
         * - suelto fuera de la ruleta
         *
         * Todo eso es correcto.
         *
         * Pero comprobamos que no esté visualmente parentado
         * a un segmento mientras isPlaced == false.
         */

        Transform parent = root.parent;

        if (parent == null)
            return true;

        if (IsWheelSegment(parent))
        {
            /*
             * Visualmente está en la ruleta pero lógicamente dice
             * que no. No queremos tolerar este estado silenciosamente.
             */
            return false;
        }

        return true;
    }

    // =========================================================
    // INVALID STICKER VISUAL FEEDBACK
    // =========================================================

    private void RefreshInvalidStickerSet(
        HashSet<BaseSticker> newInvalidStickers)
    {
        /*
         * Restore stickers that have just become valid BEFORE replacing the
         * set. Stickers that remain invalid keep their cached original colors.
         */
        List<BaseSticker> becameValid =
            new List<BaseSticker>();

        foreach (BaseSticker sticker in invalidStickers)
        {
            if (sticker == null ||
                !newInvalidStickers.Contains(sticker))
            {
                becameValid.Add(
                    sticker
                );
            }
        }

        foreach (BaseSticker sticker in becameValid)
        {
            RestoreStickerColors(
                sticker
            );
        }


        invalidStickers.Clear();

        foreach (BaseSticker sticker in newInvalidStickers)
        {
            if (sticker != null)
            {
                invalidStickers.Add(
                    sticker
                );

                CacheStickerRendererColors(
                    sticker
                );
            }
        }
    }


    private void UpdateInvalidStickerFlash()
    {
        if (invalidStickers.Count == 0)
            return;


        if (!flashInvalidStickers)
        {
            RestoreAllInvalidStickerColors();
            return;
        }


        float pulse =
            (
                Mathf.Sin(
                    Time.unscaledTime *
                    invalidStickerFlashSpeed *
                    Mathf.PI *
                    2f
                ) +
                1f
            ) *
            0.5f;

        float blend =
            pulse *
            invalidStickerFlashIntensity;


        foreach (BaseSticker sticker in invalidStickers)
        {
            if (sticker == null)
                continue;

            CacheStickerRendererColors(
                sticker
            );

            Transform visualRoot =
                sticker.stickerRoot != null
                    ? sticker.stickerRoot
                    : sticker.transform;

            SpriteRenderer[] renderers =
                visualRoot.GetComponentsInChildren<SpriteRenderer>(
                    true
                );

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!originalRendererColors.TryGetValue(
                        renderer,
                        out Color originalColor))
                {
                    continue;
                }

                renderer.color =
                    Color.Lerp(
                        originalColor,
                        invalidStickerFlashColor,
                        blend
                    );
            }
        }
    }


    private void CacheStickerRendererColors(
        BaseSticker sticker)
    {
        if (sticker == null)
            return;

        Transform visualRoot =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;

        SpriteRenderer[] renderers =
            visualRoot.GetComponentsInChildren<SpriteRenderer>(
                true
            );

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null ||
                originalRendererColors.ContainsKey(renderer))
            {
                continue;
            }

            originalRendererColors.Add(
                renderer,
                renderer.color
            );
        }
    }


    private void RestoreStickerColors(
        BaseSticker sticker)
    {
        if (sticker == null)
            return;

        Transform visualRoot =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;

        SpriteRenderer[] renderers =
            visualRoot.GetComponentsInChildren<SpriteRenderer>(
                true
            );

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (originalRendererColors.TryGetValue(
                    renderer,
                    out Color originalColor))
            {
                renderer.color =
                    originalColor;

                originalRendererColors.Remove(
                    renderer
                );
            }
        }
    }


    private void RestoreAllInvalidStickerColors()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair
                 in originalRendererColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }

        originalRendererColors.Clear();
    }


    // =========================================================
    // SEGMENT IDENTIFICATION
    // =========================================================

    private bool IsWheelSegment(Transform t)
    {
        if (t == null)
            return false;

        /*
         * Los segmentos generados por WheelGenerator tienen:
         *
         * - PolygonCollider2D
         * - nombre Segment_X
         *
         * Comprobamos ambas cosas para no confundir cualquier
         * PolygonCollider2D de la escena con un segmento.
         */

        if (!t.name.StartsWith("Segment_"))
            return false;

        return t.GetComponent<PolygonCollider2D>() != null;
    }

    // =========================================================
    // BLOQUEO / DESBLOQUEO
    // =========================================================

    private void SetBlockState(bool shouldBlock)
    {
        /*
         * IMPORTANTE:
         *
         * No hacemos simplemente:
         *
         * if (hardInputLock) return;
         *
         * Queremos REAPLICAR siempre el estado al RouletteController.
         *
         * BaseSticker desbloquea temporalmente el input al terminar
         * un drag. Si todavía queda otro sticker inválido, necesitamos
         * volver a bloquearlo inmediatamente.
         */

        bool stateChanged =
            hardInputLock != shouldBlock;

        hardInputLock =
            shouldBlock;

        // ---------------------------------------------------------
        // UI
        // ---------------------------------------------------------

        if (wrongStickerPanel != null)
        {
            if (wrongStickerPanel.activeSelf != shouldBlock)
                wrongStickerPanel.SetActive(shouldBlock);
        }

        // ---------------------------------------------------------
        // ROULETTE INPUT
        // ---------------------------------------------------------

        if (controller == null)
            controller = FindObjectOfType<RouletteController>();

        if (controller != null)
            controller.SetInputBlocked(shouldBlock);

        // ---------------------------------------------------------
        // DEBUG
        // ---------------------------------------------------------

        if (!stateChanged)
            return;

        if (shouldBlock)
        {
            Debug.Log(
                "[Validator] 🔒 Hay stickers mal colocados. " +
                "La ruleta queda bloqueada hasta corregirlos."
            );

            if (GameLogManager.Instance != null)
            {
                GameLogManager.Instance
                    .LogInvalidPlacementWarning();
            }
        }
        else
        {
            Debug.Log(
                "[Validator] ✅ Todos los stickers están correctamente colocados."
            );
        }
    }
}

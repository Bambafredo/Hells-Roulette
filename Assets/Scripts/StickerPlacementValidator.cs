using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StickerPlacementValidator : MonoBehaviour
{
    public static StickerPlacementValidator Instance;

    [Header("UI")]
    public GameObject wrongStickerPanel;

    // Estado global:
    // true = hay al menos un sticker de la ruleta colocado incorrectamente.
    private bool hardInputLock = false;

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

    private void OnDestroy()
    {
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

        bool anyWrong = false;

        foreach (BaseSticker sticker in allStickers)
        {
            if (!IsStickerValid(sticker))
            {
                anyWrong = true;
                break;
            }
        }

        SetBlockState(anyWrong);
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
        }
        else
        {
            Debug.Log(
                "[Validator] ✅ Todos los stickers están correctamente colocados."
            );
        }
    }
}

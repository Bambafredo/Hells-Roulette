using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StickerPlacementValidator : MonoBehaviour
{
    public static StickerPlacementValidator Instance;

    [Header("UI")]
    public GameObject wrongStickerPanel;

    // Bloqueo global
    private bool hardInputLock = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (wrongStickerPanel != null)
            wrongStickerPanel.SetActive(false);
    }

    // Para que otros scripts consulten el estado
    public bool InputBlocked => hardInputLock;

    // =========================================================
    // HOOKS PÚBLICOS
    // =========================================================

    // Llamado desde WheelGenerator al terminar GenerateWheel()
    public void ValidateAfterWheelRegeneration()
    {
        ValidateAllStickers();
    }

    // Llamado desde BaseSticker al soltarlo (mouse up)
    public void NotifyStickerDropped(BaseSticker s)
    {
        // No intentamos ser listos: revalidamos TODO
        ValidateAllStickers();
    }

    // =========================================================
    // VALIDACIÓN PRINCIPAL
    // =========================================================

    private void ValidateAllStickers()
    {
        BaseSticker[] all = FindObjectsOfType<BaseSticker>(true);
        bool anyWrong = false;

        foreach (var s in all)
        {
            if (!IsStickerValid(s))
            {
                anyWrong = true;
            }
        }

        if (anyWrong)
            ActivateBlock();
        else
            DeactivateBlock();
    }

    /// <summary>
    /// Un sticker es "válido" si:
    /// - No está en la ruleta (bolsa, gameplay, etc.) -> true
    /// - Está en la ruleta y DebugCheckSegment dice que está bien -> true
    /// - Está en la ruleta y DebugCheckSegment dice que está mal -> false
    /// </summary>
    private bool IsStickerValid(BaseSticker s)
    {
        if (s == null) return true;

        // ¿De qué segmento cuelga REALMENTE?
        Transform segTransform = s.currentSegment;

        // Caso especial: visualmente está en un Segment_X, pero lógicamente isPlaced == false
        if (segTransform == null)
        {
            Transform root = s.stickerRoot != null ? s.stickerRoot : s.transform;
            Transform parent = root.parent;

            if (parent == null)
                return true; // está suelto en la escena / UI, no nos importa

            var poly = parent.GetComponent<PolygonCollider2D>();
            if (poly == null)
                return true; // padre no es un segmento, tampoco nos importa

            segTransform = parent;
        }

        Collider2D segCol = segTransform.GetComponent<Collider2D>();
        if (segCol == null)
            return true;

        // Aquí usamos tu validador real del sticker
        return s.DebugCheckSegment(segCol);
    }

    // =========================================================
    // BLOQUEO / DESBLOQUEO GLOBAL
    // =========================================================

    private void ActivateBlock()
    {
        if (hardInputLock)
            return;

        hardInputLock = true;

        if (wrongStickerPanel != null)
            wrongStickerPanel.SetActive(true);

        // Bloquear ruleta por redundancia
        var controller = FindObjectOfType<RouletteController>();
        if (controller != null)
            controller.inputBlocked = true;

        Debug.Log("[Validator] 🔒 Bloqueo activado: hay stickers mal colocados");
    }

    private void DeactivateBlock()
    {
        if (!hardInputLock)
            return;

        hardInputLock = false;

        if (wrongStickerPanel != null)
            wrongStickerPanel.SetActive(false);

        var controller = FindObjectOfType<RouletteController>();
        if (controller != null)
            controller.inputBlocked = false;

        Debug.Log("[Validator] ✅ Bloqueo desactivado: todos los stickers correctos");
    }
}

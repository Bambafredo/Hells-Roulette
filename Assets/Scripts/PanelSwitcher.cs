using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelSwitcher : MonoBehaviour
{
    [Header("Panels")]
    public GameObject stickerSlotsPanel;
    public GameObject enemyPanel;

    [Header("Icons")]
    public SpriteRenderer iconShowEnemies;    // aparece cuando el panel actual es el de stickers
    public SpriteRenderer iconShowStickers;   // aparece cuando el panel actual es el de enemigos

    [Header("Input")]
    public Collider2D switchCollider;   // el BoxCollider2D del propio botón

    private RouletteController controller;

    void Start()
    {
        controller = FindObjectOfType<RouletteController>();

        if (controller != null)
        {
            controller.OnSpinStart += HandleSpinStart;
            controller.OnSpinEnd += HandleSpinEnd;
        }

        // Estado inicial → mostramos Sticker Slots
        ShowStickerSlots();
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnSpinStart -= HandleSpinStart;
            controller.OnSpinEnd -= HandleSpinEnd;
        }
    }

    // ---------------------------------------------------------
    //       CLICK EN EL PANEL SWITCHER (solo si no hay spin)
    // ---------------------------------------------------------
    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            if (controller != null && controller.SpinInProgress)
                return; // 🔒 Bloqueado mientras la ruleta gira

            Vector2 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            if (switchCollider != null && switchCollider.OverlapPoint(pos))
            {
                TogglePanels();
            }
        }
#endif
    }

    // ---------------------------------------------------------
    //                SPIN EVENTS
    // ---------------------------------------------------------
    private void HandleSpinStart()
    {
        // Durante la tirada → siempre mostramos enemigos
        ShowEnemyPanel();

        // Bloquear visual del botón si quieres (opcional)
        if (iconShowEnemies != null) iconShowEnemies.enabled = false;
        if (iconShowStickers != null) iconShowStickers.enabled = true;
    }

    private void HandleSpinEnd()
    {
        // Al terminar la tirada, NO cambiamos nada.
        // Enemy panel sigue visible hasta que el jugador lo cambie manualmente.
    }

    // ---------------------------------------------------------
    //                PANEL MANAGEMENT
    // ---------------------------------------------------------
    private void TogglePanels()
    {
        if (enemyPanel.activeSelf)
            ShowStickerSlots();
        else
            ShowEnemyPanel();
    }

    private void ShowStickerSlots()
    {
        stickerSlotsPanel.SetActive(true);
        enemyPanel.SetActive(false);

        if (iconShowEnemies != null) iconShowEnemies.enabled = true;
        if (iconShowStickers != null) iconShowStickers.enabled = false;
    }

    private void ShowEnemyPanel()
    {
        stickerSlotsPanel.SetActive(false);
        enemyPanel.SetActive(true);

        if (iconShowEnemies != null) iconShowEnemies.enabled = false;
        if (iconShowStickers != null) iconShowStickers.enabled = true;
    }
}

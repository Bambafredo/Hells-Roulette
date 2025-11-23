using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelSwitcher : MonoBehaviour
{
    [Header("Panels")]
    public GameObject stickerSlotsPanel;   // Sticker_Slots
    public GameObject enemyPanel;          // Enemy_Panel

    [Header("Icons")]
    public SpriteRenderer iconEnemyPanel;      // Icono que muestra "ir al panel enemigo"
    public SpriteRenderer iconStickerSlots;    // Icono que muestra "volver a stickers"

    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;

        // Asegurarse de que el estado inicial esté actualizado
        UpdateIcons();
    }

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pos = cam.ScreenToWorldPoint(Input.mousePosition);

            // Si el click está sobre este objeto
            if (GetComponent<Collider2D>().OverlapPoint(pos))
            {
                TogglePanels();
            }
        }
#endif
    }

    private void TogglePanels()
    {
        bool stickersActive = stickerSlotsPanel.activeSelf;

        // Alternar paneles
        stickerSlotsPanel.SetActive(!stickersActive);
        enemyPanel.SetActive(stickersActive);

        // Actualizar iconos
        UpdateIcons();
    }

    private void UpdateIcons()
    {
        bool stickersActive = stickerSlotsPanel.activeSelf;

        // Si están los stickers → mostrar icono para cambiar al EnemyPanel
        iconEnemyPanel.enabled = stickersActive;

        // Si está el EnemyPanel → mostrar icono para volver a StickerSlots
        iconStickerSlots.enabled = !stickersActive;
    }
}

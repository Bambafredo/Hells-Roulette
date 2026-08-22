using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelSwitcher : MonoBehaviour
{
    [Header("Panels")]

    [Tooltip(
        "Panel antiguo de Sticker Slots. " +
        "Puede dejarse vacío en la escena PC."
    )]
    public GameObject stickerSlotsPanel;

    public GameObject enemyPanel;


    [Header("Icons")]

    /*
     * Conservamos estos nombres para no romper
     * referencias serializadas de la escena mobile.
     *
     * En PC:
     *
     * iconShowEnemies  = icono para mostrar enemigos
     * iconShowStickers = icono para ocultar enemigos / volver
     */
    public SpriteRenderer iconShowEnemies;
    public SpriteRenderer iconShowStickers;


    [Header("Input")]

    public Collider2D switchCollider;


    private RouletteController controller;


    // =========================================================
    // UNITY
    // =========================================================

    private void Start()
    {
        controller =
            FindObjectOfType<RouletteController>();

        if (controller != null)
        {
            controller.OnSpinStart += HandleSpinStart;
            controller.OnSpinEnd += HandleSpinEnd;
        }

        /*
         * MOBILE / LEGACY
         *
         * Si existe StickerSlotsPanel,
         * conservamos exactamente el comportamiento anterior:
         * comenzamos mostrando los stickers.
         */
        if (stickerSlotsPanel != null)
        {
            ShowStickerSlots();
        }

        /*
         * PC
         *
         * Si StickerSlotsPanel no existe,
         * EnemyPanel es el único panel controlado
         * por este switcher.
         *
         * Lo dejamos visible inicialmente.
         */
        else
        {
            ShowEnemyPanel();
        }
    }


    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnSpinStart -= HandleSpinStart;
            controller.OnSpinEnd -= HandleSpinEnd;
        }
    }


    // =========================================================
    // INPUT
    // =========================================================

    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        if (!Input.GetMouseButtonDown(0))
            return;

        if (controller != null &&
            controller.SpinInProgress)
        {
            return;
        }

        Camera cam = Camera.main;

        if (cam == null)
            return;

        Vector2 pos =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );

        if (switchCollider != null &&
            switchCollider.OverlapPoint(pos))
        {
            TogglePanels();
        }

#endif
    }


    // =========================================================
    // SPIN EVENTS
    // =========================================================

    private void HandleSpinStart()
    {
        /*
         * Durante una tirada los enemigos siempre
         * deben ser visibles.
         *
         * Funciona tanto en PC como en mobile.
         */
        ShowEnemyPanel();
    }


    private void HandleSpinEnd()
    {
        /*
         * No cambiamos nada automáticamente.
         *
         * EnemyPanel permanece como haya quedado
         * hasta que el jugador pulse el botón.
         */
    }


    // =========================================================
    // PANEL MANAGEMENT
    // =========================================================

    private void TogglePanels()
    {
        if (enemyPanel == null)
            return;


        // -----------------------------------------------------
        // MOBILE / LEGACY
        // -----------------------------------------------------
        //
        // Si existe StickerSlotsPanel seguimos alternando
        // exactamente entre ambos paneles.
        // -----------------------------------------------------

        if (stickerSlotsPanel != null)
        {
            if (enemyPanel.activeSelf)
                ShowStickerSlots();
            else
                ShowEnemyPanel();

            return;
        }


        // -----------------------------------------------------
        // PC
        // -----------------------------------------------------
        //
        // No existe panel de stickers:
        // simplemente mostramos/ocultamos enemigos.
        // -----------------------------------------------------

        if (enemyPanel.activeSelf)
            HideEnemyPanel();
        else
            ShowEnemyPanel();
    }


    // =========================================================
    // LEGACY STICKER PANEL
    // =========================================================

    private void ShowStickerSlots()
    {
        /*
         * Si estamos en PC y este panel no existe,
         * simplemente ocultamos enemigos.
         */
        if (stickerSlotsPanel == null)
        {
            HideEnemyPanel();
            return;
        }

        stickerSlotsPanel.SetActive(true);

        if (enemyPanel != null)
            enemyPanel.SetActive(false);

        SetIcons(
            showEnemiesIcon: true
        );
    }


    // =========================================================
    // ENEMY PANEL
    // =========================================================

    private void ShowEnemyPanel()
    {
        if (stickerSlotsPanel != null)
            stickerSlotsPanel.SetActive(false);

        if (enemyPanel != null)
            enemyPanel.SetActive(true);

        SetIcons(
            showEnemiesIcon: false
        );
    }


    private void HideEnemyPanel()
    {
        if (enemyPanel != null)
            enemyPanel.SetActive(false);

        /*
         * En mobile este método normalmente no se utiliza,
         * pero si existe el panel antiguo lo devolvemos.
         */
        if (stickerSlotsPanel != null)
            stickerSlotsPanel.SetActive(true);

        SetIcons(
            showEnemiesIcon: true
        );
    }


    // =========================================================
    // ICONS
    // =========================================================

    private void SetIcons(
        bool showEnemiesIcon)
    {
        if (iconShowEnemies != null)
        {
            iconShowEnemies.enabled =
                showEnemiesIcon;
        }

        if (iconShowStickers != null)
        {
            iconShowStickers.enabled =
                !showEnemiesIcon;
        }
    }
}

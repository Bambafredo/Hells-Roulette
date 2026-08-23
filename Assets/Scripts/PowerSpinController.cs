using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PowerSpinController : MonoBehaviour
{
    // =========================================================
    // REFERENCES
    // =========================================================

    [Header("References")]
    public RouletteController roulette;

    [Tooltip("Collider del interruptor.")]
    public Collider2D switchCollider;

    [Tooltip("SpriteRenderer del interruptor.")]
    public SpriteRenderer switchRenderer;

    [Tooltip("SpriteRenderer de la parte rellena de la barra.")]
    public SpriteRenderer powerFillRenderer;


    // =========================================================
    // POWER
    // =========================================================

    [Header("Power")]

    [Tooltip(
        "Segundos que tarda la barra en ir desde 0 hasta su potencia máxima. " +
        "Después volverá hacia 0 y repetirá el ciclo."
    )]
    [Min(0.05f)]
    public float chargeDuration = 1.25f;

    [Tooltip(
        "Potencia máxima que puede generar ESTE interruptor. " +
        "1 = 100% de la potencia disponible en RouletteController. " +
        "0.75 = máximo 75%, etc."
    )]
    [Range(0.05f, 1f)]
    public float maxSwitchPower = 1f;


    // =========================================================
    // OPTIONAL VISUALS
    // =========================================================

    [Header("Optional Switch Visuals")]

    [Tooltip("Sprite normal del interruptor. Opcional.")]
    public Sprite releasedSprite;

    [Tooltip("Sprite del interruptor mientras está pulsado. Opcional.")]
    public Sprite pressedSprite;


    // =========================================================
    // INTERNAL
    // =========================================================

    private Camera cam;

    private bool isCharging = false;

    /*
     * Potencia REAL actual.
     *
     * Ya incluye maxSwitchPower.
     *
     * Por ejemplo:
     * maxSwitchPower = 0.8
     * charge01 oscilará entre 0 y 0.8.
     */
    private float charge01 = 0f;

    private float chargeElapsed = 0f;

    private Sprite initialSwitchSprite;


    // =========================================================
    // FILL DATA
    // =========================================================

    private Vector3 fillOriginalScale;

    /*
     * Borde izquierdo REAL del Fill cuando está
     * colocado al 100% exactamente como lo dejamos
     * en el Editor.
     */
    private float fillOriginalLeftWorldX;

    private bool fillInitialized = false;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool IsCharging
    {
        get { return isCharging; }
    }

    public float CurrentCharge01
    {
        get { return charge01; }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        cam = Camera.main;

        ResolveReferences();

        CacheVisualState();

        SetSwitchPressed(false);
        SetBarPower(0f);
    }


    private void OnEnable()
    {
        ResolveReferences();

        if (!fillInitialized)
            CacheVisualState();
    }


    private void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE

        ResolveReferences();

        if (cam == null)
            cam = Camera.main;

        if (cam == null ||
            roulette == null ||
            switchCollider == null)
        {
            return;
        }

        Vector2 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );


        // =====================================================
        // START CHARGE
        // =====================================================

        if (!isCharging &&
            Input.GetMouseButtonDown(0))
        {
            if (!switchCollider.OverlapPoint(mouseWorld))
                return;

            TryBeginCharge();

            return;
        }


        // =====================================================
        // CHARGING
        // =====================================================

        if (isCharging)
        {
            if (Input.GetMouseButton(0))
            {
                chargeElapsed +=
                    Time.deltaTime;

                /*
                 * PingPong:
                 *
                 * 0 → 1 → 0 → 1 → 0...
                 *
                 * chargeDuration representa cuánto tarda
                 * cada recorrido:
                 *
                 * 0 → MAX = chargeDuration
                 * MAX → 0 = chargeDuration
                 */
                float oscillator =
                    Mathf.PingPong(
                        chargeElapsed /
                        Mathf.Max(
                            0.05f,
                            chargeDuration
                        ),
                        1f
                    );

                /*
                 * Aplicamos el máximo particular
                 * de este interruptor.
                 *
                 * maxSwitchPower = 0.75:
                 *
                 * 0 → 0.75 → 0 → 0.75...
                 */
                charge01 =
                    oscillator *
                    maxSwitchPower;

                SetBarPower(
                    charge01
                );
            }


            // -------------------------------------------------
            // RELEASE
            // -------------------------------------------------

            if (Input.GetMouseButtonUp(0))
            {
                ReleaseCharge();
            }
        }

#endif
    }


    // =========================================================
    // MANUAL SPIN POWER DISPLAY
    // =========================================================

    private void LateUpdate()
    {
        if (isCharging)
            return;

        if (roulette == null)
            return;

        /*
         * CAMBIO:
         *
         * Mientras estamos físicamente arrastrando la ruleta
         * NO mostramos una preview de potencia.
         *
         * La barra permanece vacía.
         */
        if (roulette.IsDraggingWheel)
        {
            SetBarPower(0f);
            return;
        }

        /*
         * Al SOLTAR la ruleta, RouletteController ya habrá
         * calculado DisplayPower01.
         *
         * Ahí sí mostramos la potencia real del lanzamiento.
         */
        SetBarPower(
            roulette.DisplayPower01
        );
    }


    private void OnDisable()
    {
        CancelCharge();
    }


    private void OnDestroy()
    {
        CancelCharge();
    }


    private void OnApplicationFocus(
        bool hasFocus)
    {
        /*
         * Evitamos que un Alt+Tab mientras cargamos
         * deje el input de la ruleta bloqueado.
         */
        if (!hasFocus)
        {
            CancelCharge();
        }
    }


    // =========================================================
    // REFERENCES
    // =========================================================

    private void ResolveReferences()
    {
        if (roulette == null)
        {
            roulette =
                FindObjectOfType<RouletteController>();
        }

        if (switchCollider == null)
        {
            switchCollider =
                GetComponent<Collider2D>();
        }

        if (switchRenderer == null)
        {
            switchRenderer =
                GetComponent<SpriteRenderer>();
        }
    }


    // =========================================================
    // START CHARGE
    // =========================================================

    private void TryBeginCharge()
    {
        if (roulette == null)
            return;

        /*
         * Exactamente las mismas restricciones
         * que una tirada normal.
         */
        if (!roulette.CanStartNewSpin())
            return;


        isCharging = true;

        chargeElapsed = 0f;
        charge01 = 0f;

        SetBarPower(0f);

        SetSwitchPressed(true);


        /*
         * Impedimos que el mismo click sea interpretado
         * como un drag manual de la ruleta.
         */
        roulette.SetInputBlocked(true);
    }


    // =========================================================
    // RELEASE
    // =========================================================

    private void ReleaseCharge()
    {
        if (!isCharging)
            return;


        isCharging = false;

        SetSwitchPressed(false);


        float finalPower =
            Mathf.Clamp01(
                charge01
            );


        /*
         * Intentamos lanzar ANTES de liberar el input.
         *
         * TryStartPowerSpin puede funcionar con
         * inputBlocked porque ese flag bloquea únicamente
         * el input manual de RouletteController.
         */
        bool started =
            roulette != null &&
            roulette.TryStartPowerSpin(
                finalPower
            );


        if (roulette != null)
        {
            roulette.SetInputBlocked(false);
        }


        if (started)
        {
            /*
             * La barra queda enseñando la potencia exacta
             * de la tirada que acabamos de realizar.
             */
            SetBarPower(
                finalPower
            );
        }
        else
        {
            /*
             * Si hemos soltado prácticamente en 0
             * puede no alcanzar minThrowSpeed.
             *
             * No hay tirada y vaciamos la barra.
             */
            charge01 = 0f;
            chargeElapsed = 0f;

            SetBarPower(0f);
        }
    }


    // =========================================================
    // CANCEL
    // =========================================================

    private void CancelCharge()
    {
        if (!isCharging)
            return;

        isCharging = false;

        charge01 = 0f;
        chargeElapsed = 0f;

        SetSwitchPressed(false);

        SetBarPower(0f);

        if (roulette != null)
        {
            roulette.SetInputBlocked(false);
        }
    }


    // =========================================================
    // INITIAL VISUAL STATE
    // =========================================================

    private void CacheVisualState()
    {
        if (switchRenderer != null)
        {
            initialSwitchSprite =
                switchRenderer.sprite;
        }


        if (powerFillRenderer == null)
            return;


        /*
         * IMPORTANTE:
         *
         * En el Editor, Fill debe estar colocado
         * exactamente como quieres que se vea al 100%.
         */
        fillOriginalScale =
            powerFillRenderer
                .transform
                .localScale;


        /*
         * Guardamos el borde izquierdo físico REAL.
         *
         * A partir de ahora este punto no se moverá,
         * independientemente del pivot del sprite.
         */
        fillOriginalLeftWorldX =
            powerFillRenderer
                .bounds
                .min
                .x;


        fillInitialized = true;
    }


    // =========================================================
    // BAR
    // =========================================================

    private void SetBarPower(
        float power01)
    {
        if (powerFillRenderer == null)
            return;

        if (!fillInitialized)
            CacheVisualState();

        if (!fillInitialized)
            return;


        float p =
            Mathf.Clamp01(
                power01
            );


        /*
         * Lo activamos antes de consultar bounds
         * para que Renderer tenga geometría actualizada.
         */
        powerFillRenderer.enabled =
            true;


        Transform fill =
            powerFillRenderer.transform;


        // -----------------------------------------------------
        // SCALE
        // -----------------------------------------------------

        Vector3 newScale =
            fillOriginalScale;

        newScale.x =
            fillOriginalScale.x *
            p;

        fill.localScale =
            newScale;


        // -----------------------------------------------------
        // KEEP REAL LEFT EDGE FIXED
        // -----------------------------------------------------

        /*
         * Después de cambiar la escala preguntamos a Unity
         * dónde ha quedado realmente el borde izquierdo.
         */
        float currentLeftWorldX =
            powerFillRenderer
                .bounds
                .min
                .x;


        /*
         * Movemos el GO exactamente la diferencia necesaria
         * para devolver ese borde a su posición original.
         */
        float correction =
            fillOriginalLeftWorldX -
            currentLeftWorldX;


        Vector3 worldPosition =
            fill.position;

        worldPosition.x +=
            correction;

        fill.position =
            worldPosition;


        // -----------------------------------------------------
        // ZERO
        // -----------------------------------------------------

        /*
         * En 0 ocultamos el renderer para evitar
         * una línea de un pixel.
         */
        if (p <= 0.001f)
        {
            powerFillRenderer.enabled =
                false;
        }
    }


    // =========================================================
    // SWITCH VISUAL
    // =========================================================

    private void SetSwitchPressed(
        bool pressed)
    {
        if (switchRenderer == null)
            return;


        if (pressed)
        {
            if (pressedSprite != null)
            {
                switchRenderer.sprite =
                    pressedSprite;
            }

            return;
        }


        if (releasedSprite != null)
        {
            switchRenderer.sprite =
                releasedSprite;
        }
        else if (initialSwitchSprite != null)
        {
            switchRenderer.sprite =
                initialSwitchSprite;
        }
    }
}

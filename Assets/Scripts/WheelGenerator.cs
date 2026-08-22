using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WheelSegmentData
{
    public int index;
    public PolygonCollider2D collider;
    public SegmentMesh meshComponent;
}

public class WheelGenerator : MonoBehaviour
{
    [Header("Segments")]
    [Range(2, 36)]
    public int segmentCount = 8;

    [Tooltip("Ángulos individuales de cada segmento (en grados).")]
    public List<float> segmentAngles = new List<float>();

    [Min(0.1f)]
    public float radius = 2.5f;

    [Min(3)]
    public int meshResolution = 20;

    [Header("Visual")]
    public bool regenerateOnPlay = true;
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    [Header("Pins (ticks)")]
    public bool generatePins = true;
    public float pinRadiusOffset = -0.05f;
    public Vector2 pinSize = new Vector2(0.08f, 0.30f);
    public bool pinsAreTriggers = true;
    public string pinsParentName = "Pins";

    [Header("Runtime Stickers")]
    [Tooltip("Contenedor fuera de la rueda donde aparcamos stickers al regenerar.")]
    public Transform dynamicStickerContainer;

    [Tooltip("Referencia al FlagPin para NO destruirlo al regenerar.")]
    public FlagPin flagPin;

    [HideInInspector]
    public List<WheelSegmentData> segments = new List<WheelSegmentData>();

    // ------------------------------------------------------------
    // Info temporal para restaurar stickers tras regenerar
    // ------------------------------------------------------------

    private struct StickerRegenInfo
    {
        public Transform root;
        public int segmentIndex;
    }

    private readonly List<StickerRegenInfo> stickerRegenInfos =
        new List<StickerRegenInfo>();

    // =====================================================================
    // NORMALIZACIÓN DE ÁNGULOS = SIEMPRE 360º
    // =====================================================================

    void NormalizeAngles()
    {
        if (segmentAngles == null)
            segmentAngles = new List<float>();

        // Si el tamaño de la lista no coincide con el número
        // de segmentos, la reseteamos.
        if (segmentAngles.Count != segmentCount)
        {
            segmentAngles.Clear();

            float def = 360f / segmentCount;

            for (int i = 0; i < segmentCount; i++)
                segmentAngles.Add(def);

            return;
        }

        float sum = 0f;

        foreach (var a in segmentAngles)
            sum += a;

        // Si ya suman 360 (con tolerancia), no tocamos nada.
        if (Mathf.Abs(sum - 360f) < 0.01f)
            return;

        // Reescalamos proporcionalmente.
        float correction = 360f / sum;

        for (int i = 0; i < segmentAngles.Count; i++)
            segmentAngles[i] *= correction;
    }

    // =====================================================================
    // SCALE SEGMENT
    // =====================================================================

    public void ScaleSegmentAngle(int index, float factor)
    {
        if (index < 0 || index >= segmentAngles.Count)
            return;

        segmentAngles[index] *= factor;

        NormalizeAngles();

        GenerateWheel();
    }

    // =====================================================================
    // MAIN GENERATION
    // =====================================================================

    [ContextMenu("Generate Wheel")]
    public void GenerateWheel()
    {
        NormalizeAngles();

        // 1) Guardar stickers ANTES de destruir la rueda actual.
        SaveStickersBeforeGeneration();

        // 2) Destruir hijos actuales respetando el FlagPin.
        ForceDestroyChildren();

        // 3) Limpiar datos y regenerar segmentos.
        segments.Clear();

        Transform root =
            new GameObject("Segments").transform;

        root.SetParent(transform, false);

        int segmentLayer =
            LayerMask.NameToLayer("Segment");

        int pinLayer =
            LayerMask.NameToLayer("Pin");

        float angleStart = 0f;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = segmentAngles[i];

            GameObject segGO =
                new GameObject("Segment_" + i);

            segGO.transform.SetParent(root, false);

            // Rotación acumulada del segmento.
            segGO.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angleStart
                );

            // ---------------------------------------------------------
            // MESH FILTER
            // ---------------------------------------------------------

            MeshFilter mf =
                segGO.GetComponent<MeshFilter>();

            if (mf == null)
                mf = segGO.AddComponent<MeshFilter>();

            // ---------------------------------------------------------
            // MESH RENDERER
            // ---------------------------------------------------------

            MeshRenderer mr =
                segGO.GetComponent<MeshRenderer>();

            if (mr == null)
                mr = segGO.AddComponent<MeshRenderer>();

            // ---------------------------------------------------------
            // SEGMENT MESH
            // ---------------------------------------------------------

            SegmentMesh sm =
                segGO.GetComponent<SegmentMesh>();

            if (sm == null)
                sm = segGO.AddComponent<SegmentMesh>();

            sm.angle = angle;
            sm.radius = radius;
            sm.resolution = meshResolution;

            sm.color =
                Color.HSVToRGB(
                    i / (float)segmentCount,
                    0.85f,
                    1f
                );

            sm.GenerateMesh();

            mr.sortingLayerName =
                sortingLayerName;

            mr.sortingOrder =
                sortingOrder;

            // ---------------------------------------------------------
            // COLLIDER DEL SEGMENTO
            // ---------------------------------------------------------

            PolygonCollider2D poly =
                segGO.GetComponent<PolygonCollider2D>();

            if (poly == null)
                poly =
                    segGO.AddComponent<PolygonCollider2D>();

            poly.isTrigger = true;

            List<Vector2> pts =
                new List<Vector2>();

            // Centro de la ruleta.
            pts.Add(Vector2.zero);

            int res =
                Mathf.Max(
                    3,
                    meshResolution
                );

            for (int k = 0; k <= res; k++)
            {
                float t =
                    k / (float)res;

                float a =
                    t *
                    angle *
                    Mathf.Deg2Rad;

                pts.Add(
                    new Vector2(
                        Mathf.Cos(a) * radius,
                        Mathf.Sin(a) * radius
                    )
                );
            }

            poly.SetPath(
                0,
                pts.ToArray()
            );

            if (segmentLayer != -1)
                segGO.layer = segmentLayer;

            segments.Add(
                new WheelSegmentData
                {
                    index = i,
                    collider = poly,
                    meshComponent = sm
                }
            );

            angleStart += angle;
        }

        // 4) Generar pins.
        if (generatePins)
            GeneratePins(pinLayer);

        /*
         * IMPORTANTE:
         *
         * Hemos creado y movido bastantes Collider2D en este mismo frame.
         * Sincronizamos antes de restaurar los stickers para asegurarnos
         * de que las consultas físicas utilizan la geometría nueva.
         */
        Physics2D.SyncTransforms();

        // 5) Restaurar stickers sobre la nueva rueda.
        RestoreStickersAfterGeneration();

        /*
         * Volvemos a sincronizar porque los stickers también han cambiado
         * de parent/transform.
         */
        Physics2D.SyncTransforms();

        /*
         * WheelGenerator YA NO decide si un sticker es válido.
         *
         * StickerPlacementValidator llama a BaseSticker.DebugCheckSegment(),
         * que a su vez utiliza StickerPlacementUtility.
         *
         * Por tanto tenemos una única fuente de verdad.
         */
        StickerPlacementValidator.Instance?
            .ValidateAfterWheelRegeneration();
    }

    // =====================================================================
    // GUARDAR STICKERS ANTES DE REGENERAR
    // =====================================================================

    void SaveStickersBeforeGeneration()
    {
        stickerRegenInfos.Clear();

        // Asegurar que existe un contenedor dinámico.
        if (dynamicStickerContainer == null)
        {
            var go =
                new GameObject(
                    "DynamicStickerContainer"
                );

            dynamicStickerContainer =
                go.transform;
        }

        BaseSticker[] allStickers =
            FindObjectsOfType<BaseSticker>(true);

        foreach (BaseSticker s in allStickers)
        {
            if (s == null)
                continue;

            // Solo los stickers colocados en la ruleta.
            if (!s.isPlaced ||
                s.currentSegment == null)
            {
                continue;
            }

            int segIdx =
                ExtractIndexFromName(
                    s.currentSegment.name
                );

            if (segIdx < 0)
                continue;

            /*
             * Usamos stickerRoot como autoridad.
             *
             * Conservamos el fallback para no romper stickers antiguos
             * que pudieran no tenerlo asignado correctamente.
             */
            Transform root =
                s.stickerRoot != null
                    ? s.stickerRoot
                    : (
                        s.transform.parent != null
                            ? s.transform.parent
                            : s.transform
                    );

            stickerRegenInfos.Add(
                new StickerRegenInfo
                {
                    root = root,
                    segmentIndex = segIdx
                }
            );

            /*
             * Sacamos el sticker fuera de la rueda ANTES
             * de destruir los segmentos.
             *
             * Mantenemos posición y rotación mundiales.
             */
            root.SetParent(
                dynamicStickerContainer,
                true
            );
        }

        Physics2D.SyncTransforms();
    }

    // =====================================================================
    // GET SEGMENT INDEX
    // =====================================================================

    // Se mantiene por si queremos utilizarlo para debug
    // o eliminar dependencia del nombre Segment_X más adelante.
    int GetSegmentIndexFromSegmentTransform(
        Transform segTransform)
    {
        if (segTransform == null)
            return -1;

        for (int i = 0;
             i < segments.Count;
             i++)
        {
            if (segments[i].collider == null)
                continue;

            if (segments[i].collider.transform ==
                segTransform)
            {
                return segments[i].index;
            }
        }

        return -1;
    }

    // =====================================================================
    // RESTAURAR STICKERS DESPUÉS DE REGENERAR
    // =====================================================================

    void RestoreStickersAfterGeneration()
    {
        /*
         * IMPORTANTE:
         *
         * Aquí SOLO restauramos.
         *
         * Antes WheelGenerator restauraba un sticker e inmediatamente
         * lo validaba mientras otros stickers todavía no estaban
         * restaurados. Eso podía hacer que el resultado dependiese
         * del orden del foreach.
         *
         * Ahora:
         *
         * 1. Restauramos TODOS.
         * 2. Sincronizamos physics.
         * 3. StickerPlacementValidator valida TODOS.
         */

        foreach (var info in stickerRegenInfos)
        {
            Transform root =
                info.root;

            if (root == null)
                continue;

            BaseSticker s =
                root.GetComponentInChildren<BaseSticker>();

            if (s == null)
                continue;

            if (info.segmentIndex < 0 ||
                info.segmentIndex >= segments.Count)
            {
                Debug.LogWarning(
                    $"Sticker '{s.name}' lost its segment index after wheel regen."
                );

                continue;
            }

            WheelSegmentData segData =
                segments[info.segmentIndex];

            if (segData.collider == null)
            {
                Debug.LogWarning(
                    $"Sticker '{s.name}' has invalid segment collider after wheel regen."
                );

                continue;
            }

            Transform segTransform =
                segData.collider.transform;

            // Re-parent al nuevo segmento manteniendo world transform.
            root.SetParent(
                segTransform,
                true
            );

            // Actualizar estado lógico.
            s.currentSegment =
                segTransform;

            s.isPlaced =
                true;

            s.OnRestoredAfterWheelRegen();
        }

        /*
         * Muy importante antes de que otro sistema consulte
         * Collider2D.Distance / OverlapPoint / ClosestPoint.
         */
        Physics2D.SyncTransforms();

        stickerRegenInfos.Clear();
    }

    // =====================================================================
    // GENERAR PINS
    // =====================================================================

    void GeneratePins(int pinLayer)
    {
        Transform old =
            transform.Find(pinsParentName);

        if (old != null)
        {
#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                DestroyImmediate(
                    old.gameObject
                );
            }
            else
            {
                /*
                 * Destroy() es diferido hasta final de frame.
                 * Lo desactivamos primero para que sus colliders
                 * no sigan participando en Physics2D.
                 */
                old.gameObject.SetActive(false);

                Destroy(
                    old.gameObject
                );
            }

#else

            old.gameObject.SetActive(false);

            Destroy(
                old.gameObject
            );

#endif
        }

        Transform pinsRoot =
            new GameObject(
                pinsParentName
            ).transform;

        pinsRoot.SetParent(
            transform,
            false
        );

        float angleAccum = 0f;

        float r =
            radius +
            pinRadiusOffset;

        for (int i = 0;
             i < segmentCount;
             i++)
        {
            float mid =
                angleAccum;

            float rad =
                mid *
                Mathf.Deg2Rad;

            Vector3 pos =
                new Vector3(
                    Mathf.Cos(rad) * r,
                    Mathf.Sin(rad) * r,
                    0f
                );

            GameObject pin =
                new GameObject(
                    "Pin_" + i
                );

            pin.transform.SetParent(
                pinsRoot,
                false
            );

            pin.transform.localPosition =
                pos;

            pin.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    mid
                );

            BoxCollider2D bc =
                pin.AddComponent<BoxCollider2D>();

            bc.isTrigger =
                pinsAreTriggers;

            bc.size =
                pinSize;

            if (pinLayer != -1)
                pin.layer = pinLayer;

            angleAccum +=
                segmentAngles[i];
        }
    }

    // =====================================================================
    // DESTROY CHILDREN SIN CARGARSE EL FLAGPIN
    // =====================================================================

    void ForceDestroyChildren()
    {
        List<Transform> toDestroy =
            new List<Transform>();

        foreach (Transform t in transform)
        {
            /*
             * No destruimos un branch que contenga
             * el FlagPin.
             */
            if (t.GetComponentInChildren<FlagPin>(true)
                != null)
            {
                continue;
            }

            toDestroy.Add(t);
        }

        foreach (var t in toDestroy)
        {
            if (t == null)
                continue;

#if UNITY_EDITOR

            if (!Application.isPlaying)
            {
                DestroyImmediate(
                    t.gameObject
                );
            }
            else
            {
                /*
                 * Destroy es diferido.
                 *
                 * Desactivar antes evita que segmentos/pins viejos
                 * sigan participando en la física durante este frame.
                 */
                t.gameObject.SetActive(false);

                Destroy(
                    t.gameObject
                );
            }

#else

            t.gameObject.SetActive(false);

            Destroy(
                t.gameObject
            );

#endif
        }
    }

    // =====================================================================
    // START
    // =====================================================================

    void Start()
    {
        if (regenerateOnPlay)
            GenerateWheel();
    }

    // =====================================================================
    // SACAR ÍNDICE DEL NOMBRE "Segment_X"
    // =====================================================================

    int ExtractIndexFromName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1;

        if (!name.StartsWith("Segment_"))
            return -1;

        string number =
            name.Substring(
                "Segment_".Length
            );

        if (int.TryParse(
                number,
                out int idx))
        {
            return idx;
        }

        return -1;
    }
}
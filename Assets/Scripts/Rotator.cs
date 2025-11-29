using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Rotator : MonoBehaviour
{
    [Header("Rotator Settings")]
    public float rotationSpeed = 120f;

    private Collider2D rotatorCollider;

    private BaseSticker stickerInside = null;

    private void Awake()
    {
        rotatorCollider = GetComponent<Collider2D>();
        rotatorCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var sticker = other.GetComponentInParent<BaseSticker>();
        if (sticker != null)
            stickerInside = sticker;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var sticker = other.GetComponentInParent<BaseSticker>();
        if (sticker != null && sticker == stickerInside)
            stickerInside = null;
    }

    private void Update()
    {
        if (stickerInside == null) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        // Si no estás arrastrando, no girar
        if (!Input.GetMouseButton(0))
        {
            stickerInside = null;
            return;
        }
#endif

        // GIRA EL ROOT DEL STICKER
        Transform root = stickerInside.transform.parent ?? stickerInside.transform;
        root.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}

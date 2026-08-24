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

        if (rotatorCollider != null)
            rotatorCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        BaseSticker sticker = ResolveStickerFromCollider(other);

        if (sticker != null)
            stickerInside = sticker;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        BaseSticker sticker = ResolveStickerFromCollider(other);

        if (sticker != null &&
            sticker == stickerInside)
        {
            stickerInside = null;
        }
    }

    private void Update()
    {
        if (stickerInside == null)
            return;

#if UNITY_EDITOR || UNITY_STANDALONE

        // Only rotate while the player is actively holding the sticker.
        if (!Input.GetMouseButton(0))
        {
            stickerInside = null;
            return;
        }

#endif

        /*
         * Rotate the complete physical sticker prefab.
         *
         * New stickers can keep BaseSticker on an "Effect" child while
         * their real collider lives on Renderer/Sprite.
         */
        Transform root =
            stickerInside.stickerRoot != null
                ? stickerInside.stickerRoot
                : (
                    stickerInside.transform.parent != null
                        ? stickerInside.transform.parent
                        : stickerInside.transform
                  );

        root.Rotate(
            0f,
            0f,
            rotationSpeed * Time.deltaTime
        );
    }

    // =========================================================
    // STICKER RESOLUTION
    // =========================================================

    private BaseSticker ResolveStickerFromCollider(Collider2D other)
    {
        if (other == null)
            return null;

        /*
         * Backwards compatibility:
         *
         * Old stickers have Collider2D and BaseSticker in the same
         * object / parent hierarchy.
         */
        BaseSticker oldStyleSticker =
            other.GetComponentInParent<BaseSticker>();

        if (oldStyleSticker != null)
            return oldStyleSticker;

        /*
         * New sticker structure:
         *
         * StickerRoot
         * ├── Renderer
         * │   └── Sprite
         * │       └── Collider2D
         * └── Effect
         *     └── BaseSticker
         *
         * BaseSticker is a sibling of the collider, so
         * GetComponentInParent cannot find it.
         *
         * We walk upwards from the collider and inspect each local
         * subtree. The first BaseSticker whose configured
         * StickerCollider is EXACTLY the collider that touched the
         * Rotator is the correct sticker.
         *
         * This avoids identifying another sticker that happens to
         * share the same wheel segment.
         */
        Transform searchRoot =
            other.transform;

        while (searchRoot != null)
        {
            BaseSticker[] candidates =
                searchRoot.GetComponentsInChildren<BaseSticker>(true);

            foreach (BaseSticker candidate in candidates)
            {
                if (candidate == null)
                    continue;

                if (candidate.StickerCollider == other)
                    return candidate;
            }

            searchRoot =
                searchRoot.parent;
        }

        return null;
    }
}

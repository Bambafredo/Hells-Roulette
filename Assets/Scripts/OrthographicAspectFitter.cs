using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps horizontal world-space framing consistent on displays narrower than
/// the authored reference aspect ratio.
///
/// Hell's Roulette is authored at 16:9 with orthographic size 5.
/// On 16:10 Steam Deck, a normal orthographic camera shows less horizontal world.
/// This component increases orthographicSize just enough to preserve width.
///
/// 16:9 -> exact authored framing.
/// Narrower -> preserves horizontal framing by showing a bit more vertically.
/// Wider -> keeps authored vertical framing and simply shows more horizontally.
/// </summary>
[RequireComponent(typeof(Camera))]
public class OrthographicAspectFitter : MonoBehaviour
{
    [Header("Reference Framing")]
    [SerializeField] private float referenceAspect = 16f / 9f;
    [SerializeField] private float baseOrthographicSize = 5f;

    [Header("Behaviour")]
    [SerializeField] private bool onlyCompensateNarrowerAspects = true;

    private Camera targetCamera;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyAspectFit();
    }

    private void OnEnable()
    {
        ApplyAspectFit();
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight)
        {
            ApplyAspectFit();
        }
    }

    private void ApplyAspectFit()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null ||
            !targetCamera.orthographic ||
            Screen.height <= 0)
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float currentAspect = (float)Screen.width / Screen.height;

        if (currentAspect <= 0f || referenceAspect <= 0f)
        {
            targetCamera.orthographicSize = baseOrthographicSize;
            return;
        }

        if (onlyCompensateNarrowerAspects &&
            currentAspect >= referenceAspect)
        {
            targetCamera.orthographicSize = baseOrthographicSize;
            return;
        }

        targetCamera.orthographicSize =
            baseOrthographicSize *
            referenceAspect /
            currentAspect;
    }

    public void RefreshAspectFit()
    {
        ApplyAspectFit();
    }
}

// https://linework.ameye.dev/guides/outline-on-hover/

using UnityEngine;
using UnityEngine.EventSystems;

public class Outline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
#if UNITY_6000_0_OR_NEWER
    [SerializeField] private RenderingLayerMask outlineLayer;
#else
    [SerializeField] [RenderingLayerMask] private int outlineLayer;
#endif
    [SerializeField] private Activate activate = Activate.OnHover;

    private Renderer[] renderers;
    private uint originalLayer;
    private bool isOutlineActive;

    private enum Activate
    {
        OnHover,
        OnClick
    }

    private void Start()
    {
        renderers = TryGetComponent<Renderer>(out var meshRenderer)
            ? new[] {meshRenderer}
            : GetComponentsInChildren<Renderer>();
        originalLayer = renderers[0].renderingLayerMask;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (activate != Activate.OnHover) return;
        SetOutline(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (activate != Activate.OnHover) return;
        SetOutline(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (activate != Activate.OnClick) return;
        isOutlineActive = !isOutlineActive;
        SetOutline(isOutlineActive);
    }

    private void SetOutline(bool enable)
    {
        foreach (var rend in renderers)
        {
#if UNITY_6000_0_OR_NEWER
            rend.renderingLayerMask = enable
                ? originalLayer | outlineLayer
                : originalLayer;
#else
            rend.renderingLayerMask = enable 
	        ? originalLayer | 1u << (int)Mathf.Log(outlineLayer, 2)
	        : originalLayer;
#endif
        }
    }
}
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MyHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Outline outline;

    void Start()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false;   // apagado al inicio
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        outline.enabled = true;    // encender halo
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        outline.enabled = false;   // apagar halo
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class HandleDragHandlerThreshold : MonoBehaviour, IDragHandler
{
    public ThresholdSlider parentSlider; // threshold용
    private RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentSlider.bar,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        float barWidth = parentSlider.bar.rect.width;
        if (barWidth <= 0f) return;

        float newRatio = localPoint.x / barWidth;

        if (gameObject.name.Contains("Low"))
        {
            parentSlider.OnHandleLowDrag(newRatio);
        }
        else
        {
            parentSlider.OnHandleHighDrag(newRatio);
        }
    }
}
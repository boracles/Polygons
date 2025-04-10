using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class HandleDragHandlerMulti : MonoBehaviour, IDragHandler
{
    public MultiSegmentSlider parentSlider;
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

        if (gameObject.name.Contains("Handle1"))
        {
            parentSlider.OnHandle1Drag(newRatio);
        }
        else
        {
            parentSlider.OnHandle2Drag(newRatio);
        }
    }
}
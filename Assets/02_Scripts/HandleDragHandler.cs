using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class HandleDragHandler : MonoBehaviour, IDragHandler
{
    public MultiSegmentSlider parentSlider;
    private RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // bar 영역 내에서 마우스 좌표 -> local 좌표로 변환
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentSlider.bar, eventData.position, eventData.pressEventCamera, out localPoint
        );

        // handle1인지 handle2인지 구분 위해, name으로 분기
        if (gameObject.name.Contains("Handle1"))
        {
            parentSlider.OnHandle1Drag(localPoint.x);
        }
        else
        {
            parentSlider.OnHandle2Drag(localPoint.x);
        }
    }
}
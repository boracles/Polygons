using UnityEngine;
using UnityEngine.UI;

public class MultiSegmentSlider : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform bar;              // 슬라이더 전체 배경
    public RectTransform segmentSphere;     // 검정 영역
    public RectTransform segmentCube;     // 흰색 영역
    public RectTransform segmentEmpty;     // 빈칸 영역

    public RectTransform handle1;          // 첫 번째 핸들
    public RectTransform handle2;          // 두 번째 핸들

    [Header("Output (Optional)")]
    public Text ratioText;                 // 비율 표시
    
    private float totalWidth; // bar의 실제 폭(픽셀 단위)
    float blackRatio;
    float whiteRatio;
    float emptyRatio;
    
    public float GetBlackRatio() { return blackRatio; }
    public float GetWhiteRatio() { return whiteRatio; }
    public float GetEmptyRatio() { return emptyRatio; }

    void Start()
    {
        totalWidth = bar.rect.width;
        
        // 초기 핸들 위치
        handle1.anchoredPosition = new Vector2(0.2f * totalWidth, 0f);
        handle2.anchoredPosition = new Vector2(0.7f * totalWidth, 0f);

        UpdateSegments();
    }

    // 첫 번째 핸들 드래그할 때
    public void OnHandle1Drag(float newX)
    {
        float clamped = Mathf.Clamp(newX, 0, handle2.anchoredPosition.x);
        handle1.anchoredPosition = new Vector2(clamped, 0f);
        UpdateSegments();
    }

    // 두 번째 핸들 드래그할 때 
    public void OnHandle2Drag(float newX)
    {
        float clamped = Mathf.Clamp(newX, handle1.anchoredPosition.x, totalWidth);
        handle2.anchoredPosition = new Vector2(clamped, 0f);
        UpdateSegments();
    }

    // 세그먼트 UI 갱신 및 SegregationManager에 즉시 반영 -> 보드 재생성
    private void UpdateSegments()
    {
        float x1 = handle1.anchoredPosition.x;
        float x2 = handle2.anchoredPosition.x;
        
        segmentSphere.anchoredPosition = new Vector2(0f, 0f);
        segmentSphere.sizeDelta = new Vector2(x1, segmentSphere.sizeDelta.y);
        
        segmentCube.anchoredPosition = new Vector2(x1, 0f);
        segmentCube.sizeDelta = new Vector2(x2 - x1, segmentCube.sizeDelta.y);
        
        segmentEmpty.anchoredPosition = new Vector2(x2, 0f);
        segmentEmpty.sizeDelta = new Vector2(totalWidth - x2, segmentEmpty.sizeDelta.y);

        // 비율 계산
        blackRatio = x1 / totalWidth;
        whiteRatio = (x2 - x1) / totalWidth;
        emptyRatio = 1f - (blackRatio + whiteRatio);

        // 텍스트 표시
        if (ratioText != null)
        {
            ratioText.text = string.Format(
                "구={0:P0}, 큐브={1:P0}, 빈칸={2:P0}",
                blackRatio, whiteRatio, emptyRatio
            );
        }

        // SegregationManager에 즉시 반영 + 보드 재생성! 
        SegregationManager mgr = FindObjectOfType<SegregationManager>();
        if (mgr != null)
        {
            mgr.blackRatio = blackRatio;
            mgr.whiteRatio = whiteRatio;
            mgr.emptyRatio = emptyRatio;

            // 보드를 즉시 재생성(매번 Destroy→Instantiate 수행)
            mgr.InitBoard();
        }
    }
}

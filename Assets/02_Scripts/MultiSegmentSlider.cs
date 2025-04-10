using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiSegmentSlider : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform bar;           
    public RectTransform segmentSphere;    
    public RectTransform segmentCube;  
    public RectTransform segmentEmpty;

    public RectTransform handle1;          // 첫 번째 핸들
    public RectTransform handle2;          // 두 번째 핸들

    [Header("Output")]
    public TMP_Text ratioText;                 // 비율 표시
    
    private float handle1Ratio = 0.2f;
    private float handle2Ratio = 0.7f;
    
    private float totalWidth; // bar의 실제 폭(픽셀 단위)
    float sphereRatio;
    float cubeRatio;
    float emptyRatio;
    
    public float GetBlackRatio() { return sphereRatio; }
    public float GetWhiteRatio() { return cubeRatio; }
    public float GetEmptyRatio() { return emptyRatio; }

    void Start()
    {
        totalWidth = bar.rect.width;
        handle1Ratio = 0.4f; 
        handle2Ratio = 0.8f;

        
        UpdateHandlePositions();
        UpdateSegments();
    }

    // 첫 번째 핸들 드래그할 때
    public void OnHandle1Drag(float newRatio)
    {
        float clamped = Mathf.Clamp(newRatio, 0, handle2Ratio);
        handle1Ratio = clamped;

        UpdateHandlePositions();
        UpdateSegments();
    }
    
    public void OnHandle2Drag(float newRatio)
    {
        float clamped = Mathf.Clamp(newRatio, handle1Ratio, 1.0f);
        handle2Ratio = clamped;
        
        UpdateHandlePositions();
        UpdateSegments();
    }
    
    private void UpdateHandlePositions()
    {
        float barWidth = bar.rect.width;

        Vector2 pos1 = handle1.anchoredPosition;
        pos1.x = handle1Ratio * barWidth; 
        handle1.anchoredPosition = pos1;

        Vector2 pos2 = handle2.anchoredPosition;
        pos2.x = handle2Ratio * barWidth;
        handle2.anchoredPosition = pos2;
    }

    
    private void UpdateSegments()
    {
        float barWidth = bar.rect.width;
        
        float x1 = handle1Ratio * barWidth;
        float x2 = handle2Ratio * barWidth;

        segmentSphere.anchoredPosition = new Vector2(0f, 0f);
        segmentSphere.sizeDelta = new Vector2(x1, segmentSphere.sizeDelta.y);
        
        segmentCube.anchoredPosition = new Vector2(x1, 0f);
        segmentCube.sizeDelta = new Vector2(x2 - x1, segmentCube.sizeDelta.y);
        
        segmentEmpty.anchoredPosition = new Vector2(x2, 0f);
        segmentEmpty.sizeDelta = new Vector2(totalWidth - x2, segmentEmpty.sizeDelta.y);
        
        sphereRatio = handle1Ratio;
        cubeRatio = handle2Ratio - handle1Ratio;
        emptyRatio = 1f - handle2Ratio;
        
        if (ratioText != null)
        {
            // 0.5 → 50, 0.2 → 20 식으로 표기
            float spherePerc = sphereRatio * 100f;
            float cubePerc   = cubeRatio * 100f;    
            float emptyPerc  = emptyRatio * 100f;   

            ratioText.text = 
                $"the sphere:cube ratio is <color=#FF0000>{spherePerc:0}:{cubePerc:0}</color>, board is <color=#FF0000>{emptyPerc:0}% empty</color>";
        }
        
        SegregationManager mgr = FindObjectOfType<SegregationManager>();
        if (mgr != null)
        {
            mgr.blackRatio = sphereRatio;
            mgr.whiteRatio = cubeRatio;
            mgr.emptyRatio = emptyRatio;
            mgr.InitBoard();
        }
    }
}

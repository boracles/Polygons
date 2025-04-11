using UnityEngine;
using TMPro;

public class ThresholdSlider : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform bar;
    public RectTransform handleLow;
    public RectTransform handleHigh;

    public RectTransform segmentLow;  
    public RectTransform segmentMiddle; 
    public RectTransform segmentHigh;    
    
    [Header("Display Text")]
    public TMP_Text infoText;
    
    private float thresholdLow = 0.2f; 
    private float thresholdHigh = 0.8f;

    void Start()
    {
        UpdateHandlePositions();
        UpdateSegmentsAndDisplay();
    }

    public void OnHandleLowDrag(float newRatio)
    {
        thresholdLow = Mathf.Clamp(newRatio, 0f, thresholdHigh);
        UpdateHandlePositions();
        UpdateSegmentsAndDisplay();
    }

    public void OnHandleHighDrag(float newRatio)
    {
        thresholdHigh = Mathf.Clamp(newRatio, thresholdLow, 1f);
        UpdateHandlePositions();
        UpdateSegmentsAndDisplay();
    }

    private void UpdateHandlePositions()
    {
        float width = bar.rect.width;
        
        Vector2 posL = handleLow.anchoredPosition;
        posL.x = thresholdLow * width; 
        handleLow.anchoredPosition = posL;
        
        Vector2 posH = handleHigh.anchoredPosition;
        posH.x = thresholdHigh * width;
        handleHigh.anchoredPosition = posH;
    }
    private void UpdateSegmentsAndDisplay()
    {
        float width = bar.rect.width;

        // 구간별 x좌표
        float xLow = thresholdLow * width;
        float xHigh = thresholdHigh * width;

        // segmentLow: [0 ~ xLow]
        segmentLow.anchoredPosition = new Vector2(0f, 0f);
        segmentLow.sizeDelta = new Vector2(xLow, segmentLow.sizeDelta.y);

        // segmentMiddle: [xLow ~ xHigh]
        segmentMiddle.anchoredPosition = new Vector2(xLow, 0f);
        segmentMiddle.sizeDelta = new Vector2(xHigh - xLow, segmentMiddle.sizeDelta.y);

        // segmentHigh: [xHigh ~ width]
        segmentHigh.anchoredPosition = new Vector2(xHigh, 0f);
        segmentHigh.sizeDelta = new Vector2(width - xHigh, segmentHigh.sizeDelta.y);

        // Text 표시
        float lowPercent = thresholdLow * 100f;
        float highPercent = thresholdHigh * 100f;
        if (infoText != null)
        {
            infoText.text =
                $"i'll move if <color=#B71C1C><{lowPercent:0}%</color> " +
                $"or <color=#B71C1C>>{highPercent:0}%</color> of my neighbors are like me";
        }
        
        SegregationManager mgr = FindObjectOfType<SegregationManager>();
        if (mgr != null)
        {
            mgr.lowerThreshold = thresholdLow;
            mgr.upperThreshold = thresholdHigh;
            
            mgr.RecalcAllAgentsState();
        }
    }
}

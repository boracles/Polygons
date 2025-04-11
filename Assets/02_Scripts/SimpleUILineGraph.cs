using UnityEngine;
using UnityEngine.UI.Extensions; // UI Extensions namespace
using TMPro;
using System.Collections.Generic;

public class SimpleUILineGraph : MonoBehaviour
{
    public UILineRenderer lineRenderer;
    public RectTransform graphArea;
    public TMP_Text endLabel;
    
    [SerializeField] private int ringSize = 100;
    [SerializeField] private float spacing = 10f;
    
    public void UpdateGraph(List<float> data, float currentRate)
    {
        // 데이터 없는 경우
        if(data == null || data.Count == 0)
        {
            lineRenderer.Points = new Vector2[0];
            return;
        }
        
        // 데이터 1개
        if(data.Count == 1)
        {
            float h = graphArea.rect.height;
            float y = data[0] * h;
            lineRenderer.Points = new Vector2[] { new Vector2(0,y) };
            lineRenderer.RelativeSize = false;
            return;
        }
        
        // ringSize 로직
        int totalCount = data.Count;
        int countToDraw = Mathf.Min(totalCount, ringSize);
        int startIndex = totalCount - countToDraw;
        
        // 다중 세그먼트 리스트
        List<List<Vector2>> segmentList = new List<List<Vector2>>();
        segmentList.Add(new List<Vector2>()); // 첫 세그먼트 생성
        
        float graphHeight = graphArea.rect.height;
        int prevRingIndex = -1;
        
        for(int i = 0; i < countToDraw; i++)
        {
            int globalIndex = startIndex + i;
            int ringIndex   = globalIndex % ringSize;

            // 랩핑 감지 -> 새 세그먼트
            if (i > 0 && ringIndex < prevRingIndex)
            {
                segmentList.Add(new List<Vector2>());
            }
            prevRingIndex = ringIndex;
            
            float x = ringIndex * spacing;
            float y = data[globalIndex] * graphHeight;
            segmentList[segmentList.Count - 1].Add(new Vector2(x, y));
        }
        
        List<Vector2[]> finalSegments = new List<Vector2[]>(segmentList.Count);
        for(int s = 0; s < segmentList.Count; s++)
        {
            finalSegments.Add(segmentList[s].ToArray());
        }

        lineRenderer.Segments = finalSegments;
        lineRenderer.RelativeSize = false;
        
        // 마지막 점 레이블
        if(endLabel != null && segmentList.Count > 0)
        {
            // 마지막 세그먼트
            List<Vector2> lastSegment = segmentList[segmentList.Count - 1];
            if(lastSegment.Count > 0)
            {
                Vector2 lastPoint = lastSegment[lastSegment.Count - 1];
                string labelText = Mathf.RoundToInt(currentRate * 100f) + "%";
                endLabel.text = labelText;
                endLabel.rectTransform.anchoredPosition = lastPoint + new Vector2(20f, 0f);
            }
        }
    }
}
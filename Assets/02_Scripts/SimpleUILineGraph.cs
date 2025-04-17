using UnityEngine;
using UnityEngine.UI.Extensions; // UI Extensions namespace
using TMPro;
using System.Collections.Generic;

public class SimpleUILineGraph : MonoBehaviour
{
    public UILineRenderer lineRenderer;
    public RectTransform graphArea;
    public TMP_Text endLabel;
    
    [SerializeField] private int ringSize = 300;
    [SerializeField] private float spacing = 10f;
    
    // 그래프 전체 초기화
    public void ClearGraph()
    {
        if(lineRenderer != null)
        {
            lineRenderer.Segments = new List<Vector2[]>();
            lineRenderer.RelativeSize = false;
        }
        if(endLabel != null)
            endLabel.text = "";
    }
    
    public void StartNewPhaseGraph(List<float> data, float currentRate)
    {
        // 이전 라인 지우기
        ClearGraph();
        // 새 데이터 그리기
        UpdateGraph(data, currentRate);
    }
    
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
        
        int totalCount = data.Count;
        float graphH = graphArea.rect.height;
        
        if(totalCount < ringSize)
        {
            // 하나의 세그먼트
            List<Vector2> singleSeg = new List<Vector2>(totalCount);
            float maxX = (ringSize - 1) * spacing; 
            
            for(int i=0; i< totalCount; i++)
            {
                float x = i* (maxX/(ringSize-1));
                float y = data[i]* graphH;
                singleSeg.Add(new Vector2(x,y));
            }

            var finalSegs = new List<Vector2[]>();
            finalSegs.Add(singleSeg.ToArray());
            lineRenderer.Segments = finalSegs;
            lineRenderer.RelativeSize = false;
            
            // 마지막 점 레이블
            if(endLabel!=null && singleSeg.Count>0)
            {
                Vector2 lastPt= singleSeg[singleSeg.Count -1];
                endLabel.text= Mathf.RoundToInt(currentRate*100f)+"%";
                endLabel.rectTransform.anchoredPosition= lastPt + new Vector2(20f,0f);
            }
        }
        else
        {
            // 데이터 >= ringSize -> ring
            int countToDraw = ringSize;
            int startIndex  = totalCount - countToDraw;

            float graphHeight = graphArea.rect.height;
            List<List<Vector2>> segmentList = new List<List<Vector2>>();
            segmentList.Add(new List<Vector2>());
            
            int prevRingIndex = -1;
            for(int i=0; i<countToDraw; i++)
            {
                int globalIndex = startIndex + i;
                int ringIndex   = globalIndex % ringSize;

                // 래핑 감지 -> “과거 세그먼트 전부 지우고” 새 세그먼트 시작
                if(i>0 && ringIndex < prevRingIndex)
                {
                    // 이전 세그먼트는 전부 제거
                    segmentList.Clear();

                    // 새로 리스트를 다시 생성
                    segmentList = new List<List<Vector2>>();
                    segmentList.Add(new List<Vector2>());
                }

                prevRingIndex = ringIndex;

                float x= ringIndex * spacing;
                float y= data[globalIndex] * graphHeight;
                segmentList[segmentList.Count - 1].Add(new Vector2(x, y));
            }

            List<Vector2[]> finalSegments = new List<Vector2[]>(segmentList.Count);
            for(int s=0; s<segmentList.Count; s++)
            {
                finalSegments.Add(segmentList[s].ToArray());
            }
            lineRenderer.Segments = finalSegments;
            lineRenderer.RelativeSize = false;

            // 마지막 점
            if(endLabel!=null && segmentList.Count>0)
            {
                var lastSeg= segmentList[segmentList.Count-1];
                if(lastSeg.Count>0)
                {
                    Vector2 lastPt= lastSeg[lastSeg.Count -1];
                    endLabel.text= Mathf.RoundToInt(currentRate*100f)+"%";
                    endLabel.rectTransform.anchoredPosition= lastPt + new Vector2(20f,0f);
                }
            }
        }
    }
}
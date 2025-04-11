using UnityEngine;
using UnityEngine.UI.Extensions; // UI Extensions namespace
using System.Collections.Generic;

public class SimpleUILineGraph : MonoBehaviour
{
    public UILineRenderer lineRenderer;
    public RectTransform graphArea;

    // 원하는 픽셀 간격
    [SerializeField] private float xSpacing = 30f;
    public void UpdateGraph(List<float> data)
    {
        if(data == null || data.Count == 0)
        {
            // 데이터가 없으면 선을 그리지 않음
            lineRenderer.Points = new Vector2[0];
            return;
        }
        
        Vector2[] points = new Vector2[data.Count];
        float h = graphArea.rect.height;
        
        for(int i=0; i<data.Count; i++)
        {
            float x = i * xSpacing;
            float y = data[i] * h;
            points[i] = new Vector2(x, y);
        }
        lineRenderer.Points = points;
        lineRenderer.RelativeSize = false; // use absolute coords
    }
}
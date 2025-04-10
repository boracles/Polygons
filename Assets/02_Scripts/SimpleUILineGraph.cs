using UnityEngine;
using UnityEngine.UI.Extensions; // UI Extensions namespace
using System.Collections.Generic;

public class SimpleUILineGraph : MonoBehaviour
{
    public UILineRenderer lineRenderer;
    public RectTransform graphArea;

    public void UpdateGraph(List<float> data)
    {
        Vector2[] points = new Vector2[data.Count];
        float w = graphArea.rect.width;
        float h = graphArea.rect.height;
        for(int i=0; i<data.Count; i++)
        {
            float x = (float)i / (data.Count - 1) * w; // 0~w
            float y = data[i] * h;                    // 0~h
            points[i] = new Vector2(x, y);
        }
        lineRenderer.Points = points;
        lineRenderer.RelativeSize = false; // use absolute coords
    }
}
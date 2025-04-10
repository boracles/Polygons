using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RawImage))]
public class GraphDrawer : MonoBehaviour
{
    public int graphWidth = 300;
    public int graphHeight = 150;
    public Color backgroundColor = Color.black;
    public Color lineColor = Color.red;

    private RawImage rawImage;
    private Texture2D tex;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        // 초기 Texture2D 생성
        tex = new Texture2D(graphWidth, graphHeight, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        ClearTexture(); // 배경 초기화
        rawImage.texture = tex;
    }

    /// <summary>
    /// dataList: 라운드별 segregationRate (0~1)
    /// </summary>
    public void UpdateGraph(List<float> dataList)
    {
        ClearTexture();
        // dataList.Count가 graphWidth를 넘어가면 X축 스케일 조정이 필요
        // 간단히 "가장 오른쪽 끝"이 최신 데이터로 그리는 예시

        int maxCount = Mathf.Min(dataList.Count, graphWidth);
        // Y축은 0~1를 graphHeight 범위로 변환
        // (0 -> 0, 1 -> graphHeight-1)

        for(int i=1; i<maxCount; i++)
        {
            float prevVal = dataList[dataList.Count - i];
            float currVal = dataList[dataList.Count - (i+1)];

            int x1 = graphWidth - i;
            int x0 = graphWidth - (i+1);

            int y0 = (int)Mathf.Clamp(currVal * (graphHeight-1), 0, graphHeight-1);
            int y1 = (int)Mathf.Clamp(prevVal * (graphHeight-1), 0, graphHeight-1);

            // 선을 그린다 (직선 보간)
            DrawLine(tex, x0, y0, x1, y1, lineColor);
        }

        tex.Apply();
    }

    void ClearTexture()
    {
        Color[] bg = new Color[graphWidth*graphHeight];
        for(int i=0; i<bg.Length; i++)
            bg[i] = backgroundColor;
        tex.SetPixels(bg);
        tex.Apply();
    }

    /// <summary>
    /// (x0,y0) ~ (x1,y1) 픽셀 선 긋기 (Bresenham 또는 단순 DDA)
    /// 여기서는 간단한 DDA
    /// </summary>
    void DrawLine(Texture2D t, int x0, int y0, int x1, int y1, Color col)
    {
        int dx = x1 - x0;
        int dy = y1 - y0;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        float sx = dx / (float)steps;
        float sy = dy / (float)steps;
        float x = x0;
        float y = y0;
        for(int i=0; i<=steps; i++)
        {
            if((int)x >= 0 && (int)x < graphWidth && (int)y >= 0 && (int)y < graphHeight)
            {
                t.SetPixel((int)x, (int)y, col);
            }
            x += sx;
            y += sy;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

public class ThresholdLandscapeManager : MonoBehaviour
{
    [Header("에이전트 프리팹")]
    [SerializeField] GameObject mainPrefab;
    [SerializeField] GameObject targetPrefab;

    [Header("비율")]
    [Range(0f, 1f)] public float mainRatio   = .7f;   // 예: 70%
    [Range(0f, 1f)] public float targetRatio = .3f;   // 예: 30%
    
    [Header("그리드")]
    const int   GRID   = 20; 
    [SerializeField] float cellGap   = 1.0f;   // 좌표 간격
    [SerializeField] int   spawn = 144;  // 스폰 수

    void Start()
    {
        var rooms = GenerateRooms();   // 200칸
        Shuffle(rooms);
        rooms.RemoveRange(spawn, rooms.Count - spawn);

        int mainCnt = Mathf.RoundToInt(spawn * mainRatio);
        int targCnt = spawn - mainCnt;

        for (int i = 0; i < mainCnt; i++)
            Instantiate(mainPrefab, rooms[i], Quaternion.identity, transform);

        for (int i = 0; i < targCnt; i++)
            Instantiate(targetPrefab, rooms[mainCnt + i], Quaternion.identity, transform);
    }
    /* (x+z) 짝수 칸 좌표 리스트 (200개) */
    List<Vector3> GenerateRooms()
    {
        var list = new List<Vector3>(200);
        const float Y = 0.5f;          // 요청한 높이

        for (int z = 0; z < GRID; z++)
        for (int x = 0; x < GRID; x++)
            if ( (x + z) % 2 == 0 )                 // 체커보드
                list.Add(new Vector3(
                    x * cellGap,                    // 0‥19
                    Y,
                    -z * cellGap));                  // 0‥-19  (음수)
        return list;
    }

    /* Fisher–Yates 셔플 */
    void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

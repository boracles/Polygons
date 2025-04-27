using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class ThresholdLandscapeManager : MonoBehaviour
{
    [Header("Grid Size (world-unit)")]
    public int width  = 20;
    public int height = 20;

    [Header("Agent Prefabs")]
    public GameObject mainPrefab;
    public GameObject targetPrefab;

    [Header("Spawn Ratio (0-1, 합계 = 1)")]
    [Range(0,1)] public float mainRatio   = .7f;   // mainPrefab 비율
    [Range(0,1)] public float targetRatio = .3f;   // targetPrefab 비율

    [Header("초기 배치 비율")]
    [Range(0,1)]
    public float startInRoomRatio = .5f;           // 처음에 ‘방’에 들어갈 확률 (나머지는 길)

    const int TOTAL_AGENTS = 144;                  // 총 에이전트 수 == 144

    /*─────── 방 좌표 관리 ───────*/
    readonly List<Vector3> rooms = new();          // 144개 방 world 좌표
    readonly HashSet<(int,int)> roomSet = new();   // 빠른 룩업용
    bool[] occupied;                   

    /* 싱글턴(간단 접근용) */
    public static ThresholdLandscapeManager I { get; private set; }
    void Awake() => I = this;

// ─ 기존 함수 그대로 두고 ─
public bool TryReserveFreeRoom(out int roomIndex)
    => TryReserveFreeRoom(out roomIndex, exclude:-1);

// ─ ‘이 방은 빼고 찾아 줘’ 버전 ─
public bool TryReserveFreeRoom(out int roomIndex, int exclude)
{
    for (int i = 0; i < occupied.Length; ++i)
        if (i != exclude && !occupied[i])
        {
            occupied[i] = true;
            roomIndex   = i;
            return true;
        }
    roomIndex = -1;
    return false;
}


    /*──────────── ENTRY ────────────*/
    void Start()
    {
        BuildRoomCoordinates();
        occupied = new bool[rooms.Count];
        SpawnAgentsOnce();
    }

    /*──────── Room 좌표 생성 ────────*/
    static readonly int[] idx = {1,2,4,5,7,8,11,12,14,15,17,18};
    
   void BuildRoomCoordinates()
{
    rooms.Clear();
    roomSet.Clear();

    foreach (int gx in idx)
    foreach (int gz in idx)
    {
        Vector3 approx = new Vector3(gx, 0.5f, -gz);         // 격자 중앙

        // ★ 한번만 NavMesh 위로 스냅해서 저장
        if (NavMesh.SamplePosition(approx, out var hit, 1f, NavMesh.AllAreas))
            approx = hit.position;                           // 스냅 성공

        rooms.Add(approx);                                   // 항상 NavMesh 위!
        roomSet.Add((gx, gz));                               // 룩업용 해시
    }
}
    
   public void VacateRoom(int roomIndex)
   {
       if (roomIndex < 0 || roomIndex >= occupied.Length) return;
       occupied[roomIndex] = false;
   }
    public Vector3 GetRoomPosition(int roomIndex) => rooms[roomIndex];

    void SpawnAgentsOnce()
{
    int needMain   = Mathf.RoundToInt(TOTAL_AGENTS * mainRatio / (mainRatio + targetRatio));
    int needTarget = TOTAL_AGENTS - needMain;

    int spawned = 0;
    int safety  = 10_000;

    while (spawned < TOTAL_AGENTS && --safety > 0)
    {
        /* ───── (A) 어떤 프리팹? ───── */
        GameObject prefab;
        if (needMain>0 && needTarget>0)
        {
            float p = needMain / (float)(needMain + needTarget);
            if (Random.value < p) { prefab = mainPrefab;   --needMain; }
            else                  { prefab = targetPrefab; --needTarget; }
        }
        else if (needMain>0) { prefab = mainPrefab;   --needMain; }
        else                 { prefab = targetPrefab; --needTarget; }

        /* ───── (B) 배치 위치 계산 ───── */
        Vector3 spawnPos = Vector3.zero;   // ← 여기서 미리 선언!

        int     roomIdx   = -1;
        bool    positionReady = false;     // 값이 준비됐는지 표시

        bool putInRoom = Random.value < startInRoomRatio;

        if (putInRoom && TryReserveFreeRoom(out roomIdx))
        {
            spawnPos = rooms[roomIdx];
            if (NavMesh.SamplePosition(spawnPos, out var hit, 1f, NavMesh.AllAreas))
            {
                spawnPos      = hit.position;
                positionReady = true;
            }
            else
            {
                VacateRoom(roomIdx);
            }
        }
        else     // 길 위에 두기
        {
            for (int t = 0; t < 50 && !positionReady; ++t)
            {
                int gx = Random.Range(0, width);
                int gz = Random.Range(0, height);
                if (roomSet.Contains((gx, gz))) continue;

                Vector3 desired = new Vector3(gx, 0.5f, -gz);
                if (NavMesh.SamplePosition(desired, out var hit, 1f, NavMesh.AllAreas))
                {
                    spawnPos      = hit.position;
                    positionReady = true;
                }
            }
        }

        if (!positionReady) continue;   // 유효 좌표 못 찾음 → 다음 반복

        /* ───── (C) Instantiate ───── */
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);   // 부모 없이 생성
        go.transform.SetParent(transform, true);                              // 부모 설정

        var agent = go.GetComponent<RoomAgent>() ?? go.AddComponent<RoomAgent>();
        agent.Init(roomIdx);          // roomIdx == -1 → 길에서 시작

        ++spawned;
    }
}

}

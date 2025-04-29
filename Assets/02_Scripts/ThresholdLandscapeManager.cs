using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ThresholdLandscapeManager : MonoBehaviour
{
    public static ThresholdLandscapeManager I { get; private set; }
    void Awake() => I = this;
    
    /* ───── 방 인덱스(12칸) ───── */
    static readonly int[] idx = { 1,2, 4,5, 7,8, 11,12, 14,15, 17,18 };
    
    /* ───── 룸 테이블 ───── */
    public class RoomInfo
    {
        public Vector3 pos;          // 방 월드 좌표
        public GameObject occupant;  // 점유 중 에이전트 (null 이면 빈방)
    }
    readonly Dictionary<int, RoomInfo>        rooms        = new();        // roomId → info
    readonly Dictionary<(int x,int z), int>   gridToRoomId = new();

    public class GroupInfo
    {
        public readonly List<int> roomIds = new();          // 이 블록 안 방ID
    }
    readonly Dictionary<int, GroupInfo> groups = new();     // groupId(0‥35) → info
    readonly Dictionary<int, int>       roomToGroup = new();// roomId → groupId

    /* ───── 그리드 파라미터 ───── */
    const int GRID = 20;           // 0‥19
    [SerializeField] float cellGap = 1f;
    const float Y = 0.5f;

    /* ───── 초기 스폰 설정 ───── */
    [Header("에이전트 프리팹")]
    [SerializeField] GameObject mainPrefab;
    [SerializeField] GameObject targetPrefab;
    [Header("비율")]
    [Range(0,1)] public float mainRatio = .7f;
    [Range(0,1)] public float targetRatio = .3f;
    [SerializeField] int spawn = 144;           // 생성 수
    
    void Start()
    {
        InitRooms();          // 흰 셀 144개 등록
        SpawnInitialAgents(); // 길/방 구분 없이 144개 무작위 배치
    }
    
    /* 144개 방 등록 */
    void InitRooms()
    {
        int id = 0;
        foreach (int z in idx)
        {
            int zPos = System.Array.IndexOf(idx, z);        // 0‥11
            int gZ   = zPos >> 1;                           // 0‥5

            foreach (int x in idx)
            {
                int xPos   = System.Array.IndexOf(idx, x);  // 0‥11
                int gX     = xPos >> 1;                     // 0‥5
                int gId    = gZ * 6 + gX;                   // 0‥35

                /* ─ 방 등록 ─ */
                Vector3 p = new(x * cellGap, Y, -z * cellGap);
                rooms.Add(id, new RoomInfo { pos = p, occupant = null });

                /* ─ 그룹 등록 ─ */
                if (!groups.ContainsKey(gId)) groups[gId] = new GroupInfo();
                groups[gId].roomIds.Add(id);
                roomToGroup[id] = gId;

                /* 역매핑 (xi,zi) → roomId */
                gridToRoomId[(x, z)] = id;
                id++;
            }
        }
    }
    
    /* ───── 위치 → roomId 판정 ───── */
    public bool TryGetRoomIdByPosition(Vector3 pos, out int roomId)
    {
        int xi = Mathf.RoundToInt( pos.x / cellGap );
        int zi = Mathf.RoundToInt(-pos.z / cellGap );
        
        return gridToRoomId.TryGetValue((xi, zi), out roomId);
    }
    
    /* ───────── 초기 144개 에이전트 스폰 ───────── */
    void SpawnInitialAgents()
    {
        List<Vector3> cells = GenerateAllCells();   // 400칸
        Shuffle(cells);
        cells.RemoveRange(spawn, cells.Count - spawn); // 앞쪽 144칸 사용

        int mainCnt   = Mathf.RoundToInt(spawn * mainRatio);
        int targetCnt = spawn - mainCnt;

        int p = 0;        // ★ idx → p 로 변경
        for (int i = 0; i < mainCnt;   i++) Instantiate(mainPrefab,   cells[p++], Quaternion.identity, transform);
        for (int i = 0; i < targetCnt; i++) Instantiate(targetPrefab, cells[p++], Quaternion.identity, transform);
    }
    
    /* 400칸 전체 좌표 리스트 */
    List<Vector3> GenerateAllCells()
    {
        var list = new List<Vector3>(GRID * GRID);
        for (int z = 0; z < GRID; z++)
        for (int x = 0; x < GRID; x++)
            list.Add(new Vector3(x*cellGap, Y, -z*cellGap));
        return list;
    }

    /* Fisher–Yates 셔플 */
    void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count-1; i > 0; i--)
        {
            int j = Random.Range(0, i+1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    
    /* ───────── Room API ───────── */
    public Vector3 GetRoomPosition(int id)          => rooms[id].pos;
    public bool     IsOccupied(int id)              => rooms[id].occupant != null;
    public void     OccupyRoom(int id, GameObject a)=> rooms[id].occupant = a;
    public void     VacateRoom(int id)              { if (id>=0) rooms[id].occupant=null; }

    public bool TryReserveFreeRoom(out int id, int exclude=-1)
    {
        id = -1;
        List<int> free = new();
        
        foreach (var kv in rooms)
            if (kv.Key != exclude && kv.Value.occupant == null)
                free.Add(kv.Key);

        if (free.Count==0) return false;
        
        id = free[Random.Range(0, free.Count)];
        return true;
    }
    
    /* ───── Occupancy 갱신 ───── */
    public void UpdateOccupancy(GameObject agent, int prevRoomId, out int newRoomId)
    {
        newRoomId = -1;

        // 현재 위치가 방 위인가?
        if (!TryGetRoomIdByPosition(agent.transform.position, out int id))
        {
            // 길 위—방을 벗어났다면 이전 occupant 해제
            if (prevRoomId >= 0 && rooms[prevRoomId].occupant == agent)
                VacateRoom(prevRoomId);
            return;
        }

        // 동일 방이면 끝
        if (rooms[id].occupant == agent)
        {
            newRoomId = id;
            return;
        }

        // 다른 에이전트가 점유 중이면 실패
        if (rooms[id].occupant != null) return;

        // 빈 방 → 점유
        OccupyRoom(id, agent);
        if (prevRoomId >= 0 && rooms[prevRoomId].occupant == agent)
            VacateRoom(prevRoomId);

        newRoomId = id;
    }
    
    public int GetOccupiedCount(int groupId)
    {
        if (!groups.TryGetValue(groupId, out var g)) return 0;

        int cnt = 0;
        foreach (int r in g.roomIds)
            if (rooms[r].occupant != null) cnt++;

        return cnt;
    }
}

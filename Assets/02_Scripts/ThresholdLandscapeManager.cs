using UnityEngine;
using System.Collections.Generic;

public class ThresholdLandscapeManager : MonoBehaviour
{
    public static ThresholdLandscapeManager I { get; private set; }
    
    readonly List<int> freeCache = new List<int>(400);
    
    void Awake() => I = this;

    /* ───── 방 인덱스(12칸) ───── */
    static readonly int[] idx = { 1,2, 4,5, 7,8, 11,12, 14,15, 17,18 };
    readonly Dictionary<int, GameObject> reserved = new();   // roomId → 예약자
    
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
    [SerializeField] float roomHeight = 0.5f; 

    /* ───── 초기 스폰 설정 ───── */
    [Header("에이전트 프리팹")]
    [SerializeField] GameObject mainPrefab;
    [SerializeField] GameObject targetPrefab;
    [Header("비율")]
    [Range(0,1)] public float mainRatio = .7f;
    [Range(0,1)] public float targetRatio = .3f;
    [SerializeField] int initialSpawnCount = 132;
    
    
    public bool IsReserved(int roomId) => reserved.ContainsKey(roomId);
    public bool IsReservedBy(int roomId, GameObject agent) =>
        reserved.TryGetValue(roomId, out var holder) && holder == agent;

    static readonly Vector3[] DIRS =    // 매 호출 GC 0
    {
        Vector3.right,
        Vector3.left,
        Vector3.forward,
        Vector3.back
    };
    
    void Start()
    {
        InitRooms();
        SpawnInitialAgents();
    }
    
    void InitRooms()
    {
        var idxToOrder = new Dictionary<int,int>();
        for (int i = 0; i < idx.Length; i++) idxToOrder[idx[i]] = i;

        int id = 0;
        foreach (int z in idx)
        {
            int zPos = idxToOrder[z];
            int gZ   = zPos >> 1;

            foreach (int x in idx)
            {
                int xPos = idxToOrder[x];
                int gX   = xPos >> 1;
                int gId  = gZ * 6 + gX;

                /* ─ 방 등록 ─ */
                Vector3 p = new(x * cellGap, roomHeight, -z * cellGap); 
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
        roomId = -1;                              // 3) 실패값 미리 설정

        // 2) 그리드 바깥이면 즉시 false
        if (pos.x < 0f || pos.x >= GRID * cellGap ||
            -pos.z < 0f || -pos.z >= GRID * cellGap)
            return false;

        const float EPS = 1e-4f;                  // 1) 경계 오차
        float half = 0.5f * cellGap;

        int xi = Mathf.FloorToInt((pos.x + half + EPS) / cellGap);
        int zi = Mathf.FloorToInt((-pos.z + half + EPS) / cellGap);

        return gridToRoomId.TryGetValue((xi, zi), out roomId);
    }
    
    void SpawnInitialAgents()
    {
        List<Vector3> cells = GenerateAllCells();
        Shuffle(cells);
        cells.RemoveRange(initialSpawnCount, cells.Count - initialSpawnCount);

        int mainCnt   = Mathf.RoundToInt(initialSpawnCount * mainRatio);
        int targetCnt = initialSpawnCount - mainCnt;

        int p = 0;
        for (int i = 0; i < mainCnt;   i++) Instantiate(mainPrefab,   cells[p++], Quaternion.identity, transform);
        for (int i = 0; i < targetCnt; i++) Instantiate(targetPrefab, cells[p++], Quaternion.identity, transform);
    }
    
    /* 400칸 전체 좌표 리스트 */
    List<Vector3> GenerateAllCells()
    {
        var list = new List<Vector3>(GRID * GRID);
        for (int z = 0; z < GRID; z++)
        for (int x = 0; x < GRID; x++)
            list.Add(new Vector3(x * cellGap, roomHeight, -z * cellGap));  // ★ Y → roomHeight
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

    public bool TryReserveFreeRoom(out int id, GameObject agent, int exclude = -1)
    {
        id = -1;
        
        /* 0) 후보 리스트 재사용 ─ GC 0 */
        freeCache.Clear();
        if (freeCache.Capacity < rooms.Count)      // 맵 확장 대비
            freeCache.Capacity = rooms.Count;
        
        foreach (var kv in rooms)
        {
            int key = kv.Key;
            if (key == exclude)                continue;
            if (kv.Value.occupant != null)     continue;
            if (reserved.ContainsKey(key))     continue;
            freeCache.Add(key);
        }

        int n = freeCache.Count;
        if (n == 0) return false;

        /* 1) Fisher–Yates 1-pass */
        for (int i = n - 1; i >= 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (freeCache[i], freeCache[j]) = (freeCache[j], freeCache[i]);
            int candidate = freeCache[i];

            if (rooms[candidate].occupant == null && !reserved.ContainsKey(candidate))
            {
                reserved[candidate] = agent;
                id = candidate;
                return true;
            }
        }
        return false;   // 모든 후보 경합
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
        
        // 예약인데 나 아닌가?
        if (reserved.TryGetValue(id, out var holder))
        {
            if (holder != agent)          // 남이 예약 → 점유 금지
                return;                   // 그대로 길로 간주
            reserved.Remove(id);          // 내 예약이면 소유권 인계
        }

        // 빈 방 → 점유
        OccupyRoom(id, agent);
        if (prevRoomId >= 0 && rooms[prevRoomId].occupant == agent)
            VacateRoom(prevRoomId);

        newRoomId = id;
    }

    /* ─ 실제 도착했을 때만 Occupy ─ */
    public void OnRoomArrived(int roomId, GameObject agent)
    {
        if (rooms[roomId].occupant != null && rooms[roomId].occupant != agent)
            return;

        if (reserved.TryGetValue(roomId, out var holder)) 
        { 
            // 남의 예약이면 실패
            if (holder != agent)           
                return;
            reserved.Remove(roomId);       // 내 예약 → 소유권 인계
        } 
        rooms[roomId].occupant = agent;    // 이제 점유
    }

    /* 본인 확인용 – 기존 함수를 그대로 둔다 */
    public void CancelReservation(int roomId, GameObject agent)
    {
        if (reserved.TryGetValue(roomId, out var holder) && holder == agent)
            reserved.Remove(roomId);
    }

    /* 강제 해제용 – agent 체크 없이 제거 */
    public void CancelReservation(int roomId)
    {
        if (!reserved.Remove(roomId))
            Debug.LogWarning($"[ThresholdLandscape] CancelReservation 실패: " +
                             $"room {roomId} 에 활성 예약이 없습니다.");
    }

    /* ─ 방 중심에서 가장 가까운 Road 좌표 반환 ─ */
    public Vector3 GetNearestRoadPos(int roomId)
    {
        if (roomId < 0 || !rooms.ContainsKey(roomId))
            return Vector3.zero;

        Vector3 p = rooms[roomId].pos;

        foreach (var d in DIRS)            // ← DIRS 사용
        {
            Vector3 q = p + d * cellGap;
            if (!TryGetRoomIdByPosition(q, out _))
                return q;                  // 첫 번째 길 좌표 즉시 반환
        }

        /* 실질적으로 발생하기 어려운 경우 대비 */
        return p + Vector3.forward * cellGap;
    }

}

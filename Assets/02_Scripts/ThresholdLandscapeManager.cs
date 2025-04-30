using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ThresholdLandscapeManager : MonoBehaviour
{
    public static ThresholdLandscapeManager I { get; private set; }
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
    const float Y = 0.5f;

    /* ───── 초기 스폰 설정 ───── */
    [Header("에이전트 프리팹")]
    [SerializeField] GameObject mainPrefab;
    [SerializeField] GameObject targetPrefab;
    [Header("비율")]
    [Range(0,1)] public float mainRatio = .7f;
    [Range(0,1)] public float targetRatio = .3f;
    [SerializeField] int spawn = 132;           // 생성 수
    
    
    public bool IsReserved(int roomId) => reserved.ContainsKey(roomId);
    public bool IsReservedBy(int roomId, GameObject agent) =>
        reserved.TryGetValue(roomId, out var holder) && holder == agent;

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

    public bool TryReserveFreeRoom(out int id, GameObject agent, int exclude = -1)
    {
        id = -1;
        
        // 예약·점유 모두 없는 방 목록
        var free = rooms
            .Where(kv => kv.Key != exclude &&
                         kv.Value.occupant == null &&
                         !reserved.ContainsKey(kv.Key))
            .Select(kv => kv.Key)
            .ToList();

        while (free.Count > 0)
        {
            // 무작위 후보
            int pick = Random.Range(0, free.Count);
            int candidate = free[pick];
            free.RemoveAt(pick);                  // 실패하면 다른 방을 고르기 위해 제거

            // 더블-체크
            if (rooms[candidate].occupant != null || reserved.ContainsKey(candidate))
                continue;                        // 이미 누가 선점 → 다음 후보

            reserved[candidate] = agent;         // 최종 예약
            id = candidate;
            return true;
        }

        return false;  
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

/* ─ 실패·포기 시 예약 해제 ─ */
    public void CancelReservation(int roomId, GameObject agent)
    {
        if (reserved.TryGetValue(roomId, out var holder) && holder == agent)
            reserved.Remove(roomId);
    }

    /* ─ 방 중심에서 가장 가까운 Road 좌표 반환 ─ */
    public Vector3 GetNearestRoadPos(int roomId)
    {
        if (roomId < 0 || !rooms.ContainsKey(roomId))
            return Vector3.zero;        // 혹은 transform.position …

        Vector3 p = rooms[roomId].pos;
        Vector3[] dir = { Vector3.right, Vector3.left,
            Vector3.forward, Vector3.back };

        foreach (var d in dir)
        {
            Vector3 q = p + d * cellGap;      // 주변 한 칸
            if (!TryGetRoomIdByPosition(q, out _))   // 방이 아니면 길
                return q;
        }
        return p;                             // (안전용) 네 면이 전부 방일 일은 없음
    }
    
    public bool IsSomeoneCryingInRoom(int roomId)
    {
        if (!rooms.TryGetValue(roomId, out var info)) return false;
        if (info.occupant == null) return false;

        Collider[] colliders = Physics.OverlapSphere(info.pos, 0.5f);

        foreach (var col in colliders)
        {
            // Agent 기반 탐지
            if (col.TryGetComponent<Agent>(out var agent) &&
                agent != info.occupant &&
                agent.label == Agent.Label.Target)
            {
                var baby = agent.GetComponent<Baby>();
                if (baby != null && baby.IsCrying()) return true;
            }

            // Baby 감지
            if (col.TryGetComponent<Baby>(out var standaloneBaby) &&
                standaloneBaby != null && standaloneBaby.IsCrying())
            {
                Debug.Log($"Baby {standaloneBaby.name} is crying (detected without Agent)");
                return true;
            }
        }

        return false;
    }

}

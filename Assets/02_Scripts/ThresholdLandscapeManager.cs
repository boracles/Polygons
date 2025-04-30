using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ThresholdLandscapeManager : MonoBehaviour
{
    public static ThresholdLandscapeManager I { get; private set; }
    void Awake() => I = this;

    /* 방 인덱스(12칸) */
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
        public readonly List<int> roomIds = new();
        public float unsatisfiedDuration = 0f;   // 누적된 불만족 시간
        public bool isRed = false;              // 이미 빨간 상태인지
        
        public float coolDownTimer = 0f;  // 복구 전 대기시간
    }
    readonly Dictionary<int, GroupInfo> groups = new();
    readonly Dictionary<int, int>       roomToGroup = new();
    
    const int GRID = 20;
    [SerializeField] float cellGap = 1f;
    const float Y = 0.5f;
    
    [Header("에이전트 프리팹")]
    [SerializeField] GameObject mainPrefab;
    [SerializeField] GameObject targetPrefab;
    
    [Header("비율")]
    [Range(0,1)] public float mainRatio = .7f;
    [Range(0,1)] public float targetRatio = .3f;
    [SerializeField] int spawn = 132;
    
    [SerializeField] float unsatisfiedThresholdSec = 2.4f;
    [SerializeField] int minUnhappyAgents = 2;
    [SerializeField] float recoverCooldown = 5f; // 복구까지 대기 시간 (초)
    
    readonly Dictionary<int, List<Renderer>> groupRenderers = new();
    
    
    public bool IsReserved(int roomId) => reserved.ContainsKey(roomId);
    public bool IsReservedBy(int roomId, GameObject agent) =>
        reserved.TryGetValue(roomId, out var holder) && holder == agent;

    void Start()
    {
        InitRooms();        
        SpawnInitialAgents();
        InitGroupRenderers();
    }
    
    void InitGroupRenderers()
    {
        GameObject root = GameObject.Find("_ROOM");
        for (int i = 0; i < 36; i++) // ROOM01~ROOM36
        {
            string groupName = $"ROOM{i + 1:D2}";
            Transform group = root.transform.Find(groupName);
            if (group == null) continue;

            var renderers = new List<Renderer>();
            foreach (Transform child in group)
            {
                if (child.TryGetComponent<RoomandRoad>(out var tag) &&
                    tag.spaceType == RoomandRoad.SpaceType.Room &&
                    child.TryGetComponent<Renderer>(out var rend))
                {
                    renderers.Add(rend);
                }
            }
            groupRenderers[i] = renderers;
        }
    }
    
    void InitRooms()
    {
        int id = 0;
        for (int gz = 0; gz < 6; gz++) // 그룹 줄 (아래→위)
        for (int gx = 0; gx < 6; gx++) // 그룹 칸 (왼→오른쪽)
        {
            int groupId = (5 - gz) * 6 + gx; // ✅ 행우선 + z축 반전 (ROOM01이 좌하단)

            for (int dz = 0; dz < 2; dz++)
            for (int dx = 0; dx < 2; dx++)
            {
                int x = idx[gx * 2 + dx];
                int z = idx[gz * 2 + dz];

                Vector3 p = new Vector3(x * cellGap, Y, -z * cellGap);
                rooms.Add(id, new RoomInfo { pos = p, occupant = null });

                if (!groups.ContainsKey(groupId)) groups[groupId] = new GroupInfo();
                groups[groupId].roomIds.Add(id);
                roomToGroup[id] = groupId;
                gridToRoomId[(x, z)] = id;

                id++;
            }
        }
    }

    void Update()
    {
        foreach (var kvp in groups)
        {
            int groupId = kvp.Key;
            var group = kvp.Value;
            
            foreach (int rid in group.roomIds)
            {
                if (rooms.TryGetValue(rid, out var info) && info.occupant != null)
                {
                    var agent = info.occupant.GetComponent<Agent>();
                    if (agent != null)
                    {
                        string match = agent.currentState == Agent.SatisfactionState.UnSatisfied ? "❗" : "";
                        Debug.Log($"[Group {groupId}] roomId {rid} → {agent.name}, state: {agent.currentState} {match}");
                    }
                }
            }

            var agents = group.roomIds
                .Where(id => rooms.TryGetValue(id, out var r) && r.occupant != null)
                .Select(id => rooms[id].occupant.GetComponent<Agent>())
                .Where(a => a != null)
                .ToList();

            int unhappyCount = agents.Count(a => a.currentState == Agent.SatisfactionState.UnSatisfied);

            if (unhappyCount >= minUnhappyAgents)
            {
                group.unsatisfiedDuration += Time.deltaTime;

                if (!group.isRed && group.unsatisfiedDuration >= unsatisfiedThresholdSec)
                {
                    group.isRed = true;
                    SetGroupColor(groupId, Color.red);
                    Debug.Log($"[그룹 {groupId}] 상태 RED 전환");
                }
            }
        }
    }

    void SetGroupColor(int groupId, Color color)
    {
        if (!groupRenderers.TryGetValue(groupId, out var renderers)) return;

        foreach (var r in renderers)
            r.material.color = color;
    }

    /* 위치 → roomId 판정 */
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

    public bool TryReserveFreeRoom(out int id, GameObject agentObj, int exclude = -1)
    {
        id = -1;
        var agent = agentObj.GetComponent<Agent>();
        if (agent == null) return false;
        
        var free = rooms
            .Where(kv =>
            {
                if (kv.Key == exclude) return false;
                if (kv.Value.occupant != null || reserved.ContainsKey(kv.Key)) return false;

                // Target만 빨간 방 제외
                if (agent.label == Agent.Label.Target &&
                    roomToGroup.TryGetValue(kv.Key, out var groupId) &&
                    groups.TryGetValue(groupId, out var group) &&
                    group.isRed)
                    return false;

                return true;
            })
            .Select(kv => kv.Key)
            .ToList();

        while (free.Count > 0)
        {
            int pick = Random.Range(0, free.Count);
            int candidate = free[pick];
            free.RemoveAt(pick);

            if (rooms[candidate].occupant != null || reserved.ContainsKey(candidate))
                continue;

            reserved[candidate] = agentObj;
            id = candidate;
            return true;
        }

        return false;
    }

    /* Occupancy 갱신 */
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

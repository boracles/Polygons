using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;    

public class ThresholdLandscapeManager : MonoBehaviour
{
    [Header("Grid Size")]
    public int width = 20;
    public int height = 20;

    [Header("Prefabs")]
    public GameObject mainPrefab; 
    public GameObject targetPrefab;

    [Header("Initial Ratios (0~1)")]
    [Range(0, 1)] public float emptyRatio  = 0.30f;
    [Range(0, 1)] public float mainRatio  = 0.35f;   // Main 표본용
    [Range(0, 1)] public float targetRatio  = 0.35f;   // Target 표본용
    
    private int[,] board;                        // 0 빈 / 1 검 / 2 흰
    private GameObject[,] agentObjects;          // Agent 프리팹 참조
    private Zone[] zones;                        // 25개 Zone (4×4 cells)

    private const int CELLS_PER_ZONE = 16;       // 4×4 묶음
    private const float ALLOW_VALUE   = 2f;      // bias × targetNear <= 2 → 행복

    private Coroutine simCoroutine;
    
    [SerializeField] float wanderInterval = 2f;
    [SerializeField] float arriveEps      = 0.05f;
    
    HashSet<Vector2Int> occupiedRooms = new HashSet<Vector2Int>();
    
    void Awake()
    {
        board        = new int[width, height];
        agentObjects = new GameObject[width, height];

        int zonesX = width  / 4;
        int zonesZ = height / 4;
        zones = new Zone[zonesX * zonesZ];
        for (int i = 0; i < zones.Length; i++)
            zones[i] = new Zone(i, CELLS_PER_ZONE);  
    }

    void Start()
    {
        InitBoard();
    }

    public void OnClickStart()
    {
        if (simCoroutine == null)   // 정지 상태 → 시작
            simCoroutine = StartCoroutine(SimLoop());
    }
    
    void InitBoard()
    {
        // 보드·에이전트 비우기 (재시작 대비)
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            if (agentObjects[x, z] != null)
                Destroy(agentObjects[x, z]);
            board[x, z] = 0;
            agentObjects[x, z] = null;
        }

        // 존 멤버 클리어
        foreach (var z in zones) z.ClearMembers();

        // 무작위 배치
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            float rnd = Random.value;
            if (rnd < emptyRatio)       continue;           // 빈칸
            else if (rnd < emptyRatio + mainRatio) board[x, z] = 1;  // 검
            else                                    board[x, z] = 2;  // 흰

            GameObject prefab = (board[x, z] == 1) ? mainPrefab : targetPrefab;
            InstantiateAgent(prefab, x, z, board[x, z]);
        }
    }
    
    IEnumerator SimLoop()
    {
        WaitForSeconds tickWait = new WaitForSeconds(0.1f);
        while (true)
        {
            UpdateZones();
            yield return MoveUnsatisfiedAgents();
            yield return tickWait;
        }
    }
    
    // 존 통계 갱신 및 폐쇄 판단
    void UpdateZones()
    {
        foreach (var z in zones) z.ClearMembers();

        for (int x = 0; x < width; x++)
        for (int zIdx = 0; zIdx < height; zIdx++)
        {
            GameObject obj = agentObjects[x, zIdx];
            if (!obj) continue;
            Agent ag = obj.GetComponent<Agent>();
            int zoneID = (x / 4) + (zIdx / 4) * (width / 4);
            zones[zoneID].AddMember(ag);
        }

        foreach (var z in zones)
        {
            z.UpdateStats();
            z.TryClose(); // 닫히면 내부 isClosed true
        }
    }
    
    // 불만족자 수집 & 이동
    IEnumerator MoveUnsatisfiedAgents()
    {
        List<Vector2Int> unsatisfied = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            if (board[x, z] == 0) continue;
            if (!IsSatisfied(x, z)) unsatisfied.Add(new Vector2Int(x, z));
        }

        // 이동 순회
        foreach (var pos in unsatisfied)
        {
            int oldX = pos.x; int oldZ = pos.y;
            if (board[oldX, oldZ] == 0) continue; // 이미 이동했을 수 있음
            Vector2Int? cand = FindCandidate(board[oldX, oldZ]);
            if (cand.HasValue)
            {
                yield return StartCoroutine(MoveAgent(oldX, oldZ, cand.Value.x, cand.Value.y));
            }
        }
    }
    
    // Agent 평가
    bool IsSatisfied(int x, int z)
    {
        Agent ag = agentObjects[x, z].GetComponent<Agent>();
        if (!ag) return true;

        // 조건 1) bias × targetNear
        int targetNear = CountNeighborsWithLabel(x, z, Agent.Label.Target);
        bool condBias = (ag.bias * targetNear) <= ALLOW_VALUE;

        // 조건 2) Zone 상태
        int zoneID = (x / 4) + (z / 4) * 5;
        bool condZone = !zones[zoneID].IsClosed || ag.label == Agent.Label.Main;

        return condBias && condZone;
    }

    int CountNeighborsWithLabel(int x, int z, Agent.Label lbl)
    {
        int cnt = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        for (int nz = z - 1; nz <= z + 1; nz++)
        {
            if (nx == x && nz == z) continue;
            if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
            if (board[nx, nz] == 0) continue;
            Agent ngb = agentObjects[nx, nz].GetComponent<Agent>();
            if (ngb && ngb.label == lbl) cnt++;
        }
        return cnt;
    }
    
    // 빈칸 후보
    Vector2Int? FindCandidate(int color)
    {
        List<Vector2Int> list = new();
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            if (board[x, z] != 0) continue;
            if (IsSatisfiedIf(color, x, z)) list.Add(new Vector2Int(x, z));
        }
        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    bool IsSatisfiedIf(int color, int x, int z)
    {
        // 새 위치에 가정 배치했다고 가정, condBias만 검증
        int targetNear = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        for (int nz = z - 1; nz <= z + 1; nz++)
        {
            if (nx == x && nz == z) continue;
            if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
            if (board[nx, nz] == 0) continue;
            Agent ngb = agentObjects[nx, nz].GetComponent<Agent>();
            if (ngb && ngb.label == Agent.Label.Target) targetNear++;
        }

        float biasDummy = (color == 1) ? Random.Range(0.4f, 1f) : Random.Range(0f, 0.3f);
        bool condBias = (biasDummy * targetNear) <= ALLOW_VALUE;

        int zoneID = (x / 4) + (z / 4) * 5;
        bool condZone = !zones[zoneID].IsClosed || color == 1; // Main assumed color 1

        return condBias && condZone;
    }

    // 이동
    IEnumerator MoveAgent(int oldX, int oldZ, int newX, int newZ)
    { 
        // 목적지 재확인 - 이미 차 있으면 취소
        if (board[newX, newZ] != 0) 
            yield break;

        // 배열 선점
        int c = board[oldX, oldZ];
        board[newX, newZ]        = c;                   // 새 칸 예약
        board[oldX, oldZ]        = 0;                   // 옛 칸 비우기

        GameObject agObj         = agentObjects[oldX, oldZ];
        agentObjects[newX, newZ] = agObj;
        agentObjects[oldX, oldZ] = null;
        
        // 실제 이동 
        Vector3 start = agObj.transform.position;
        Vector3 end = new Vector3(newX, 0.5f, -newZ);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.1f; // 0.1s 이동
            agObj.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }
        agObj.transform.position = end;
    }
    
    // 에이전트 생성
    void InstantiateAgent(GameObject prefab, int x, int z, int color)
    {
        GameObject agentObj = Instantiate(prefab,
            new Vector3(x, 0.5f, -z),
            Quaternion.identity,
            transform);
        
        Agent ag = agentObj.GetComponent<Agent>();
        if (ag == null) return;
        
        NavMeshAgent nav = agentObj.GetComponent<NavMeshAgent>();
        if (nav)
        {
            if (NavMesh.SamplePosition(agentObj.transform.position, out _, 0.05f, NavMesh.AllAreas))
            {
                nav.enabled = true;
                StartCoroutine(Wander(nav, ag));             // 길 위 → 즉시 Wander
            }
            else
            {
                nav.enabled = false;                        // 방 안
                StartCoroutine(WaitAndWalkOut(nav, ag, 5f));     // 5초 기다렸다가 길로
            }
        }
        
        agentObjects[x, z] = agentObj;
        ag.color = color;
        
        float r = Random.value;
        if (r < 0.8f)
        {
            ag.label = Agent.Label.Main;
            ag.bias  = Random.Range(0.4f, 1.0f);
        }
        else
        {
            ag.label = Agent.Label.Target;
            ag.bias  = Random.Range(0.0f, 0.3f);
        }
    }
    
    IEnumerator WaitAndWalkOut(NavMeshAgent nav, Agent ag, float waitSec)
    {
        /* 0) 방 안에서 대기 */
        yield return new WaitForSeconds(waitSec);

        /* 1) 가장 가까운 빈 길 셀 찾기 */
        Vector2Int? road = FindNearestFreeRoad(
            Mathf.RoundToInt(nav.transform.position.x),
            Mathf.RoundToInt(-nav.transform.position.z), 4);

        if (!road.HasValue)     // 길이 없으면 그냥 방에 남는다
            yield break;

        int gx = road.Value.x, gz = road.Value.y;

        /* 2) 셀 점유 예약 (다른 에이전트 진입 차단) */
        if (board[gx, gz] != 0) yield break;          // 경합 시 실패
        board[gx, gz]        = (ag.label == Agent.Label.Main) ? 1 : 2;
        agentObjects[gx, gz] = nav.gameObject;

        /* 3) 방 셀 해제 */
        int rx = Mathf.RoundToInt(nav.transform.position.x);
        int rz = Mathf.RoundToInt(-nav.transform.position.z);
        board[rx, rz]        = 0;
        agentObjects[rx, rz] = null;

        /* 4) Nav 꺼두고 Lerp 이동 */
        nav.enabled = false;
        Vector3 startPos   = nav.transform.position;
        Vector3 targetPos  = new Vector3(gx, 0.5f, -gz);
        float   travelTime = 0.6f, t = 0f;

        while (t < travelTime)
        {
            t += Time.deltaTime;
            nav.transform.position = Vector3.Lerp(startPos, targetPos, t / travelTime);
            yield return null;
        }
        nav.transform.position = targetPos;   // 정확히 스냅

        /* 5) Nav 다시 켜고 Wander 재시작 */
        nav.enabled = true;
        nav.Warp(targetPos);                  // Nav 내부 좌표 동기화
        StartCoroutine(Wander(nav, ag));
    }

    Vector2Int? FindNearestFreeRoad(int startX, int startZ, int maxR = 4)
    {
        for (int r = 0; r <= maxR; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                // 테두리만 보도록 해도 되고, 여기선 전체 순회
                int x = startX + dx;
                int z = startZ + dz;
                if (x < 0 || x >= width || z < 0 || z >= height) continue;

                if (board[x, z] != 0) continue;                // 이미 점유
                // 물리적으로 다른 Agent가 있는지 체크
                Vector3 world = new Vector3(x, 0.5f, -z);

                // NavMesh 위인지도 보증
                if (!NavMesh.SamplePosition(world, out _, 0.05f, NavMesh.AllAreas))
                    continue;

                return new Vector2Int(x, z);                   // 빈 길 셀 발견
            }
        }
        return null; // 근처에 빈 길 없음
    }

    IEnumerator Wander(NavMeshAgent nav, Agent ag)
    {
        while (nav && nav.isActiveAndEnabled)
        {
            /* ① 20% 확률로 빈 Room 셀 선택 */
            if (Random.value < 0.2f)
            {
                if (TrySetRoomDestination(nav, ag))   // 성공 시 ‘스냅 대기 루틴’으로 전환
                    yield break;                      // 이 코루틴 종료
            }

            /* ② 아니면 길(네브메시) 위에서만 랜덤 산책 */
            Vector3 randDir = Random.insideUnitSphere * 10f; randDir.y = 0;
            if (NavMesh.SamplePosition(nav.transform.position + randDir,
                    out NavMeshHit hit, 10f, NavMesh.AllAreas))
                nav.SetDestination(hit.position);

            yield return new WaitForSeconds(wanderInterval);
        }
    }
    
    bool TrySetRoomDestination(NavMeshAgent nav, Agent ag)
    {
        /* (1) 주변 빈 Room 셀 하나 찾기 */
        Vector2Int? cell = FindRandomEmptyRoomNear(nav.transform.position, 5);
        if (!cell.HasValue) return false;

        int tx = cell.Value.x, tz = cell.Value.y;

        /* (2) 방 주변 네브메시 위 기준점 선택 (4방향 탐색) */
        Vector3 roadPos;
        if (!FindRoadAdjacency(tx, tz, out roadPos)) return false;

        /* 3. 예약 – 방 셀 먼저 선점 (다른 에이전트 진입 봉쇄) */
        Vector2Int roomPos = new Vector2Int(tx, tz);
        if (!occupiedRooms.Add(roomPos)) return false; 
        
        board[tx, tz] = (ag.label == Agent.Label.Main) ? 1 : 2;
        agentObjects[tx, tz] = nav.gameObject; 

        /* (4) MoveAgent 에서 옛칸 비우는 로직 재사용 */
        int ox = Mathf.RoundToInt(nav.transform.position.x);
        int oz = Mathf.RoundToInt(-nav.transform.position.z);
        board[ox, oz]        = 0;
        agentObjects[ox, oz] = null;

        /* (5) NavMeshAgent 목적지 = roadPos */
        nav.SetDestination(roadPos);

        /* (6) 스냅을 기다리는 코루틴 시작 */
        StartCoroutine(SnapWhenArrived(nav, ag, tx, tz));
        return true;
    }

/* 방 셀로 스냅 */
    IEnumerator SnapWhenArrived(NavMeshAgent nav, Agent ag, int cellX, int cellZ)
    {
        // 길 끝까지 도달할 때까지 대기 
        while (nav.pathPending || nav.remainingDistance > arriveEps)
            yield return null;

        /* Nav 다시 켜고 Wander 재시작 */
        nav.enabled = false;
        Vector3 targetPos = new Vector3(cellX, 0.5f, -cellZ);
        Vector3 startPos  = nav.transform.position;
        float   t         = 0f;
        const float slide = 0.6f;
        while (t < slide)
        {
            t += Time.deltaTime;
            nav.transform.position = Vector3.Lerp(startPos, targetPos, t / slide);
            yield return null;
        }
        nav.transform.position = targetPos;
        
        // 5초 머무르기
        yield return new WaitForSeconds(5f);
        
        // 가장 가까운 빈 길 셀로 나가기 
        Vector2Int? road = FindNearestFreeRoad(cellX, cellZ, 4);
        if (road.HasValue)
        {
            int gx = road.Value.x, gz = road.Value.y;

            /* 방 셀 반납 */
            occupiedRooms.Remove(new Vector2Int(cellX, cellZ));
            board[cellX, cellZ]        = 0;
            agentObjects[cellX, cellZ] = null;

            /* 길 셀 점유 */
            board[gx, gz]        = (ag.label == Agent.Label.Main) ? 1 : 2;
            agentObjects[gx, gz] = nav.gameObject;

            /* Nav ON + Warp + Wander 재시작 */
            nav.enabled = true;
            nav.Warp(new Vector3(gx, 0.5f, -gz));
            StartCoroutine(Wander(nav, ag));
        }
        else
        {
            /* 길이 없으면 그대로 방 안에 남지만 예약은 해제 */
            occupiedRooms.Remove(new Vector2Int(cellX, cellZ));
        }
    }

    Vector2Int? FindRandomEmptyRoomNear(Vector3 from, int radius)
    {
        int cx = Mathf.RoundToInt(from.x);
        int cz = Mathf.RoundToInt(-from.z);

        List<Vector2Int> rooms = new();
        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            int x = cx + dx, z = cz + dz;
            if (x < 0 || x >= width || z < 0 || z >= height) continue;

            /* Room 셀 = 네브메시가 없는(Non-walkable) 곳으로 규정 */
            if (board[x, z] == 0 &&     // 아직 비어있고
                !NavMesh.SamplePosition(new Vector3(x, 0, -z), out _, 0.05f, NavMesh.AllAreas))
                rooms.Add(new Vector2Int(x, z));
        }
        if (rooms.Count == 0) return null;
        return rooms[Random.Range(0, rooms.Count)];
    }

    bool FindRoadAdjacency(int roomX, int roomZ, out Vector3 roadPos)
    {
        /* 상 하 좌 우 네 칸 중 네브메시 있는 곳 반환 */
        int[] dirs = {1, 0, -1, 0, 1};        // (dx,dz) 순회
        for (int i = 0; i < 4; i++)
        {
            int x = roomX + dirs[i], z = roomZ + dirs[i+1];
            if (x < 0 || x >= width || z < 0 || z >= height) continue;

            Vector3 p = new Vector3(x, 0, -z);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.05f, NavMesh.AllAreas))
            { roadPos = hit.position; return true; }
        }
        roadPos = Vector3.zero; return false;
    }



}

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
    bool IsRoad(int x, int z) => (x % 4 == 0) || (z % 4 == 0);
    
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
        GameObject go = Instantiate(prefab, new Vector3(x, .5f, -z), Quaternion.identity, transform);
        Agent        ag  = go.GetComponent<Agent>();
        NavMeshAgent nav = go.GetComponent<NavMeshAgent>();

        agentObjects[x, z] = go;
        board[x, z]        = color;          // 1 또는 2

        // NavMeshAgent는 즉시 ON
        nav.enabled = true;

        // 방이라면 5초 후 나가도록 예약
        if (!IsRoad(x, z))
            StartCoroutine(LeaveRoomAfterDelay(nav, ag, x, z, 5f));
        else
            StartCoroutine(Wander(nav, ag)); // 길이면 바로 Wander
    }

    IEnumerator LeaveRoomAfterDelay(NavMeshAgent nav, Agent ag,
        int roomX, int roomZ, float wait)
    {
        yield return new WaitForSeconds(wait);

        // 가장 가까운 길 셀 탐색 (맨해튼 격자 BFS)
        Vector2Int? dest = FindNearestFreeRoad(roomX, roomZ, 4);
        if (!dest.HasValue)          // 못 찾으면 그냥 방에 남음
            yield break;

        int gx = dest.Value.x, gz = dest.Value.y;

        // 보드 예약·해제
        board[gx, gz]        = board[roomX, roomZ];
        agentObjects[gx, gz] = nav.gameObject;
        board[roomX, roomZ]  = 0;
        agentObjects[roomX, roomZ] = null;

        // 길 셀 중심으로 이동
        Vector3 goal = new Vector3(gx, .5f, -gz);
        nav.SetDestination(goal);

        // 도착 대기
        while (nav.pathPending || nav.remainingDistance > arriveEps)
            yield return null;

        // Wander 재시작
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
            Vector3 randDir = Random.insideUnitSphere * 8f; randDir.y = 0;
            if (NavMesh.SamplePosition(nav.transform.position + randDir,
                    out NavMeshHit hit, 8f, NavMesh.AllAreas))
                nav.SetDestination(hit.position);

            yield return new WaitForSeconds(wanderInterval);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ThresholdLandscapeManager : MonoBehaviour
{
    [Header("Grid Size")]    public int width = 20, height = 20;
    [Header("Prefabs")]      public GameObject mainPrefab, targetPrefab;
    [Header("Ratios (0-1)")] [Range(0,1)] public float emptyRatio=.6f, mainRatio=.3f, targetRatio=.1f;
    [Header("Behaviour")]    [SerializeField] float wanderInterval = 2f, arriveEps=.05f;

    /* ── 내부 상태 ─────────────────────────────────── */
    const int RESERVED       = -1;   // 새로 들어갈 때 예약
    const int RESERVED_LEAVE = -2;   // 나가는 중  잠금  ← 추가
    
    int[,] board;                    // 0 empty / 1 main / 2 target / –1 reserved
    GameObject[,] agents;

    readonly List<Vector3> rooms = new();         // 방 중심 world 좌표
    static readonly int[] idx = {1,2,4,5,7,8,11,12,14,15,17,18};
    static readonly HashSet<int> idxSet = new(idx);

    Dictionary<Vector2Int, float> cooldown = new();   // 방 → 금지 해제 시각
    
    // ★ NEW : 에이전트별 쿨다운 시각
    Dictionary<NavMeshAgent, float> agentCooldown = new();
    const float ROOM_EXIT_DELAY = 2f;    // 방을 나간 뒤 최소 2초는 예약 금지
    
    /* ── 초기화 ─────────────────────────────────────── */
    void Awake()
    {
        board  = new int[width, height];
        agents = new GameObject[width, height];

        foreach (int x in idx)
        foreach (int z in idx)
            rooms.Add(new Vector3(x, .5f, -z));
    }
    void Start() => ResetBoard();

    void ResetBoard()
    {
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            if (agents[x,z]) Destroy(agents[x,z]);
            board[x,z] = 0;
            agents[x,z]= null;
        }

        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            float r = Random.value;
            if (r < emptyRatio) continue;

            int label = (r < emptyRatio + mainRatio) ? 1 : 2;
            SpawnAgent(x, z, label);
        }
    }
    /* ── 좌표 헬퍼 ─────────────────────────────────── */
    static bool IsRoom(int x,int z) => idxSet.Contains(x) && idxSet.Contains(z);
    static Vector3 Cell(int x,int z)=> new(x, .5f, -z);


    Vector3 RandomRoad(Vector3 from, float radius = 6f)
    {
        for (int i = 0; i < 10; i++)
        {
            var p = from + Random.insideUnitSphere * radius; p.y = 0;
            if (NavMesh.SamplePosition(p, out var hit, .4f, NavMesh.AllAreas))
                return hit.position;
        }
        return from;
    }

    /* ── 에이전트 스폰 ─────────────────────────────── */
    void SpawnAgent(int gx,int gz,int label)
    {
        var prefab = label == 1 ? mainPrefab : targetPrefab;
        var go     = Instantiate(prefab, Cell(gx,gz), Quaternion.identity, transform);

        var nav = go.GetComponent<NavMeshAgent>();
        nav.stoppingDistance  = 0;
        nav.autoBraking       = true;
        nav.avoidancePriority = Random.Range(30, 70);

        board[gx,gz]  = label;
        agents[gx,gz] = go;

        if (IsRoom(gx,gz)) StartCoroutine(RoomRoutine(nav, gx, gz, label));
        else               StartCoroutine(RoadRoutine(nav, gx, gz, label));
    }

    /* ── 방 예약 헬퍼 ──────────────────────────────── */
    bool TryReserveRoom(out Vector2Int room, Vector2Int forbidden)
    {
        float now = Time.time;
        
        int start = Random.Range(0, rooms.Count);
        for (int k = 0; k < rooms.Count; k++)
        {
            var c = rooms[(start + k) % rooms.Count];
            int x = Mathf.RoundToInt(c.x);
            int z = -Mathf.RoundToInt(c.z);
            var v = new Vector2Int(x, z);

            if (v == forbidden)               continue;        // 내 자리
            if (board[x,z] != 0) continue; 
            if (cooldown.TryGetValue(v, out float until) && now < until)
                continue;                                     // 쿨다운 중

            board[x,z] = RESERVED;
            room = v;
            return true;
        }
        room = default; return false;
    }
    
    /* ── Agent·Obstacle 토글 ─────────────────────── */
    IEnumerator Freeze(NavMeshAgent ag, NavMeshObstacle ob)
    {
        ag.ResetPath(); ag.isStopped = true;
        yield return null;
        ag.enabled = false;
        if (ob) ob.enabled = true;          // carve
    }
    IEnumerator UnFreeze(NavMeshAgent ag, NavMeshObstacle ob)
    {
        if (ob) ob.enabled = false;
        yield return null;
        ag.enabled = true; ag.isStopped = false;
    }
    
    /* ── 이동·점유 코루틴 ─────────────────────────── */
    IEnumerator MoveIntoRoom(NavMeshAgent nav, NavMeshObstacle ob, Vector2Int dst, int label)
    {
        Vector3 center = Cell(dst.x, dst.y);

        /* 1) 이동 */
        NavMesh.SamplePosition(center, out var hit, .3f, NavMesh.AllAreas);
        nav.SetDestination(hit.position);
        yield return new WaitUntil(() => !nav.pathPending && nav.remainingDistance <= arriveEps);

        /* 2) 방 점유 시도 */
        yield return Freeze(nav, ob);

        // 필요하면 10 cm 이내 스냅
        if (Vector3.Distance(nav.transform.position, center) < .1f)
            nav.transform.position = center;

        if (agents[dst.x,dst.y] == null)
        {
            agents[dst.x,dst.y] = nav.gameObject;
            board [dst.x,dst.y] = label;
        }
        else   // 이미 누가 차지
        {
            cooldown[new Vector2Int(dst.x, dst.y)] = Time.time + 10f; // 10 초 쿨

            yield return UnFreeze(nav, ob);
            nav.SetDestination(RandomRoad(nav.transform.position, 8f));
            
            agentCooldown[nav] = Time.time + ROOM_EXIT_DELAY; // 2 초 개인 쿨
            StartWander(nav, label);
            yield break;
        }

        // ── 방 안 5초 대기 후 ──
        yield return new WaitForSeconds(5f);
        yield return UnFreeze(nav, ob);
        agentCooldown[nav] = Time.time + ROOM_EXIT_DELAY;     // ★ 성공 경로도 설정
        StartWander(nav, label);
    }
    
    /* ── 주요 코루틴들 ────────────────────────────── */
    IEnumerator RoadRoutine(NavMeshAgent nav,int sx,int sz,int label)
    {
        if (agentCooldown.TryGetValue(nav, out float until) && Time.time < until)
        {
            yield return Wander(nav, label);      // 아직 쿨다운 → 그냥 Wander
            yield break;
        }
        
        if (!TryReserveRoom(out var dst, new Vector2Int(sx, sz)))
        { yield return Wander(nav, label); yield break; }

        board[sx,sz] = RESERVED_LEAVE; 
        agents[sx,sz] = null;
        
        yield return MoveIntoRoom(nav, nav.GetComponent<NavMeshObstacle>(), dst, label);
    }

    IEnumerator RoomRoutine(NavMeshAgent nav,int gx,int gz,int label)
    {
        yield return new WaitForSeconds(5f);      // 방 안 5 초
        board[gx,gz] = RESERVED_LEAVE;
        agents[gx,gz] = null;

        if (agentCooldown.TryGetValue(nav, out float until) && Time.time < until)
        {
            yield return Wander(nav, label);      // 아직 쿨다운 → 그냥 Wander
            yield break;
        }
        
        if (!TryReserveRoom(out var dst, new Vector2Int(gx, gz)))
        { yield return Wander(nav, label); yield break; }
        yield return MoveIntoRoom(nav, nav.GetComponent<NavMeshObstacle>(), dst, label);
    }

    IEnumerator Wander(NavMeshAgent nav,int label)
    {
        /* ── 방을 떠날 때 현재 자리 비우기 ── */
        int ox = Mathf.RoundToInt(nav.transform.position.x);
        int oz = -Mathf.RoundToInt(nav.transform.position.z);
        if (IsRoom(ox,oz) && agents[ox,oz] == nav.gameObject)
        {
            if (board[ox,oz] == RESERVED_LEAVE)
                board[ox,oz] = 0;
            agents[ox,oz] = null;
        }
        
        /* ── wander 루프 ── */
        while (nav)
        {
            int cx = Mathf.RoundToInt(nav.transform.position.x);
            int cz = -Mathf.RoundToInt(nav.transform.position.z);
            
            if (board[cx,cz] == RESERVED_LEAVE)
                board[cx,cz] = 0;                 // 이제 진짜 비로소 빈 방
            
            if (agentCooldown.TryGetValue(nav, out float until) && Time.time < until)
            {
                nav.SetDestination(RandomRoad(nav.transform.position, 8f));
                yield return new WaitForSeconds(wanderInterval);
                continue;                       // 다음 while 회차
            }

            if (TryReserveRoom(out var dst, new Vector2Int(cx, cz)))
            {
                yield return MoveIntoRoom(nav, nav.GetComponent<NavMeshObstacle>(),
                    dst, label);
                continue;   // 도착하면 루프 재시작
            }

            nav.SetDestination(RandomRoad(nav.transform.position, 8f));
            yield return new WaitForSeconds(wanderInterval);
        }

        if (nav.TryGetComponent<AlreadyWandering>(out var aw))
            Destroy(aw);
    }

    /* ── Wander 시작 헬퍼 ─────────────────────────── */
    void StartWander(NavMeshAgent nav,int label)
    {
        if (nav.TryGetComponent<AlreadyWandering>(out _)) return;
        nav.gameObject.AddComponent<AlreadyWandering>();
        StartCoroutine(Wander(nav,label));
    }
 
    /* ── 빈 태그 컴포넌트 ─────────────────────────── */
    sealed class AlreadyWandering : MonoBehaviour {}
}

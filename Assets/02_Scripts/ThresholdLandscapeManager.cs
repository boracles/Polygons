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

    /*──────── Cooldowns ─────────*/
    readonly Dictionary<Vector2Int, float> cooldown      = new();   // 방 쿨
    readonly Dictionary<NavMeshAgent,float> agentCooldown= new();   // 개인 쿨
    readonly HashSet<Vector2Int> reserved = new();  // 예약 중 + 점유 중 모두 포함
    readonly Dictionary<NavMeshAgent, Vector2Int> occupied = new();    // 실제 점유

    const float ROOM_EXIT_DELAY = 2f;
    
    
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

    /* ── 좌표 헬퍼 ─────────────────────────────────── */
    static int CellX(Vector3 p) => Mathf.RoundToInt(p.x);
    static int CellZ(Vector3 p) => Mathf.RoundToInt(-p.z);
    static bool IsRoom(int x,int z) => idxSet.Contains(x) && idxSet.Contains(z);
    static Vector3 CellCenter(int x,int z)=> new(x, .5f, -z);
    
    Vector3 RandomRoad(Vector3 from,float r=6f)
    {
        for(int i=0;i<10;i++)
        {
            var p=from+Random.insideUnitSphere*r; p.y=0;
            if(NavMesh.SamplePosition(p,out var hit,.4f,NavMesh.AllAreas))
                return hit.position;
        }
        return from;
    }
    
    /*──────── Board reset & spawn ─*/
    void ResetBoard()
    {
        for(int x=0;x<width;x++)
        for(int z=0;z<height;z++){
            if(agents[x,z]) Destroy(agents[x,z]);
            board[x,z]=0; agents[x,z]=null;
        }

        for(int x=0;x<width;x++)
        for(int z=0;z<height;z++){
            float r=Random.value;
            if(r<emptyRatio) continue;
            int label = (r<emptyRatio+mainRatio)?1:2;
            SpawnAgent(x,z,label);
        }
    }

    void SpawnAgent(int gx,int gz,int label)
    {
        /* (1) prefab - 먼저 결정  */
        var prefab = (label == 1) ? mainPrefab : targetPrefab;

        /* (2) 실제 스폰 위치 = Road 또는 요청 셀 */
        Vector3 pos = CellCenter(gx, gz);
        if (IsRoom(gx, gz))                 // 방 좌표면 복도 쪽으로 이동
            pos = RandomRoad(pos, 4f);

        if (!NavMesh.SamplePosition(pos, out var hit, 1f, NavMesh.AllAreas))
            return;                         // 주위에 NavMesh 없으면 스킵

        /* (3) Instantiate */
        var go  = Instantiate(prefab, hit.position, Quaternion.identity, transform);

        /* (4) NavMeshAgent 세팅 */
        var nav = go.GetComponent<NavMeshAgent>();
        nav.stoppingDistance  = 0;
        nav.autoBraking       = true;
        nav.avoidancePriority = Random.Range(30, 70);

        // (5) micro-obstacle 추가 --------------- 
        var block = new GameObject("Block");
        block.transform.SetParent(go.transform, false);

        var obs = block.AddComponent<NavMeshObstacle>();
        obs.shape               = NavMeshObstacleShape.Box;
        obs.size                = new Vector3(0.2f, 2f, 0.2f);
        obs.carving             = true;   // ← 반드시 켜 주세요!
        obs.carveOnlyStationary = true;   // 이미 체크해 둔 옵션
        obs.enabled             = false;  // Freeze() 때 true 로 바뀜

        /* (6) board/agents 는 ‘실제 셀’ 좌표로 기록 */
        int cx = CellX(hit.position);
        int cz = CellZ(hit.position);
        board[cx, cz]  = label;
        agents[cx, cz] = go;

        /* (7) 처음 위치가 Room ? → RoomRoutine : RoadRoutine */
        if (IsRoom(cx, cz))
            StartCoroutine(RoomRoutine(nav, obs, cx, cz, label));
        else
            StartCoroutine(RoadRoutine(nav, obs, cx, cz, label));
    }
    
    bool TryReserveRoom(out Vector2Int room, Vector2Int forbid)
    {
        float now = Time.time;
        
        int start = Random.Range(0, rooms.Count);
        for (int i = 0; i < rooms.Count; ++i)
        {
            var c  = rooms[(start+i)%rooms.Count];
            var v  = new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(-c.z));

            if (v == forbid)                     continue;      // 내 자리
            if (reserved.Contains(v))            continue;      // 이미 예약/점유
            if (cooldown.TryGetValue(v, out var t) && now < t) continue;

            reserved.Add(v);                     // ★ 원자적 예약!
            room = v;
            return true;
        }
        room = default;
        return false;
    }

    IEnumerator Freeze(NavMeshAgent ag, NavMeshObstacle ob)
    {
        ag.ResetPath(); 
        ag.isStopped=true; 
        yield return null; 
        ag.enabled=false; 
        ob.enabled=true;
    }
    IEnumerator UnFreeze(NavMeshAgent ag,NavMeshObstacle ob)
    { 
        ob.enabled=false; 
        yield return null; 
        ag.enabled=true; 
        ag.isStopped=false; 
    }
    
    IEnumerator MoveIntoRoom(NavMeshAgent nav,NavMeshObstacle ob,Vector2Int dst,int label)
    {
        // (A) 이동 명령 성공했는지 즉시 확인
        if (!SafeMove(nav, CellCenter(dst.x, dst.y)))
        {
            reserved.Remove(dst);        // 예약 해제
            yield break;
        }
        
        // (B) 도착 판정 : path 가 있고, 아직 계산중이 아니며,
//     멈출 거리(stoppingDistance)를 포함해서 거의 다 왔다
        yield return new WaitUntil(() =>
            nav.enabled &&
            !nav.pathPending &&
            nav.hasPath &&
            nav.remainingDistance <= nav.stoppingDistance + arriveEps);

        // 3) 도착 실패(가로막힘 등) → 예약 취소
        if (nav.remainingDistance > arriveEps)
        { reserved.Remove(dst); yield break; }

        // 4) 점유 확정
        yield return Freeze(nav, ob);
        occupied[nav] = dst;
        board[dst.x,dst.y] = label;

        yield return new WaitForSeconds(5f);

        // 5) 떠나기
        yield return UnFreeze(nav, ob);
        occupied.Remove(nav);

        board[dst.x,dst.y] = RESERVED_LEAVE;
        StartCoroutine(ReleaseAfterExit(dst));     // reserved 해제는 여기서!
        agentCooldown[nav] = Time.time + ROOM_EXIT_DELAY;
        StartWander(nav, ob, label);
    }
    
    IEnumerator ReleaseAfterExit(Vector2Int cell)
    {
        yield return new WaitForSeconds(0.4f);              // NavMeshObstacle carve 끝날 시간
        board[cell.x,cell.y] = 0;
        reserved.Remove(cell);                              // ★ 이제 진짜 빈 방
    }
    
    /* ── 주요 코루틴들 ────────────────────────────── */
    IEnumerator RoadRoutine(NavMeshAgent nav,NavMeshObstacle ob,int sx,int sz,int label)
    {
        if(agentCooldown.TryGetValue(nav,out float t)&&Time.time<t)
        { yield return Wander(nav,ob,label); yield break; }

        if(!TryReserveRoom(out var dst,new Vector2Int(sx,sz)))
        { yield return Wander(nav,ob,label); yield break; }

        yield return new WaitForSeconds(0.5f);
        board[sx,sz]=RESERVED_LEAVE; 
        agents[sx,sz]=null;
        yield return MoveIntoRoom(nav,ob,dst,label);
    }

    IEnumerator RoomRoutine(NavMeshAgent nav,NavMeshObstacle ob,int gx,int gz,int label)
    {
        yield return new WaitForSeconds(0.5f);
        board[gx,gz]=RESERVED_LEAVE; 
        agents[gx,gz]=null;

        if(agentCooldown.TryGetValue(nav,out float t)&&Time.time<t)
        { yield return Wander(nav,ob,label); yield break; }

        if(!TryReserveRoom(out var dst,new Vector2Int(gx,gz)))
        { yield return Wander(nav,ob,label); yield break; }

        yield return MoveIntoRoom(nav,ob,dst,label);
    }

    IEnumerator Wander(NavMeshAgent nav,NavMeshObstacle ob,int label)
    {
        while (nav)
        {
            if (agentCooldown.TryGetValue(nav,out var t) && Time.time < t)
            {
                SafeMove(nav, RandomRoad(nav.transform.position,8f));
                yield return new WaitForSeconds(wanderInterval);
                continue;
            }

            /* 빈 방 찾기 시도 → 없으면 그냥 계속 돌아다님 */
            if (TryReserveRoom(out var dst,
                    new Vector2Int(CellX(nav.transform.position),
                        CellZ(nav.transform.position))))
            {
                yield return MoveIntoRoom(nav,ob,dst,label);
                continue;
            }

            // ★ 반드시 유효한 경로를 유지하게 함
            if (!nav.hasPath || nav.pathStatus != NavMeshPathStatus.PathComplete)
                SafeMove(nav, RandomRoad(nav.transform.position,8f));

            yield return new WaitForSeconds(wanderInterval);
        }
    }

    /* ── Wander 시작 헬퍼 ─────────────────────────── */
    void StartWander(NavMeshAgent nav,NavMeshObstacle ob,int label)
    {
        if(nav.TryGetComponent<AlreadyWandering>(out _)) return;
        nav.gameObject.AddComponent<AlreadyWandering>();
        StartCoroutine(Wander(nav,ob,label));
    }
 
    bool SafeMove(NavMeshAgent nav, Vector3 target)
    {
        if (!nav || !nav.enabled) return false;

        // NavMesh 위로 올리기
        if (!nav.isOnNavMesh)
        {
            if (!NavMesh.SamplePosition(nav.transform.position,
                    out var hit, 2f, NavMesh.AllAreas))
                return false;
            nav.Warp(hit.position);
        }

        // 새 목적지 → 항상 SetDestination
        if (!nav.SetDestination(target))
            return false;

        // 경로가 완전히 계산될 때까지 1프레임 양보
        return true;
    }
    
    sealed class AlreadyWandering : MonoBehaviour {}
}

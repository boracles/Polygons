using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ThresholdLandscapeManager : MonoBehaviour
{
    /* ────────────── 인스펙터 값 ────────────── */
    [Header("Grid Size")]
    public int width = 20, height = 20;

    [Header("Prefabs")]
    public GameObject mainPrefab, targetPrefab;

    [Header("Initial Ratios (0~1)")]
    [Range(0,1)] public float emptyRatio = 0.60f;
    [Range(0,1)] public float mainRatio  = 0.3f;
    [Range(0,1)] public float targetRatio= 0.11f;

    [Header("Agent Behaviour")]
    [SerializeField] float wanderInterval = 2f;
    [SerializeField] float arriveEps      = 0.05f;

    /* ────────────── 내부 상태 ────────────── */
    int[,]        board;        // 0 빈, 1 메인, 2 타깃, -1 예약
    GameObject[,] agentObjs;

    readonly List<Vector3>      wanderTargets = new();
    readonly HashSet<Vector2Int> roomSet       = new();
    readonly Queue<Vector2Int>   vacantRooms   = new();

    static readonly int[] roomIdx   = {1,2,4,5,7,8,11,12,14,15,17,18};
    static readonly HashSet<int> roomSet1D = new(roomIdx);
    const int RESERVED = -1;

    /* ────────────── Unity life-cycle ────────────── */
    void Awake()
    {
        board     = new int[width, height];
        agentObjs = new GameObject[width, height];
    }
    void Start() => InitBoard();
    
    /* ---------- 공용 헬퍼 ---------- */
    static bool InGrid(int x,int z,int w,int h) => x>=0 && x<w && z>=0 && z<h;
    static bool IsRoom (int x,int z) => roomSet1D.Contains(x) && roomSet1D.Contains(z);
    static bool IsRoad (int x,int z) => !IsRoom(x,z);

    bool IsRoomFree(int x,int z) => InGrid(x,z,width,height) && IsRoom(x,z) && board[x,z]==0;
    Vector3 CellCenter(int x,int z)=>new(x, .5f, -z);

    static void Shuffle<T>(IList<T> list)
    {
        for(int i=list.Count-1;i>0;--i){
            int j = Random.Range(0,i+1);
            (list[i],list[j])=(list[j],list[i]);
        }
    }
    static void Shuffle(Queue<Vector2Int> q)
    {
        var a=q.ToArray(); q.Clear(); Shuffle(a); foreach(var v in a)q.Enqueue(v);
    }

    bool NavPos(Vector3 src, float maxDist, out Vector3 p)
    {
        if (NavMesh.SamplePosition(src, out var hit, maxDist, NavMesh.AllAreas)){
            p = hit.position; return true;
        }
        p = src; return false;
    }

    /* ---------- 보드 초기화 ---------- */
    void InitBoard()
    {
        /* ① 방 좌표 목록 */
        wanderTargets.Clear(); roomSet.Clear();
        foreach(int x in roomIdx)
        foreach(int z in roomIdx){
            roomSet.Add(new Vector2Int(x,z));
            wanderTargets.Add(CellCenter(x,z));
        }
        Shuffle(wanderTargets);

        /* ② 2차원 배열 클리어 */
        for(int x=0;x<width;++x)
        for(int z=0;z<height;++z){
            if(agentObjs[x,z]) Destroy(agentObjs[x,z]);
            board[x,z]=0; agentObjs[x,z]=null;
        }

        /* ③ 랜덤 스폰 */
        for(int x=0;x<width;++x)
        for(int z=0;z<height;++z){
            float r = Random.value;
            if(r<emptyRatio) continue;
            int label = (r < emptyRatio+mainRatio) ? 1 : 2;
            SpawnAgent(x,z,label);
        }

        /* ④ 빈 방 큐 재구축 */
        vacantRooms.Clear();
        for(int x=0;x<width;++x)
        for(int z=0;z<height;++z)
            if(IsRoom(x,z) && board[x,z]==0)
                vacantRooms.Enqueue(new Vector2Int(x,z));
        Shuffle(vacantRooms);
    }

    /* ────────────── 에이전트 스폰 ────────────── */
    void SpawnAgent(int gx,int gz,int label)
    {
        var prefab = (label==1) ? mainPrefab : targetPrefab;
        var go     = Instantiate(prefab, CellCenter(gx,gz), Quaternion.identity, transform);

        var nav = go.GetComponent<NavMeshAgent>();
        nav.stoppingDistance = 0;           // 정확히 셀 중앙까지
        nav.autoBraking      = true;
        nav.updateRotation   = true;

        board[gx,gz]=label; agentObjs[gx,gz]=go;

        if(IsRoad(gx,gz))
            StartCoroutine(TryOccupyRoom(nav,gx,gz,label));
        else
            StartCoroutine(LeaveRoomAfterDelay(nav,gx,gz,label));
    }

    /* ────────────── 빈 방 큐 ────────────── */
    bool TryDequeueVacant(out Vector2Int room)
    {
        while(vacantRooms.Count>0){
            var c = vacantRooms.Dequeue();
            if(InGrid(c.x,c.y,width,height) && board[c.x,c.y]==0){
                board[c.x,c.y]=RESERVED;
                room=c; return true;
            }
        }
        room=default; return false;
    }

    /* ────────────── NavMesh carve helpers ────────────── */
    void FreezeInRoom(NavMeshAgent ag,NavMeshObstacle ob)
    {
        ag.ResetPath(); ag.velocity = Vector3.zero;
        ag.enabled=false;
        if(ob) ob.enabled=true;           // carve
    }
    void ResumeFromRoom(NavMeshAgent ag,NavMeshObstacle ob)
    {
        int x=Mathf.RoundToInt(ag.transform.position.x);
        int z=-Mathf.RoundToInt(ag.transform.position.z);

        if(InGrid(x,z,width,height) && IsRoom(x,z) && agentObjs[x,z]==ag.gameObject){
            board[x,z]=0; agentObjs[x,z]=null;
            vacantRooms.Enqueue(new Vector2Int(x,z));
        }
        if(ob) ob.enabled=false;

        Vector3 p=CellCenter(x,z);
        ag.enabled=true; ag.Warp(p); ag.isStopped=false;
    }

    /* ────────────── 코루틴 ────────────── */
    IEnumerator TryOccupyRoom(NavMeshAgent nav,int sx,int sz,int label)
    {
        if(!TryDequeueVacant(out var dst)){
            yield return StartCoroutine(Wander(nav)); yield break;
        }

        nav.SetDestination(CellCenter(dst.x,dst.y));
        yield return new WaitUntil(()=>!nav.pathPending && nav.remainingDistance<=arriveEps);

        board[sx,sz]=0; agentObjs[sx,sz]=null;
        board[dst.x,dst.y]=label; agentObjs[dst.x,dst.y]=nav.gameObject;

        var ob = nav.GetComponent<NavMeshObstacle>();
        FreezeInRoom(nav,ob);
        yield return new WaitForSeconds(5);
        ResumeFromRoom(nav,ob);
        StartCoroutine(Wander(nav));
    }

    IEnumerator LeaveRoomAfterDelay(NavMeshAgent nav,int gx,int gz,int label)
    {
        yield return new WaitForSeconds(5);

        if(!TryDequeueVacant(out var dst)){
            StartCoroutine(Wander(nav)); yield break;
        }
        board[gx,gz]=0; agentObjs[gx,gz]=null; vacantRooms.Enqueue(new Vector2Int(gx,gz));

        nav.SetDestination(CellCenter(dst.x,dst.y));
        yield return new WaitUntil(()=>!nav.pathPending && nav.remainingDistance<=arriveEps);

        board[dst.x,dst.y]=label; agentObjs[dst.x,dst.y]=nav.gameObject;

        var ob = nav.GetComponent<NavMeshObstacle>();
        FreezeInRoom(nav,ob);
        yield return new WaitForSeconds(5);
        ResumeFromRoom(nav,ob);
        StartCoroutine(Wander(nav));
    }
    IEnumerator Wander(NavMeshAgent nav)
    {
        while(nav && nav.isActiveAndEnabled)
        {
            /* 빈 방 탐색 */
            Vector3 goal=Vector3.zero; bool found=false;
            for(int i=0;i<30;i++){
                var cand=wanderTargets[Random.Range(0,wanderTargets.Count)];
                int x=Mathf.RoundToInt(cand.x), z=-Mathf.RoundToInt(cand.z);
                if(IsRoomFree(x,z)){goal=cand;found=true;break;}
            }

            if(found){
                int tx=Mathf.RoundToInt(goal.x), tz=-Mathf.RoundToInt(goal.z);
                board[tx,tz]=RESERVED;

                nav.SetDestination(CellCenter(tx,tz));
                yield return new WaitUntil(()=>!nav.pathPending && nav.remainingDistance<=arriveEps);

                nav.Warp(CellCenter(tx,tz));                           // 스냅
                int lab=(nav.GetComponent<Agent>().label==Agent.Label.Main)?1:2;
                board[tx,tz]=lab; agentObjs[tx,tz]=nav.gameObject;

                var ob=nav.GetComponent<NavMeshObstacle>();
                FreezeInRoom(nav,ob);
                yield return new WaitForSeconds(5);
                ResumeFromRoom(nav,ob);
                continue;                                             // 다음 루프
            }

            /* 빈방 없으면 길 wander */
            if(NavPos(RandomRoadPoint(nav.transform.position,8f),0.2f,out var roam))
                nav.SetDestination(roam);

            float t=0;
            while(t<wanderInterval){
                if(!nav.pathPending && nav.remainingDistance<=arriveEps) break;
                t+=Time.deltaTime; yield return null;
            }
        }
    }
    
    /// <summary>
    /// 현재 위치 from 주변의 NavMesh 위 ‘도로 셀’(y=0) 좌표를 무작위로 리턴.
    /// 실패하면 原 좌표를 그대로 반환한다.
    /// </summary>
    /// <param name="from">기준 월드 좌표</param>
    /// <param name="radius">탐색 반경</param>
    Vector3 RandomRoadPoint(Vector3 from, float radius = 6f)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = Random.insideUnitSphere * radius;
            dir.y = 0;                                     // 평면 상
            Vector3 cand = from + dir;

            // cand 가 NavMesh(즉, 길) 위에 있으면 그 위치 반환
            if (NavMesh.SamplePosition(cand, out NavMeshHit hit, 0.4f, NavMesh.AllAreas))
                return hit.position;
        }

        // 10 회 시도 모두 실패 → 제자리
        return from;
    }
    
}

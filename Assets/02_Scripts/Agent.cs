using UnityEngine;
using UnityEngine.AI; 
using System.Collections; 

public class Agent : MonoBehaviour
{
    public enum SatisfactionState
    {
        UnSatisfied = 0,
        Meh = 1,
        Satisfied = 2
    }
    
    /* 위치 상태 */
    public enum PlaceState { Road, Room }
    public PlaceState place = PlaceState.Road;
    
    int currentRoom = -1;   // 실제 점유 중인 방
    int targetRoom  = -1;   // 이동 목표로 예약한 방
    int lastRoom    = -1;   // 직전에 비웠던 방
    
    enum Phase { Moving, Leaving, Resting }              // ★ Exiting·Roading 제거
    Phase phase = Phase.Moving;
    
    Coroutine restCR;
    Coroutine reservationCR;  

    public enum Label 
    {
        Main,   // Adult-only
        Target  // Caregiver + Child (배제 대상)
    } 
    public Label label = Label.Main;    // Main이 기본값
    
    public enum AgentKind { Normal, Target }      // 역할
    public enum Trait     { Inclusive, Exclusive, Resistant, Avoidant }

    [Header("역할 & 성향")]
    public AgentKind kind = AgentKind.Normal;     // 인스펙터에서 설정
    public Trait     trait = Trait.Inclusive;

    [Header("분리 상태")]
    public SatisfactionState currentState = SatisfactionState.Satisfied;

    // 1=검정, 2=흰색
    public int color;

    NavMeshAgent nav;
    private NavMeshObstacle obs;
    
    [SerializeField] float restMin = 5.0f;
    [SerializeField] float restMax = 10.0f;
    [SerializeField] float roadWanderRadius = 2f;
    
    float stillTimer = 0f;
    
    PlaceState prevPlace = PlaceState.Road;   // 직전 장소 저장

    // 이웃 ratio에 따라 본인의 state를 결정
    public void SetStateByRatio(float ratio)
    {
        // 0.33 미만이면 UnSatisfied, 1.0 이상이면 Meh, 나머지는 Satisfied
        if(ratio < 0.33f)
        {
            currentState = SatisfactionState.UnSatisfied;
        }
        else if(ratio >= 1.0f)
        {
            currentState = SatisfactionState.Meh;
        }
        else
        {
            currentState = SatisfactionState.Satisfied;
        }
        UpdateAnimator();
    }
    
    // 외부에서 직접 SatisfactionState를 지정할 때
    public void SetState(SatisfactionState newState)
    {
        currentState = newState;
        UpdateAnimator();
    }

    void Awake()
    {
        nav = GetComponent<NavMeshAgent>(); // NavMeshAgent가 없다면 null
        obs = GetComponent<NavMeshObstacle>();
        
        nav.avoidancePriority = Random.Range(20, 80);
    }
    
    void Start()
    {
        // Spawn 이후 첫 프레임 전에 실행
        if (ThresholdLandscapeManager.I != null &&
            ThresholdLandscapeManager.I.TryGetRoomIdByPosition(transform.position, out int rid))
        {
            // 방 위에서 태어났다면 즉시 점유 표기
            ThresholdLandscapeManager.I.OccupyRoom(rid, gameObject);
            currentRoom = rid;
            phase = Phase.Resting;          // 상태 전환
            StartCoroutine(EnableObstacleNextFrame());  // ★ 휴식 코루틴 시동
        }
    }

    void Update()
{
    /* 0 ─ 매니저 존재 확인 ─────────────────────────────── */
    if (ThresholdLandscapeManager.I == null) return;

    /* 1 ─ Occupancy 갱신 : Moving 단계에서 프레임당 한 번만 */
    if (phase == Phase.Moving)
        ThresholdLandscapeManager.I.UpdateOccupancy(
            gameObject, currentRoom, out currentRoom);

    /* 2 ─ 현재 장소(Road/Room) 계산 */
    place = (currentRoom >= 0) ? PlaceState.Room : PlaceState.Road;

    if (place != prevPlace)
    {
        bool inRoomNow = (place == PlaceState.Room);
        BroadcastMessage("OnRoomStatusChanged", inRoomNow, SendMessageOptions.DontRequireReceiver);
        prevPlace = place;
    }

    /* 3 ─ 방 도착 → 휴식 진입 */
    bool roomArrived = nav.isActiveAndEnabled &&
                       currentRoom >= 0 &&
                       !nav.pathPending &&
                       nav.remainingDistance <= nav.stoppingDistance &&
                       nav.velocity.sqrMagnitude < 0.0025f;

    if (phase == Phase.Moving && roomArrived)
    {
        ThresholdLandscapeManager.I.OnRoomArrived(currentRoom, gameObject);
        targetRoom = -1;
        BeginRest();                     // phase → Resting
    }

    /* 4 ─ 길 도착 → 빈 방 시도 또는 방황 */
    bool roadArrived = nav.isActiveAndEnabled &&
                       currentRoom < 0 &&
                       !nav.pathPending &&
                       nav.remainingDistance <= nav.stoppingDistance &&
                       nav.velocity.sqrMagnitude < 0.0025f;

    if (phase == Phase.Moving && roadArrived)
    {
        if (!TryClaimEmptyRoom())
        {
            RandomRoadWander();
            StartCoroutine(RoadRetryCooldown(0.3f));
        }
    }

    /* 5 ─ 이동 중 예약이 사라졌으면 즉시 재시도 */
    if (phase == Phase.Moving &&
        targetRoom >= 0 &&
        !ThresholdLandscapeManager.I.IsReservedBy(targetRoom, gameObject))
    {
        targetRoom = -1;
        TryClaimEmptyRoom();
    }

    /* 6 ─ Leaving ⇒ 방을 완전히 빠져나왔는지 확인 */
    bool leftRoom = phase == Phase.Leaving &&
                    currentRoom < 0 &&
                    !nav.pathPending &&
                    nav.remainingDistance <= nav.stoppingDistance &&
                    nav.velocity.sqrMagnitude < 0.0025f;

    if (leftRoom)
    {
        phase = Phase.Moving;
        TryClaimEmptyRoom();
    }

    /* 7 ─ 애니메이터 파라미터 갱신 */
    UpdateAnimator();

    /* 8 ─ 정지 감시 : Moving 상태에서 1 초 이상 멈춰 있으면 경로 재계산 */
    if (phase == Phase.Moving && nav.enabled &&
        nav.velocity.sqrMagnitude < 0.0001f)
    {
        stillTimer += Time.deltaTime;
        if (stillTimer > 1f)
        {
            nav.ResetPath();
            targetRoom = -1;
            TryClaimEmptyRoom();
            stillTimer = 0f;
        }
    }
    else
    {
        stillTimer = 0f;
    }
}

    /* ──────────────────────────────────────────────
 *  휴식(방 점유) 시작 : 이동-에이전트를 잠시 정지시키고
 *  NavMeshObstacle 를 켠 뒤 RestTimer 로 넘어간다.
 * ──────────────────────────────────────────────*/
    void BeginRest()
    {
        // 1) 예약 타임아웃 코루틴 중지
        if (reservationCR != null)
        {
            StopCoroutine(reservationCR);
            reservationCR = null;
        }

        // 2) 상태 전환
        phase       = Phase.Resting;
        targetRoom  = -1;

        // 3) 에이전트 멈춤 & 경로 초기화
        nav.ResetPath();
        nav.isStopped = true;
        nav.velocity  = Vector3.zero;

        // 4) 한 프레임 뒤에 Obstacle 을 켜기 위해 Nav 끄고 코루틴 호출
        StartCoroutine(EnableObstacleNextFrame());
    }

/* ──────────────────────────────────────────────
 *  길 위에서 빈 방이 없을 때 짧게 ‘멈춘 뒤’ 다시 Moving 으로
 *  돌아가도록 하는 쿨타임 코루틴
 * ──────────────────────────────────────────────*/
    IEnumerator RoadRetryCooldown(float sec)
    {
        phase = Phase.Resting;           // 잠깐 대기 상태
        yield return new WaitForSeconds(sec);
        phase = Phase.Moving;
    }


    
    IEnumerator EnableObstacleNextFrame()
    {
        yield return null;                    // 1 frame

        nav.enabled = false;
        
        // ④ Obstacle 설정 & 활성
        obs.carving = true;                   // 반드시 ON
        obs.carveOnlyStationary = false;      // 떨림 무시하고 즉시 carve
        obs.enabled = true;

        // ⑤ 휴식 타이머
        float t = Random.Range(restMin, restMax);
        if (restCR != null) StopCoroutine(restCR);
        restCR = StartCoroutine(RestTimer(t));
    }
    
    /* ───── 빈 방 찾고 점유 등록 ───── */
    bool TryClaimEmptyRoom() 
    {
        if (!ThresholdLandscapeManager.I.TryReserveFreeRoom(
                out int rid, gameObject, exclude:lastRoom))
            return false;

        targetRoom = rid;
        nav.SetDestination(ThresholdLandscapeManager.I.GetRoomPosition(rid));

        float dist = Vector3.Distance(transform.position,
            ThresholdLandscapeManager.I.GetRoomPosition(rid));
        float travelTime = dist / nav.speed;   // nav.speed == 3 → 10m ≈ 3.3s
        float buffer = 0.3f;                   // 여유
        reservationCR = StartCoroutine(
            ReservationTimeout(rid, travelTime + buffer));
        
        return true;
    }
    /* ───── 예약 타임아웃 ───── */
    IEnumerator ReservationTimeout(int roomId, float sec)
    {
        yield return new WaitForSeconds(sec);

        // 아직 점유 못했고 예약도 살아있을 때만 해제
        if (targetRoom == roomId && currentRoom < 0 &&
            ThresholdLandscapeManager.I.IsReserved(roomId))
        {
            ThresholdLandscapeManager.I.CancelReservation(roomId, gameObject);
            targetRoom = -1;

            // ➜ 아직 이동 중이라면 바로 Repath
            nav.ResetPath();
            if (phase == Phase.Moving)
            {
                yield return null;      // 1 frame 쉬고
                TryClaimEmptyRoom();    // 새 방 재시도
            }
        }
    }
    
    /* 빈 방이 없을 땐 길 위에서 랜덤 워크 */
    void RandomRoadWander()
    {
        for (int i = 0; i < 6; i++)               // 최대 6회 시도
        {
            Vector2 off = Random.insideUnitCircle.normalized * 2f;
            Vector3 dest = transform.position + new Vector3(off.x, 0, off.y);

            if (!NavMesh.SamplePosition(dest, out var hit, 0.6f, NavMesh.AllAreas))
                continue;                         // NavMesh 밖 → 재시도

            // 방이면 한 칸 더
            if (ThresholdLandscapeManager.I.TryGetRoomIdByPosition(hit.position, out _))
                dest += new Vector3(off.x,0,off.y);

            if (nav.enabled && nav.SetDestination(dest)) return; // 성공!
        }

        // 6회 실패 → 마지막 수단: 바로 앞 한 걸음
        nav.SetDestination(transform.position + transform.forward * 0.5f);
    }
    
    void UpdateAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if (!anim) return;
        anim.SetBool("IsInRoom", place == PlaceState.Room);
        anim.SetInteger("SatisfactionState", (int)currentState);
    }

    /* ───── 휴식 종료 ───── */
    IEnumerator RestTimer(float wait)
    {
        yield return new WaitForSeconds(wait);

        int rid = currentRoom;                             // ★ 백업
        if (rid >= 0) ThresholdLandscapeManager.I.VacateRoom(rid);
        
        /* 현재 방 비우기 */
        lastRoom     = rid;   
        currentRoom = -1;                // (UpdateOccupancy 가 해제 처리)

        obs.enabled = false;
        yield return new WaitForSeconds(0.05f);

        /* ───── 방을 나가는 짧은 이동 ───── */
        nav.enabled   = true;
        nav.isStopped = false;

        // 길 좌표 계산
        Vector3 exitPos = ThresholdLandscapeManager.I.GetNearestRoadPos(rid);

        phase = Phase.Leaving; 
        
        // 한 걸음 이동
        bool ok = nav.SetDestination(exitPos);
        if (!ok) nav.Warp(exitPos);
    }
    
    IEnumerator RepathUntilSuccess()
    {
        int tries = 0;
        while (tries < 4 && !TryClaimEmptyRoom())
        {
            RandomRoadWander();
            yield return new WaitForSeconds(0.7f);
            tries++;
        }
    }

}
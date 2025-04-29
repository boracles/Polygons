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
    
    enum Phase { Moving, Resting }              // ★ Exiting·Roading 제거
    Phase phase = Phase.Moving;
    
    Coroutine restCR;

    public enum Label 
    {
        Main,   // Adult-only
        Target  // Caregiver + Child (배제 대상)
    } 
    public Label label = Label.Main;    // Main이 기본값
    public float bias;                  // 0-1, 편견 강도

    [Header("분리 상태")]
    public SatisfactionState currentState = SatisfactionState.Satisfied;

    // 1=검정, 2=흰색
    public int color;

    NavMeshAgent nav; 
    
    [SerializeField] float restMin = 3f;
    [SerializeField] float restMax = 7f;
    [SerializeField] float roadWanderRadius = 2f;
    
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
    }
    
    void Update()
    {
        // ThresholdLandscape 씬이 아니면 매니저가 없으므로 즉시 리턴
        if (ThresholdLandscapeManager.I == null) return;
        
        // 매 프레임 위치 판정 및 Occupy/ Vacate 처리
        ThresholdLandscapeManager.I.UpdateOccupancy(gameObject, currentRoom, out currentRoom);

        /* 방 도착 ⇒ 휴식 진입 */
        bool roomArrived =
            currentRoom >= 0 &&
            !nav.pathPending &&
            nav.remainingDistance <= nav.stoppingDistance &&
            nav.velocity.sqrMagnitude < 0.0025f;

        if (phase == Phase.Moving && roomArrived)
        {
            currentRoom = targetRoom;                // ★ 이제 진짜 점유
            targetRoom  = -1;
            BeginRest();
        }                     
        
        /* 길 위 도착 ⇒ 즉시 빈 방 시도 */
        bool roadArrived =
            currentRoom < 0 &&
            !nav.pathPending &&
            nav.remainingDistance <= nav.stoppingDistance &&
            nav.velocity.sqrMagnitude < 0.0025f;

        if (roadArrived && phase == Phase.Moving)
            TryClaimEmptyRoom();                 // ↓ ②

        UpdateAnimator();
    }
    
    /* ───── 휴식 진입 ───── */
    void BeginRest()
    {
        phase = Phase.Resting;
        nav.ResetPath();
        float t = Random.Range(restMin, restMax);
        if (restCR != null) StopCoroutine(restCR);
        restCR = StartCoroutine(RestTimer(t));
    }
    
    /* ───── 빈 방 찾고 점유 등록 ───── */
    bool TryClaimEmptyRoom() 
    {
        if (!ThresholdLandscapeManager.I.TryTakeFreeRoom(
                out int rid, exclude: lastRoom, agent: gameObject))
            return false;

        targetRoom = rid;                     // ★ 도착 전까지는 targetRoom
        nav.SetDestination(ThresholdLandscapeManager.I.GetRoomPosition(rid));
        return true;
    }
    
    /* 빈 방이 없을 땐 길 위에서 랜덤 워크 */
    void RandomRoadWander()
    {
        Vector3 offset = Random.insideUnitCircle.normalized * 2f;
        Vector3 dest   = transform.position + new Vector3(offset.x, 0, offset.y);

        // 목적지가 방이면 살짝 더 이동해 길로
        if (ThresholdLandscapeManager.I.TryGetRoomIdByPosition(dest, out _))
            dest += new Vector3(offset.x, 0, offset.y);

        if (nav) nav.SetDestination(dest); else transform.position = dest;
    }
    
    void UpdateAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if (!anim) return;
        anim.SetBool("IsInRoom", place == PlaceState.Room);
        anim.SetInteger("SatisfactionState", (int)currentState);
    }
    
    /* 방 점유 직후 호출 */
    void StartResting()
    {
        phase = Phase.Resting;
        
        nav.isStopped = true;      // 경로 유지 O, 이동 정지
        nav.ResetPath();           // 경로 제거 (필요하면 주석)
        
        float t = Random.Range(3f, 7f);      // 3-7초
        if (restCR != null) StopCoroutine(restCR);
        restCR = StartCoroutine(RestTimer(t));
    }
    
    /* ───── 휴식 종료 ───── */
    IEnumerator RestTimer(float wait)
    {
        yield return new WaitForSeconds(wait);

        /* 현재 방 비우기 */
        ThresholdLandscapeManager.I.VacateRoom(currentRoom);
        lastRoom   = currentRoom;
        currentRoom = -1;             // 방을 떠났으니 -1
        phase       = Phase.Moving;

        /* 빈 방 시도, 실패 시 한 번 길 배회 */
        if (!TryClaimEmptyRoom())
            RandomRoadWander();
    }
}
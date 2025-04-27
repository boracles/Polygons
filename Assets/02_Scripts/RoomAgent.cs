using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class RoomAgent : MonoBehaviour
{
    enum Phase { Resting, Moving }

    [SerializeField] float restDuration = 5f;
    [SerializeField] float minMoveDistance = .5f;     // 방·현재 위치 최소 간격
    [SerializeField] float stuckThreshold  = 1f;      // ★ 1초 이상 제자리면 재시도

    NavMeshAgent nav;
    Phase        phase;
    int          currentRoom = -1;

    Coroutine    restCR;
    float        stillTimer;                          // ★ 제자리 시간 누적

    /* ─────────── 초기화 ─────────── */
    public void Init(int startRoom = -1)
    {
        nav = GetComponent<NavMeshAgent>();
        if (startRoom >= 0) StartResting(startRoom);
        else                RequestAndMove();
    }

    /* ─────────── 상태 전환 ─────────── */
    void StartResting(int roomIdx)
    {
        phase       = Phase.Resting;
        currentRoom = roomIdx;

        nav.ResetPath();
        nav.isStopped = true;

        if (restCR != null) StopCoroutine(restCR);
        restCR = StartCoroutine(RestTimer());
    }
    IEnumerator RestTimer()
    {
        yield return new WaitForSeconds(restDuration);
        LeaveRoomAndMove();
    }

    void LeaveRoomAndMove()
    {
        ThresholdLandscapeManager.I.VacateRoom(currentRoom);   // roomIdx가 -1이면 안전하게 무시
        currentRoom = -1;
        RequestAndMove();
    }

    void RequestAndMove()
    {
        // 1) 빈 방 시도 (현재 방 제외)
        bool gotRoom = ThresholdLandscapeManager.I.TryReserveFreeRoom(out int roomIdx,
            exclude: currentRoom);

        // 2) 그래도 못 구했으면 '-1' 을 담아서, 5초 뒤(휴식) 다시 재시도
        if (!gotRoom)
        {
            Debug.Log($"{name} 빈 방 없음 → 5초 뒤 재시도");
            StartResting(-1);
            return;
        }

        // 3) 방을 구했다면 곧장 이동
        MoveToRoom(roomIdx);
    }

    void MoveToRoom(int roomIdx)
{
    Vector3 target = (roomIdx >= 0)
        ? ThresholdLandscapeManager.I.GetRoomPosition(roomIdx)
        : transform.position + Random.insideUnitSphere * 2f;

    /* 너무 가까우면 5 초 쉬고 다시 시도(재귀 금지) */
    if (Vector3.Distance(transform.position, target) < minMoveDistance)
    {
        if (roomIdx >= 0) ThresholdLandscapeManager.I.VacateRoom(roomIdx);
        StartResting(-1);
        return;
    }

    currentRoom = roomIdx;
    phase       = Phase.Moving;
    stillTimer  = 0f;

    nav.isStopped = false;
    nav.ResetPath();

    /* ① 실제 목적지 지정 ─ 실패하면 5 초 뒤 재시도 */
    if (!nav.SetDestination(target))
    {
        Debug.LogWarning($"{name} 경로 실패 → 5초 뒤 재시도");
        StartResting(-1);
        return;
    }
}


    /* ─────────── 매 프레임 체크 ─────────── */
    void Update()
    {
        if (phase != Phase.Moving || nav.pathPending) return;

        /* ① 정상 도착 */
        if (nav.pathStatus == NavMeshPathStatus.PathComplete &&
            nav.remainingDistance <= 0.05f)
        {
            StartResting(currentRoom);
            return;
        }

        /* ② ‘길 잃음’ + 정지 ⇒ Stuck 타이머 */
        if (!nav.hasPath || nav.velocity.sqrMagnitude < 0.0001f)
            stillTimer += Time.deltaTime;
        else
            stillTimer = 0f;

        if (stillTimer > stuckThreshold)
        {
            Debug.Log($"{name} Stuck → 재시도");
            StartResting(currentRoom);           // 5초 쉬고 다시 방 찾기
        }
    }
}

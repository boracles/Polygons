using UnityEngine;
using UnityEngine.AI;

/// <summary>방 ↔ 도로 이동·정지 로직</summary>
[RequireComponent(typeof(NavMeshAgent))]
public class RoomAgent : MonoBehaviour
{
    enum Phase { Resting, Moving }

    NavMeshAgent nav;
    Phase phase;
    float restTimer;

    int currentRoom = -1;    // 점유 중인 방 인덱스 (없으면 -1)

    /*──────────── 초기화 ────────────*/
    public void Init(int startRoom = -1)     // Manager에서 바로 호출
    {
        nav = GetComponent<NavMeshAgent>();

        if (startRoom >= 0)  StartResting(startRoom);   // 방 안에서 시작
        else                 RequestAndMove();          // 도로에서 시작(예비용)
    }

    void Start()            // 프리팹에 직접 붙여 사용해도 안전
    {
        if (nav == null) nav = GetComponent<NavMeshAgent>();
        if (phase == 0 && currentRoom < 0) RequestAndMove();
    }

    /*──────────── 매 프레임 ───────────*/
    void Update()
    {
        switch (phase)
        {
            /* ① 쉬는 중 ------------------------------------------------*/
            case Phase.Resting:
                restTimer -= Time.deltaTime;
                if (restTimer <= 0f)
                {
                    // 방 비우고 이동 시작
                    ThresholdLandscapeManager.I.VacateRoom(currentRoom);
                    currentRoom = -1;
                    RequestAndMove();
                }
                break;

            /* ② 이동 중 ------------------------------------------------*/
            case Phase.Moving:
                if (!nav.pathPending && nav.remainingDistance <= nav.stoppingDistance)
                {
                    StartResting(currentRoom);   // 목적지 도착 → 휴식
                }
                break;
        }
    }

    /*──────────── 상태 전환 ───────────*/
    void StartResting(int roomIdx)
    {
        phase         = Phase.Resting;
        restTimer     = 5f;              // 5초 휴식
        currentRoom   = roomIdx;
        nav.ResetPath();
        nav.isStopped = true;
    }

    void RequestAndMove()
    {
        if (ThresholdLandscapeManager.I.TryReserveFreeRoom(out int roomIdx))
        {
            currentRoom   = roomIdx;
            phase         = Phase.Moving;
            nav.isStopped = false;
            nav.SetDestination(ThresholdLandscapeManager.I.GetRoomPosition(roomIdx));
        }
        else
        {
            // 만실: 아무 곳이나 배회(옵션)
            phase = Phase.Moving;
            nav.isStopped = false;
            nav.SetDestination(transform.position + Random.insideUnitSphere * 5f);
        }
    }
}

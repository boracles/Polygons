using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class MotherAI : MonoBehaviour
{
    // 이동 경로가 될 웨이포인트들 (Hierarchy 상에 생성한 빈 오브젝트의 Transform 등)
    public Transform[] waypoints;
    // 웨이포인트에 도착했다고 보는 거리 기준
    public float waypointThreshold = 1.0f;

    private NavMeshAgent agent;
    private int currentIndex = 0;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // 코루틴 방식으로 이동 처리
        StartCoroutine(MoveSequence());
        if (NavMesh.SamplePosition(new Vector3(0, 0, 0), out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            Debug.Log("NavMesh 있음! 위치: " + hit.position);
        }
        else
        {
            Debug.LogWarning("여기 NavMesh 없음!");
        }
        
    }

    // 코루틴: 순차적으로 모든 웨이포인트를 이동
    IEnumerator MoveSequence()
    {
        // 1프레임 대기하여 NavMeshAgent 준비 시간 부여
        yield return null;

        // 혹시 Agent가 NavMesh 위에 없으면 Warp로 보정
        if (!agent.isOnNavMesh)
        {
            // 씬에서 캐릭터 근처 NavMesh 상 좌표를 찾는다
            Vector3 safePos = FindClosePointOnNavMesh(transform.position, 10f);
            // 그 위치로 순간이동(Warp)
            agent.Warp(safePos);

            // 한 프레임 더 대기
            yield return null;
        }

        // 웨이포인트가 설정되지 않았다면 종료
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("Waypoints가 없습니다.");
            yield break;
        }

        // 순차 이동
        currentIndex = 0;
        while (currentIndex < waypoints.Length)
        {
            // 목적지 설정
            agent.SetDestination(waypoints[currentIndex].position);

            // NavMeshAgent가 경로를 계산 중(pathPending)이면 대기
            yield return new WaitWhile(() => agent.pathPending);

            // 남은 거리가 waypointThreshold 이하가 될 때까지 대기
            yield return new WaitUntil(() => 
                agent.remainingDistance <= waypointThreshold
            );

            Debug.Log($"웨이포인트 {currentIndex} 도착!");

            // 다음 웨이포인트 인덱스로
            currentIndex++;
        }

        // 모든 웨이포인트 이동 완료
        Debug.Log("엄마가 루트를 모두 완료했습니다.");
        // 이후 아기 내려놓기 / 장면 전환 / 이벤트 등 원하는 로직 수행
    }

    // NavMesh 근처의 좌표를 찾는 헬퍼 함수 (NavMesh.SamplePosition 사용)
    Vector3 FindClosePointOnNavMesh(Vector3 origin, float distance)
    {
        NavMeshHit hit;
        // origin 근처 distance 반경 내에서 유효 NavMesh 좌표 찾기
        if (NavMesh.SamplePosition(origin, out hit, distance, NavMesh.AllAreas))
        {
            return hit.position;
        }
        // 못 찾으면 그냥 origin 반환(실패 상황 대비)
        return origin;
    }
}

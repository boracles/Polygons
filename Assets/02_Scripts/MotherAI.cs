using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class MotherAI : MonoBehaviour
{
    public Transform[] waypoints;
    public float waypointThreshold = 1.0f;
    
    public BabyController baby;           // 아기 스크립트 연결
    public Transform strollerSeat; 

    private NavMeshAgent agent;
    private int currentIndex = 0;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        StartCoroutine(MoveSequence());
    }
    
    IEnumerator MoveSequence()
    {
        yield return null;
        
        if (!agent.isOnNavMesh)
        {
            Vector3 safePos = FindClosePointOnNavMesh(transform.position, 10f);
            agent.Warp(safePos);
            yield return null;
        }
        
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("Waypoints가 없습니다.");
            yield break;
        }
        
        currentIndex = 0;
        while (currentIndex < waypoints.Length)
        {
            agent.SetDestination(waypoints[currentIndex].position);
            yield return new WaitWhile(() => agent.pathPending);
            yield return new WaitUntil(() => 
                agent.remainingDistance <= waypointThreshold
            );

            Debug.Log($"웨이포인트 {currentIndex} 도착!");
            currentIndex++;
        }
        
        Debug.Log("엄마가 루트를 모두 완료했습니다.");
        
        // 아기에게 유모차로 이동 명령
        if (baby != null && strollerSeat != null)
        {
            baby.MoveToStroller(strollerSeat);
        }
    }
    
    Vector3 FindClosePointOnNavMesh(Vector3 origin, float distance)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(origin, out hit, distance, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return origin;
    }
}

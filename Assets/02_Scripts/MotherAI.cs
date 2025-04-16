using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class MotherAI : MonoBehaviour
{
    public Transform[] waypoints;
    public float waypointThreshold = 1.0f;
    
    public BabyController baby;          
    public Transform strollerSeat; 

    private NavMeshAgent agent;
    private int currentIndex = 0;
    private Vector3 startPosition;
    
    private bool isReturning = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        startPosition = transform.position; // 출발 지점 저장
        StartCoroutine(MoveSequence());
    }
    
    IEnumerator MoveSequence()
    {
        yield return null;
        
        if (!agent.isOnNavMesh)
        {
            Vector3 safePos = FindClosePointOnNavMesh(transform.position, 10.0f);
            agent.Warp(safePos);
            yield return null;
            
            if (!agent.isOnNavMesh)
            {
                Debug.LogError("NavMesh에 올라가지 못함");
                yield break;
            }
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

            currentIndex++;
        }
        
        Debug.Log("엄마가 목적지 도착.");
        // 🚫 이동 멈추기
        agent.isStopped = true;
        agent.ResetPath();
        
        // 약간 기다렸다가 아기 이동
        yield return new WaitForSeconds(2.0f); 
        
        // 아기에게 유모차로 이동 명령
        if (baby != null && strollerSeat != null)
        {
            baby.MoveToStroller(strollerSeat);
        }
        
        // 1초 기다렸다가 돌아감
        yield return new WaitForSeconds(1.0f);
        
        isReturning = true;
        agent.isStopped = false;
        agent.SetDestination(startPosition);

        // 돌아가는 도중 체크
        yield return new WaitWhile(() => agent.pathPending);
        yield return new WaitUntil(() => agent.remainingDistance <= waypointThreshold);

        Debug.Log("엄마가 출발 지점으로 돌아옴");
        gameObject.SetActive(false);
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

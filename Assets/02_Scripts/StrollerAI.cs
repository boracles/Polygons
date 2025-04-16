using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class StrollerAI : MonoBehaviour
{
    public Transform destination;          // 목적지 Transform
    public float stopThreshold = 1.0f;

    private NavMeshAgent agent;
    private bool isMoving = false;
    
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public void StartMoving()
    {
        if (destination == null)
        {
            Debug.LogWarning("유모차 목적지가 설정되지 않음");
            return;
        }

        isMoving = true;
        agent.SetDestination(destination.position);
        StartCoroutine(WaitUntilArrival());
    }

    IEnumerator WaitUntilArrival()
    {
        yield return new WaitWhile(() => agent.pathPending);
        yield return new WaitUntil(() => agent.remainingDistance <= stopThreshold);

        agent.isStopped = true;
        agent.ResetPath();

        Debug.Log("유모차 도착 완료");
    }
}
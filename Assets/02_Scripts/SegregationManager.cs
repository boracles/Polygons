using UnityEngine;
using System.Collections;
using UnityEngine.UI; 
using TMPro;
using System.Collections.Generic;

public class SegregationManager : MonoBehaviour
{
    [Header(("Grid Size"))]
    public int width = 20;
    public int height = 20;

    [Header("Prefabs")]
    public GameObject blackSpherePrefab;
    public GameObject whiteCubePrefab;
    
    [Header(("Thresholds"))]
    [Range(0f, 1f)] public float blackThreshold = 0.33f;
    [Range(0f, 1f)] public float whiteThreshold = 0.33f;
    
    [Header("UI")]
    public TMP_Text statusText;
    public float autoRoundInterval = 1f; 

    // 내부 관리
    private int[,] board;   // 0=빈칸, 1=검정 구, 2=흰색 큐브
    private GameObject[,] agentObjects;
    private bool isAutoRunning = false;
    private int roundCount = 0;
    private Coroutine autoCoroutine = null;

    void Start()
    { 
        InitBoard();
        UpdateStatusText();
    }
    
    public void InitBoard()
    {
        if(board != null)
        {
            for(int x = 0; x < width; x++)
            {
                for(int z = 0; z < height; z++)
                {
                    if(agentObjects != null && agentObjects[x,z] != null)
                    {
                        Destroy(agentObjects[x,z]);
                    }
                }
            }
        }

        board = new int[width, height];
        agentObjects = new GameObject[width, height];
        roundCount = 0;

        for(int x = 0; x < width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                float rand = Random.value;
                if(rand < 0.3f)
                {
                    // 빈 칸
                    board[x,z] = 0;
                }
                else
                {
                    if(rand < 0.65f)
                    {
                        board[x,z] = 1;  // 검정
                        InstantiateAgent(blackSpherePrefab, x, z, 1);
                    }
                    else
                    {
                        board[x,z] = 2;  // 흰색
                        InstantiateAgent(whiteCubePrefab, x, z, 2);
                    }
                }
            }
        }

        // 초기 만족/불만족 설정
        for(int x=0; x<width; x++)
        {
            for(int z=0; z<height; z++)
            {
                if(board[x,z] != 0)
                {
                    bool sat = IsSatisfied(x,z);
                    SetAgentSatisfied(x,z, sat);
                }
            }
        }
    }

    
    // 에이전트 생성 함수
    void InstantiateAgent(GameObject prefab, int x, int z, int color)
    {
        GameObject agent = Instantiate(prefab, new Vector3(x,0.5f,-z), Quaternion.identity);
        agentObjects[x,z] = agent;
        
        // 색깔 정보
        Agent agentComp = agent.GetComponent<Agent>();
        if(agentComp != null)
        {
            agentComp.color = color; // 1=검정, 2=흰색
        }
    }
    
    // 순차적으로 불만족자를 한 명씩 이동
    IEnumerator DoOneRoundSequential()
    {
        roundCount++;
        int moveCount = 0;
        
        while(true)
        {
            // 불만족자 수집
            List<(int x, int z)> unsatisfiedList = new List<(int, int)>();
            for(int x=0; x<width; x++)
            {
                for(int z=0; z<height; z++)
                {
                    if(board[x,z] == 0) continue;
                    if(!IsSatisfied(x,z))
                    {
                        unsatisfiedList.Add((x,z));
                    }
                }
            }
            
            // 불만족자가 하나도 없으면 -> 이 라운드 종료
            if(unsatisfiedList.Count == 0)
            {
                break; 
            }
            
            // 첫 번째 불만족자만 이동 (혹은 랜덤 한 명)
            var (oldX, oldZ) = unsatisfiedList[0];
            if(board[oldX, oldZ] == 0) 
            {
                // 혹시 이미 이동되었으면 패스
            }
            else
            {
                int color = board[oldX, oldZ];
                
                // Hit Reaction 애니메이션: 불만족 상태이므로
                SetAgentSatisfied(oldX, oldZ, false);
                yield return new WaitForSeconds(0.2f);
                
                Vector2Int? candidate = FindRandomCandidate(color);
                if(candidate.HasValue)
                {
                    moveCount++;
                    Vector2Int c = candidate.Value;

                    // (간단 버전) 즉시 파괴 후 생성, 혹은 Lerp 이동 구현 가능
                    yield return StartCoroutine(MoveAgentLerp(oldX, oldZ, c.x, c.y, 0.5f));
                    
                    bool nowSat = IsSatisfied(c.x, c.y);
                    SetAgentSatisfied(c.x, c.y, nowSat);
                }
            }

            // 이동 후, 잠깐 대기
            yield return new WaitForSeconds(0.1f);
        }
        
        UpdateStatusText(moveCount);
    }
    
    // 기존 오브젝트를 부드럽게 새 위치로 이동
    IEnumerator MoveAgentLerp(int oldX, int oldZ, int newX, int newZ, float lerpDuration)
    {
       int color = board[oldX, oldZ];
       
               // 1) 에이전트/오브젝트 가져오기
               GameObject agent = agentObjects[oldX, oldZ];
               if(agent == null) yield break; // 혹시 null이면 중단
       
               // 2) 보드 갱신(배열에서 old 자리 비우고 new 자리 차지)
               board[oldX, oldZ] = 0;
               board[newX, newZ] = color;
       
               // 3) agentObjects 갱신
               agentObjects[oldX, oldZ] = null;
               agentObjects[newX, newZ] = agent;
       
               // 4) Lerp 동작 (시작-끝 위치 계산)
               Vector3 startPos = agent.transform.position;               // 현재 위치
               Vector3 endPos   = new Vector3(newX, 0.5f, -newZ);         // 목표 위치
       
               float elapsed = 0f;
               while(elapsed < lerpDuration)
               {
                   elapsed += Time.deltaTime;
                   float t = Mathf.Clamp01(elapsed / lerpDuration);
                   agent.transform.position = Vector3.Lerp(startPos, endPos, t);
                   yield return null;
               }
               // 마지막에 위치 확정
               agent.transform.position = endPos;
    }

    /// <summary>
    /// 에이전트의 Animator에서 isSatisfied = true/false
    /// </summary>
    void SetAgentSatisfied(int x, int z, bool satisfied)
    {
        GameObject agent = agentObjects[x,z];
        if(agent == null) return;
        
        Agent agentComp = agent.GetComponent<Agent>();
        if(agentComp != null)
        {
            agentComp.currentState = satisfied ? Agent.SatisfactionState.Satisfied 
                : Agent.SatisfactionState.UnSatisfied;
        }
        
        Animator anim = agent.GetComponent<Animator>();
        if(anim != null)
        {
            anim.SetBool("isSatisfied", satisfied);
        }
    }
    
    bool IsSatisfied(int x, int z)
    {
        if(board[x,z] == 0) return true; // 빈 칸이면 스킵 or 만족 취급
        
        GameObject agent = agentObjects[x,z];
        if(agent == null) return true;

        Agent agentComp = agent.GetComponent<Agent>();
        if(agentComp == null) return true;

        int myColor = agentComp.color; // 1 or 2
        int sameCount = 0;
        int totalNeighbors = 0;

        for(int nx = x-1; nx <= x+1; nx++)
        {
            for(int nz = z-1; nz <= z+1; nz++)
            {
                if(nx == x && nz == z) continue;
                if(nx<0 || nx>=width || nz<0 || nz>=height) continue;

                if(board[nx,nz] != 0)
                {
                    totalNeighbors++;
                    if(board[nx,nz] == myColor)
                    {
                        sameCount++;
                    }
                }
            }
        }

        if(totalNeighbors == 0)
        {
            // 이웃이 없으면 일단 만족 처리
            agentComp.SetStateByRatio(1f);
            return true;
        }

        float ratio = (float)sameCount / totalNeighbors;
        bool satisfied;
        if(myColor == 1) // 검정
        {
            satisfied = ratio >= blackThreshold;
        }
        else // 흰색
        {
            satisfied = ratio >= whiteThreshold;
        }

        // Agent에 ratio 전달, 내부 state도 업데이트
        agentComp.SetStateByRatio(ratio);
        return satisfied;
    }

    Vector2Int? FindRandomCandidate(int color)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for(int x=0; x<width; x++)
        {
            for(int z=0; z<height; z++)
            {
                if(board[x,z] == 0)
                {
                    if(IsSatisfiedIf(color, x, z))
                    {
                        candidates.Add(new Vector2Int(x,z));
                    }
                }
            }
        }

        if(candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }
        return null;
    }

    bool IsSatisfiedIf(int color, int x, int z)
    {
        int sameCount = 0;
        int totalNeighbors = 0;
        
        for(int nx = x-1; nx <= x+1; nx++)
        {
            for(int nz = z-1; nz <= z+1; nz++)
            {
                if(nx == x && nz == z) continue;
                if(nx<0 || nx>=width || nz<0 || nz>=height) continue;

                if(board[nx,nz] != 0)
                {
                    totalNeighbors++;
                   if(board[nx,nz] == color)
                   { 
                       sameCount++;
                   }
                }
            }
        }
        
        if(totalNeighbors == 0) return true; // 이웃이 없으면 만족

        float ratio = (float)sameCount / totalNeighbors;
        if(color == 1) // 검정
        {
            return (ratio >= blackThreshold);
        }
        else // 흰색
        {
            return (ratio >= whiteThreshold);
        }
    }

    private void UpdateStatusText(int movedCount = 0)
    {
        if(statusText != null)
        {
            int blackCount = 0;
            int whiteCount = 0;
            for(int x=0; x<width; x++)
            {
                for(int z=0; z<height; z++)
                {
                    if(board[x,z] == 1) blackCount++;
                    else if(board[x,z] == 2) whiteCount++;
                }
            }

            statusText.text = 
                $"Round: {roundCount}\n" +
                $"Blacks: {blackCount}, Whites: {whiteCount}\n" +
                $"Moved This Round: {movedCount}";
        }
    }

    IEnumerator AutoRunCoroutine()
    {
        while(isAutoRunning)
        {
            yield return StartCoroutine(DoOneRoundSequential());
            yield return new WaitForSeconds(autoRoundInterval);
        }
    }
    
    public void OnClickNew()
    {
        isAutoRunning = false;
        StopAllCoroutines();
        if(autoCoroutine != null)
        {
            StopCoroutine(autoCoroutine);
            autoCoroutine = null;
        }
        InitBoard();
        UpdateStatusText(0);
    }

    public void OnClickStart()
    {
        isAutoRunning = !isAutoRunning;
        if(isAutoRunning)
        {
            autoCoroutine = StartCoroutine(AutoRunCoroutine());
        }
        else
        {
            if(autoCoroutine != null)
            {
                StopCoroutine(autoCoroutine);
                autoCoroutine = null;
            }
        }
    }
}


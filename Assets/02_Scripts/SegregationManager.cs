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
                    // occupant
                    // ex) rand < 0.65f => 검정, else 흰색
                    if(rand < 0.65f)
                    {
                        board[x,z] = 1;  // 검정
                        // 프리팹 Instatiate
                        InstantiateAgent(blackSpherePrefab, x, z);
                    }
                    else
                    {
                        board[x,z] = 2;  // 흰색
                        InstantiateAgent(whiteCubePrefab, x, z);
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

    
    // 에이전트 생성 함수: Prefab 생성 후, Animator 초기 파라미터 세팅(Idle/Locomotion 등)
    void InstantiateAgent(GameObject prefab, int x, int z)
    {
        GameObject agent = Instantiate(prefab, new Vector3(x,0.5f,-z), Quaternion.identity);
        agentObjects[x,z] = agent;
    }

    /// <summary>
    /// 순차적으로 불만족자를 한 명씩 이동 & 그때마다 애니메이션
    /// </summary>
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
            
            // 2) 불만족자가 하나도 없으면 -> 이 라운드 종료
            if(unsatisfiedList.Count == 0)
            {
                break; 
            }
            
            // 3) (예시) 첫 번째 불만족자만 이동 (혹은 랜덤 한 명)
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
                // 잠깐 대기(히트 리액션 보여주고 싶다면)
                yield return new WaitForSeconds(0.2f);
                
                Vector2Int? candidate = FindRandomCandidate(color);
                if(candidate.HasValue)
                {
                    moveCount++;
                    Vector2Int c = candidate.Value;

                    // (간단 버전) 즉시 파괴 후 생성, 혹은 Lerp 이동 구현 가능
                    yield return StartCoroutine(MoveAgentInstant(oldX, oldZ, c.x, c.y, 0.2f));
                    
                    SetAgentSatisfied(c.x, c.y, true);
                }
            }

            // 4) 하나 이동 후, 잠깐 대기
            yield return new WaitForSeconds(0.1f);
        }

        // 모든 불만족자가 없어졌으므로 라운드 끝
        UpdateStatusText(moveCount);
    }

    /// <summary>
    /// (간단 버전) 기존 오브젝트 파괴 후 새 위치에 생성. 
    /// (원한다면 Lerp 애니메이션 사용 가능)
    /// </summary>
    IEnumerator MoveAgentInstant(int oldX, int oldZ, int newX, int newZ, float waitTime)
    {
        int color = board[oldX, oldZ];

        // 기존 자리 비움
        board[oldX, oldZ] = 0;
        if(agentObjects[oldX, oldZ] != null)
        {
            Destroy(agentObjects[oldX, oldZ]);
            agentObjects[oldX, oldZ] = null;
        }

        // 새 위치
        board[newX, newZ] = color;
        GameObject prefab = (color == 1) ? blackSpherePrefab : whiteCubePrefab;
        InstantiateAgent(prefab, newX, newZ);
        
        yield return new WaitForSeconds(waitTime);
    }
    
    /// <summary>
    /// 에이전트의 Animator에서 isSatisfied = true/false
    /// </summary>
    void SetAgentSatisfied(int x, int z, bool satisfied)
    {
        GameObject agent = agentObjects[x,z];
        if(agent == null) return;

        Animator anim = agent.GetComponent<Animator>();
        if(anim != null)
        {
            anim.SetBool("isSatisfied", satisfied);
        }
    }
    
    bool IsSatisfied(int x, int z)
    {
        // 먼저 occupant인지 확인
        if(board[x,z] == 0) return true; // 빈 칸이면 스킵 or 만족 취급

        // 에이전트
        GameObject agent = agentObjects[x,z];
        Agent agentComp = agent.GetComponent<Agent>();
        if(agentComp == null) return true; // fallback

        int myColor = agentComp.color; // 1 or 2

        int sameCount = 0;
        int totalNeighbors = 0;

        for(int nx = x-1; nx <= x+1; nx++)
        {
            for(int nz = z-1; nz <= z+1; nz++)
            {
                if(nx == x && nz == z) continue;
                if(nx<0 || nx>=width || nz<0 || nz>=height) continue;

                if(board[nx,nz] == 1) // occupant
                {
                    // 이웃의 agent
                    GameObject neighObj = agentObjects[nx,nz];
                    if(neighObj != null)
                    {
                        Agent neighComp = neighObj.GetComponent<Agent>();
                        if(neighComp != null)
                        {
                            totalNeighbors++;
                            if(neighComp.color == myColor)
                            {
                                sameCount++;
                            }
                        }
                    }
                }
            }
        }

        if(totalNeighbors == 0) return true;

        float ratio = (float)sameCount / totalNeighbors;
        // threshold 등은 blackThreshold, whiteThreshold 없이
        //  if(myColor==1) ...
        //  else ...
        //    or single threshold etc.
        return (ratio >= 0.33f); // etc.
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
                    if(board[nx,nz] == color) sameCount++;
                }
            }
        }
        if(totalNeighbors == 0) return true;

        float ratio = (float)sameCount / totalNeighbors;
        if(color == 1)      return (ratio >= blackThreshold);
        else /* color==2 */ return (ratio >= whiteThreshold);
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


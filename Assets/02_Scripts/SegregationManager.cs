using UnityEngine;
using System.Collections;
using TMPro;
using System.Collections.Generic;

public class SegregationManager : MonoBehaviour
{
    [Header("Grid Size")]
    public int width = 20;
    public int height = 20;

    [Header("Prefabs")]
    public GameObject blackSpherePrefab;
    public GameObject whiteCubePrefab;
    
    [Header("Thresholds")]
    [Range(0f, 1f)] public float blackThreshold = 0.33f;
    [Range(0f, 1f)] public float whiteThreshold = 0.33f;

    [Header("UI")]
    public TMP_Text statusText;
    public float autoRoundInterval = 1f; 
    
    [Header("Ratios")]
    [Range(0f, 1f)] public float blackRatio = 0.35f;
    [Range(0f, 1f)] public float whiteRatio = 0.35f;
    [Range(0f, 1f)] public float emptyRatio = 0.3f;
    public float lowerThreshold = 0.2f; 
    public float upperThreshold = 0.8f; 
    
    public int[,] board;
    private GameObject[,] agentObjects;
    
    public SimpleUILineGraph lineGraph;
    public List<float> segregationHistory = new List<float>();

    private bool isAutoRunning = false;
    private int roundCount = 0;
    private Coroutine autoCoroutine = null;

    void Awake()
    {
        if (board == null)
        {
            InitBoard();
        }
    }
    
    void Start()
    {
        InitBoard();
        UpdateStatusText();
    }

    public void InitBoard()
    {
        // 기존 보드 파괴
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

        // 무작위 배치: 빈칸, 쿠, 큐브
        for(int x = 0; x < width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                float rand = Random.value;
                
                if(rand < emptyRatio)
                {
                    board[x,z] = 0; // 빈칸
                }
                else if(rand < emptyRatio + blackRatio)
                {
                    board[x,z] = 1;  
                    InstantiateAgent(blackSpherePrefab, x, z, 1);
                }
                else
                {
                    board[x,z] = 2;  
                    InstantiateAgent(whiteCubePrefab, x, z, 2);
                }
            }
        }
        
        // 초기 만족도 설정
        for (int x = 0; x < width; x++) {
            for (int z = 0; z < height; z++) {
                if (board[x, z] != 0) {
                    var st = EvaluateSatisfactionState(x, z);
                    SetAgentSatisfactionState(x, z, st);
                }
            }
        }
        // 보드 생성 직후 분리도 기록 + 그래프 초기화
        segregationHistory.Clear();
        float initialRate = CalculateSegregationRate();
        segregationHistory.Add(initialRate);
        if(lineGraph != null)
            lineGraph.UpdateGraph(segregationHistory);
    }
    
    void InstantiateAgent(GameObject prefab, int x, int z, int color)
    {
        GameObject agent = Instantiate(prefab, new Vector3(x,0.5f,-z), Quaternion.identity);
        agentObjects[x,z] = agent;
        
        Agent agentComp = agent.GetComponent<Agent>();
        if(agentComp != null)
        {
            agentComp.color = color;
        }
    }
    
    IEnumerator DoOneRoundAll()
    {
        roundCount++;
        int moveCountInThisRound = 0;
        
        // 불만족자 수집
        List<(int x,int z)> unsatisfiedList = CollectUnsatisfiedList();
        if(unsatisfiedList.Count==0)
        {
            // 불만족자가 없으면 바로 종료
            float segRate = CalculateSegregationRate();
            segregationHistory.Add(segRate);
            
            if(lineGraph != null)
                lineGraph.UpdateGraph(segregationHistory);
            
            Debug.Log($"Round {roundCount}, No unsatisfied. SegRate={segRate:F3}");
            yield break;
        }
        
        bool anyMoved = false;
        
        // 각 불만족 에이전트 순회
        foreach(var (oldX, oldZ) in unsatisfiedList)
        {
            // 이미 이동되었거나 제거된 경우 스킵
            if(board[oldX,oldZ]==0) continue;
            
            // 불만족 표시
            SetAgentUnSatisfied(oldX, oldZ);
            yield return new WaitForSeconds(0.05f);

            bool success = false;
            // 이동 시도
            yield return StartCoroutine(TryMoveOne(oldX, oldZ, (didMove)=>{
                success = didMove;
            }));

            if(success)
            {
                anyMoved=true;
                moveCountInThisRound++;
            }
            
            // 에이전트 한 명 이동(성공 혹은 실패) 후, 실시간 분리도 계산 + 그래프 갱신
            float segRateMid = CalculateSegregationRate();
            segregationHistory.Add(segRateMid);

            if(lineGraph != null)
                lineGraph.UpdateGraph(segregationHistory);

            // 약간의 대기
            yield return new WaitForSeconds(0.05f);
        }
        
        // 라운드 정보 갱신
        UpdateStatusText(moveCountInThisRound);
        
        // 만약 아무도 이동 안 했다면 => 안정 상태로 간주
        if(!anyMoved)
        {
            float segRate = CalculateSegregationRate();
            segregationHistory.Add(segRate);
            
            if(lineGraph != null)
                lineGraph.UpdateGraph(segregationHistory);
            
            Debug.Log($"After Round {roundCount}, SegregationRate={segRate:F3}");
            yield break;
        }
        
        // 일부 이동이 발생했을 때, 라운드가 끝난 시점에 최종 분리도 한번 더 계산
        float segRate2 = CalculateSegregationRate();
        segregationHistory.Add(segRate2);
        
        if(lineGraph != null)
            lineGraph.UpdateGraph(segregationHistory);
        
        Debug.Log($"After Round {roundCount} (some moved), SegregationRate={segRate2:F3}");
    }
    
    List<(int x, int z)> CollectUnsatisfiedList()
    {
        List<(int x, int z)> results = new List<(int, int)>();
        for(int x=0; x<width; x++)
        {
            for(int z=0; z<height; z++)
            {
                if(board[x,z] == 0) continue;
                if(!IsSatisfied(x,z))
                {
                    results.Add((x,z));
                }
            }
        }
        return results;
    }
    
    IEnumerator TryMoveOne(int oldX, int oldZ, System.Action<bool> onFinish)
    {
        int color = board[oldX, oldZ];
        Vector2Int? candidate = FindRandomCandidate(color);
        if(!candidate.HasValue)
        {
            // 이동 실패
            onFinish?.Invoke(false);
            yield break;
        }
        // 이동 후보가 있으면 => 이동 코루틴
        Vector2Int c=candidate.Value;
        yield return StartCoroutine(MoveAgentAndRecalc(oldX, oldZ, c.x, c.y, 0.05f));
        onFinish?.Invoke(true);
    }
    
    IEnumerator MoveAgentAndRecalc(int oldX, int oldZ, int newX, int newZ, float lerpDuration)
    {
        // 안정성 체크
        if (board == null) yield break;
        if (agentObjects[oldX, oldZ] == null) yield break;
        if (board[oldX, oldZ] == 0) yield break;

        GameObject ag = agentObjects[oldX, oldZ];
        if (ag == null) yield break;
        
        // 이동로직 진행
        int color = board[oldX, oldZ];
        board[oldX, oldZ] = 0;
        board[newX, newZ] = color;
        agentObjects[oldX, oldZ] = null;
        agentObjects[newX, newZ] = ag;
        
        Vector3 startPos = ag.transform.position;
        Vector3 endPos = new Vector3(newX, 0.5f, -newZ);

        float elapsed=0f;
        while(elapsed < lerpDuration)
        {
            elapsed+=Time.deltaTime;
            float t=Mathf.Clamp01(elapsed/lerpDuration);
            ag.transform.position = Vector3.Lerp(startPos,endPos,t);
            yield return null;
        }
        ag.transform.position = endPos;

        // 이동 후 만족도 재계산
        var newState = EvaluateSatisfactionState(newX,newZ);
        SetAgentSatisfactionState(newX,newZ, newState);
    }
    
    // 보드 전체 에이전트 재계산
    public void RecalcAllAgentsState()
    {
        if (board == null)
        {
            Debug.LogWarning("board가 아직 생성되지 않았습니다. InitBoard() 이후에 호출하세요.");
            return;
        }
        
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (board[x, z] != 0)
                {
                    var st = EvaluateSatisfactionState(x, z);
                    SetAgentSatisfactionState(x, z, st);
                }
            }
        }
        
        UpdateStatusText();
    }

    void SetAgentUnSatisfied(int x,int z)
    {
        GameObject ag=agentObjects[x,z];
        if(!ag) return;
        Agent agentComp=ag.GetComponent<Agent>();
        if(agentComp)
        {
            agentComp.currentState=Agent.SatisfactionState.UnSatisfied;
        }
    }
    
    void SetAgentSatisfactionState(int x,int z,Agent.SatisfactionState st)
    {
        GameObject ag=agentObjects[x,z];
        if(!ag) return;
        Agent agentComp=ag.GetComponent<Agent>();
        if(agentComp)
        {
            agentComp.SetState(st);
        }
    }
    
    Agent.SatisfactionState EvaluateSatisfactionState(int x,int z)
    {
        if(board[x,z]==0) return Agent.SatisfactionState.Satisfied; 
        int c=board[x,z];
        int sameCount=0, totalNeighbors=0;
        
        for(int nx=x-1; nx<=x+1; nx++)
        {
            for(int nz=z-1; nz<=z+1; nz++)
            {
                if(nx==x && nz==z) continue;
                if(nx<0||nx>=width||nz<0||nz>=height) continue;
                if(board[nx,nz]!=0)
                {
                    totalNeighbors++;
                    if(board[nx,nz]==c) sameCount++;
                }
            }
        }
        if(totalNeighbors==0)
        {
            // 이웃없음 => Meh
            return Agent.SatisfactionState.Meh;
        }
        float ratio=(float)sameCount/totalNeighbors;
        float threshold=(c==1)?blackThreshold:whiteThreshold;
        
        // lowerThreshold ~ upperThreshold 범위 밖이면 불만족
        if(ratio < lowerThreshold || ratio > upperThreshold)
            return Agent.SatisfactionState.UnSatisfied;
        else
            return Agent.SatisfactionState.Satisfied;
    }
    
    bool IsSatisfiedIf(int color,int x,int z)
    {
        // 이동 후보지 찾을 때 쓰이는 로직
        int sameCount=0, totalNeighbors=0;
        for(int nx=x-1; nx<=x+1; nx++)
        {
            for(int nz=z-1; nz<=z+1; nz++)
            {
                if(nx==x && nz==z) continue;
                if(nx<0||nx>=width||nz<0||nz>=height) continue;
                if(board[nx,nz]!=0)
                {
                    totalNeighbors++;
                    if(board[nx,nz]==color) sameCount++;
                }
            }
        }
        if(totalNeighbors==0) return true; // 주변 없으면 임시로 만족
        float ratio=(float)sameCount/totalNeighbors;
        float threshold=(color==1)?blackThreshold:whiteThreshold;
        return (ratio>=threshold);
    }
    
    Vector2Int? FindRandomCandidate(int color)
    {
        // 빈칸 중에서 "이 위치에 가면 만족인가?"를 체크
        List<Vector2Int> cands= new List<Vector2Int>();
        for(int x=0; x<width; x++)
        {
            for(int z=0; z<height; z++)
            {
                if(board[x,z]==0)
                {
                    if(IsSatisfiedIf(color,x,z))
                    {
                        cands.Add(new Vector2Int(x,z));
                    }
                }
            }
        }
        if(cands.Count>0)
        {
            return cands[Random.Range(0,cands.Count)];
        }
        return null;
    }
    
    bool IsSatisfied(int x,int z)
    {
        // CollectUnsatisfiedList() 등에서 간단히 만족 여부 판단
        int c=board[x,z];
        int sameCount=0, totalNeighbors=0;
        for(int nx=x-1; nx<=x+1; nx++)
        {
            for(int nz=z-1; nz<=z+1; nz++)
            {
                if(nx==x && nz==z) continue;
                if(nx<0||nx>=width||nz<0||nz>=height) continue;
                if(board[nx,nz]!=0)
                {
                    totalNeighbors++;
                    if(board[nx,nz]==c) sameCount++;
                }
            }
        }
        if(totalNeighbors==0) return true; // 이웃없음 => 만족
        float ratio=(float)sameCount/totalNeighbors;
        float threshold=(c==1)?blackThreshold:whiteThreshold;
        return (ratio>=threshold);
    }

    // SegregationRate 계산(1에 가까울수록 분리)
    public float CalculateSegregationRate()
    {
        int agentCount = 0;
        float sumRatio = 0f;

        for(int x=0; x<width; x++)
        {
            for(int z=0; z<height; z++)
            {
                if (board[x, z] != 0)
                {
                    // 색깔(노랑 = 1, 하양=2)
                    int myColor = board[x,z];
                    
                    // 이웃 계산
                    int sameCount = 0;
                    int totalN=0;

                    for (int nx = x - 1; nx <= x + 1; nx++)
                    {
                        for (int nz = z - 1; nz <= z + 1; nz++)
                        {
                            if (nx == x && nz == z) continue; // 자기 자신 제외
                            if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;

                            // occupant가 있는 경우
                            if (board[nx, nz] != 0)
                            {
                                totalN++;
                                if (board[nx, nz] == myColor)
                                {
                                    sameCount++;
                                }
                            }
                        }
                    }

                    // 이웃이 전혀 없는 경우(=0)라면 임시로 sameness=1 정도로 처리
                    float ratio = (totalN == 0) ? 1f : (float)sameCount / totalN;

                    sumRatio += ratio; 
                    agentCount++;
                }
            }
        }
            
        // 에이전트가 전혀 없으면 0 리턴
        if(agentCount == 0) return 0f;
            
        // 먼저 전체 average_sameness 계산
        float averageSameness = sumRatio / agentCount;

        // 니키 케이스 스타일의 분리도 산출
        float segregation = (averageSameness - 0.5f) * 2f;
        
        // 니키 케이스 예제에서는 음수면 0% 정도로 취급.
        if(segregation < 0f) segregation = 0f;

        // 1.0 넘어갈 수 있으나, 그 자체로 "강한 분리"로 보면 됨
        return segregation;
    } 
        
    private void UpdateStatusText(int movedCount=0)
    {
        if(statusText!=null)
        {
            int blackCount=0,whiteCount=0;
            for(int x=0; x<width; x++)
            {
                for(int z=0; z<height; z++)
                {
                    if(board[x,z]==1) blackCount++;
                    else if(board[x,z]==2) whiteCount++;
                }
            }
            statusText.text=
                $"Round: {roundCount}\n"+
                $"Blacks: {blackCount}, Whites: {whiteCount}\n"+
                $"Moved This Round: {movedCount}";
        }
    }

    private bool IsDone()
    {
        // 불만족자가 하나도 없으면 시뮬레이션 종료
        return (CollectUnsatisfiedList().Count == 0);
    }
    
    IEnumerator AutoRunCoroutine()
    {
        while(isAutoRunning)
        {
            yield return StartCoroutine(DoOneRoundAll());
            
            // 분리도 계산하고 그래프 갱신
            float segRate = CalculateSegregationRate();
            segregationHistory.Add(segRate);
            lineGraph.UpdateGraph(segregationHistory);

            // 라운드 끝났는데 만약 불만족자도 없고 완전히 끝나면 -> break
            if(IsDone())
            {
                isAutoRunning = false;
                break;
            }

            // 다음 라운드까지 대기
            yield return new WaitForSeconds(autoRoundInterval);
        }
        // 여기까지 오면 AutoRunCoroutine이 종료됨 => 그래프는 더 이상 업데이트 안 됨
    }

    public void OnClickNew()
    {
        isAutoRunning=false;
        StopAllCoroutines();
        if(autoCoroutine!=null)
        {
            StopCoroutine(autoCoroutine);
            autoCoroutine=null;
        }
        InitBoard();
        UpdateStatusText(0);
    }

    public void OnClickStart()
    {
        isAutoRunning=!isAutoRunning;
        if(isAutoRunning)
        {
            autoCoroutine=StartCoroutine(AutoRunCoroutine());
        }
        else
        {
            if(autoCoroutine!=null)
            {
                StopCoroutine(autoCoroutine);
                autoCoroutine=null;
            }
        }
    }
    
    // 마우스로 수동 이동 시
    public void OnAgentManualMove(Agent agent, int oldX, int oldZ, int newX, int newZ)
    {
        // 보드 갱신
        board[oldX, oldZ] = 0;
        agentObjects[oldX, oldZ] = null;

        int color = agent.color;
        board[newX, newZ] = color;
        agentObjects[newX, newZ] = agent.gameObject;

        // 위치 스냅
        agent.transform.position = new Vector3(newX, 0.5f, -newZ);

        // 이동 후 재계산
        var newState = EvaluateSatisfactionState(newX, newZ);
        agent.SetState(newState);
        
        // 주변 이웃도 업데이트
        for (int nx = newX - 1; nx <= newX + 1; nx++)
        {
            for (int nz = newZ - 1; nz <= newZ + 1; nz++)
            {
                if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
                if (board[nx, nz] != 0)
                {
                    var neighborObj = agentObjects[nx, nz];
                    if (neighborObj != null)
                    {
                        Agent neighborAgent = neighborObj.GetComponent<Agent>();
                        if (neighborAgent != null)
                        {
                            var st = EvaluateSatisfactionState(nx, nz);
                            neighborAgent.SetState(st);
                        }
                    }
                }
            }
        }
        
        UpdateStatusText();
        
        // 수동 이동 후 분리도도 즉시 측정해서 그래프 갱신
        float segRate = CalculateSegregationRate();
        segregationHistory.Add(segRate);

        if(lineGraph != null)
            lineGraph.UpdateGraph(segregationHistory);
    }
    
    public (int,int) FindAgentPosition(Agent agent)
    {
        for(int x = 0; x < width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                if(agentObjects[x,z] == agent.gameObject)
                {
                    return (x,z);
                }
            }
        }
        return (-1,-1); // 못찾으면
    }
}

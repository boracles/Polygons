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
        // 기존 보드 정리
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

        // 랜덤 초기화
        for(int x = 0; x < width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                float rand = Random.value;
                
                if(rand < emptyRatio)
                {
                    board[x,z] = 0; // 빈칸
                }
                // blackRatio 범위이면 검정(1)
                else if(rand < emptyRatio + blackRatio)
                {
                    board[x,z] = 1;  
                    InstantiateAgent(blackSpherePrefab, x, z, 1);
                }
                // 나머지는 흰색(2)
                else
                {
                    board[x,z] = 2;  
                    InstantiateAgent(whiteCubePrefab, x, z, 2);
                }
            }
        }
        
        // 새로 생성된 모든 에이전트에 대해 초기 SatisfactionState 설정
        for (int x = 0; x < width; x++) {
            for (int z = 0; z < height; z++) {
                if (board[x, z] != 0) {
                    var st = EvaluateSatisfactionState(x, z);
                    SetAgentSatisfactionState(x, z, st);
                }
            }
        }
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
        
        List<(int x,int z)> unsatisfiedList = CollectUnsatisfiedList();
        if(unsatisfiedList.Count==0)
        {
            // 불만족자가 없으면 바로 종료
            yield break;
        }
        
        bool anyMoved = false;
        
        foreach(var (oldX, oldZ) in unsatisfiedList)
        {
            // 혹시 이미 이동된 에이전트(좌표가 0이 됨 등)
            if(board[oldX,oldZ]==0) continue;

            // 불만족 Hit Reaction
            SetAgentUnSatisfied(oldX, oldZ);
            yield return new WaitForSeconds(0.05f);

            bool success = false;
            yield return StartCoroutine(TryMoveOne(oldX, oldZ, (didMove)=>{
                success = didMove;
            }));

            if(success) // 성공적응로 이동
            {
                anyMoved=true;
                moveCountInThisRound++;
            }
            yield return new WaitForSeconds(0.05f);
        }
        
        // 라운드 정보 업데이트 
        UpdateStatusText(moveCountInThisRound);

        // 만약 아무도 이동 안 했다면 => "더 이상 변화가 없음"으로 간주하고 종료
        if(!anyMoved)
        {
            yield break;
        }
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
        // 이동 후보가 있으면 => 이동 코루틴도 기다림
        Vector2Int c=candidate.Value;
        yield return StartCoroutine(MoveAgentAndRecalc(oldX, oldZ, c.x, c.y, 0.05f));
        onFinish?.Invoke(true);
    }
    
    IEnumerator MoveAgentAndRecalc(int oldX, int oldZ, int newX, int newZ, float lerpDuration)
    {
        // 안정성 체크
        if (board == null) yield break;
        if (agentObjects[oldX, oldZ] == null) yield break;
        
        // 해당 자리가 이미 비었는지(=이동/삭제되었는지) 확인
        if (board[oldX, oldZ] == 0) yield break;

        GameObject ag = agentObjects[oldX, oldZ];
        if (ag == null) yield break; // 이미 Destroy 되었다면 중단
        
        // 이동로직 진행
        int color = board[oldX, oldZ];

        // 보드, agentObjects 갱신
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
    
    // 현재 보드상의 모든 에이전트에 대해 SatisfactionState를 다시 계산하여 갱신
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
        if (ratio < lowerThreshold || ratio > upperThreshold)
        {
            // 불만족
            return Agent.SatisfactionState.UnSatisfied;
        }
        else
        {
            // 만족
            return Agent.SatisfactionState.Satisfied;
        }
    }
    
    bool IsSatisfiedIf(int color,int x,int z)
    {
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
        if(totalNeighbors==0) return true; // Meh => 이동 가능
        float ratio=(float)sameCount/totalNeighbors;
        float threshold=(color==1)?blackThreshold:whiteThreshold;
        return (ratio>=threshold);
    }
    
    Vector2Int? FindRandomCandidate(int color)
    {
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

    IEnumerator AutoRunCoroutine()
    {
        while(isAutoRunning)
        {
            // 한 라운드 실행
            yield return StartCoroutine(DoOneRoundAll());
            yield return new WaitForSeconds(autoRoundInterval);
        }
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
    
    // 마우스로 수동으로 에이전트가 옮겨진 경우에 호출
    public void OnAgentManualMove(Agent agent, int oldX, int oldZ, int newX, int newZ)
    {
        // 보드 및 agentObjects에서 옮기기 전/후 자리 업데이트
        board[oldX, oldZ] = 0;
        agentObjects[oldX, oldZ] = null;

        int color = agent.color;  // 1=검정, 2=흰색
        board[newX, newZ] = color;
        agentObjects[newX, newZ] = agent.gameObject;

        // 에이전트 실제 transform 위치도 스냅
        Vector3 newWorldPos = new Vector3(newX, 0.5f, -newZ);
        agent.transform.position = newWorldPos;

        // 재계산: 이 에이전트만 새 위치에서 만족도 다시 평가
        var newState = EvaluateSatisfactionState(newX, newZ);
        agent.SetState(newState);
        
        // 이웃들 좌표 범위 [newX-1..newX+1], [newZ-1..newZ+1] 등
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

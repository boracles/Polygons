using UnityEngine;
using System.Collections;
using UnityEngine.UI; 
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
    
    private int[,] board;   // 0=빈칸, 1=황, 2=백
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
                if(rand < 0.3f)
                {
                    board[x,z] = 0; // 빈칸
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
        
        // 모든 위치(x,z)에 대해
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

        // 불만족자 목록
        List<(int x,int z)> unsatisfiedList = CollectUnsatisfiedList();
        if(unsatisfiedList.Count==0)
        {
            // 불만족자가 없으면 바로 종료
            yield break;
        }
        
        bool anyMoved = false;

        // unsatisfiedList의 모든 에이전트 순회 (순차든 랜덤이든)
        foreach(var (oldX, oldZ) in unsatisfiedList)
        {
            // 혹시 이미 이동된 에이전트(좌표가 0이 됨 등)
            if(board[oldX,oldZ]==0) continue;

            // 불만족 Hit Reaction
            SetAgentUnSatisfied(oldX, oldZ);
            yield return new WaitForSeconds(0.2f);

            bool success = false;
            yield return StartCoroutine(TryMoveOne(oldX, oldZ, (didMove)=>{
                success = didMove;
            }));

            if(success)
            {
                anyMoved=true;
                moveCountInThisRound++;
            }
            yield return new WaitForSeconds(0.1f);
        }
        
        UpdateStatusText(moveCountInThisRound);

        // 만약 아무도 이동 안 했다면 => 안정 상태로 간주하고 종료
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
        yield return StartCoroutine(MoveAgentAndRecalc(oldX, oldZ, c.x, c.y, 0.2f));
        onFinish?.Invoke(true);
    }
    
    IEnumerator MoveAgentAndRecalc(int oldX, int oldZ, int newX, int newZ, float lerpDuration)
    {
        int color = board[oldX, oldZ];
        GameObject ag = agentObjects[oldX, oldZ];
        if(ag == null) yield break;

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

    void SetAgentUnSatisfied(int x,int z)
    {
        GameObject ag=agentObjects[x,z];
        if(!ag) return;
        Agent agentComp=ag.GetComponent<Agent>();
        if(agentComp)
        {
            agentComp.currentState=Agent.SatisfactionState.UnSatisfied;
            // agentComp.UpdateAnimator();
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
        if(ratio<threshold)
        {
            return Agent.SatisfactionState.UnSatisfied;
        }
        else if(ratio>=1.0f)
        {
            return Agent.SatisfactionState.Meh;
        }
        else
        {
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
}

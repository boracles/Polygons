using UnityEngine;
using System.Collections.Generic;

public class SegregationManager : MonoBehaviour
{
    public int width = 20;
    public int height = 20;

    // 에이전트 식별
    // 0 = 빈칸, 1 = 검정 구, 2 = 흰색 큐브
    private int[,] board;

    // Prefabs
    public GameObject blackSpherePrefab;
    public GameObject whiteCubePrefab;

    // 만족 임계치 (예: 0.5f -> 50%)
    [Range(0f, 1f)] public float blackThreshold = 0.5f;
    [Range(0f, 1f)] public float whiteThreshold = 0.5f;

    // 3D 상 에이전트 오브젝트를 보관 (board와 싱크 맞추기)
    private GameObject[,] agentObjects;

    void Start()
    {
        board = new int[width, height];
        agentObjects = new GameObject[width, height];

        // 초기 랜덤 배치
        for(int x = 0; x < width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                float rand = Random.value;
                if(rand < 0.3f) // 30% 확률로 비움
                {
                    board[x,z] = 0;
                }
                else if(rand < 0.65f) // 예: 35% black
                {
                    board[x,z] = 1; // black
                    agentObjects[x,z] = Instantiate(blackSpherePrefab, 
                        new Vector3(x, 0.5f, -z), Quaternion.identity);
                }
                else // 나머지 35% white
                {
                    board[x,z] = 2; // white
                    agentObjects[x,z] = Instantiate(whiteCubePrefab, 
                        new Vector3(x, 0.5f, -z), Quaternion.identity);
                }
            }
        }
    }

    // 라운드를 수동으로 한 번씩 돌리는 경우
    public void DoOneRound()
    {
        List<(int x,int z)> unsatisfiedList = new List<(int,int)>();

        // 1) 불만족 에이전트 찾기
        for(int x = 0; x < width; x++)
        {
            for(int z = 0; z < height; z++)
            {
                if(board[x,z] == 0) continue; // 빈칸은 패스
                if(!IsSatisfied(x,z)) 
                {
                    unsatisfiedList.Add((x,z));
                }
            }
        }

        // 2) 후보 위치 찾아서 이동
        foreach(var (x,z) in unsatisfiedList)
        {
            int color = board[x,z];
            // 후보 위치 중 하나 찾기(예: 무작위)
            Vector2Int? candidate = FindCandidate(color);
            if(candidate.HasValue)
            {
                // 이동
                Vector2Int c = candidate.Value;
                // 기존 위치 비움
                board[x,z] = 0;
                Destroy(agentObjects[x,z]);
                agentObjects[x,z] = null;

                // 새 위치로 설정
                board[c.x, c.y] = color;
                if(color == 1)
                {
                    agentObjects[c.x,c.y] = Instantiate(blackSpherePrefab,
                        new Vector3(c.x, 0.5f, -c.y), Quaternion.identity);
                }
                else
                {
                    agentObjects[c.x,c.y] = Instantiate(whiteCubePrefab,
                        new Vector3(c.x, 0.5f, -c.y), Quaternion.identity);
                }
            }
        }
    }

    bool IsSatisfied(int x, int z)
    {
        int color = board[x,z];
        // 이웃 탐색
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
        if(totalNeighbors == 0) return true; // 이웃이 없으면 만족한다고 치자

        float ratio = (float)sameCount / totalNeighbors;
        if(color == 1) // black
        {
            return (ratio >= blackThreshold);
        }
        else // white
        {
            return (ratio >= whiteThreshold);
        }
    }

    Vector2Int? FindCandidate(int color)
    {
        // 매우 단순한 방식: 모든 빈칸 중 무작위로 골라서, 만족하면 그 자리 반환
        // (더 정교하게: 가장 가까운, 가장 만족도 높은, etc.)
        List<Vector2Int> empties = new List<Vector2Int>();
        for(int x=0; x<width; x++)
        {
            for(int z=0; z<height; z++)
            {
                if(board[x,z] == 0) // 빈칸
                {
                    // 임시로 color 배치 가정 후 만족도 체크
                    if(IsSatisfiedIf(color, x, z))
                    {
                        empties.Add(new Vector2Int(x,z));
                    }
                }
            }
        }
        if(empties.Count > 0)
        {
            return empties[Random.Range(0, empties.Count)];
        }
        else
        {
            return null; // 이동 불가
        }
    }

    bool IsSatisfiedIf(int color, int x, int z)
    {
        // (x,z)에 color가 들어간다고 가정했을 때 만족도 판단
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
        if(color == 1) // black
        {
            return (ratio >= blackThreshold);
        }
        else // white
        {
            return (ratio >= whiteThreshold);
        }
    }
}


using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 블록을 대표하는 ‘공간 단위’
/// 내부 편견·밀집도를 계산해 닫힘 여부 결정
/// </summary>
public class Zone
{
    /* ---------- 기본 속성 ---------- */
    public readonly int id;                 // 0 ~ 24 등
    public bool IsClosed { get; private set; }

    /* 멤버 관리 */
    private readonly List<Agent> members = new();   // 이 존에 속한 에이전트 목록

    /* ---------- 폐쇄 판단용 캐시 ---------- */
    private float avgBiasMain;      // 메인(다수) 구성원의 평균 편견
    private float targetDensity;    // 타깃 인구 밀도(존 전체 칸 대비)

    /* 기준값 */
    private readonly float biasCloseThreshold;
    private readonly float densityCloseThreshold;   // e.g., 0.05
    private readonly int   zoneCellCount;           // 이 존이 차지하는 셀 수 (보통 16)

    /* ---------- 생성자 ---------- */
    public Zone(int id, int zoneCellCount,
                float biasThreshold = 0.6f,
                float densityThreshold = 0.05f)
    {
        this.id                  = id;
        this.zoneCellCount       = zoneCellCount;
        biasCloseThreshold       = biasThreshold;
        densityCloseThreshold    = densityThreshold;
        IsClosed                 = false;
    }

    /* ---------- 멤버 조작 ---------- */
    public void ClearMembers()          => members.Clear();
    public void AddMember(Agent a)      => members.Add(a);
    public void RemoveMember(Agent a)   => members.Remove(a);

    /* ---------- 통계 갱신 ---------- */
    public void UpdateStats()
    {
        if (members.Count == 0)
        {
            avgBiasMain   = 0f;
            targetDensity = 0f;
            return;
        }

        float biasSum   = 0f;
        int   mainCount = 0;
        int   targetCnt = 0;

        foreach (var a in members)
        {
            if (a.label == Agent.Label.Main)
            {
                biasSum   += a.bias;
                mainCount ++;
            }
            else if (a.label == Agent.Label.Target)
            {
                targetCnt ++;
            }
        }

        avgBiasMain   = mainCount > 0 ? biasSum / mainCount : 0f;
        targetDensity = (float)targetCnt / zoneCellCount;
    }

    /* 폐쇄 */
    /// <summary>
    /// 조건을 만족하면 존을 '닫힘' 상태로 전환
    /// 반환: true = 이번 호출에서 새로 닫힘 발생
    /// </summary>
    public bool TryClose()
    {
        if (IsClosed) return false;  // 이미 닫힌 경우

        bool shouldClose = (avgBiasMain   > biasCloseThreshold) &&
                           (targetDensity > densityCloseThreshold);

        if (shouldClose)
        {
            IsClosed = true;
            return true;
        }
        return false;
    }
}

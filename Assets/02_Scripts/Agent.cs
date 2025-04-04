using UnityEngine;

public class Agent : MonoBehaviour
{
    public enum SatisfactionState
    {
        UnSatisfied = 0,
        Meh = 1,
        Satisfied = 2
    }

    [Header("Schelling State")]
    public SatisfactionState currentState = SatisfactionState.Satisfied;

    // 1=검정, 2=흰색
    public int color;

    /// <summary>
    /// 이웃 비율(ratio)에 따라 본인의 state를 결정
    /// </summary>
    public void SetStateByRatio(float ratio)
    {
        // 원하는 기준에 맞게 수정 가능
        // 예: 0.33 미만이면 UnSatisfied, 1.0 이상이면 Meh, 나머지는 Satisfied
        if(ratio < 0.33f)
        {
            currentState = SatisfactionState.UnSatisfied;
        }
        else if(ratio >= 1.0f)
        {
            currentState = SatisfactionState.Meh;
        }
        else
        {
            currentState = SatisfactionState.Satisfied;
        }
        UpdateAnimator();
    }

    /// <summary>
    /// 외부에서 직접 SatisfactionState를 지정할 때
    /// </summary>
    public void SetState(SatisfactionState newState)
    {
        currentState = newState;
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if(anim == null) return;

        // Animator에는 int 파라미터 "SatisfactionState"를 만든다고 가정
        anim.SetInteger("SatisfactionState", (int)currentState);
    }
}
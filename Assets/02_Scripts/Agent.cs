using UnityEngine;

public class Agent : MonoBehaviour
{
    public enum SatisfactionState
    {
        UnSatisfied = 0,
        Meh = 1,
        Satisfied = 2
    }
    
    public enum Label 
    {
        Main,   // Adult-only
        Target  // Caregiver + Child (배제 대상)
    } 
    public Label label = Label.Main;    // Main이 기본값
    public float bias;                  // 0-1, 편견 강도

    [Header("분리 상태")]
    public SatisfactionState currentState = SatisfactionState.Satisfied;

    // 1=검정, 2=흰색
    public int color;
    
    // 이웃 ratio에 따라 본인의 state를 결정
    public void SetStateByRatio(float ratio)
    {
        // 0.33 미만이면 UnSatisfied, 1.0 이상이면 Meh, 나머지는 Satisfied
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
    
    // 외부에서 직접 SatisfactionState를 지정할 때
    public void SetState(SatisfactionState newState)
    {
        currentState = newState;
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if(anim == null) return;
        
        anim.SetInteger("SatisfactionState", (int)currentState);
    }
}
using UnityEngine;

public class Agent : MonoBehaviour
{
    public enum SatisfactionState
    {
        Satisfied,
        UnSatisfied,
        Meh
    }

    [Header("Schelling State")]
    public SatisfactionState currentState = SatisfactionState.Satisfied;
    
    public int color;

    public void SetStateByRatio(float ratio)
    {
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

    private void UpdateAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if(anim == null) return;
        
        bool isSatisfied = (currentState == SatisfactionState.Satisfied ||currentState == SatisfactionState.Meh );
        anim.SetBool("isSatisfied", isSatisfied);
    }
}
using UnityEngine;

public class Agent : MonoBehaviour
{
    public enum SatisfactionState
    {
        UnSatisfied = 0,
        Meh = 1,
        Satisfied = 2
    }
    
    /* 위치 상태 */
    public enum PlaceState { Road, Room }
    public PlaceState place = PlaceState.Road;
    
    int currentRoom = -1;   // 지금 점유한 방ID (-1 = 없음)
    
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

    void Update()
    {
        // ThresholdLandscape 씬이 아니면 매니저가 없으므로 즉시 리턴
        if (ThresholdLandscapeManager.I == null) return;
        
        // 매 프레임 위치 판정 및 Occupy/ Vacate 처리
        ThresholdLandscapeManager.I.UpdateOccupancy(gameObject, currentRoom, out currentRoom);

        // place 값 갱신
        PlaceState newPlace = currentRoom >= 0 ? PlaceState.Room : PlaceState.Road;
        if (newPlace != place)
        {
            place = newPlace;
            UpdateAnimator();   // 필요시 애니·색상·UI 등 갱신
        }
    }
    
    void UpdateAnimator()
    {
        Animator anim = GetComponent<Animator>();
        if (!anim) return;
        anim.SetBool("IsInRoom", place == PlaceState.Room);
        anim.SetInteger("SatisfactionState", (int)currentState);
    }
}
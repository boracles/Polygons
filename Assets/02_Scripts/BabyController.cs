using UnityEngine;

public class BabyController : MonoBehaviour
{
    public void MoveToStroller(Transform strollerSeat)
    {
        transform.SetParent(strollerSeat);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Debug.Log("아기가 유모차에 탑승했습니다.");
        // 여기에 애니메이션 / 사운드 추가 가능
    }
}
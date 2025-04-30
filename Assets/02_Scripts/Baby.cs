using UnityEngine;
using System.Collections;
public class Baby : MonoBehaviour
{
    public enum Label 
    {
        Main,   // Adult-only
        Target  // Caregiver + Child (배제 대상)
    } 
    public Label label = Label.Target;  
    
    [Header("Audio")]
    [SerializeField] AudioSource audioSource;   // Baby 오브젝트에 AudioSource
    [SerializeField] AudioClip   cryClip;       // 2초짜리 울음 파일

    [Header("Gap Between Cries (sec)")]
    [SerializeField] Vector2 gapRange = new(2f, 4f);  // 침묵 구간 랜덤

    Coroutine cryCR;
    float clipLen;
    bool isCrying = true; 

    public bool IsCrying() => isCrying;
    
    void Awake()
    {
        audioSource ??= GetComponent<AudioSource>();
        clipLen = cryClip.length;
    }
    
    public void OnRoomStatusChanged(bool inRoom)
    {
        if (inRoom)
        {
            if (cryCR == null) cryCR = StartCoroutine(CryLoop());
        }
        else
        {
            if (cryCR != null) StopCoroutine(cryCR);
            cryCR = null;
            audioSource.Stop();
            isCrying = false;
        }
    }
    
    IEnumerator CryLoop()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 2f));

        while (true)
        {
            isCrying = true;
            audioSource.PlayOneShot(cryClip);
            yield return new WaitForSeconds(cryClip.length);

            isCrying = false;
            float gap = Random.Range(gapRange.x, gapRange.y);
            yield return new WaitForSeconds(gap);
        }
    }
}

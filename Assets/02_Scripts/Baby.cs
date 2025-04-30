using UnityEngine;
using System.Collections;
public class Baby : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] AudioSource audioSource;   // Baby 오브젝트에 AudioSource
    [SerializeField] AudioClip   cryClip;       // 2초짜리 울음 파일

    [Header("Gap Between Cries (sec)")]
    [SerializeField] Vector2 gapRange = new(2f, 4f);  // 침묵 구간 랜덤

    Coroutine cryCR;
    float clipLen;

    public bool IsCrying { get; private set; }  
    
    void Awake()
    {
        audioSource ??= GetComponent<AudioSource>();
        clipLen = cryClip.length;
    }

    /* ───────── Agent → Baby 신호 ───────── */
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
        }
    }

    /* ───────── 간헐적 울음 ───────── */
    IEnumerator CryLoop()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 2f));

        while (true)
        {
            IsCrying = true;                    // ← 시작
            audioSource.PlayOneShot(cryClip);
            yield return new WaitForSeconds(cryClip.length);
            IsCrying = false;                   // ← 끝

            float gap = Random.Range(gapRange.x, gapRange.y);
            yield return new WaitForSeconds(gap);
        }
    }
}

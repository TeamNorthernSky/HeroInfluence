using TMPro;
using UnityEngine;

/// <summary>
/// 테스트베드 전용. Animator를 지정 스테이트에 고정한다 —
/// 컨트롤러 트랜지션이 다른 스테이트로 끌고 가면 되돌리고,
/// 비루프 클립은 끝나는 즉시 처음부터 재생해 루프처럼 관찰할 수 있게 한다.
/// 라벨에는 스테이트명과 실제 재생 중인 클립명을 표시한다.
/// </summary>
public class StateLoopPlayer : MonoBehaviour
{
    private Animator animator;
    private string stateName;
    private int stateHash;
    private TextMeshPro label;
    private Transform labelT;
    private Camera cam;
    private string lastClipShown;

    public void Init(Animator animator, string stateName, TextMeshPro label)
    {
        this.animator = animator;
        this.stateName = stateName;
        this.label = label;
        stateHash = Animator.StringToHash(stateName);
        labelT = label != null ? label.transform : null;
        if (label != null)
            label.text = stateName;
        animator.Play(stateHash, 0, 0f);
    }

    private void Update()
    {
        if (animator == null)
            return;

        var st = animator.GetCurrentAnimatorStateInfo(0);
        bool inTarget = st.shortNameHash == stateHash;

        if (!inTarget)
            animator.Play(stateHash, 0, 0f);
        else if (!st.loop && st.normalizedTime >= 1f)
            animator.Play(stateHash, 0, 0f);

        if (inTarget && label != null)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            string clipName = clips.Length > 0 ? clips[0].clip.name : "(no clip)";
            if (clipName != lastClipShown)
            {
                lastClipShown = clipName;
                label.text = $"{stateName}\n<size=60%>{clipName}</size>";
            }
        }
    }

    private void LateUpdate()
    {
        if (labelT == null)
            return;
        if (cam == null)
            cam = Camera.main;
        if (cam != null)
            labelT.rotation = cam.transform.rotation; // 카메라 정면 빌보드
    }
}

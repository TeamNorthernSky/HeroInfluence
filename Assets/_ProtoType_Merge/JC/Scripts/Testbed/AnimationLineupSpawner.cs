using TMPro;
using UnityEngine;

/// <summary>
/// 테스트베드 전용. 캐릭터 프리팹을 스테이트 수만큼 복제해 그리드로 배치하고,
/// 각 인스턴스가 지정 스테이트를 고정 루프 재생하도록 StateLoopPlayer를 부착한다.
/// 프리팹/스테이트 목록이 인스펙터 필드라 다른 캐릭터·컨트롤러로 재활용 가능.
/// </summary>
public class AnimationLineupSpawner : MonoBehaviour
{
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private string[] stateNames =
    {
        "Idle", "MoveForward",
        "ClassSkill_1", "ClassSkill_2", "ClassSkill_3", "ClassSkill_4",
        "ClassSkill_5", "ClassSkill_6", "ClassSkill_7",
        "WeaponSkill_1", "WeaponSkill_2", "WeaponSkill_3",
        "MoveReturn", "Gaurd", "Hit", "Dead",
    };

    [Header("배치")]
    [SerializeField] private int perRow = 8;
    [SerializeField] private float spacingX = 2f;
    [SerializeField] private float spacingZ = 3f;

    [Header("라벨")]
    [SerializeField] private float labelHeight = 2.2f;
    [SerializeField] private float labelFontSize = 2f;
    [SerializeField] private TMP_FontAsset labelFont;

    private void Start()
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning("[AnimationLineupSpawner] characterPrefab이 비어 있습니다.", this);
            return;
        }

        for (int i = 0; i < stateNames.Length; i++)
        {
            int row = i / Mathf.Max(1, perRow);
            int col = i % Mathf.Max(1, perRow);
            Vector3 pos = transform.position + new Vector3(col * spacingX, 0f, -row * spacingZ);

            GameObject inst = Instantiate(characterPrefab, pos, Quaternion.identity, transform);
            inst.name = $"{characterPrefab.name}_{stateNames[i]}";

            Animator animator = inst.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"[AnimationLineupSpawner] '{inst.name}'에 Animator가 없습니다.", inst);
                continue;
            }
            animator.applyRootMotion = false; // 라인업 고정 배치 유지(원본 프리팹 무수정)

            TextMeshPro label = CreateLabel(inst.transform, stateNames[i]);
            inst.AddComponent<StateLoopPlayer>().Init(animator, stateNames[i], label);
        }
    }

    private TextMeshPro CreateLabel(Transform parent, string text)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, labelHeight, 0f);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = labelFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
        if (labelFont != null)
            tmp.font = labelFont;
        return tmp;
    }
}

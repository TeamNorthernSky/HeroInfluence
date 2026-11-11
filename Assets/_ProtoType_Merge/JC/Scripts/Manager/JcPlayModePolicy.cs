using UnityEngine;

// JC | 2026-11-11 | 일반 에디터 테스트는 고비용 이펙트 예열을 생략하고, 명시적인 검증 모드와 빌드는 기존 준비를 수행합니다.
// 씬 복원·진입 완료·대화 시작 보호 조건은 이 설정으로 건너뛰지 않습니다.
public static class JcPlayModePolicy
{
    public static bool UsePlayerPreparation
    {
        get
        {
#if UNITY_EDITOR
            return EditorValidationEnabled;
#else
            return true;
#endif
        }
    }

#if UNITY_EDITOR
    // JC | 2026-11-11 | PC·프로젝트별 로컬 선택이며, 팀원에게 전달되는 기본 동작은 빠른 일반 테스트입니다.
    private static string PreferenceKey => "JC.PlayValidation.PlayerPreparation:" + Application.dataPath;
    public static bool EditorValidationEnabled
    {
        get => UnityEditor.EditorPrefs.GetBool(PreferenceKey, false);
        set => UnityEditor.EditorPrefs.SetBool(PreferenceKey, value);
    }
#endif
}

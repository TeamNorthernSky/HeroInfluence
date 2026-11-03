using System;
using UnityEngine;

/// <summary>
/// 애니메이션 테스트 씬에서 키를 눌러 카메라를 모델 묶음에 맞춘다(기본: Z=A~C, X=D~F, C=G~H).
/// Play를 시작할 때는 씬에 저장된 카메라 위치(전체 보기) 그대로이고, 키를 누르면 그 묶음 위치로 바로 옮긴다.
/// <para>카메라 오브젝트에 붙인다. 테스트 전용이다.</para>
/// </summary>
public class AnimTestCameraSwitch : MonoBehaviour
{
    [Serializable]
    private struct View
    {
        public KeyCode key;
        public string label;
        public Vector3 position;
        public Vector3 eulerAngles;
    }

    [Tooltip("키별 카메라 위치·회전(월드).")]
    [SerializeField] private View[] views = Array.Empty<View>();

    private string _currentLabel = "All";
    private GUIStyle _labelStyle;

    private void Update()
    {
        foreach (View view in views)
        {
            if (Input.GetKeyDown(view.key)) Apply(view);
        }
    }

    /// <summary>라벨이 같은 위치로 옮긴다(테스트 코드에서 키 대신 부를 때).</summary>
    public void Show(string label)
    {
        foreach (View view in views)
        {
            if (view.label == label) Apply(view);
        }
    }

    private void Apply(View view)
    {
        transform.SetPositionAndRotation(view.position, Quaternion.Euler(view.eulerAngles));
        _currentLabel = view.label;
    }

    // AnimTestSkillFxSet의 스킬 표시 아래 줄에 그린다. 기본 GUI 폰트에 한글이 없을 수 있어 영문으로 표시한다.
    private void OnGUI()
    {
        if (_labelStyle == null) _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        string keys = string.Join(" / ", Array.ConvertAll(views, v => $"{v.key}={v.label}"));
        GUI.Label(new Rect(16f, 44f, 760f, 34f), $"View {_currentLabel}   ({keys})", _labelStyle);
    }
}

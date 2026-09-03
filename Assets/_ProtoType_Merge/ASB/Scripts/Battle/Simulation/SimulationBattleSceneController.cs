using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class SimulationBattleSceneController : MonoBehaviour
{
    private const string SimulationSceneName = "BattleSimulationScene";
    private const int AllySlotCount = 6;

    [SerializeField] private BattleSceneManager battleSceneManager;

    private readonly AllyRowState[] allyRows = new AllyRowState[AllySlotCount];
    private readonly List<DHPlayerUnitTemplate> playerTemplates = new List<DHPlayerUnitTemplate>();
    private readonly List<DHEnemyGroupTemplate> enemyGroups = new List<DHEnemyGroupTemplate>();

    private Vector2 scrollPosition;
    private Rect windowRect;
    private int enemyGroupIndex;
    private string enemyLevelText = "1";
    private string validationMessage = string.Empty;
    private bool catalogReady;
    private bool battleStarted;

    private sealed class AllyRowState
    {
        public bool Enabled;
        public int TemplateIndex;
        public string LevelText = "1";
        public string WeaponKey = string.Empty;
        public string SkillIndexText = "0";
        public string HpRatioText = "1";
    }

    private void Awake()
    {
        for (int i = 0; i < allyRows.Length; i++)
            allyRows[i] = new AllyRowState { Enabled = i == 0 };

        if (battleSceneManager == null)
            battleSceneManager = FindFirstObjectByType<BattleSceneManager>(FindObjectsInactive.Include);

        if (battleSceneManager != null)
            battleSceneManager.enabled = false;

        CombatContext context = CombatContext.Instance;
        if (context != null && context.IsSimulation)
            context.ClearSimulation();
    }

    private void Start()
    {
        if (!string.Equals(SceneManager.GetActiveScene().name, SimulationSceneName, StringComparison.Ordinal))
        {
            enabled = false;
            return;
        }

        LoadCatalogs();
    }

    private void OnGUI()
    {
        if (battleStarted)
            return;

        float width = Mathf.Min(1120f, Screen.width - 32f);
        float height = Mathf.Min(720f, Screen.height - 32f);
        windowRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        GUI.skin.window.fontSize = 22;
        GUI.skin.label.fontSize = 16;
        GUI.skin.button.fontSize = 15;
        GUI.skin.textField.fontSize = 15;
        GUI.skin.toggle.fontSize = 15;
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "모의 전투 설정");
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Space(8f);
        if (!catalogReady)
        {
            GUILayout.Label("전투 템플릿을 불러오는 중입니다...");
            if (GUILayout.Button("다시 불러오기", GUILayout.Height(38f)))
                LoadCatalogs();
            return;
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label("아군 설정 (UI 순서가 전투 배치 순서입니다)");
        GUILayout.Space(4f);
        DrawAllyHeader();
        for (int i = 0; i < allyRows.Length; i++)
            DrawAllyRow(i, allyRows[i]);

        GUILayout.Space(18f);
        GUILayout.Label("적 설정");
        GUILayout.BeginHorizontal(GUI.skin.box);
        GUILayout.Label("적 그룹", GUILayout.Width(90f));
        if (GUILayout.Button("◀", GUILayout.Width(42f)))
            CycleEnemyGroup(-1);
        GUILayout.Label(GetEnemyGroupLabel(), GUILayout.Width(410f));
        if (GUILayout.Button("▶", GUILayout.Width(42f)))
            CycleEnemyGroup(1);
        GUILayout.Space(24f);
        GUILayout.Label("적 레벨", GUILayout.Width(80f));
        enemyLevelText = GUILayout.TextField(enemyLevelText, GUILayout.Width(80f));
        GUILayout.EndHorizontal();

        GUILayout.Space(14f);
        GUI.color = string.IsNullOrWhiteSpace(validationMessage)
            ? Color.white
            : new Color(1f, 0.55f, 0.45f);
        GUILayout.Label(string.IsNullOrWhiteSpace(validationMessage)
            ? "모든 입력을 검증한 뒤 전투를 시작합니다. 실제 저장 데이터는 변경하지 않습니다."
            : validationMessage);
        GUI.color = Color.white;
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("초기화", GUILayout.Height(46f)))
            ResetInputs();
        if (GUILayout.Button("모의 전투 시작", GUILayout.Height(46f)))
            StartSimulation();
        GUILayout.EndHorizontal();
    }

    private static void DrawAllyHeader()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("사용", GUILayout.Width(55f));
        GUILayout.Label("슬롯", GUILayout.Width(50f));
        GUILayout.Label("유닛", GUILayout.Width(400f));
        GUILayout.Label("레벨", GUILayout.Width(65f));
        GUILayout.Label("무기 키", GUILayout.Width(120f));
        GUILayout.Label("스킬", GUILayout.Width(70f));
        GUILayout.Label("HP 비율", GUILayout.Width(85f));
        GUILayout.EndHorizontal();
    }

    private void DrawAllyRow(int index, AllyRowState row)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);
        row.Enabled = GUILayout.Toggle(row.Enabled, string.Empty, GUILayout.Width(55f));
        GUILayout.Label((index + 1).ToString(), GUILayout.Width(50f));

        GUI.enabled = row.Enabled;
        if (GUILayout.Button("◀", GUILayout.Width(38f)))
            CycleTemplate(row, -1);
        GUILayout.Label(GetPlayerTemplateLabel(row.TemplateIndex), GUILayout.Width(310f));
        if (GUILayout.Button("▶", GUILayout.Width(38f)))
            CycleTemplate(row, 1);
        row.LevelText = GUILayout.TextField(row.LevelText, GUILayout.Width(65f));
        row.WeaponKey = GUILayout.TextField(row.WeaponKey ?? string.Empty, GUILayout.Width(120f));
        row.SkillIndexText = GUILayout.TextField(row.SkillIndexText, GUILayout.Width(70f));
        row.HpRatioText = GUILayout.TextField(row.HpRatioText, GUILayout.Width(85f));
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void LoadCatalogs()
    {
        validationMessage = string.Empty;
        playerTemplates.Clear();
        enemyGroups.Clear();

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null)
        {
            catalogReady = false;
            validationMessage = "DHCsvTemplateCatalog가 준비되지 않았습니다.";
            return;
        }

        playerTemplates.AddRange(catalog.GetAllPlayerUnitTemplates());
        enemyGroups.AddRange(catalog.GetAllEnemyGroupTemplates()
            .Where(group => group != null)
            .OrderBy(group => group.GroupKey, StringComparer.Ordinal));

        catalogReady = playerTemplates.Count > 0 && enemyGroups.Count > 0;
        if (!catalogReady)
            validationMessage = $"카탈로그가 비어 있습니다. 아군={playerTemplates.Count}, 적 그룹={enemyGroups.Count}";

        ClampSelections();
    }

    private void StartSimulation()
    {
        validationMessage = string.Empty;
        if (!catalogReady)
        {
            validationMessage = "템플릿 카탈로그가 준비되지 않았습니다.";
            return;
        }

        if (battleSceneManager == null)
        {
            validationMessage = "BattleSceneManager를 찾을 수 없습니다.";
            return;
        }

        CombatContext context = CombatContext.Instance;
        if (context == null)
        {
            validationMessage = "CombatContext를 찾을 수 없습니다.";
            return;
        }

        var runtimeAllies = new List<SimulationAllyRuntimeData>();
        for (int i = 0; i < allyRows.Length; i++)
        {
            AllyRowState row = allyRows[i];
            if (!row.Enabled)
                continue;

            if (!TryBuildAllyInput(row, out SimulationAllyInput input, out string inputError))
            {
                validationMessage = $"아군 슬롯 {i + 1}: {inputError}";
                return;
            }

            if (!SimulationUnitFactory.TryCreateRuntimeData(input, i, out SimulationAllyRuntimeData runtimeData, out string createError))
            {
                validationMessage = $"아군 슬롯 {i + 1}: {createError}";
                return;
            }

            runtimeAllies.Add(runtimeData);
        }

        if (runtimeAllies.Count == 0)
        {
            validationMessage = "아군을 한 명 이상 선택하세요.";
            return;
        }

        if (enemyGroups.Count == 0 || enemyGroupIndex < 0 || enemyGroupIndex >= enemyGroups.Count)
        {
            validationMessage = "적 그룹을 선택하세요.";
            return;
        }

        DHEnemyGroupTemplate enemyGroup = enemyGroups[enemyGroupIndex];
        if (!TryParseInt(enemyLevelText, 1, SimulationUnitFactory.MaxSimulationLevel, out int enemyLevel))
        {
            validationMessage = "적 레벨은 1 이상의 숫자여야 합니다.";
            return;
        }

        if (!SimulationUnitFactory.ValidateEnemyGroup(enemyGroup.GroupKey, out string enemyError))
        {
            validationMessage = enemyError;
            return;
        }

        string partyId = "SIM_" + Guid.NewGuid().ToString("N");
        context.BeginSimulation(
            partyId,
            runtimeAllies,
            enemyGroup.GroupKey,
            enemyLevel,
            SimulationSceneName);

        // BattleSceneManager 활성화(OnEnable)가 동기적으로 실패하면 설정 화면으로 되돌아올 수 있게 롤백한다.
        // battleStarted는 활성화가 성공한 뒤에만 true로 두어, 실패 시 UI가 숨겨진 채 갇히지 않게 한다.
        try
        {
            battleSceneManager.enabled = true;
        }
        catch (Exception ex)
        {
            battleSceneManager.enabled = false;
            context.ClearSimulation();
            battleStarted = false;
            validationMessage = $"전투 시작에 실패했습니다: {ex.Message}";
            Debug.LogException(ex, this);
            return;
        }

        battleStarted = true;
    }

    private bool TryBuildAllyInput(AllyRowState row, out SimulationAllyInput input, out string error)
    {
        input = null;
        error = string.Empty;
        if (row.TemplateIndex < 0 || row.TemplateIndex >= playerTemplates.Count)
        {
            error = "유닛 템플릿을 선택하세요.";
            return false;
        }

        if (!TryParseInt(row.LevelText, 1, SimulationUnitFactory.MaxSimulationLevel, out int level))
        {
            error = "레벨은 1 이상의 숫자여야 합니다.";
            return false;
        }

        if (!TryParseInt(row.SkillIndexText, 0, int.MaxValue, out int skillIndex))
        {
            error = "스킬 인덱스는 0 이상의 숫자여야 합니다.";
            return false;
        }

        if (!float.TryParse(row.HpRatioText, NumberStyles.Float, CultureInfo.InvariantCulture, out float hpRatio) ||
            hpRatio <= 0f || hpRatio > 1f)
        {
            error = "HP 비율은 0보다 크고 1 이하여야 합니다. 예: 1 또는 0.5";
            return false;
        }

        input = new SimulationAllyInput
        {
            Enabled = true,
            UnitTemplateKey = playerTemplates[row.TemplateIndex].UnitKey,
            Level = level,
            WeaponKey = row.WeaponKey,
            SkillIndex = skillIndex,
            HpRatio = hpRatio
        };
        return true;
    }

    private void ResetInputs()
    {
        validationMessage = string.Empty;
        enemyGroupIndex = 0;
        enemyLevelText = "1";
        for (int i = 0; i < allyRows.Length; i++)
        {
            allyRows[i].Enabled = i == 0;
            allyRows[i].TemplateIndex = 0;
            allyRows[i].LevelText = "1";
            allyRows[i].WeaponKey = string.Empty;
            allyRows[i].SkillIndexText = "0";
            allyRows[i].HpRatioText = "1";
        }

        CombatContext context = CombatContext.Instance;
        if (context != null && context.IsSimulation)
            context.ClearSimulation();
    }

    private void CycleTemplate(AllyRowState row, int delta)
    {
        if (playerTemplates.Count == 0)
            return;
        row.TemplateIndex = Wrap(row.TemplateIndex + delta, playerTemplates.Count);
    }

    private void CycleEnemyGroup(int delta)
    {
        if (enemyGroups.Count == 0)
            return;
        enemyGroupIndex = Wrap(enemyGroupIndex + delta, enemyGroups.Count);
    }

    private string GetPlayerTemplateLabel(int index)
    {
        if (index < 0 || index >= playerTemplates.Count)
            return "선택 없음";
        DHPlayerUnitTemplate template = playerTemplates[index];
        return $"{template.UnitName} / {template.ClassName} [{template.UnitKey}]";
    }

    private string GetEnemyGroupLabel()
    {
        if (enemyGroupIndex < 0 || enemyGroupIndex >= enemyGroups.Count)
            return "선택 없음";
        DHEnemyGroupTemplate group = enemyGroups[enemyGroupIndex];
        return $"{group.GroupName} [{group.GroupKey}] / {group.Members.Count}명";
    }

    private void ClampSelections()
    {
        for (int i = 0; i < allyRows.Length; i++)
            allyRows[i].TemplateIndex = playerTemplates.Count > 0
                ? Mathf.Clamp(allyRows[i].TemplateIndex, 0, playerTemplates.Count - 1)
                : 0;
        enemyGroupIndex = enemyGroups.Count > 0 ? Mathf.Clamp(enemyGroupIndex, 0, enemyGroups.Count - 1) : 0;
    }

    private static int Wrap(int value, int count)
    {
        if (count <= 0)
            return 0;
        value %= count;
        return value < 0 ? value + count : value;
    }

    private static bool TryParseInt(string text, int min, int max, out int value)
    {
        if (!int.TryParse(text, out value))
            return false;
        value = Mathf.Clamp(value, min, max);
        return true;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>UI 스테이터스 아이콘 종류(키). 파일명 규약 UI_icon_{ATK|DEF|EXE|HP|IP}.</summary>
public enum UIStatusIconType { ATK, DEF, EXE, HP, IP }

/// <summary>UI 특수 건물(거점) 아이콘 종류(키). 파일명 규약 UI_icon_{bank|blockStore|jewelryStore|library}.</summary>
public enum UIBuildingIconType { Bank, BlockStore, JewelryStore, Library }

/// <summary>
/// [JC 260625] UI 공용 아이콘(자원 4종·스테이터스·특수 건물)의 단일 보관·해석 객체(ScriptableObject).
///
/// 포트레이트(<see cref="PortraitLibrary"/>)·히어로 아이콘(<see cref="HeroIconLibrary"/>)이 CSV 경로표 기반인 것과 달리,
/// UI 아이콘은 고정된 소수 집합이므로 <b>enum 키 → Sprite 직접 직렬화(인스펙터 드래그&드롭)</b> 방식을 쓴다.
/// 인스펙터 슬롯을 비워두면 Resources 경로 규약으로 자동 폴백(현 폴더 기준)하므로, 에셋 미설정 상태로도 동작한다.
/// UI는 <see cref="UIIcons"/> facade를 통해서만 접근한다.
///
/// 자원 키는 기존 <see cref="ResourceType"/>(Money/Chip/Crystal/Supply)를 재사용한다.
/// </summary>
[CreateAssetMenu(fileName = "UIIconLibrary", menuName = "HeroInfluence/UI Icon Library")]
public class UIIconLibrary : ScriptableObject
{
    [Header("자원 아이콘 (키=ResourceType) — 비우면 폴백")]
    [SerializeField] private Sprite resMoney;    // Money
    [SerializeField] private Sprite resMedal;    // Chip(메달)
    [SerializeField] private Sprite resCrystal;  // Crystal
    [SerializeField] private Sprite resBlock;    // Supply(자재/블록)

    [Header("스테이터스 아이콘 (키=UIStatusIconType) — 비우면 폴백")]
    [SerializeField] private Sprite statATK;
    [SerializeField] private Sprite statDEF;
    [SerializeField] private Sprite statEXE;
    [SerializeField] private Sprite statHP;
    [SerializeField] private Sprite statIP;

    [Header("특수 건물 아이콘 (키=UIBuildingIconType) — 비우면 폴백")]
    [SerializeField] private Sprite bldgBank;
    [SerializeField] private Sprite bldgBlockStore;
    [SerializeField] private Sprite bldgJewelryStore;
    [SerializeField] private Sprite bldgLibrary;

    [Header("폴백: Resources 경로 규약 (인스펙터 슬롯 미설정 시)")]
    [SerializeField] private string resourceFolder = "Icon_UI_Sprite/Icon_UI_Resource_Sprite/";
    [SerializeField] private string statusFolder   = "Icon_UI_Sprite/Icon_UI_StatusIcon_Sprite/";
    [SerializeField] private string buildingFolder = "Icon_UI_Sprite/Icon_UI_SpecialBuilding_Sprite/";

    private readonly Dictionary<string, Sprite> _resCache = new Dictionary<string, Sprite>();

    private Sprite LoadRes(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_resCache.TryGetValue(path, out var s)) { s = Resources.Load<Sprite>(path); _resCache[path] = s; }
        return s;
    }

    private Sprite Resolve(Sprite direct, string folder, string file)
        => direct != null ? direct : LoadRes(folder + file);

    // ── 자원 (키=ResourceType, 파일명 규약) ───────────────────
    public Sprite GetResourceIcon(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money:   return Resolve(resMoney,   resourceFolder, "UI_icon_money");
            case ResourceType.Chip:    return Resolve(resMedal,   resourceFolder, "UI_icon_medal");
            case ResourceType.Crystal: return Resolve(resCrystal, resourceFolder, "UI_icon_crystal");
            case ResourceType.Supply:  return Resolve(resBlock,   resourceFolder, "UI_icon_block");
            default: return null;
        }
    }

    // ── 스테이터스 (키=UIStatusIconType) ──────────────────────
    public Sprite GetStatusIcon(UIStatusIconType type)
    {
        switch (type)
        {
            case UIStatusIconType.ATK: return Resolve(statATK, statusFolder, "UI_icon_ATK");
            case UIStatusIconType.DEF: return Resolve(statDEF, statusFolder, "UI_icon_DEF");
            case UIStatusIconType.EXE: return Resolve(statEXE, statusFolder, "UI_icon_EXE");
            case UIStatusIconType.HP:  return Resolve(statHP,  statusFolder, "UI_icon_HP");
            case UIStatusIconType.IP:  return Resolve(statIP,  statusFolder, "UI_icon_IP");
            default: return null;
        }
    }

    // ── 특수 건물 (키=UIBuildingIconType) ─────────────────────
    public Sprite GetBuildingIcon(UIBuildingIconType type)
    {
        switch (type)
        {
            case UIBuildingIconType.Bank:         return Resolve(bldgBank,         buildingFolder, "UI_icon_bank");
            case UIBuildingIconType.BlockStore:   return Resolve(bldgBlockStore,   buildingFolder, "UI_icon_blockStore");
            case UIBuildingIconType.JewelryStore: return Resolve(bldgJewelryStore, buildingFolder, "UI_icon_jewelryStore");
            case UIBuildingIconType.Library:      return Resolve(bldgLibrary,      buildingFolder, "UI_icon_library");
            default: return null;
        }
    }
}

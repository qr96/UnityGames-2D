using System.Text;

/// <summary>
/// 영웅 정보 텍스트 공용 조각 (목록 한 줄 요약 / 무기 조건 한글화).
/// 상세·카드 본문은 각 패널이 필드 구조로 직접 구성 (가독성 개편).
/// </summary>
public static class HeroInfoText
{

    public static string WeaponReqKorean(WeaponRequirement req)
    {
        switch (req)
        {
            case WeaponRequirement.Melee: return "근접 무기";
            case WeaponRequirement.Bow: return "활";
            case WeaponRequirement.MagicTool: return "마법 도구";
            default: return "무기 조건 없음";
        }
    }

    /// <summary>목록 항목용 한 줄 요약</summary>
    public static string ListLabel(OwnedHero hero)
    {
        if (hero == null) return "";
        string name = hero.definition != null ? hero.definition.displayName : hero.heroId;
        string active = hero.activeSkill != null ? hero.activeSkill.displayName : "-";
        return $"{name}  Lv.{hero.level}   [{active}]";
    }
}
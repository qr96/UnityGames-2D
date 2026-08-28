using System.Text;

/// <summary>
/// 보유/후보 영웅 정보 텍스트 공용 빌더 (영입 스펙 v1 — 정보 전부 공개).
/// HP·공격력은 "현재 레벨 값 / 최대(Lv.10) 값"으로 표기.
/// 영웅 관리 패널과 영입 상점 패널이 공유.
/// </summary>
public static class HeroInfoText
{
    public static string Build(OwnedHero hero)
    {
        if (hero == null) return "";
        var sb = new StringBuilder();

        string name = hero.definition != null ? hero.definition.displayName : hero.heroId;
        sb.AppendLine($"{name}  Lv.{hero.level}");
        sb.AppendLine();
        sb.AppendLine($"HP            {hero.MaxHP:0} / 최대 {hero.stats.hpLv10:0}");
        sb.AppendLine($"공격력        {hero.Attack:0.#} / 최대 {hero.stats.attackLv10:0.#}");
        sb.AppendLine($"치확 / 치피   {hero.CritChance:0.#}% / {hero.CritDamage:0}%");

        if (hero.activeSkill != null)
        {
            string mode = hero.activeSkill.activation == SkillActivation.OnRelease ? "내려놓기" : "자동";
            sb.AppendLine($"액티브        {hero.activeSkill.displayName}  ({mode} · {WeaponReqKorean(hero.activeSkill.weaponRequirement)} · 쿨 {hero.activeSkill.cooldown:0}초)");
        }
        else
        {
            sb.AppendLine("액티브        -");
        }

        string trait = TraitCatalog.DisplayName(hero.traitId);
        sb.AppendLine($"특성          {(string.IsNullOrEmpty(trait) ? "-" : trait)}");
        return sb.ToString();
    }

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

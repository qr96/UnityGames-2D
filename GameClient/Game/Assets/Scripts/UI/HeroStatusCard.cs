using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단 영웅 상태 바의 카드 1장 (전투 HUD 개편) — 색점/이름/HP/스킬 쿨.
/// UI 구성은 GameUIBuilder, 갱신은 HeroStatusBar가 담당.
/// </summary>
public class HeroStatusCard : MonoBehaviour
{
    public Image colorDot;
    public Text nameText;
    public Text hpText;
    public Image hpFill;     // type=Filled(Horizontal)
    public Text skillText;   // 스킬 모노그램 (첫 글자 — 아이콘 아트 전 자리표시)
    public Image skillIcon;  // 스킬 아이콘 원 (자동 발동 — 버튼 아님)
    public Image skillFill;  // 쿨다운 오버레이 (Radial360, 남은 쿨만큼 덮음)
    public Image background;
    public CanvasGroup group;

    Hero hero;
    SkillRunner runner;

    static readonly Color HpHigh = new Color(0.35f, 0.85f, 0.45f);
    static readonly Color HpLow = new Color(0.9f, 0.35f, 0.3f);
    static readonly Color IconReady = new Color(0.90f, 0.72f, 0.30f);   // 준비 = 금색
    static readonly Color IconCharging = new Color(0.40f, 0.46f, 0.62f); // 충전 중 = 회청
    static readonly Color BgNormal = new Color(0f, 0f, 0f, 0.30f);
    static readonly Color BgHighlight = new Color(0.35f, 0.85f, 0.5f, 0.45f); // 포션 범위 강조

    public Hero BoundHero => hero;

    /// <summary>포션 범위 등 외부 강조 (전장 링과 동기 — HeroStatusBar가 호출)</summary>
    public void SetHighlighted(bool on)
    {
        if (background != null)
            background.color = on ? BgHighlight : BgNormal;
    }

    public void Bind(Hero target)
    {
        hero = target;
        runner = target != null ? target.GetComponent<SkillRunner>() : null;
        gameObject.SetActive(hero != null);
        if (hero == null) return;

        if (nameText != null)
            nameText.text = hero.Runtime != null && hero.Runtime.definition != null
                ? hero.Runtime.definition.displayName : hero.name;
        if (colorDot != null)
        {
            var sr = hero.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) colorDot.color = sr.color;
        }
        if (skillText != null)
        {
            string skillName = runner != null && runner.Skill != null ? runner.Skill.displayName : "";
            skillText.text = string.IsNullOrEmpty(skillName) ? "-" : skillName.Substring(0, 1); // 모노그램
        }
        Refresh();
    }

    public void Refresh()
    {
        if (hero == null) return;

        bool dead = hero.IsDead;
        if (group != null) group.alpha = dead ? 0.35f : 1f;

        float ratio = Mathf.Clamp01(hero.HPRatio);
        if (hpText != null) hpText.text = dead ? "전사" : $"{hero.CurrentHP:0}/{hero.MaxHP:0}";
        if (hpFill != null)
        {
            hpFill.fillAmount = dead ? 0f : ratio;
            hpFill.color = Color.Lerp(HpLow, HpHigh, ratio);
        }

        if (runner != null)
        {
            float cd = runner.CooldownRatio; // 0=방금 씀, 1=준비 완료
            if (skillFill != null)
                skillFill.fillAmount = 1f - cd; // 남은 쿨만큼 어둡게 덮음 (시계 방향 감소)
            if (skillIcon != null)
                skillIcon.color = cd >= 1f ? IconReady : IconCharging;
        }
    }
}
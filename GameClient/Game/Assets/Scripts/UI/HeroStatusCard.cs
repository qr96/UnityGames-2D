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
    public Text skillText;
    public Image skillFill;  // 쿨다운 진행 (type=Filled)
    public CanvasGroup group;

    Hero hero;
    SkillRunner runner;

    static readonly Color HpHigh = new Color(0.35f, 0.85f, 0.45f);
    static readonly Color HpLow = new Color(0.9f, 0.35f, 0.3f);
    static readonly Color SkillReady = new Color(0.85f, 0.68f, 0.25f, 0.95f);
    static readonly Color SkillCharging = new Color(0.35f, 0.42f, 0.60f, 0.9f);

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
            skillText.text = runner != null && runner.Skill != null ? runner.Skill.displayName : "-";
        Refresh();
    }

    public void Refresh()
    {
        if (hero == null) return;

        bool dead = hero.IsDead;
        if (group != null) group.alpha = dead ? 0.35f : 1f;

        float ratio = Mathf.Clamp01(hero.HPRatio);
        if (hpText != null) hpText.text = dead ? "전사" : $"{hero.CurrentHP:0} / {hero.MaxHP:0}";
        if (hpFill != null)
        {
            hpFill.fillAmount = dead ? 0f : ratio;
            hpFill.color = Color.Lerp(HpLow, HpHigh, ratio);
        }

        if (skillFill != null && runner != null)
        {
            float cd = runner.CooldownRatio; // 0=방금 씀, 1=준비 완료
            skillFill.fillAmount = cd;
            skillFill.color = cd >= 1f ? SkillReady : SkillCharging;
        }
    }
}
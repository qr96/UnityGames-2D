using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영입 상점 패널 (영입 스펙 v1 + 카드 가독성 개편).
/// 카드 구조: 텍스트 덩어리가 아니라 필드별 Text로 분해 —
///   이름(볼드) / 특성 뱃지(우상단) / 스탯 4칸(라벨·값·보조 분리) / 액티브 / 특성 설명.
/// 라벨·보조는 작은 회색, 값은 크고 흰색 — 스캔 가능한 정보 위계.
/// UI 구성/연결은 LobbySceneBuilder가 담당.
/// </summary>
public class RecruitPanel : MonoBehaviour
{
    [System.Serializable]
    public class CandidateCard
    {
        public Text nameText;       // "시안  Lv.1" (볼드)
        public Text[] statValues = new Text[4]; // HP / 공격 / 치확 / 치피 값
        public Text[] statSubs = new Text[4];   // 보조줄 (최대치 — 치확/치피는 빈칸)
        public Text activeText;     // "전투 함성  (내려놓기 · 쿨 15초)" — 이름 강조 + 조건 회색
        public Text traitText;      // "처형인 — 설명" — 이름 금색 + 설명 회색 (액티브 줄과 동형)
        public Button recruitButton;
    }

    [Tooltip("데이터 소스 (비워두면 자동 탐색)")]
    public LobbyController lobby;

    [Header("UI 연결 (빌더가 자동 연결)")]
    public Text goldText;
    public CandidateCard[] cards = new CandidateCard[RecruitShop.CandidateCount];

    const string SubColor = "#8f9bb3";   // 보조 정보 회색 (라벨/조건/설명 공용)
    const string TraitColor = "#f2cc66"; // 특성 이름 금색

    public void Open()
    {
        if (lobby == null)
            lobby = Object.FindFirstObjectByType<LobbyController>();

        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // 빌더의 영구 리스너는 인자를 넘길 수 없어 슬롯별 메서드로 연결
    public void OnClickRecruit0() => Recruit(0);
    public void OnClickRecruit1() => Recruit(1);
    public void OnClickRecruit2() => Recruit(2);

    void Recruit(int index)
    {
        if (!RecruitShop.TryRecruit(index)) return;
        Refresh();
    }

    void Refresh()
    {
        if (goldText != null)
            goldText.text = $"골드  {GoldWallet.Gold}     보유 영웅  {HeroRoster.Heroes.Count} / {HeroRoster.MaxRoster}";

        var candidates = RecruitShop.Candidates;
        for (int i = 0; i < cards.Length; i++)
        {
            OwnedHero hero = i < candidates.Count ? candidates[i] : null;
            FillCard(cards[i], hero, RecruitShop.CanRecruit(i));
        }
    }

    void FillCard(CandidateCard card, OwnedHero hero, bool canRecruit)
    {
        if (card == null) return;

        if (hero == null)
        {
            Set(card.nameText, "빈 자리");
            for (int i = 0; i < 4; i++) { Set(card.statValues[i], "-"); Set(card.statSubs[i], ""); }
            Set(card.activeText, $"<color={SubColor}>다음 원정 종료 시 새 후보가 도착합니다.</color>");
            Set(card.traitText, "");
            SetButton(card.recruitButton, "-", false);
            return;
        }

        string name = hero.definition != null ? hero.definition.displayName : hero.heroId;
        Set(card.nameText, $"{name}  Lv.{hero.level}");

        Set(card.statValues[0], $"{hero.MaxHP:0}");
        Set(card.statSubs[0], $"최대 {hero.stats.hpLv10:0}");
        Set(card.statValues[1], $"{hero.Attack:0.#}");
        Set(card.statSubs[1], $"최대 {hero.stats.attackLv10:0.#}");
        Set(card.statValues[2], $"{hero.CritChance:0.#}%");
        Set(card.statSubs[2], "");
        Set(card.statValues[3], $"{hero.CritDamage:0}%");
        Set(card.statSubs[3], "");

        if (hero.activeSkill != null)
        {
            string mode = hero.activeSkill.activation == SkillActivation.OnRelease ? "내려놓기" : "자동";
            Set(card.activeText,
                $"<b>{hero.activeSkill.displayName}</b>   <color={SubColor}>{mode} · " +
                $"{HeroInfoText.WeaponReqKorean(hero.activeSkill.weaponRequirement)} · 쿨 {hero.activeSkill.cooldown:0}초</color>");
        }
        else Set(card.activeText, "-");

        string traitName = TraitCatalog.DisplayName(hero.traitId);
        string desc = TraitCatalog.Description(hero.traitId);
        Set(card.traitText, string.IsNullOrEmpty(traitName)
            ? "-"
            : $"<color={TraitColor}><b>{traitName}</b></color>   <color={SubColor}>{desc}</color>");

        SetButton(card.recruitButton, $"영입\n{RecruitShop.Price} 골드", canRecruit);
    }

    static void Set(Text t, string value)
    {
        if (t != null) t.text = value;
    }

    static void SetButton(Button b, string label, bool interactable)
    {
        if (b == null) return;
        b.interactable = interactable;
        Text t = b.GetComponentInChildren<Text>(true);
        if (t != null) t.text = label;
    }
}
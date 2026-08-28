using System.Collections.Generic;

/// <summary>특성 종류 (특성 스펙 v1 — 12종). 조건 판정/효과 적용은 TraitRunner.</summary>
public enum TraitKind
{
    DesperateFighter, // 궁지의 투사: 자신 HP 낮음 → 공격력 증가
    Tenacity,         // 끈질김: 자신 HP 낮음 → 받는 피해 감소
    Executioner,      // 처형인: HP 낮은 적에게 주는 피해 증가
    Duelist,          // 결투가: 가까이에 적이 1명뿐 → 공격력 증가
    Brawler,          // 난전광: 가까이에 적이 여러 명 → 공격력 증가
    LoneWolf,         // 고독한 늑대: 가까이에 아군 없음 → 공격력 증가
    Camaraderie,      // 전우애: 가까이에 아군 있음 → 받는 피해 감소
    Guardian,         // 수호자: 가까운 아군 HP 낮음 → 자신이 받는 피해 감소
    Vengeance,        // 복수심: 아군 사망 시 해당 전투 동안 공격력 증가
    Vanguard,         // 선봉장: 전투 시작 후 일정 시간 공격력 증가
    SurvivalInstinct, // 생존본능: HP 일정 이하 → 전투당 1회 소량 회복
    Reckless,         // 무모함: 주는 피해 증가 + 받는 피해 증가 (상시)
}

/// <summary>
/// 특성 카탈로그 (특성 스펙 v1: 조건 → 효과, 조건부 고유 특성 1개).
/// 수치는 전부 임시값 — 플레이테스트 후 밸런싱.
/// 필드 사용처: power = 효과 % / threshold = HP 임계 % / radius = 판정 반경 / duration = 지속 초.
/// </summary>
public static class TraitCatalog
{
    public class Entry
    {
        public string id;
        public string displayName;
        public TraitKind kind;
        public string description; // 정보 공개 UI용 (임시 수치 포함)
        public float power;
        public float threshold;
        public float radius;
        public float duration;
    }

    static readonly List<Entry> entries = new List<Entry>
    {
        new Entry { id = "desperate",   displayName = "궁지의 투사", kind = TraitKind.DesperateFighter,
            power = 30f, threshold = 35f,
            description = "HP 35% 이하일 때 공격력 +30%" },
        new Entry { id = "tenacity",    displayName = "끈질김", kind = TraitKind.Tenacity,
            power = 30f, threshold = 35f,
            description = "HP 35% 이하일 때 받는 피해 -30%" },
        new Entry { id = "executioner", displayName = "처형인", kind = TraitKind.Executioner,
            power = 30f, threshold = 35f,
            description = "HP 35% 이하의 적에게 주는 피해 +30%" },
        new Entry { id = "duelist",     displayName = "결투가", kind = TraitKind.Duelist,
            power = 25f, radius = 3f,
            description = "주변 3 이내 적이 1명뿐이면 공격력 +25%" },
        new Entry { id = "brawler",     displayName = "난전광", kind = TraitKind.Brawler,
            power = 25f, radius = 3f, threshold = 3f, // threshold = 최소 적 수
            description = "주변 3 이내 적이 3명 이상이면 공격력 +25%" },
        new Entry { id = "lonewolf",    displayName = "고독한 늑대", kind = TraitKind.LoneWolf,
            power = 30f, radius = 4f,
            description = "주변 4 이내 아군이 없으면 공격력 +30%" },
        new Entry { id = "camaraderie", displayName = "전우애", kind = TraitKind.Camaraderie,
            power = 20f, radius = 4f,
            description = "주변 4 이내 아군이 있으면 받는 피해 -20%" },
        new Entry { id = "guardian",    displayName = "수호자", kind = TraitKind.Guardian,
            power = 30f, radius = 4f, threshold = 35f,
            description = "주변 4 이내 HP 35% 이하 아군이 있으면 받는 피해 -30%" },
        new Entry { id = "vengeance",   displayName = "복수심", kind = TraitKind.Vengeance,
            power = 30f,
            description = "아군 사망 시 해당 전투 동안 공격력 +30%" },
        new Entry { id = "vanguard",    displayName = "선봉장", kind = TraitKind.Vanguard,
            power = 30f, duration = 8f,
            description = "전투 시작 후 8초간 공격력 +30%" },
        new Entry { id = "survival",    displayName = "생존본능", kind = TraitKind.SurvivalInstinct,
            power = 20f, threshold = 30f,
            description = "HP가 30% 이하가 되면 전투당 1회 최대 HP의 20% 회복" },
        new Entry { id = "reckless",    displayName = "무모함", kind = TraitKind.Reckless,
            power = 25f,
            description = "주는 피해 +25%, 받는 피해 +25%" },
    };

    public static IReadOnlyList<Entry> Entries => entries;

    public static Entry Find(string id) =>
        string.IsNullOrEmpty(id) ? null : entries.Find(t => t.id == id);

    public static string DisplayName(string id)
    {
        var e = Find(id);
        return e != null ? e.displayName : "";
    }

    public static string Description(string id)
    {
        var e = Find(id);
        return e != null ? e.description : "";
    }

    /// <summary>랜덤 특성 id (영입 후보 생성용) — 12종 전체에서 굴림.</summary>
    public static string RandomId(System.Random rng) =>
        entries.Count > 0 ? entries[rng.Next(entries.Count)].id : "";
}
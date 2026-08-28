using System.Collections.Generic;

/// <summary>
/// 특성 카탈로그 (영웅 스펙 v2: 조건부 고유 특성 1개).
/// ※ 효과 목록 미확정 — 현재는 신원(id/이름)만 관리하고 UI 공개용으로 사용.
///    효과 정의가 확정되면 TraitDefinition(조건/효과 데이터)으로 승격 예정.
/// </summary>
public static class TraitCatalog
{
    public class Entry
    {
        public string id;
        public string displayName;
        // TODO: 발동 조건 / 효과 (특성 목록 확정 시)
    }

    static readonly List<Entry> entries = new List<Entry>
    {
        new Entry { id = "tenacity",    displayName = "끈질김" },
        new Entry { id = "executioner", displayName = "처형인" },
        new Entry { id = "camaraderie", displayName = "전우애" },
    };

    public static IReadOnlyList<Entry> Entries => entries;

    public static string DisplayName(string id)
    {
        var e = entries.Find(t => t.id == id);
        return e != null ? e.displayName : "";
    }

    /// <summary>랜덤 특성 id (영입 후보 생성용) — 목록 확정 전에는 알려진 3종에서 굴림.</summary>
    public static string RandomId(System.Random rng) =>
        entries.Count > 0 ? entries[rng.Next(entries.Count)].id : "";
}

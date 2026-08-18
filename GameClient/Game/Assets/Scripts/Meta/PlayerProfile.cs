using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영구 데이터 (런과 무관하게 유지).
/// GDD 7: 특정 조건을 만족하면 영웅을 로비에 영구 해금 → 이후 시작 3명 후보가 됨.
/// 지금은 PlayerPrefs + JSON. 저장 항목이 늘면 파일 저장으로 교체.
/// </summary>
[Serializable]
public class PlayerProfile
{
    const string SaveKey = "player_profile_v1";

    public List<string> unlockedHeroIds = new List<string>();

    public bool IsUnlocked(HeroDefinition def) =>
        def != null && unlockedHeroIds.Contains(def.id);

    public void Unlock(HeroDefinition def)
    {
        if (def == null || unlockedHeroIds.Contains(def.id)) return;
        unlockedHeroIds.Add(def.id);
        Save();
    }

    /// <summary>최초 실행 시 기본 보유 영웅 해금</summary>
    public void EnsureDefaults(HeroDatabase db)
    {
        foreach (var h in db.heroes)
            if (h != null && h.unlockedByDefault && !unlockedHeroIds.Contains(h.id))
                unlockedHeroIds.Add(h.id);
        Save();
    }

    public List<HeroDefinition> GetUnlockedHeroes(HeroDatabase db)
    {
        var list = new List<HeroDefinition>();
        foreach (var h in db.heroes)
            if (IsUnlocked(h)) list.Add(h);
        return list;
    }

    public void Save() => PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));

    public static PlayerProfile Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) return new PlayerProfile();
        try { return JsonUtility.FromJson<PlayerProfile>(json) ?? new PlayerProfile(); }
        catch { return new PlayerProfile(); }
    }

    /// <summary>개발용: 저장 데이터 초기화</summary>
    public static void Wipe() => PlayerPrefs.DeleteKey(SaveKey);
}

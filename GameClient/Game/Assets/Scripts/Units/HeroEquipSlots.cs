using UnityEngine;

/// <summary>
/// 배치 화면에서 영웅 아래에 표시되는 장비 슬롯 3칸 (월드 스페이스).
/// - 채워진 칸: 밝은 색 + 장비명 앞 2글자
/// - 장비 바에서 아이템을 이 칸 위로 드롭하면 장착, 점유된 칸이면 기존 장비와 교체
/// - 교전 중에는 자동 숨김 (GDD 8: 전투 중 장비 변경 불가)
/// 비주얼은 자리표시자 — 아트가 들어오면 Build()만 교체.
/// </summary>
public class HeroEquipSlots : MonoBehaviour
{
    public const int SlotCount = HeroRunInstance.MaxEquipSlots;

    const float SlotSize = 0.34f;
    const float Spacing = 0.42f;
    static readonly Vector3 Offset = new Vector3(0f, -0.78f, 0f);
    static readonly Color EmptyColor = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color FilledColor = new Color(0.55f, 0.75f, 1f, 0.95f);

    Hero hero;
    Transform rootT;
    readonly SpriteRenderer[] frames = new SpriteRenderer[SlotCount];
    readonly TextMesh[] labels = new TextMesh[SlotCount];

    static Font cachedFont;
    static bool fontSearched;

    void Awake()
    {
        hero = GetComponent<Hero>();
        Build();
        Refresh();
    }

    void Update()
    {
        // 배치(비교전) 중에만 표시
        bool show = !BattleController.CombatActive && hero != null && !hero.IsDead;
        if (rootT != null && rootT.gameObject.activeSelf != show)
            rootT.gameObject.SetActive(show);
    }

    public Vector3 GetSlotWorldPosition(int index) => frames[index].transform.position;

    /// <summary>장비 변경 후 호출 — 칸 색/라벨 갱신</summary>
    public void Refresh()
    {
        if (hero == null || hero.Runtime == null) return;
        var equipment = hero.Runtime.equipment;

        for (int i = 0; i < SlotCount; i++)
        {
            bool filled = i < equipment.Count;
            frames[i].color = filled ? FilledColor : EmptyColor;
            if (labels[i] != null)
            {
                string name = filled ? equipment[i].displayName : "";
                labels[i].text = name.Length > 2 ? name.Substring(0, 2) : name;
            }
        }
    }

    void Build()
    {
        rootT = new GameObject("EquipSlots").transform;
        rootT.SetParent(transform, false);
        rootT.localPosition = Offset;

        for (int i = 0; i < SlotCount; i++)
        {
            float x = (i - (SlotCount - 1) / 2f) * Spacing;

            SpriteRenderer frame = UnitFactory.MakeVisual(rootT, UnitFactory.Square, EmptyColor, SlotSize, sortingOrder: 11);
            frame.transform.localPosition = new Vector3(x, 0f, 0f);
            frames[i] = frame;

            labels[i] = MakeLabel(rootT, x);
        }
    }

    static TextMesh MakeLabel(Transform parent, float x)
    {
        Font font = GetFont();
        if (font == null) return null;

        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.05f;

        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 40;
        tm.characterSize = 1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = font.material; // 내장 폰트는 머티리얼 지정 필요
        mr.sortingOrder = 12;
        return tm;
    }

    static Font GetFont()
    {
        if (fontSearched) return cachedFont;
        fontSearched = true;
        try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (cachedFont == null)
        {
            try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return cachedFont;
    }
}

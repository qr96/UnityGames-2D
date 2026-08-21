using UnityEngine;

/// <summary>
/// 임시 비주얼(런타임 생성 스프라이트) 기반 유닛 생성 유틸.
/// 아트/프리팹이 들어오면 이 클래스만 프리팹 Instantiate 방식으로 교체하면 됨.
/// </summary>
public static class UnitFactory
{
    static Sprite circle;
    static Sprite square;

    public static Sprite Circle => circle != null ? circle : (circle = CreateCircleSprite(128));
    public static Sprite Square => square != null ? square : (square = CreateSquareSprite());

    // ---------- 유닛 스폰 ----------

    public static Hero SpawnHero(HeroRunInstance instance, Vector3 pos)
    {
        HeroDefinition def = instance.definition;

        var go = new GameObject($"Hero_{def.displayName}");
        go.transform.position = pos;
        MakeVisual(go.transform, Circle, def.color, def.size, sortingOrder: 5);

        Hero hero = go.AddComponent<Hero>();
        hero.Init(instance); // 장비 반영된 최종 스탯으로 초기화
        hero.radius = def.size * 0.5f; // 겹침 방지 반경 = 비주얼 반지름

        AddHealthBar(hero, new Color(0.3f, 1f, 0.4f));
        return hero;
    }

    public static Enemy SpawnEnemy(string name, Vector3 pos,
        float maxHP, float dmg, float range, float interval, float speed)
    {
        const float enemySize = 0.85f;
        var go = new GameObject($"Enemy_{name}");
        go.transform.position = pos;
        MakeVisual(go.transform, Circle, new Color(0.85f, 0.3f, 0.3f), enemySize, sortingOrder: 5);

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.radius = enemySize * 0.5f;
        var data = ScriptableObject.CreateInstance<EnemyData>();
        data.enemyName = name;
        data.maxHP = maxHP;
        data.attackDamage = dmg;
        data.attackRange = range;
        data.attackInterval = interval;
        data.moveSpeed = speed;
        enemy.data = data;

        AddHealthBar(enemy, new Color(1f, 0.45f, 0.35f));
        return enemy;
    }

    /// <summary>투사체 생성. 비주얼 교체 지점 (아트 시 프리팹으로).</summary>
    public static Projectile SpawnProjectile(Vector3 from, Unit target, float damage, float speed, Color color)
    {
        var go = new GameObject("Projectile");
        go.transform.position = from;
        MakeVisual(go.transform, Circle, color, 0.25f, sortingOrder: 6);

        var p = go.AddComponent<Projectile>();
        p.Init(target, damage, speed);
        return p;
    }

    /// <summary>
    /// 스폰 예고 마커 생성. 연출 교체 지점: 아트가 들어오면 이 함수의 비주얼 구성만
    /// 마법진 프리팹/파티클 Instantiate로 바꾸면 됨 (SpawnMarker 로직은 그대로).
    /// </summary>
    public static SpawnMarker CreateSpawnMarker(Vector3 pos, float diameter = 1f)
    {
        var go = new GameObject("SpawnMarker");
        go.transform.position = pos;

        var marker = go.AddComponent<SpawnMarker>();
        marker.ring = MakeVisual(go.transform, Circle, new Color(1f, 0.3f, 0.2f, 0.28f), diameter, sortingOrder: 1);
        marker.fill = MakeVisual(go.transform, Circle, new Color(1f, 0.3f, 0.2f, 0.55f), diameter, sortingOrder: 2);
        marker.fill.transform.localScale = Vector3.zero;
        return marker;
    }

    // ---------- 비주얼 ----------

    public static SpriteRenderer MakeVisual(Transform parent, Sprite sprite, Color color, float size, int sortingOrder)
    {
        var go = new GameObject("Visual");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.color = color;
        r.sortingOrder = sortingOrder;
        return r;
    }

    public static void AddHealthBar(Unit unit, Color fillColor)
    {
        const float width = 0.9f;

        var barRoot = new GameObject("HPBar").transform;
        barRoot.SetParent(unit.transform, false);
        barRoot.localPosition = new Vector3(0f, 0.7f, 0f);

        var bg = MakeVisual(barRoot, Square, new Color(0f, 0f, 0f, 0.6f), 1f, sortingOrder: 10);
        bg.transform.localScale = new Vector3(width, 0.13f, 1f);

        var fillRoot = new GameObject("FillRoot").transform;
        fillRoot.SetParent(barRoot, false);
        fillRoot.localPosition = new Vector3(-width / 2f, 0f, 0f);

        var fill = MakeVisual(fillRoot, Square, fillColor, 1f, sortingOrder: 11);
        fill.transform.localScale = new Vector3(width, 0.1f, 1f);
        fill.transform.localPosition = new Vector3(width / 2f, 0f, 0f);

        var hb = barRoot.gameObject.AddComponent<HealthBar>();
        hb.target = unit;
        hb.fill = fillRoot;
    }

    // ---------- 스프라이트 생성 ----------

    static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite CreateSquareSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
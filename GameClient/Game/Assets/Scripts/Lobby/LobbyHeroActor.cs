using System.Collections;
using UnityEngine;

/// <summary>
/// 로비의 영웅 배우 — 생활 연출 (GDD 5).
/// 단순 루프: Idle → (이동 / 친구 근처 / 앉기 / 훈련 / 쉬기 중 랜덤) → Idle → 반복.
/// 복잡한 AI 없음. 비주얼 연출은 자식 Visual의 스케일/위치/투명도만 사용 —
/// 아트(스프라이트/애니메이션)가 들어오면 각 행동의 연출부만 교체.
/// </summary>
public class LobbyHeroActor : MonoBehaviour
{
    [Header("행동 튜닝")]
    public float moveSpeed = 1.6f;
    public float bobHeight = 0.08f;   // 걷기 통통거림
    public float bobFrequency = 6f;

    public HeroDefinition Definition { get; private set; }

    LobbyController lobby;
    SpriteRenderer sr;
    Transform visual;
    Vector3 baseScale;
    Color baseColor;

    public void Init(HeroDefinition def, LobbyController lobby)
    {
        Definition = def;
        this.lobby = lobby;

        sr = UnitFactory.MakeVisual(transform, UnitFactory.Circle, def.color, def.size, sortingOrder: 5);
        visual = sr.transform;
        baseScale = visual.localScale;
        baseColor = sr.color;

        UnitFactory.MakeWorldLabel(transform, def.displayName, new Vector3(0f, -0.85f, 0f), 0.05f, 6);

        StartCoroutine(LifeLoop());
    }

    IEnumerator LifeLoop()
    {
        // 시작 위상 분산 — 모두가 동시에 움직이지 않도록
        yield return new WaitForSeconds(Random.Range(0f, 1.8f));

        while (true)
        {
            yield return IdleFor(Random.Range(1.5f, 4f));

            float roll = Random.value;
            if (roll < 0.35f)
            {
                // 좌우/목적지 이동
                yield return MoveTo(lobby.RandomWanderPoint());
            }
            else if (roll < 0.55f)
            {
                // 다른 영웅 근처에서 머무르기
                LobbyHeroActor friend = lobby.GetRandomOtherActor(this);
                if (friend != null)
                {
                    Vector2 around = Random.insideUnitCircle.normalized * Random.Range(1.2f, 1.9f);
                    Vector3 dest = lobby.ClampToSpace(friend.transform.position + (Vector3)around);
                    yield return MoveTo(dest);
                    yield return IdleFor(Random.Range(2f, 4f)); // 근처에서 어울림
                }
            }
            else if (roll < 0.70f)
            {
                yield return Sit(Random.Range(2.5f, 5f));
            }
            else if (roll < 0.85f)
            {
                yield return Train(Random.Range(2f, 3.5f));
            }
            else
            {
                yield return Rest(Random.Range(3f, 5.5f));
            }
        }
    }

    // ---------- 행동 연출 (자리표시자) ----------

    /// <summary>서 있기 — 미세한 숨쉬기 펄스</summary>
    IEnumerator IdleFor(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float breathe = 1f + Mathf.Sin(Time.time * 2.2f) * 0.02f;
            visual.localScale = new Vector3(baseScale.x, baseScale.y * breathe, baseScale.z);
            yield return null;
        }
        ResetVisual();
    }

    /// <summary>목적지까지 걷기 — 통통거림</summary>
    IEnumerator MoveTo(Vector3 destination)
    {
        while (Vector2.Distance(transform.position, destination) > 0.06f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, destination, moveSpeed * Time.deltaTime);
            visual.localPosition = new Vector3(0f,
                Mathf.Abs(Mathf.Sin(Time.time * bobFrequency)) * bobHeight, 0f);
            yield return null;
        }
        ResetVisual();
    }

    /// <summary>앉아 있기 — 낮게 웅크림</summary>
    IEnumerator Sit(float duration)
    {
        visual.localScale = new Vector3(baseScale.x * 1.08f, baseScale.y * 0.78f, baseScale.z);
        visual.localPosition = new Vector3(0f, -0.07f, 0f);
        yield return new WaitForSeconds(duration);
        ResetVisual();
    }

    /// <summary>간단한 훈련 — 제자리에서 콩콩 (실제 스탯 상승 없음, GDD 7)</summary>
    IEnumerator Train(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            visual.localPosition = new Vector3(0f,
                Mathf.Abs(Mathf.Sin(t * 9f)) * 0.22f, 0f);
            yield return null;
        }
        ResetVisual();
    }

    /// <summary>쉬기 — 반투명 + 느린 숨</summary>
    IEnumerator Rest(float duration)
    {
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.55f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float breathe = 1f + Mathf.Sin(Time.time * 1.1f) * 0.03f;
            visual.localScale = new Vector3(baseScale.x, baseScale.y * 0.9f * breathe, baseScale.z);
            yield return null;
        }
        ResetVisual();
    }

    void ResetVisual()
    {
        visual.localScale = baseScale;
        visual.localPosition = Vector3.zero;
        sr.color = baseColor;
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 페이드 오버레이 (검은 화면). 최상위 캔버스에 자동 생성되는 싱글턴.
/// 맵 이동 전환 등에서 사용: yield return ScreenFader.Get().Fade(1f, 0.35f);
/// </summary>
public class ScreenFader : MonoBehaviour
{
    static ScreenFader instance;

    Image overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static ScreenFader Get()
    {
        if (instance == null)
        {
            var go = new GameObject("ScreenFader");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000; // 모든 UI 위

            instance = go.AddComponent<ScreenFader>();

            var imgGO = new GameObject("Overlay", typeof(Image));
            imgGO.transform.SetParent(go.transform, false);
            var img = imgGO.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false; // 입력 차단은 페이즈 가드가 담당

            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            instance.overlay = img;
        }
        return instance;
    }

    /// <summary>목표 알파(0=투명, 1=암전)까지 duration 동안 페이드</summary>
    public IEnumerator Fade(float targetAlpha, float duration)
    {
        float start = overlay.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetAlpha(targetAlpha);
    }

    void SetAlpha(float a)
    {
        Color c = overlay.color;
        c.a = a;
        overlay.color = c;
    }
}

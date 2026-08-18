using UnityEngine;

/// <summary>
/// 간단한 월드 체력바. fill(왼쪽 기준점 오브젝트)의 X 스케일을 HP 비율로 조절.
/// </summary>
public class HealthBar : MonoBehaviour
{
    public Unit target;
    public Transform fill;

    void LateUpdate()
    {
        if (target == null || fill == null) return;
        Vector3 s = fill.localScale;
        s.x = Mathf.Clamp01(target.HPRatio);
        fill.localScale = s;
    }
}

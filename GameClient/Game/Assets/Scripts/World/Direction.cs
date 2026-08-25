/// <summary>이동 방향 (상하좌우) — 장소의 출구 슬롯과 탐험 방향 선택 UI가 공유</summary>
public enum Direction
{
    North, // 상
    South, // 하
    West,  // 좌
    East,  // 우
}

public static class DirectionUtil
{
    public static string Korean(Direction d)
    {
        switch (d)
        {
            case Direction.North: return "상";
            case Direction.South: return "하";
            case Direction.West:  return "좌";
            case Direction.East:  return "우";
            default: return "?";
        }
    }

    public static string Arrow(Direction d)
    {
        switch (d)
        {
            case Direction.North: return "▲";
            case Direction.South: return "▼";
            case Direction.West:  return "◀";
            case Direction.East:  return "▶";
            default: return "?";
        }
    }
}

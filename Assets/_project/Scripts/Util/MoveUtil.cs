using UnityEngine;

public static class MoveUtil
{
    private const float ARRIVE_THRESHOLD = 0.05f;

    // 목표 지점으로 이동, 도착하면 true 반환
    public static bool MoveTowards(Transform t, Vector3 target, float speed)
    {
        t.position = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
        return Vector3.Distance(t.position, target) <= ARRIVE_THRESHOLD;
    }
}

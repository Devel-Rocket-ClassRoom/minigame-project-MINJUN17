using UnityEngine;

// URP에서는 Project Settings → Graphics의 Transparency Sort가 무시되므로
// 카메라에 직접 세팅해야 함. 이 컴포넌트를 Main Camera에 붙이면 됨.
[RequireComponent(typeof(Camera))]
public class TransparencySortSetup : MonoBehaviour
{
    [Header("정렬 축")]
    [Tooltip("(0,1,0) = Y가 클수록 뒤로 그려짐 (탑다운 2D 자연스러운 정렬)")]
    [SerializeField] private Vector3 sortAxis = new Vector3(0, 1, 0);

    private void Awake()
    {
        var cam = GetComponent<Camera>();
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = sortAxis;
    }
}

using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

public class PlacementTester : MonoBehaviour
{
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private FurnitureData testFurniture;

    // 에디터에서 마우스를 터치로 시뮬레이션
    private void OnEnable() => TouchSimulation.Enable();
    private void OnDisable() => TouchSimulation.Disable();

    [SerializeField] private float uiScale = 2.5f;

    private void OnGUI()
    {
        Matrix4x4 originalMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

        GUI.skin.label.fontSize = 18;
        GUI.skin.button.fontSize = 16;

        GUILayout.BeginArea(new Rect(10, 10, 280, 420));

        GUILayout.Label($"Mode: {placementSystem.Mode}");
        GUILayout.Space(10);

        if (GUILayout.Button("새 오브젝트 생성 (Place)", GUILayout.Height(45)))
        {
            Debug.Log("Place 버튼 클릭됨");
            placementSystem.StartPlace(testFurniture);
        }

        if (GUILayout.Button("이동 (Move)", GUILayout.Height(45)))
            placementSystem.StartMove();

        if (GUILayout.Button("삭제 (Remove)", GUILayout.Height(45)))
            placementSystem.StartRemove();

        GUILayout.Space(20);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("확정 (Confirm)", GUILayout.Height(55)))
            placementSystem.Confirm();

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("취소 (Cancel)", GUILayout.Height(55)))
            placementSystem.Cancel();

        GUI.backgroundColor = Color.white;
        GUILayout.EndArea();

        GUI.matrix = originalMatrix;
    }
}

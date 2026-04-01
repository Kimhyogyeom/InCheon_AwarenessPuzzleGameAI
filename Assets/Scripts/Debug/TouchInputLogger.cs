using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 터치 및 마우스 입력 상태를 콘솔 로그로 출력하는 디버그 스크립트
/// Development Build의 output_log.txt에서 확인 가능
/// </summary>
public class TouchInputLogger : MonoBehaviour
{
    [Header("로그 설정")]
    [SerializeField] private bool _logOnPress = true;
    [SerializeField] private bool _logWhileHeld = false;
    [SerializeField] private bool _logOnRelease = true;
    [SerializeField] private bool _logNormalized = true;

    void Update()
    {
        LogMouseInput();
        LogTouchInput();
    }

    private void LogMouseInput()
    {
        if (Mouse.current == null) return;

        Vector2 pos = Mouse.current.position.ReadValue();
        Vector2 norm = new Vector2(pos.x / Screen.width, pos.y / Screen.height);

        if (_logOnPress && Mouse.current.leftButton.wasPressedThisFrame)
            Log($"마우스 클릭 시작 | ({pos.x:F0}, {pos.y:F0})" + (_logNormalized ? $" | 정규화 ({norm.x:F3}, {norm.y:F3})" : ""));

        if (_logWhileHeld && Mouse.current.leftButton.isPressed)
            Log($"마우스 드래그 중 | ({pos.x:F0}, {pos.y:F0})" + (_logNormalized ? $" | 정규화 ({norm.x:F3}, {norm.y:F3})" : ""));

        if (_logOnRelease && Mouse.current.leftButton.wasReleasedThisFrame)
            Log($"마우스 클릭 종료 | ({pos.x:F0}, {pos.y:F0})" + (_logNormalized ? $" | 정규화 ({norm.x:F3}, {norm.y:F3})" : ""));
    }

    private void LogTouchInput()
    {
        if (Touchscreen.current == null) return;

        var touches = Touchscreen.current.touches;
        for (int i = 0; i < touches.Count; i++)
        {
            var touch = touches[i];
            var phase = touch.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;

            Vector2 pos = touch.position.ReadValue();
            Vector2 norm = new Vector2(pos.x / Screen.width, pos.y / Screen.height);
            string normStr = _logNormalized ? $" | 정규화 ({norm.x:F3}, {norm.y:F3})" : "";

            if (_logOnPress && phase == UnityEngine.InputSystem.TouchPhase.Began)
                Log($"터치{i} 시작 | ({pos.x:F0}, {pos.y:F0}){normStr}");

            if (_logWhileHeld && (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary))
                Log($"터치{i} {(phase == UnityEngine.InputSystem.TouchPhase.Moved ? "이동" : "정지")} | ({pos.x:F0}, {pos.y:F0}){normStr}");

            if (_logOnRelease && (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled))
                Log($"터치{i} {(phase == UnityEngine.InputSystem.TouchPhase.Ended ? "종료" : "취소")} | ({pos.x:F0}, {pos.y:F0}){normStr}");
        }
    }

    private void Log(string msg) => Debug.Log($"[INPUT] {msg}");
}

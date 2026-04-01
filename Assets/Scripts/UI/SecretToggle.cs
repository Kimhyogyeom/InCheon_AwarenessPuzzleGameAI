using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 C 키를 누르면 숨겨진 오브젝트를 토글 (개발자 전용)
/// </summary>
public class SecretToggle : MonoBehaviour
{
    [SerializeField] private GameObject secretPanel;

    void Start()
    {
        if (secretPanel != null)
            secretPanel.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (secretPanel != null)
            {
                bool newState = !secretPanel.activeSelf;
                secretPanel.SetActive(newState);
                Debug.Log($"[SecretToggle] 패널 {(newState ? "활성화" : "비활성화")}");
            }
        }
    }
}

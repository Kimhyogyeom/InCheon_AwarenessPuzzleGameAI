using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 이미지를 10번 클릭하면 숨겨진 오브젝트를 토글 (운영자/개발자 전용)
/// </summary>
public class SecretToggle : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject secretPanel;  // 토글할 오브젝트 (티칭 버튼 등)
    [SerializeField] private int requiredClicks = 10;  // 필요 클릭 수
    [SerializeField] private float resetTime = 3.0f;  // 클릭 초기화 시간 (초)

    private int clickCount = 0;
    private float lastClickTime = 0f;

    void Start()
    {
        if (secretPanel != null)
            secretPanel.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 시간 초과 시 카운트 리셋
        if (Time.time - lastClickTime > resetTime)
        {
            clickCount = 0;
        }

        lastClickTime = Time.time;
        clickCount++;

        if (clickCount >= requiredClicks)
        {
            clickCount = 0;

            if (secretPanel != null)
            {
                bool newState = !secretPanel.activeSelf;
                secretPanel.SetActive(newState);
                Debug.Log($"[SecretToggle] 패널 {(newState ? "활성화" : "비활성화")}");
            }
        }
    }
}

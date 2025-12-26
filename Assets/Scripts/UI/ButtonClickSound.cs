using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 시 사운드 재생
/// Button 컴포넌트가 있는 오브젝트에 추가하면 자동으로 클릭 사운드 연결
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    [SerializeField] private string _sfxName = "ButtonClick";

    void Start()
    {
        Button button = GetComponent<Button>();
        button.onClick.AddListener(PlayClickSound);
    }

    private void PlayClickSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(_sfxName);
        }
    }
}

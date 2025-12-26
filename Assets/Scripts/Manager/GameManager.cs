using UnityEngine;

/// <summary>
/// 게임 전체를 관리하는 싱글톤 매니저
/// </summary>
public class GameManager : MonoBehaviour
{
    // 싱글톤 인스턴스 (외부에서 읽기만 가능)
    public static GameManager _Instance { get; private set; }

    // 게임 모드 열거형
    public enum GameMode
    {
        EASY,   // 쉬움 모드
        HARD    // 어려움 모드
    }

    // 현재 게임 모드
    public GameMode _currentMode = GameMode.EASY;

    // 속도 배율 (EASY: 1, HARD: 2) - 외부에서 읽기만 가능
    public float _speedMultiplier { get; private set; } = 1f;

    [Header("AI 설명 텍스트")]
    [SerializeField] private string _easyDescription = "AI는 기본 속도로 퍼즐을 풀게 됩니다.";
    [SerializeField] private string _hardDescription = "AI는 2배 속도로 퍼즐을 풀게 됩니다.";

    void Awake()
    {
        // 싱글톤 패턴: 인스턴스가 없으면 생성, 있으면 중복 제거
        if (_Instance == null)
        {
            _Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 게임 모드 설정 및 속도 배율 적용
    /// </summary>
    /// <param name="mode">설정할 게임 모드</param>
    public void SetGameMode(GameMode mode)
    {
        SoundManager.Instance.PlaySFX("ButtonClick");

        _currentMode = mode;
        _speedMultiplier = (mode == GameMode.EASY) ? 1f : 2f;

        // Description 패널로 전환 (모드에 맞는 설명 표시)
        if (UIManager._Instance != null)
        {
            string description = (mode == GameMode.EASY) ? _easyDescription : _hardDescription;
            UIManager._Instance.ShowDescriptionPanel(description);
        }
    }

    /// <summary>
    /// Description 패널에서 다음 버튼 클릭 시 - 퍼즐 패널로 이동
    /// </summary>
    public void OnNextButtonClick()
    {
        SoundManager.Instance.PlaySFX("ButtonClick");
        if (UIManager._Instance != null)
        {
            UIManager._Instance.ShowPuzzlePanel();
        }
    }

    /// <summary>
    /// Puzzle 패널에서 다음 버튼 클릭 시 - 퍼즐 게임 패널로 이동
    /// </summary>
    public void OnPuzzleNextButtonClick()
    {
        SoundManager.Instance.PlaySFX("ButtonClick");
        if (UIManager._Instance != null)
        {
            UIManager._Instance.ShowPuzzleGamePanel();
        }
    }

    /// <summary>
    /// EASY 모드로 설정 (속도 배율: 1)
    /// </summary>
    public void SetEasyMode()
    {
        SetGameMode(GameMode.EASY);
    }

    /// <summary>
    /// HARD 모드로 설정 (속도 배율: 2)
    /// </summary>
    public void SetHardMode()
    {
        SetGameMode(GameMode.HARD);
    }

    /// <summary>
    /// 게임 전체 초기화 (처음 상태로)
    /// </summary>
    public void ResetGame()
    {
        _currentMode = GameMode.EASY;
        _speedMultiplier = 1f;
    }
}

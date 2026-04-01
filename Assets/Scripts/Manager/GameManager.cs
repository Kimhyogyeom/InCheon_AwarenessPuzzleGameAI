using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Inactivity Timer 설정")]
    [SerializeField] private float _inactivityTimeout = 30f; // 30초 무입력 시 초기화
    private float _inactivityTimer = 30f;
    private int _lastDisplayedSeconds = 30;
    private bool _isCleaningUp = false; // 로봇 정리 중 플래그
    private bool _isInResultPanel = false; // 결과창 대기 중 플래그

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

    void Update()
    {
        UpdateInactivityTimer();
    }

    /// <summary>
    /// 무입력 타이머 업데이트
    /// </summary>
    private void UpdateInactivityTimer()
    {
        // 로봇 정리 중에는 타이머 멈춤
        if (_isCleaningUp) return;

        // Ready 패널에서는 타이머 로직 비활성화 (나머지 패널에서는 모두 작동)
        bool isReadyPanel = UIManager._Instance != null && UIManager._Instance.IsReadyPanel();
        if (isReadyPanel)
        {
            return;
        }

        // 입력 감지 (새 Input System 사용)
        bool hasInput = false;

        // 마우스 입력 (클릭 시작만 감지, isPressed 제외 - 터치 시뮬레이션 오감지 방지)
        if (Mouse.current != null)
        {
            hasInput |= Mouse.current.leftButton.wasPressedThisFrame ||
                        Mouse.current.rightButton.wasPressedThisFrame ||
                        Mouse.current.middleButton.wasPressedThisFrame;
        }

        // 터치 입력 (Began/Moved만 감지 - Stationary는 phantom touch 방지를 위해 제외)
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                var phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began ||
                    phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    hasInput = true;
                    break;
                }
            }
        }

        // 키보드 입력
        if (Keyboard.current != null)
        {
            hasInput |= Keyboard.current.anyKey.wasPressedThisFrame;
        }

        if (hasInput)
        {
            // 입력 있음 -> 타이머 리셋
            Debug.Log($"[TIMER DEBUG] 입력 감지! 타이머 리셋: {_inactivityTimeout}초");
            _inactivityTimer = _inactivityTimeout;
        }

        // 타이머 감소 (게임 플레이 중에만 실행)
        float beforeTimer = _inactivityTimer;
        _inactivityTimer -= Time.deltaTime;
        if (_inactivityTimer < 0f) _inactivityTimer = 0f;

        // 5초마다 타이머 값 출력
        if (Mathf.FloorToInt(beforeTimer) % 5 == 0 && Mathf.FloorToInt(_inactivityTimer) != Mathf.FloorToInt(beforeTimer))
        {
            Debug.Log($"[TIMER DEBUG] 타이머 진행 중: {_inactivityTimer:F1}초");
        }

        // UI 업데이트 (매 초마다)
        int currentSeconds = Mathf.CeilToInt(_inactivityTimer);
        if (currentSeconds != _lastDisplayedSeconds)
        {
            _lastDisplayedSeconds = currentSeconds;
            if (UIManager._Instance != null)
            {
                UIManager._Instance.UpdateInactivityTimerText(currentSeconds);
            }
        }

        // 타이머 만료 (0초)
        if (_inactivityTimer <= 0f)
        {
            OnInactivityTimeout();
            _inactivityTimer = _inactivityTimeout; // 중복 호출 방지
        }
    }

    /// <summary>
    /// 무입력 타이머 만료 처리
    /// </summary>
    private void OnInactivityTimeout()
    {
        if (_isCleaningUp) return; // 이미 정리 중이면 무시

        // 결과창 대기 중이면 바로 Ready 화면으로
        if (_isInResultPanel)
        {
            Debug.Log("[TIMER] 결과창에서 입력 없음 - Ready 화면으로 복귀");
            _isInResultPanel = false;
            _isCleaningUp = true;
            ReturnToReady();
            return;
        }

        _isCleaningUp = true; // 정리 시작

        if (UIManager._Instance != null)
        {
            if (UIManager._Instance.IsInGamePlay())
            {
                // 게임 플레이 중 -> 패배 처리 (자동 정리 포함)
                Debug.Log("[TIMER] 게임 중 입력 없음 - 패배 처리");
                PuzzleManager puzzleManager = FindObjectOfType<PuzzleManager>();
                if (puzzleManager != null)
                {
                    puzzleManager.OnTimeUp(); // 패배 처리 (자동 정리 시작됨)
                }
                else
                {
                    ReturnToReady();
                }
            }
            else
            {
                // 패널 이동 중 -> 바로 Ready 화면으로 복귀
                ReturnToReady();
            }
        }
    }

    /// <summary>
    /// 정리 완료 콜백 (UIManager에서 호출)
    /// </summary>
    public void OnCleanupComplete()
    {
        _isCleaningUp = false;
    }

    /// <summary>
    /// 결과창 타이머 시작 (PuzzleManager 자동 정리 완료 후 호출)
    /// </summary>
    public void StartResultPanelTimer()
    {
        Debug.Log("[TIMER] 결과창 타이머 시작");
        _isInResultPanel = true;
        _isCleaningUp = false; // 정리 완료
        _inactivityTimer = _inactivityTimeout; // 타이머 리셋
        _lastDisplayedSeconds = (int)_inactivityTimeout;

        if (UIManager._Instance != null)
        {
            UIManager._Instance.UpdateInactivityTimerText(_lastDisplayedSeconds);
        }
    }

    /// <summary>
    /// Ready 화면으로 복귀 및 초기화
    /// </summary>
    private void ReturnToReady()
    {
        // 타이머 리셋
        _inactivityTimer = _inactivityTimeout;
        _lastDisplayedSeconds = (int)_inactivityTimeout;

        // 진행 중인 로봇/퍼즐 강제 중지
        PuzzleManager puzzleManager = FindObjectOfType<PuzzleManager>();
        if (puzzleManager != null)
        {
            puzzleManager.EmergencyStop();
        }

        // Ready 화면으로 강제 전환 (모든 패널 초기화)
        if (UIManager._Instance != null)
        {
            UIManager._Instance.ForceShowReadyPanel();
            UIManager._Instance.UpdateInactivityTimerText(_lastDisplayedSeconds);
        }

        // 게임 상태 초기화
        ResetGame();
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

        // 타이머 리셋 (Description 패널에서 타이머 시작)
        _inactivityTimer = _inactivityTimeout;
        _lastDisplayedSeconds = (int)_inactivityTimeout;

        // Description 패널로 전환 (모드에 맞는 설명 표시)
        if (UIManager._Instance != null)
        {
            string description = (mode == GameMode.EASY) ? _easyDescription : _hardDescription;
            UIManager._Instance.ShowDescriptionPanel(description);
            UIManager._Instance.UpdateInactivityTimerText(_lastDisplayedSeconds);
        }
    }

    /// <summary>
    /// Description 패널에서 다음 버튼 클릭 시 - 퍼즐 패널로 이동
    /// </summary>
    public void OnNextButtonClick()
    {
        SoundManager.Instance.PlaySFX("ButtonClick");

        // 타이머 리셋 (Puzzle 패널에서 타이머 계속)
        _inactivityTimer = _inactivityTimeout;
        _lastDisplayedSeconds = (int)_inactivityTimeout;

        if (UIManager._Instance != null)
        {
            UIManager._Instance.ShowPuzzlePanel();
            UIManager._Instance.UpdateInactivityTimerText(_lastDisplayedSeconds);
        }
    }

    /// <summary>
    /// Puzzle 패널에서 다음 버튼 클릭 시 - 퍼즐 게임 패널로 이동
    /// </summary>
    public void OnPuzzleNextButtonClick()
    {
        SoundManager.Instance.PlaySFX("ButtonClick");

        // 게임 플레이 시작 시 타이머 초기화
        _inactivityTimer = _inactivityTimeout;
        _lastDisplayedSeconds = (int)_inactivityTimeout;
        _isCleaningUp = false; // 정리 플래그 리셋 (타이머 작동 보장)
        _isInResultPanel = false; // 결과창 플래그 리셋 (다시하기 클릭 시)

        // 인게임 BGM 재생 (다시하기 포함)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayGameBGM();
        }

        if (UIManager._Instance != null)
        {
            UIManager._Instance.ShowPuzzleGamePanel();
            UIManager._Instance.UpdateInactivityTimerText(_lastDisplayedSeconds);
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
    /// 뒤로가기 버튼 클릭 시 (체험 패널 → Ready 패널)
    /// </summary>
    public void OnBackButtonClick()
    {
        SoundManager.Instance.PlaySFX("ButtonClick");

        // 타이머 리셋 및 플래그 초기화
        _inactivityTimer = _inactivityTimeout;
        _lastDisplayedSeconds = (int)_inactivityTimeout;
        _isCleaningUp = false;
        _isInResultPanel = false;

        if (UIManager._Instance != null)
        {
            // Ready 패널로 돌아가기 (역방향 슬라이드)
            UIManager._Instance.ShowReadyPanel();
        }

        // 게임 상태 초기화
        ResetGame();
    }

    /// <summary>
    /// 게임 전체 초기화 (처음 상태로)
    /// </summary>
    public void ResetGame()
    {
        _currentMode = GameMode.EASY;
        _speedMultiplier = 1f;
    }

    /// <summary>
    /// 대결 시작 시 호출 - 결과창/정리 플래그 초기화 (다시하기 등)
    /// </summary>
    public void OnBattleStarted()
    {
        _isInResultPanel = false;
        _isCleaningUp = false;
        _inactivityTimer = _inactivityTimeout;
        _lastDisplayedSeconds = (int)_inactivityTimeout;
        Debug.Log("[GameManager] OnBattleStarted - 플래그 초기화");
    }
}

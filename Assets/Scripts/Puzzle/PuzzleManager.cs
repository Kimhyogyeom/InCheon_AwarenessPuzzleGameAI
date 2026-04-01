using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 퍼즐 게임 전체 관리
/// 게임 시작 시 퍼즐 조각 무작위 배치 (겹침 방지)
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    [Header("퍼즐 영역")]
    // 퍼즐 조각들의 부모 오브젝트 (GameObjectPuzzle)
    [SerializeField] private RectTransform _puzzleContainer;

    // 게임 시작 감지용 패널 (이 패널 활성화 시 타이머 시작)
    [SerializeField] private GameObject _puzzleGamePanel;

    [Header("타이머 설정")]
    // true: 로봇 티칭 JSON에서 제한시간 가져오기, false: 인스펙터 값 사용
    [SerializeField] private bool _useRobotDuration = true;

    // 제한 시간 (초)
    [SerializeField] private float _timeLimit = 60f;

    // 타이머 표시 텍스트
    [SerializeField] private TextMeshProUGUI _timerText;

    [Header("결과 오브젝트")]
    // 공통 활성화 오브젝트 (성공/실패 둘 다)
    [SerializeField] private GameObject _resultObjectA;

    // 성공 시 활성화 오브젝트
    [SerializeField] private GameObject _successObjectB;

    // 실패 시 활성화 오브젝트
    [SerializeField] private GameObject _failObjectC;

    [Header("로봇 정리 중 패널")]
    // 로봇 정리 동작 중 터치 방지용 패널
    [SerializeField] private GameObject _cleanupBlockPanel;

    [Header("결과창 자동 정리 오브젝트")]
    // 정리 완료 후 활성화 (버튼들)
    [SerializeField] private GameObject _resultButtonsObject;

    // 정리 중 활성화 (정리 중 표시)
    [SerializeField] private GameObject _cleanupWaitObject;

    // 정리 중 진행 텍스트 (정리중 ... (1/16) 형태)
    [SerializeField] private TextMeshProUGUI _cleanupProgressText;

    [Header("랜덤 배치 범위")]
    // X축 범위 (부모 기준)
    [SerializeField] private Vector2 _randomRangeX = new Vector2(-200f, 200f);

    // Y축 범위 (부모 기준)
    [SerializeField] private Vector2 _randomRangeY = new Vector2(-200f, 200f);

    [Header("겹침 방지 설정")]
    // 퍼즐 조각 간 최소 간격
    [SerializeField] private float _minDistance = 80f;

    // 최대 배치 시도 횟수 (무한루프 방지)
    [SerializeField] private int _maxAttempts = 100;

    [Header("설정")]
    // 게임 시작 시 자동 셔플
    [SerializeField] private bool _shuffleOnStart = true;

    [Header("개발/테스트 모드")]
    // true: 타이머 무시하고 계속 플레이 가능 (테스트용)
    [SerializeField] private bool _developmentMode = false;

    // 로봇 없을 때 가짜 정리 시간 (초) - 테스트용
    [SerializeField] private float _fakeCleanupDelay = 3f;

    // 퍼즐 조각 리스트
    private PuzzlePiece[] _puzzlePieces;

    // 배치된 위치 저장
    private List<Vector2> _placedPositions = new List<Vector2>();

    // 맞춘 퍼즐 개수
    private int _placedCount = 0;

    // 총 퍼즐 개수
    private int _totalPuzzleCount = 0;

    // 남은 시간
    private float _remainingTime;

    // 게임 진행 중 여부
    private bool _isGameRunning = false;

    // 게임 종료 여부
    private bool _isGameEnded = false;

    // 패널 활성화 감지용
    private bool _wasPanelActive = false;

    // 타이머 만료 후 로봇 티칭 완료 대기 플래그
    private bool _waitingForRobotToFinish = false;


    void Awake()
    {
        // 패널 초기 상태 저장 (Awake에서 먼저 체크)
        if (_puzzleGamePanel != null)
        {
            _wasPanelActive = _puzzleGamePanel.activeInHierarchy;
        }
    }

    void Start()
    {
        // 퍼즐 조각들 가져오기
        _puzzlePieces = _puzzleContainer.GetComponentsInChildren<PuzzlePiece>();
        _totalPuzzleCount = _puzzlePieces.Length;
        _placedCount = 0;

        // 타이머 초기화 (아직 시작하지 않음)
        _remainingTime = _timeLimit;
        _isGameRunning = false;
        _isGameEnded = false;

        // 결과 오브젝트 비활성화
        if (_resultObjectA != null) _resultObjectA.SetActive(false);
        if (_successObjectB != null) _successObjectB.SetActive(false);
        if (_failObjectC != null) _failObjectC.SetActive(false);
        if (_cleanupWaitObject != null) _cleanupWaitObject.SetActive(false);
        if (_cleanupProgressText != null) _cleanupProgressText.gameObject.SetActive(false);

        if (_shuffleOnStart)
        {
            ShufflePuzzlePieces();
        }

        UpdateTimerDisplay();
    }

    /// <summary>
    /// 게임 시작 (타이머 시작 + 로봇 티칭 자동 시작)
    /// </summary>
    public void StartGame()
    {
        // 로봇 티칭 JSON에서 제한시간 가져오기
        UpdateTimeLimitFromRobot();

        _remainingTime = _timeLimit;
        _isGameRunning = true;
        _isGameEnded = false;
        _waitingForRobotToFinish = false;

        // GameManager 플래그 초기화
        if (GameManager._Instance != null)
        {
            GameManager._Instance.OnBattleStarted();
        }

        // 결과 오브젝트 확실히 비활성화
        if (_resultObjectA != null) _resultObjectA.SetActive(false);
        if (_successObjectB != null) _successObjectB.SetActive(false);
        if (_failObjectC != null) _failObjectC.SetActive(false);

        // 퍼즐 조각/슬롯 초기화 (이전 게임 잔여 상태 제거)
        ResetAllPuzzlePieces();

        UpdateTimerDisplay();

        // 로봇 티칭 자동 시작
        StartRobotTeaching();
    }

    /// <summary>
    /// 로봇 티칭 JSON에서 제한시간 가져오기
    /// </summary>
    private void UpdateTimeLimitFromRobot()
    {
        if (!_useRobotDuration)
        {
            Debug.Log($"[PuzzleManager] 제한시간 설정: {_timeLimit}초 (인스펙터 값 사용)");
            return;
        }

        var robot = FindObjectOfType<LebaiRobotController>();
        if (robot != null)
        {
            float duration = robot.GetTeachingDuration();
            if (duration > 0)
            {
                _timeLimit = duration;
                Debug.Log($"[PuzzleManager] 제한시간 설정: {_timeLimit}초 (티칭 1~16 합산)");
            }
        }
    }

    /// <summary>
    /// 로봇 티칭 시작 - 게임 모드 (리버스 1~16 순차 실행)
    /// </summary>
    private void StartRobotTeaching()
    {
        var robot = FindObjectOfType<LebaiRobotController>();
        if (robot != null)
        {
            // 게임 모드 리버스 1~16 순차 실행 (완료 시 로봇 승리)
            robot.StartGameTeachings(OnRobotTeachingComplete);
        }
    }

    /// <summary>
    /// 로봇이 리버스 1~16을 모두 완료했을 때 호출 (= 로봇 승리)
    /// 또는 타이머 만료 후 현재 티칭 완료 시 호출
    /// </summary>
    private void OnRobotTeachingComplete()
    {
        if (_isGameEnded) return;

        _waitingForRobotToFinish = false;

        // 로봇이 마지막 퍼즐까지 놓았으므로 타이머를 0으로 맞추고 게임 종료
        _remainingTime = 0f;
        UpdateTimerDisplay();
        OnTimeUp();
    }

    /// <summary>
    /// 로봇 티칭 중지 (현재 진행 중인 티칭 완료 후 중지)
    /// </summary>
    private void StopRobotTeaching()
    {
        var robot = FindObjectOfType<LebaiRobotController>();
        if (robot != null)
        {
            robot.StopGameTeachingsGraceful();
        }
    }

    void Update()
    {
        // 패널 활성화 감지
        if (_puzzleGamePanel != null)
        {
            bool isPanelActive = _puzzleGamePanel.activeInHierarchy;

            // 패널이 방금 활성화됨
            if (isPanelActive && !_wasPanelActive)
            {
                StartGame();
            }

            _wasPanelActive = isPanelActive;
        }

        if (!_isGameRunning || _isGameEnded) return;

        // 타이머 감소
        _remainingTime -= Time.deltaTime;
        if (_remainingTime < 0f) _remainingTime = 0f;
        UpdateTimerDisplay();

        // 타이머 만료 시 처리 - 개발 모드가 아닐 때만
        if (_remainingTime <= 0f && !_developmentMode && !_waitingForRobotToFinish)
        {
            var robot = FindObjectOfType<LebaiRobotController>();
            if (robot != null && robot.IsTeachingRunning)
            {
                // 로봇이 티칭 중 - 현재 티칭 완료 후 종료 (마지막 퍼즐을 다 놓고 끝내기)
                Debug.Log("[PuzzleManager] 타이머 만료 - 로봇 티칭 완료 대기 중...");
                _isGameRunning = false;
                _waitingForRobotToFinish = true;
                StopRobotTeaching(); // graceful: 현재 티칭 완료 후 중지, 콜백 호출
            }
            else
            {
                OnTimeUp();
            }
        }
    }

    /// <summary>
    /// 타이머 텍스트 업데이트
    /// </summary>
    private void UpdateTimerDisplay()
    {
        if (_timerText == null) return;

        int seconds = Mathf.CeilToInt(_remainingTime);
        _timerText.text = string.Format("{0:00}", seconds);
    }

    /// <summary>
    /// 시간 초과 시 호출 (= 로봇 승리!)
    /// </summary>
    public void OnTimeUp()
    {
        if (_isGameEnded) return;

        _isGameRunning = false;
        _isGameEnded = true;

        Debug.Log("시간 초과! 로봇 승리!");

        // 로봇 티칭 중지
        StopRobotTeaching();

        // 실패 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("Lose");
        }

        // 먼저 버튼 비활성화 준비
        StartAutoCleanup();

        // A + C 활성화 (로봇 승리 = 유저 패배)
        if (_resultObjectA != null) _resultObjectA.SetActive(true);
        if (_failObjectC != null) _failObjectC.SetActive(true);
    }

    /// <summary>
    /// 퍼즐 조각들을 무작위 위치에 배치 (겹침 방지)
    /// </summary>
    public void ShufflePuzzlePieces()
    {
        _placedPositions.Clear();

        foreach (PuzzlePiece piece in _puzzlePieces)
        {
            Vector2 randomPosition = GetNonOverlappingPosition();
            _placedPositions.Add(randomPosition);

            // 퍼즐 조각에 위치 설정
            piece.SetOriginalPosition(randomPosition);
        }
    }

    /// <summary>
    /// 겹치지 않는 랜덤 위치 찾기
    /// </summary>
    private Vector2 GetNonOverlappingPosition()
    {
        int attempts = 0;

        while (attempts < _maxAttempts)
        {
            // 랜덤 위치 생성
            float randomX = Random.Range(_randomRangeX.x, _randomRangeX.y);
            float randomY = Random.Range(_randomRangeY.x, _randomRangeY.y);
            Vector2 newPosition = new Vector2(randomX, randomY);

            // 기존 위치들과 겹치는지 확인
            if (!IsOverlapping(newPosition))
            {
                return newPosition;
            }

            attempts++;
        }

        // 최대 시도 초과 시 그냥 랜덤 위치 반환
        Debug.LogWarning("PuzzleManager: 겹치지 않는 위치를 찾지 못했습니다. 범위를 넓히거나 간격을 줄여주세요.");
        return new Vector2(
            Random.Range(_randomRangeX.x, _randomRangeX.y),
            Random.Range(_randomRangeY.x, _randomRangeY.y)
        );
    }

    /// <summary>
    /// 해당 위치가 기존 위치들과 겹치는지 확인
    /// </summary>
    private bool IsOverlapping(Vector2 position)
    {
        foreach (Vector2 placedPos in _placedPositions)
        {
            float distance = Vector2.Distance(position, placedPos);
            if (distance < _minDistance)
            {
                return true;  // 겹침
            }
        }
        return false;  // 안 겹침
    }

    /// <summary>
    /// 퍼즐 리셋 (다시 셔플)
    /// </summary>
    public void ResetPuzzle()
    {
        ShufflePuzzlePieces();
    }

    /// <summary>
    /// 퍼즐 조각이 슬롯에 배치되었을 때 호출
    /// </summary>
    public void OnPiecePlaced()
    {
        _placedCount++;
        Debug.Log($"퍼즐 진행: {_placedCount} / {_totalPuzzleCount}");

        // 모든 퍼즐 완성 체크
        if (_placedCount >= _totalPuzzleCount)
        {
            OnPuzzleCompleted();
        }
    }

    /// <summary>
    /// 퍼즐 완성 시 호출 (= 유저 승리!)
    /// </summary>
    private void OnPuzzleCompleted()
    {
        if (_isGameEnded) return;

        _isGameRunning = false;
        _isGameEnded = true;

        Debug.Log("퍼즐 완성! 유저 승리!");

        // 로봇 티칭 중지 (유저가 먼저 이겼으므로)
        StopRobotTeaching();

        // 성공 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("Win");
        }

        // 먼저 버튼 비활성화 준비
        StartAutoCleanup();

        // A + B 활성화 (유저 승리)
        if (_resultObjectA != null) _resultObjectA.SetActive(true);
        if (_successObjectB != null) _successObjectB.SetActive(true);
    }

    /// <summary>
    /// 현재 진행률 반환 (0.0 ~ 1.0)
    /// </summary>
    public float GetProgress()
    {
        if (_totalPuzzleCount == 0) return 0f;
        return (float)_placedCount / _totalPuzzleCount;
    }

    /// <summary>
    /// 맞춘 퍼즐 개수 반환
    /// </summary>
    public int GetPlacedCount()
    {
        return _placedCount;
    }

    /// <summary>
    /// 총 퍼즐 개수 반환
    /// </summary>
    public int GetTotalCount()
    {
        return _totalPuzzleCount;
    }

    /// <summary>
    /// 처음으로 버튼 - 모든 것 리셋하고 레디 화면으로
    /// </summary>
    public void OnHomeButton()
    {
        // 버튼 클릭 사운드
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("ButtonClick");
        }

        // 로봇 티칭 중지
        StopRobotTeaching();

        // 로봇 정리 시작 (터치 방지 패널 활성화)
        StartCleanupWithBlock(() =>
        {
            // 정리 완료 후 실행
            // 타이머 정지
            _isGameRunning = false;
            _isGameEnded = false;
            _remainingTime = _timeLimit;
            _placedCount = 0;

            // 결과 오브젝트 비활성화
            if (_resultObjectA != null) _resultObjectA.SetActive(false);
            if (_successObjectB != null) _successObjectB.SetActive(false);
            if (_failObjectC != null) _failObjectC.SetActive(false);

            // 퍼즐 초기화
            ResetAllPuzzlePieces();

            // 게임 매니저 초기화
            if (GameManager._Instance != null)
            {
                GameManager._Instance.ResetGame();
            }

            // 레디 패널로 이동
            if (UIManager._Instance != null)
            {
                UIManager._Instance.ShowReadyPanel();
            }

            UpdateTimerDisplay();

            // 패널 감지 상태를 현재 상태와 동기화 (Update에서 StartGame 중복 호출 방지)
            if (_puzzleGamePanel != null)
            {
                _wasPanelActive = _puzzleGamePanel.activeInHierarchy;
            }
        });
    }

    /// <summary>
    /// 다시하기 버튼 - 퍼즐 초기화하고 로봇 티칭과 함께 다시 대결 시작
    /// </summary>
    public void OnRetryButton()
    {
        // 버튼 클릭 사운드
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("ButtonClick");
        }

        // 로봇 티칭 중지
        StopRobotTeaching();

        // 로봇 정리 시작 (터치 방지 패널 활성화)
        StartCleanupWithBlock(() =>
        {
            // 정리 완료 후 실행
            // 결과 오브젝트 비활성화
            if (_resultObjectA != null) _resultObjectA.SetActive(false);
            if (_successObjectB != null) _successObjectB.SetActive(false);
            if (_failObjectC != null) _failObjectC.SetActive(false);

            // 퍼즐 초기화
            ResetAllPuzzlePieces();

            // 타이머 초기화 및 게임 시작 (로봇 티칭과 함께!)
            StartGameAfterCleanup();
        });
    }

    /// <summary>
    /// 게임 시작 (정리 후 다시하기용 - 로봇 티칭과 함께)
    /// </summary>
    private void StartGameAfterCleanup()
    {
        // 로봇 티칭 JSON에서 제한시간 가져오기
        UpdateTimeLimitFromRobot();

        _remainingTime = _timeLimit;
        _placedCount = 0;
        _isGameRunning = true;
        _isGameEnded = false;

        // 결과 오브젝트 확실히 비활성화
        if (_resultObjectA != null) _resultObjectA.SetActive(false);
        if (_successObjectB != null) _successObjectB.SetActive(false);
        if (_failObjectC != null) _failObjectC.SetActive(false);

        // 패널 감지 상태 동기화 (Update에서 StartGame 중복 호출 방지)
        if (_puzzleGamePanel != null)
        {
            _wasPanelActive = _puzzleGamePanel.activeInHierarchy;
        }

        // GameManager 플래그 초기화 (결과창/정리 상태 리셋)
        if (GameManager._Instance != null)
        {
            GameManager._Instance.OnBattleStarted();
        }

        // 대결 BGM 재시작
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayGameBGM();
        }

        UpdateTimerDisplay();

        // 로봇 티칭 시작 (대결 재시작!)
        StartRobotTeaching();
        Debug.Log("[PuzzleManager] 게임 재시작 (로봇 티칭과 함께)");
    }

    /// <summary>
    /// 로봇 정리 시작 + 터치 방지 패널 활성화
    /// </summary>
    private void StartCleanupWithBlock(System.Action onComplete)
    {
        // 터치 방지 패널 활성화
        if (_cleanupBlockPanel != null)
        {
            _cleanupBlockPanel.SetActive(true);
        }

        var robot = FindObjectOfType<LebaiRobotController>();
        if (robot != null)
        {
            // 완료된 티칭만 리버스 정리 (1~N번)
            robot.StartGameCleanup(() =>
            {
                // 정리 완료 - 터치 방지 패널 비활성화
                if (_cleanupBlockPanel != null)
                {
                    _cleanupBlockPanel.SetActive(false);
                }
                onComplete?.Invoke();
            });
        }
        else
        {
            // 로봇 없으면 바로 실행
            if (_cleanupBlockPanel != null)
            {
                _cleanupBlockPanel.SetActive(false);
            }
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 게임 결과 후 자동 정리 시작 (결과창에서)
    /// </summary>
    private void StartAutoCleanup()
    {
        Debug.Log("[StartAutoCleanup] 시작");

        // 정리 시작 즉시 기본 BGM으로 전환
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayDefaultBGM();
        }

        // 타이머 UI 숨기기 (정리 중에는 타이머 비표시)
        if (UIManager._Instance != null)
        {
            UIManager._Instance.HideInactivityTimer();
        }

        // 버튼 비활성화 (정리 중 표시는 숨김 처리)
        if (_resultButtonsObject != null)
        {
            Debug.Log("[StartAutoCleanup] 버튼 비활성화 시도");
            _resultButtonsObject.SetActive(false);
            Debug.Log("[StartAutoCleanup] 버튼 비활성화 완료");
        }
        // _cleanupWaitObject 비활성화 유지 (텍스트 표시 안 함)

        // 정리 진행 텍스트 초기화
        if (_cleanupProgressText != null)
        {
            _cleanupProgressText.text = "정리 중 ...";
            _cleanupProgressText.gameObject.SetActive(true);
        }

        var robot = FindObjectOfType<LebaiRobotController>();
        if (robot != null)
        {
            // 완료된 티칭만 리버스 정리 (1~N번)
            robot.StartGameCleanup(
                () => OnCleanupFinished(),
                (current, total) =>
                {
                    if (_cleanupProgressText != null)
                        _cleanupProgressText.text = $"정리 중 ... ({current}/{total})";
                }
            );
        }
        else
        {
            // 로봇 없으면 가짜 딜레이 후 버튼 활성화 (테스트용)
            StartCoroutine(FakeCleanupDelay());
        }
    }

    /// <summary>
    /// 자동 정리 완료 후 처리
    /// </summary>
    private void OnCleanupFinished()
    {
        // 정리 완료 - 버튼 활성화, 정리 중 표시 비활성화
        if (_resultButtonsObject != null)
        {
            _resultButtonsObject.SetActive(true);
        }
        if (_cleanupWaitObject != null)
        {
            _cleanupWaitObject.SetActive(false);
        }
        if (_cleanupProgressText != null)
        {
            _cleanupProgressText.gameObject.SetActive(false);
        }

        // 타이머 UI 다시 보이기 (결과창 타이머 시작)
        if (UIManager._Instance != null)
        {
            UIManager._Instance.ShowInactivityTimer();
        }

        Debug.Log("[PuzzleManager] 자동 정리 완료 - 결과창 타이머 시작");

        // GameManager에 결과창 타이머 시작 알림
        if (GameManager._Instance != null)
        {
            GameManager._Instance.StartResultPanelTimer();
        }
    }

    /// <summary>
    /// 가짜 정리 딜레이 (로봇 없을 때 테스트용)
    /// </summary>
    private IEnumerator FakeCleanupDelay()
    {
        Debug.Log($"[PuzzleManager] 가짜 정리 시작 ({_fakeCleanupDelay}초 대기)");

        yield return new WaitForSeconds(_fakeCleanupDelay);

        Debug.Log("[PuzzleManager] 가짜 정리 완료");

        // 정리 완료 처리
        OnCleanupFinished();
    }

    /// <summary>
    /// 모든 퍼즐 조각 초기화 (슬롯에서 빼고 원래 위치로)
    /// </summary>
    private void ResetAllPuzzlePieces()
    {
        // 슬롯 초기화
        PuzzleSlot[] slots = FindObjectsByType<PuzzleSlot>(FindObjectsSortMode.None);
        foreach (PuzzleSlot slot in slots)
        {
            slot.ResetSlot();
        }

        // 퍼즐 조각들을 원래 컨테이너로 복귀
        foreach (PuzzlePiece piece in _puzzlePieces)
        {
            piece.transform.SetParent(_puzzleContainer);
            piece.ResetPiece();
        }

        // 퍼즐 다시 셔플
        _placedCount = 0;
        ShufflePuzzlePieces();
    }

    /// <summary>
    /// 강제 초기화 (GameManager 무입력 타이머 등에 의해 강제 복귀 시 호출)
    /// 로봇 중지 + 게임 상태 초기화
    /// </summary>
    public void EmergencyStop()
    {
        _isGameRunning = false;
        _isGameEnded = true;
        _waitingForRobotToFinish = false;

        // 로봇 중지
        StopRobotTeaching();

        // 결과/정리 UI 숨김
        if (_resultObjectA != null) _resultObjectA.SetActive(false);
        if (_successObjectB != null) _successObjectB.SetActive(false);
        if (_failObjectC != null) _failObjectC.SetActive(false);
        if (_cleanupProgressText != null) _cleanupProgressText.gameObject.SetActive(false);
        if (_cleanupBlockPanel != null) _cleanupBlockPanel.SetActive(false);

        Debug.Log("[PuzzleManager] EmergencyStop - 로봇 중지 및 상태 초기화");
    }
}

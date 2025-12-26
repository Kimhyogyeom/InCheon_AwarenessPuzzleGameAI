using UnityEngine;
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

        if (_shuffleOnStart)
        {
            ShufflePuzzlePieces();
        }

        UpdateTimerDisplay();
    }

    /// <summary>
    /// 게임 시작 (타이머 시작)
    /// </summary>
    public void StartGame()
    {
        _remainingTime = _timeLimit;
        _placedCount = 0;
        _isGameRunning = true;
        _isGameEnded = false;

        // 결과 오브젝트 확실히 비활성화
        if (_resultObjectA != null) _resultObjectA.SetActive(false);
        if (_successObjectB != null) _successObjectB.SetActive(false);
        if (_failObjectC != null) _failObjectC.SetActive(false);

        UpdateTimerDisplay();
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
        UpdateTimerDisplay();

        // 시간 초과 체크
        if (_remainingTime <= 0f)
        {
            _remainingTime = 0f;
            OnTimeUp();
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
    /// 시간 초과 시 호출
    /// </summary>
    private void OnTimeUp()
    {
        if (_isGameEnded) return;

        _isGameRunning = false;
        _isGameEnded = true;

        Debug.Log("시간 초과! 실패!");

        // 실패 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("Lose");
        }

        // A + C 활성화 (실패)
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
    /// 퍼즐 완성 시 호출
    /// </summary>
    private void OnPuzzleCompleted()
    {
        if (_isGameEnded) return;

        _isGameRunning = false;
        _isGameEnded = true;

        Debug.Log("퍼즐 완성! 성공!");

        // 성공 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("Win");
        }

        // A + B 활성화 (성공)
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

        // 타이머 정지
        _isGameRunning = false;
        _isGameEnded = false;
        _remainingTime = _timeLimit;
        _placedCount = 0;

        // 패널 감지 상태 초기화 (다시 활성화될 때 타이머 시작하도록)
        _wasPanelActive = false;

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
    }

    /// <summary>
    /// 다시하기 버튼 - 퍼즐만 초기화하고 다시 시작
    /// </summary>
    public void OnRetryButton()
    {
        // 버튼 클릭 사운드
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("ButtonClick");
        }

        // 결과 오브젝트 비활성화
        if (_resultObjectA != null) _resultObjectA.SetActive(false);
        if (_successObjectB != null) _successObjectB.SetActive(false);
        if (_failObjectC != null) _failObjectC.SetActive(false);

        // 퍼즐 초기화
        ResetAllPuzzlePieces();

        // 타이머 초기화 및 게임 시작
        StartGame();
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
}

using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// UI 패널들을 관리하는 매니저 (슬라이드 애니메이션 포함)
/// </summary>
public class UIManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static UIManager _Instance { get; private set; }

    [Header("패널 참조")]
    // 레디 패널 (EASY/HARD 선택 화면)
    [SerializeField] private RectTransform _readyPanel;

    // 설명 패널 (AI 설명 화면)
    [SerializeField] private RectTransform _descriptionPanel;

    // 퍼즐 패널 (퍼즐 설명 화면)
    [SerializeField] private RectTransform _puzzlePanel;

    // 퍼즐 게임 패널 (실제 게임 플레이 화면)
    [SerializeField] private RectTransform _puzzleGamePanel;

    [Header("Description 패널 UI")]
    // AI 설명 텍스트
    [SerializeField] private TextMeshProUGUI _descriptionText;

    [Header("Inactivity Timer UI")]
    // 타이머 이미지 (게임 플레이 중에만 활성화)
    [SerializeField] private GameObject _inactivityTimerImage;

    // 상단 타이머 텍스트
    [SerializeField] private TextMeshProUGUI _inactivityTimerText;

    [Header("슬라이드 설정")]
    // 애니메이션 지속 시간
    [SerializeField] private float _slideDuration = 0.5f;

    // 슬라이드 거리 (화면 너비, Inspector에서 수동 설정 가능)
    [SerializeField] private float _slideDistance = 1920f;

    // 현재 활성화된 패널
    private RectTransform _currentPanel;

    // 애니메이션 진행 중 여부
    private bool _isAnimating = false;

    void Awake()
    {
        // 싱글톤 설정
        if (_Instance == null)
        {
            _Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 모든 패널 초기 위치 설정 (화면 오른쪽 밖으로)
        InitializePanelPositions();

        // 타이머 이미지 초기 비활성화 (게임 플레이 중에만 활성화)
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(false);
        }

        // 초기 상태: 레디 패널만 활성화
        ShowReadyPanel();
    }

    /// <summary>
    /// 모든 패널 초기 위치 설정
    /// </summary>
    private void InitializePanelPositions()
    {
        // 모든 패널 비활성화 및 위치 초기화
        if (_readyPanel != null)
        {
            _readyPanel.anchoredPosition = new Vector2(_slideDistance, 0);
            _readyPanel.gameObject.SetActive(false);
        }

        if (_descriptionPanel != null)
        {
            _descriptionPanel.anchoredPosition = new Vector2(_slideDistance, 0);
            _descriptionPanel.gameObject.SetActive(false);
        }

        if (_puzzlePanel != null)
        {
            _puzzlePanel.anchoredPosition = new Vector2(_slideDistance, 0);
            _puzzlePanel.gameObject.SetActive(false);
        }

        if (_puzzleGamePanel != null)
        {
            _puzzleGamePanel.anchoredPosition = new Vector2(_slideDistance, 0);
            _puzzleGamePanel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 패널 슬라이드 애니메이션 (앞으로 가기)
    /// </summary>
    private IEnumerator SlidePanel(RectTransform panelOut, RectTransform panelIn)
    {
        _isAnimating = true;

        float elapsed = 0f;

        // 시작 위치 설정
        Vector2 outStartPos = Vector2.zero;
        Vector2 outEndPos = new Vector2(-_slideDistance, 0);   // 왼쪽으로 나감

        Vector2 inStartPos = new Vector2(_slideDistance, 0);   // 오른쪽에서 시작
        Vector2 inEndPos = Vector2.zero;

        // 들어오는 패널 활성화
        if (panelIn != null)
        {
            panelIn.gameObject.SetActive(true);
            panelIn.anchoredPosition = inStartPos;
        }

        while (elapsed < _slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _slideDuration;

            // EaseOutCubic 이징 함수 적용 (부드러운 감속)
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            // 나가는 패널 이동
            if (panelOut != null)
                panelOut.anchoredPosition = Vector2.Lerp(outStartPos, outEndPos, easedT);

            // 들어오는 패널 이동
            if (panelIn != null)
                panelIn.anchoredPosition = Vector2.Lerp(inStartPos, inEndPos, easedT);

            yield return null;
        }

        // 최종 위치 보정
        if (panelOut != null)
        {
            panelOut.anchoredPosition = outEndPos;
            panelOut.gameObject.SetActive(false);  // 나간 패널 비활성화
        }

        if (panelIn != null)
            panelIn.anchoredPosition = inEndPos;

        _currentPanel = panelIn;
        _isAnimating = false;
    }

    /// <summary>
    /// 패널 슬라이드 애니메이션 (뒤로 가기 - 역방향)
    /// </summary>
    private IEnumerator SlidePanelReverse(RectTransform panelOut, RectTransform panelIn)
    {
        _isAnimating = true;

        Debug.Log($"[UI] SlidePanelReverse 시작 - Out: {panelOut?.name}, In: {panelIn?.name}");

        float elapsed = 0f;

        // 뒤로가기: 반대 방향
        Vector2 outStartPos = Vector2.zero;
        Vector2 outEndPos = new Vector2(_slideDistance, 0);    // 오른쪽으로 나감

        Vector2 inStartPos = new Vector2(-_slideDistance, 0);  // 왼쪽에서 시작
        Vector2 inEndPos = Vector2.zero;

        // 들어오는 패널 활성화
        if (panelIn != null)
        {
            Debug.Log($"[UI] {panelIn.name} 활성화 및 위치 설정: {inStartPos}");
            panelIn.gameObject.SetActive(true);
            panelIn.anchoredPosition = inStartPos;
        }
        else
        {
            Debug.LogError("[UI] panelIn이 null입니다!");
        }

        while (elapsed < _slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _slideDuration;

            // EaseOutCubic 이징 함수 적용 (부드러운 감속)
            float easedT = 1f - Mathf.Pow(1f - t, 3f);

            // 나가는 패널 이동
            if (panelOut != null)
                panelOut.anchoredPosition = Vector2.Lerp(outStartPos, outEndPos, easedT);

            // 들어오는 패널 이동
            if (panelIn != null)
                panelIn.anchoredPosition = Vector2.Lerp(inStartPos, inEndPos, easedT);

            yield return null;
        }

        // 최종 위치 보정
        if (panelOut != null)
        {
            Debug.Log($"[UI] {panelOut.name} 비활성화");
            panelOut.anchoredPosition = outEndPos;
            panelOut.gameObject.SetActive(false);  // 나간 패널 비활성화
        }

        if (panelIn != null)
        {
            Debug.Log($"[UI] {panelIn.name} 최종 위치: {inEndPos}");
            panelIn.anchoredPosition = inEndPos;
        }

        _currentPanel = panelIn;
        _isAnimating = false;
        Debug.Log($"[UI] SlidePanelReverse 완료 - 현재 패널: {_currentPanel?.name}");
    }

    /// <summary>
    /// 레디 패널 표시 (EASY/HARD 선택)
    /// </summary>
    public void ShowReadyPanel()
    {
        if (_isAnimating) return;

        // 기본 BGM 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayDefaultBGM();
        }

        // 타이머 이미지 비활성화
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(false);
        }

        if (_currentPanel == null)
        {
            // 최초 실행 시 바로 표시
            if (_readyPanel != null)
            {
                _readyPanel.gameObject.SetActive(true);
                _readyPanel.anchoredPosition = Vector2.zero;
                _currentPanel = _readyPanel;
            }
        }
        else
        {
            StartCoroutine(SlidePanel(_currentPanel, _readyPanel));
        }

        // GameManager에게 정리 완료 알림 (OnHomeButton에서 호출될 때를 대비)
        if (GameManager._Instance != null)
        {
            GameManager._Instance.OnCleanupComplete();
        }
    }

    /// <summary>
    /// Description 패널 표시 (AI 설명)
    /// </summary>
    public void ShowDescriptionPanel(string description)
    {
        if (_isAnimating) return;

        // 기본 BGM 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayDefaultBGM();
        }

        // 타이머 이미지 활성화 (입력 감지 시작)
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(true);
        }

        if (_descriptionText != null)
            _descriptionText.text = description;

        StartCoroutine(SlidePanel(_currentPanel, _descriptionPanel));
    }

    /// <summary>
    /// 퍼즐 패널 표시 (퍼즐 설명)
    /// </summary>
    public void ShowPuzzlePanel()
    {
        if (_isAnimating) return;

        // 기본 BGM 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayDefaultBGM();
        }

        // 타이머 이미지 활성화 (입력 감지 시작)
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(true);
        }

        StartCoroutine(SlidePanel(_currentPanel, _puzzlePanel));
    }

    /// <summary>
    /// 퍼즐 게임 패널 표시 (실제 게임 플레이)
    /// </summary>
    public void ShowPuzzleGamePanel()
    {
        if (_isAnimating) return;

        // 인게임 BGM 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayGameBGM();
        }

        // 타이머 이미지 활성화 (게임 플레이 시작)
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(true);
        }

        StartCoroutine(SlidePanel(_currentPanel, _puzzleGamePanel));
    }

    /// <summary>
    /// 타이머 텍스트 업데이트
    /// </summary>
    public void UpdateInactivityTimerText(int seconds)
    {
        if (_inactivityTimerText != null)
        {
            _inactivityTimerText.text = seconds.ToString();
        }
    }

    /// <summary>
    /// 타이머 UI 보이기
    /// </summary>
    public void ShowInactivityTimer()
    {
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(true);
        }
    }

    /// <summary>
    /// 타이머 UI 숨기기
    /// </summary>
    public void HideInactivityTimer()
    {
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 게임 플레이 중인지 확인
    /// </summary>
    public bool IsInGamePlay()
    {
        // 슬라이드 애니메이션 중에도 정확히 판단하기 위해 activeInHierarchy 체크
        return _puzzleGamePanel != null && _puzzleGamePanel.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 현재 Ready 패널인지 확인
    /// </summary>
    public bool IsReadyPanel()
    {
        return _readyPanel != null && _readyPanel.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// Description 패널 표시 (뒤로가기 전용 - 역방향 슬라이드)
    /// </summary>
    public void ShowDescriptionPanelReverse(string description)
    {
        if (_isAnimating)
        {
            Debug.Log("[UI] 애니메이션 진행 중 - 뒤로가기 무시");
            return;
        }

        Debug.Log($"[UI] 뒤로가기 시작 - Current: {_currentPanel?.name}, Target: {_descriptionPanel?.name}");

        // 타이머 이미지 활성화 (입력 감지 시작)
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(true);
        }

        if (_descriptionText != null)
            _descriptionText.text = description;

        StartCoroutine(SlidePanelReverse(_currentPanel, _descriptionPanel));
    }

    /// <summary>
    /// 강제로 Ready 패널만 표시 (모든 패널 초기화)
    /// </summary>
    public void ForceShowReadyPanel()
    {
        // 애니메이션 중단
        StopAllCoroutines();
        _isAnimating = false;

        // 타이머 이미지 비활성화
        if (_inactivityTimerImage != null)
        {
            _inactivityTimerImage.SetActive(false);
        }

        // 모든 패널 비활성화 및 위치 초기화
        if (_descriptionPanel != null)
        {
            _descriptionPanel.gameObject.SetActive(false);
            _descriptionPanel.anchoredPosition = new Vector2(_slideDistance, 0);
        }
        if (_puzzlePanel != null)
        {
            _puzzlePanel.gameObject.SetActive(false);
            _puzzlePanel.anchoredPosition = new Vector2(_slideDistance, 0);
        }
        if (_puzzleGamePanel != null)
        {
            _puzzleGamePanel.gameObject.SetActive(false);
            _puzzleGamePanel.anchoredPosition = new Vector2(_slideDistance, 0);
        }

        // Ready 패널만 활성화
        if (_readyPanel != null)
        {
            _readyPanel.gameObject.SetActive(true);
            _readyPanel.anchoredPosition = Vector2.zero;
            _currentPanel = _readyPanel;
        }

        // GameManager에게 정리 완료 알림
        if (GameManager._Instance != null)
        {
            GameManager._Instance.OnCleanupComplete();
        }
    }
}

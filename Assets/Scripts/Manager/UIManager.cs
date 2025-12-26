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
    /// 패널 슬라이드 애니메이션
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
    /// 레디 패널 표시 (EASY/HARD 선택)
    /// </summary>
    public void ShowReadyPanel()
    {
        if (_isAnimating) return;

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
    }

    /// <summary>
    /// Description 패널 표시 (AI 설명)
    /// </summary>
    public void ShowDescriptionPanel(string description)
    {
        if (_isAnimating) return;

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

        StartCoroutine(SlidePanel(_currentPanel, _puzzlePanel));
    }

    /// <summary>
    /// 퍼즐 게임 패널 표시 (실제 게임 플레이)
    /// </summary>
    public void ShowPuzzleGamePanel()
    {
        if (_isAnimating) return;

        StartCoroutine(SlidePanel(_currentPanel, _puzzleGamePanel));
    }
}

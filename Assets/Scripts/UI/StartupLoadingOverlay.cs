using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 앱 최초 시작 시 로봇 Init 완료까지 검정 오버레이 자동 표시
/// 모든 UI를 코드로 자동 생성 - Inspector 연결 불필요
/// </summary>
public class StartupLoadingOverlay : MonoBehaviour
{
    [Header("설정 (선택사항 - 기본값으로 자동 동작)")]
    [SerializeField] private float _fadeDuration = 1.5f;   // 페이드 아웃 시간
    [SerializeField] private float _spinSpeed = 220f;      // 스피너 회전 속도
    [SerializeField] private int _blinkCount = 4;          // 완료 후 깜빡임 횟수

    private CanvasGroup _overlayCanvasGroup;
    private GameObject _spinnerRoot;

    void Awake()
    {
        CreateOverlayUI();
        StartCoroutine(WaitForRobotInit());
    }

    // ─────────────────────────────────────────
    // UI 자동 생성
    // ─────────────────────────────────────────

    private void CreateOverlayUI()
    {
        // 최상위 Canvas 찾기 (없으면 생성)
        Canvas rootCanvas = FindRootCanvas();

        // ── 검정 오버레이 패널 ──
        GameObject overlay = new GameObject("_StartupOverlay");
        overlay.transform.SetParent(rootCanvas.transform, false);

        // 가장 위에 렌더링되도록 마지막 자식으로
        overlay.transform.SetAsLastSibling();

        RectTransform overlayRect = overlay.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = Color.black;

        _overlayCanvasGroup = overlay.AddComponent<CanvasGroup>();
        _overlayCanvasGroup.alpha = 1f;
        _overlayCanvasGroup.blocksRaycasts = true;
        _overlayCanvasGroup.interactable = false;

        // ── 스피너 (점 8개 원형 배치) ──
        _spinnerRoot = new GameObject("_Spinner");
        _spinnerRoot.transform.SetParent(overlay.transform, false);

        RectTransform spinnerRect = _spinnerRoot.AddComponent<RectTransform>();
        spinnerRect.anchoredPosition = Vector2.zero;
        spinnerRect.sizeDelta = new Vector2(80f, 80f);

        int dotCount = 8;
        float radius = 36f;
        float dotSize = 10f;

        for (int i = 0; i < dotCount; i++)
        {
            float angle = i * 360f / dotCount;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);

            GameObject dot = new GameObject($"dot_{i}");
            dot.transform.SetParent(_spinnerRoot.transform, false);

            RectTransform dotRect = dot.AddComponent<RectTransform>();
            dotRect.anchoredPosition = pos;
            dotRect.sizeDelta = new Vector2(dotSize, dotSize);

            Image dotImage = dot.AddComponent<Image>();
            float alpha = Mathf.Lerp(0.15f, 1f, (float)i / (dotCount - 1));
            dotImage.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private Canvas FindRootCanvas()
    {
        // 씬에서 Screen Space Overlay Canvas 우선 탐색
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (var c in canvases)
        {
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }
        // 없으면 아무 루트 캔버스
        foreach (var c in canvases)
        {
            if (c.isRootCanvas)
                return c;
        }

        // 정말 없으면 새로 생성
        GameObject canvasGO = new GameObject("Canvas_Startup");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ─────────────────────────────────────────
    // 로직
    // ─────────────────────────────────────────

    void Update()
    {
        if (_spinnerRoot != null && _spinnerRoot.activeSelf)
        {
            _spinnerRoot.transform.Rotate(0f, 0f, -_spinSpeed * Time.deltaTime);
        }
    }

    private IEnumerator WaitForRobotInit()
    {
        LebaiRobotController robot = null;
        while (robot == null)
        {
            robot = FindObjectOfType<LebaiRobotController>();
            yield return null;
        }

        while (!robot.IsInitComplete)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // 스피너 깜빡이다 사라짐
        yield return StartCoroutine(BlinkAndHideSpinner());

        // 오버레이 페이드 아웃
        yield return StartCoroutine(FadeOut());

        if (_overlayCanvasGroup != null)
            _overlayCanvasGroup.gameObject.SetActive(false);
    }

    private IEnumerator BlinkAndHideSpinner()
    {
        if (_spinnerRoot == null) yield break;

        for (int i = 0; i < _blinkCount; i++)
        {
            _spinnerRoot.SetActive(false);
            yield return new WaitForSeconds(0.12f);
            _spinnerRoot.SetActive(true);
            yield return new WaitForSeconds(0.12f);
        }

        _spinnerRoot.SetActive(false);
    }

    private IEnumerator FadeOut()
    {
        if (_overlayCanvasGroup == null) yield break;

        _overlayCanvasGroup.blocksRaycasts = false; // 페이드 중 터치 허용

        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            _overlayCanvasGroup.alpha = Mathf.Clamp01(1f - elapsed / _fadeDuration);
            yield return null;
        }

        _overlayCanvasGroup.alpha = 0f;
    }
}

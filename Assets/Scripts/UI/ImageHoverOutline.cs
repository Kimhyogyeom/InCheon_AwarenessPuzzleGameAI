using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 이미지에 마우스 호버 시 테두리(아웃라인)가 켜지는 효과
/// </summary>
[RequireComponent(typeof(Image))]
public class ImageHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("아웃라인 설정")]
    // 아웃라인 색상
    [SerializeField] private Color _outlineColor = new Color(1f, 0.5f, 0f, 1f);  // 주황

    // 아웃라인 두께
    [SerializeField] private Vector2 _outlineThickness = new Vector2(3f, 3f);

    [Header("애니메이션")]
    // 페이드 속도
    [SerializeField] private float _fadeSpeed = 10f;

    private UnityEngine.UI.Outline _outline;
    private bool _isHovering = false;
    private float _currentAlpha = 0f;

    void Awake()
    {
        // Outline 컴포넌트 가져오거나 추가
        _outline = GetComponent<Outline>();
        if (_outline == null)
        {
            _outline = gameObject.AddComponent<Outline>();
        }

        _outline.effectColor = new Color(_outlineColor.r, _outlineColor.g, _outlineColor.b, 0f);
        _outline.effectDistance = _outlineThickness;
    }

    void Update()
    {
        // 부드럽게 페이드 인/아웃
        float targetAlpha = _isHovering ? 1f : 0f;
        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, Time.deltaTime * _fadeSpeed);

        _outline.effectColor = new Color(_outlineColor.r, _outlineColor.g, _outlineColor.b, _currentAlpha);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
    }

    void OnDisable()
    {
        // 비활성화 시 호버 상태 리셋
        _isHovering = false;
        _currentAlpha = 0f;
        if (_outline != null)
        {
            _outline.effectColor = new Color(_outlineColor.r, _outlineColor.g, _outlineColor.b, 0f);
        }
    }

    /// <summary>
    /// 아웃라인 색상 변경
    /// </summary>
    public void SetOutlineColor(Color color)
    {
        _outlineColor = color;
    }
}

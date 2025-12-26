using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 버튼에 마우스 호버 시 스케일 확대 효과를 주는 스크립트
/// </summary>
public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // 확대될 스케일 배율 (1.1 = 10% 확대)
    [SerializeField] private float _hoverScale = 1.1f;

    // 애니메이션 속도
    [SerializeField] private float _animationSpeed = 10f;

    // 원래 스케일 저장
    private Vector3 _originalScale;

    // 목표 스케일
    private Vector3 _targetScale;

    void Start()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
    }

    void Update()
    {
        // 부드럽게 목표 스케일로 이동
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * _animationSpeed);
    }

    /// <summary>
    /// 마우스가 버튼 위로 올라왔을 때
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = _originalScale * _hoverScale;
    }

    /// <summary>
    /// 마우스가 버튼에서 벗어났을 때
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = _originalScale;
    }

    void OnDisable()
    {
        // 비활성화 시 원래 스케일로 즉시 리셋
        if (_originalScale != Vector3.zero)
        {
            transform.localScale = _originalScale;
            _targetScale = _originalScale;
        }
    }
}

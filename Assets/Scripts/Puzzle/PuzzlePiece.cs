using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 퍼즐 조각 드래그 처리
/// 누르고 있는 동안 들고, 떼면 내려놓거나 슬롯에 배치
/// </summary>
public class PuzzlePiece : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("퍼즐 설정")]
    // 이 퍼즐의 인덱스 (0~24)
    [SerializeField] private int _puzzleIndex;

    [Header("스케일 설정")]
    // 기본 스케일
    [SerializeField] private float _normalScale = 1f;

    // 들었을 때 스케일
    [SerializeField] private float _pickupScale = 2f;

    [Header("회전 설정")]
    // 들었을 때 Z축 회전 (기울어진 느낌)
    [SerializeField] private float _pickupRotation = 10f;

    [Header("애니메이션 설정")]
    // 부드러운 전환 속도
    [SerializeField] private float _smoothSpeed = 12f;

    [Header("슬롯 감지 설정")]
    // 슬롯 감지 거리
    [SerializeField] private float _snapDistance = 80f;

    [Header("이펙트 설정")]
    // 드롭 시 생성할 UI 이펙트 프리팹 (SpriteAnimationEffect가 붙은 프리팹)
    [SerializeField] private GameObject _dropEffectPrefab;

    // 원래 위치
    private Vector3 _originalPosition;

    // 원래 부모
    private Transform _originalParent;

    // RectTransform 캐싱
    private RectTransform _rectTransform;

    // Canvas (좌표 계산용)
    private Canvas _canvas;
    private Camera _canvasCamera;

    // 들고 있는지 여부
    private bool _isPickedUp = false;

    // 잠금 여부 (슬롯에 배치되면 잠금)
    private bool _isLocked = false;

    // 목표 스케일, 회전
    private Vector3 _targetScale;
    private Quaternion _targetRotation;

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _originalParent = transform.parent;

        if (_canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            _canvasCamera = _canvas.worldCamera;
        }

        _targetScale = Vector3.one * _normalScale;
        _targetRotation = Quaternion.identity;
    }

    void Update()
    {
        if (_isLocked) return;

        // 부드러운 스케일 전환
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * _smoothSpeed);

        // 부드러운 회전 전환
        transform.localRotation = Quaternion.Lerp(transform.localRotation, _targetRotation, Time.deltaTime * _smoothSpeed);

        // 들고 있으면 마우스 따라가기
        if (_isPickedUp)
        {
            FollowMouse();
        }
    }

    /// <summary>
    /// 마우스/터치 누름 - 집기
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isLocked) return;
        PickUp();
    }

    /// <summary>
    /// 마우스/터치 뗌 - 내려놓기
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isLocked) return;
        Drop();
    }

    /// <summary>
    /// 퍼즐 집기
    /// </summary>
    private void PickUp()
    {
        _isPickedUp = true;

        // 원래 위치 저장
        _originalPosition = _rectTransform.anchoredPosition;

        // 목표 스케일, 회전 설정
        _targetScale = Vector3.one * _pickupScale;
        _targetRotation = Quaternion.Euler(0f, 0f, _pickupRotation);

        // 맨 앞으로 가져오기
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// 퍼즐 내려놓기
    /// </summary>
    private void Drop()
    {
        _isPickedUp = false;

        // 슬롯 체크
        PuzzleSlot nearestSlot = FindNearestSlot();
        if (nearestSlot != null && nearestSlot.TryPlacePuzzle(this))
        {
            // 슬롯에 배치 성공 - PuzzleSlot에서 처리함
            return;
        }

        // 슬롯에 못 넣으면 원래 위치로 복귀
        _rectTransform.anchoredPosition = _originalPosition;

        // 실패 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("DropFail");
        }

        // 드롭 이펙트 생성
        SpawnDropEffect();

        // 원래 스케일, 회전으로 복귀
        _targetScale = Vector3.one * _normalScale;
        _targetRotation = Quaternion.identity;
    }

    /// <summary>
    /// 드롭 이펙트 생성 (UI 스프라이트 애니메이션) 
    /// </summary>
    private void SpawnDropEffect()
    {
        if (_dropEffectPrefab == null) return;

        // 이펙트를 퍼즐의 부모에 생성 (퍼즐과 같은 레벨)
        GameObject effect = Instantiate(_dropEffectPrefab, _originalParent);

        // Z축 랜덤 회전 적용
        float effectZ = Random.Range(0f, 360f);
        effect.transform.localRotation = Quaternion.Euler(0f, 0f, effectZ);

        // 퍼즐 위치에 배치
        RectTransform effectRect = effect.GetComponent<RectTransform>();
        if (effectRect != null)
        {
            effectRect.anchoredPosition = _originalPosition;
        }

        // 퍼즐 바로 뒤에 배치 (퍼즐 index 위치에 삽입하면 퍼즐이 뒤로 밀림)
        int myIndex = transform.GetSiblingIndex();
        effect.transform.SetSiblingIndex(myIndex);
    }

    /// <summary>
    /// 가장 가까운 슬롯 찾기
    /// </summary>
    private PuzzleSlot FindNearestSlot()
    {
        PuzzleSlot[] slots = FindObjectsByType<PuzzleSlot>(FindObjectsSortMode.None);
        PuzzleSlot nearest = null;
        float nearestDistance = _snapDistance;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        foreach (PuzzleSlot slot in slots)
        {
            if (slot.IsFilled()) continue;

            // 슬롯의 스크린 위치 계산
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(_canvasCamera, slotRect.position);

            float distance = Vector2.Distance(mousePosition, slotScreenPos);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = slot;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 마우스 따라가기
    /// </summary>
    private void FollowMouse()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _originalParent as RectTransform,
            mousePosition,
            _canvasCamera,
            out localPoint
        );

        _rectTransform.anchoredPosition = localPoint;
    }

    /// <summary>
    /// 퍼즐 인덱스 반환
    /// </summary>
    public int GetPuzzleIndex()
    {
        return _puzzleIndex;
    }

    /// <summary>
    /// 퍼즐 잠금 (슬롯에 배치 후)
    /// </summary>
    public void LockPuzzle()
    {
        _isLocked = true;
        _isPickedUp = false;
        _targetScale = Vector3.one;
        _targetRotation = Quaternion.identity;
    }

    /// <summary>
    /// 원래 위치 설정 (외부에서 호출)
    /// </summary>
    public void SetOriginalPosition(Vector3 position)
    {
        // Awake 전에 호출될 경우 대비
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        _originalPosition = position;
        _rectTransform.anchoredPosition = position;
    }

    /// <summary>
    /// 현재 들고 있는지 확인
    /// </summary>
    public bool IsPickedUp()
    {
        return _isPickedUp;
    }

    /// <summary>
    /// 잠금 여부 확인
    /// </summary>
    public bool IsLocked()
    {
        return _isLocked;
    }

    /// <summary>
    /// 퍼즐 조각 초기화 (잠금 해제, 스케일/회전 복구)
    /// </summary>
    public void ResetPiece()
    {
        _isLocked = false;
        _isPickedUp = false;
        _targetScale = Vector3.one * _normalScale;
        _targetRotation = Quaternion.identity;
        transform.localScale = Vector3.one * _normalScale;
        transform.localRotation = Quaternion.identity;
    }
}
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 퍼즐 슬롯 - 퍼즐 조각을 받는 곳
/// </summary>
public class PuzzleSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("슬롯 설정")]
    // 이 슬롯의 정답 인덱스 (0~24)
    [SerializeField] private int _correctIndex;

    // 하위의 FakeImage (정답 시 비활성화)
    [SerializeField] private GameObject _fakeImage;

    [Header("배치 오프셋")]
    // 퍼즐 안착 시 위치 오프셋
    [SerializeField] private Vector2 _placementOffset = new Vector2(0f, -15f);

    // 이미 맞춘 퍼즐이 있는지
    private bool _isFilled = false;

    // 현재 호버 중인 퍼즐
    private PuzzlePiece _hoveringPiece;

    /// <summary>
    /// 슬롯 인덱스 반환
    /// </summary>
    public int GetCorrectIndex()
    {
        return _correctIndex;
    }

    /// <summary>
    /// 슬롯이 비어있는지 확인
    /// </summary>
    public bool IsFilled()
    {
        return _isFilled;
    }

    /// <summary>
    /// 퍼즐 조각이 슬롯 위에 들어왔을 때
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 드래그 중인 오브젝트 확인
        if (eventData.pointerDrag != null)
        {
            _hoveringPiece = eventData.pointerDrag.GetComponent<PuzzlePiece>();
        }
    }

    /// <summary>
    /// 퍼즐 조각이 슬롯에서 나갔을 때
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _hoveringPiece = null;
    }

    /// <summary>
    /// 퍼즐 조각이 드롭되었을 때 (IDropHandler)
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // 이미 채워졌으면 무시
        if (_isFilled) return;

        PuzzlePiece piece = eventData.pointerDrag?.GetComponent<PuzzlePiece>();
        if (piece == null) return;

        // 정답 체크
        if (piece.GetPuzzleIndex() == _correctIndex)
        {
            // 정답!
            PlacePuzzle(piece);
        }
    }

    /// <summary>
    /// 퍼즐 배치 시도 (외부에서 호출)
    /// </summary>
    public bool TryPlacePuzzle(PuzzlePiece piece)
    {
        if (_isFilled) return false;
        if (piece.GetPuzzleIndex() != _correctIndex) return false;

        PlacePuzzle(piece);
        return true;
    }

    /// <summary>
    /// 퍼즐을 슬롯에 배치
    /// </summary>
    private void PlacePuzzle(PuzzlePiece piece)
    {
        _isFilled = true;

        // 퍼즐을 슬롯의 자식으로 이동
        piece.transform.SetParent(transform);

        // 퍼즐 위치를 슬롯 중앙 + 오프셋으로
        RectTransform pieceRect = piece.GetComponent<RectTransform>();
        pieceRect.anchoredPosition = _placementOffset;

        // 퍼즐 스케일/회전 초기화
        piece.transform.localScale = Vector3.one;
        piece.transform.localRotation = Quaternion.identity;

        // 퍼즐 잠금 (더 이상 움직일 수 없게)
        piece.LockPuzzle();

        // FakeImage 비활성화
        if (_fakeImage != null)
        {
            _fakeImage.SetActive(false);
        }

        // 퍼즐 완성 체크 이벤트 발생
        PuzzleManager manager = FindFirstObjectByType<PuzzleManager>();
        if (manager != null)
        {
            SoundManager.Instance.PlaySFX("DropSuccess");
            manager.OnPiecePlaced();
        }
    }

    /// <summary>
    /// 슬롯 초기화 (퍼즐 제거, FakeImage 활성화)
    /// </summary>
    public void ResetSlot()
    {
        _isFilled = false;
        _hoveringPiece = null;

        // FakeImage 다시 활성화
        if (_fakeImage != null)
        {
            _fakeImage.SetActive(true);
        }
    }
}

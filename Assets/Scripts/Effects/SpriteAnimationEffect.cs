using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Image용 스프라이트 프레임 애니메이션 이펙트
/// 지정된 스프라이트들을 순서대로 재생 후 자동 삭제
/// </summary>
public class SpriteAnimationEffect : MonoBehaviour
{
    [Header("애니메이션 설정")]
    // 애니메이션에 사용할 스프라이트 배열
    [SerializeField] private Sprite[] _sprites;

    // 프레임 간 시간 (초) - 작을수록 빠름
    [SerializeField] private float _frameTime = 0.05f;

    // 애니메이션 종료 후 자동 삭제
    [SerializeField] private bool _destroyOnComplete = true;

    // 루프 여부
    [SerializeField] private bool _loop = false;

    private Image _image;
    private int _currentFrame = 0;
    private float _timer = 0f;
    private bool _isPlaying = false;

    void Awake()
    {
        _image = GetComponent<Image>();
    }

    void Start()
    {
        // 시작 시 자동 재생
        Play();
    }

    void Update()
    {
        if (!_isPlaying || _sprites == null || _sprites.Length == 0) return;

        _timer += Time.deltaTime;

        if (_timer >= _frameTime)
        {
            _timer = 0f;
            _currentFrame++;

            // 마지막 프레임 도달
            if (_currentFrame >= _sprites.Length)
            {
                if (_loop)
                {
                    _currentFrame = 0;
                }
                else
                {
                    _isPlaying = false;

                    if (_destroyOnComplete)
                    {
                        Destroy(gameObject);
                    }
                    return;
                }
            }

            // 스프라이트 변경
            _image.sprite = _sprites[_currentFrame];
        }
    }

    /// <summary>
    /// 애니메이션 재생
    /// </summary>
    public void Play()
    {
        if (_sprites == null || _sprites.Length == 0) return;

        _currentFrame = 0;
        _timer = 0f;
        _isPlaying = true;

        // 첫 프레임 표시
        _image.sprite = _sprites[0];
    }

    /// <summary>
    /// 애니메이션 정지
    /// </summary>
    public void Stop()
    {
        _isPlaying = false;
    }

    /// <summary>
    /// 스프라이트 배열 설정 (외부에서 동적 설정용)
    /// </summary>
    public void SetSprites(Sprite[] sprites)
    {
        _sprites = sprites;
    }

    /// <summary>
    /// 프레임 속도 설정
    /// </summary>
    public void SetFrameTime(float frameTime)
    {
        _frameTime = frameTime;
    }
}

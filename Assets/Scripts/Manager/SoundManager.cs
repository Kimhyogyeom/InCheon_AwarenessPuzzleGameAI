using UnityEngine;

/// <summary>
/// BGM과 SFX를 관리하는 싱글톤 사운드 매니저
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("오디오 소스")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

    [Header("사운드 데이터")]
    [SerializeField] private SoundData _soundData;

    [Header("시작 BGM")]
    [SerializeField] private AudioClip _startBGM;

    [Header("BGM 이름 설정")]
    [SerializeField] private string _defaultBGMName = "DefaultBGM"; // 기본 BGM (Ready, Description, Puzzle, 결과창)
    [SerializeField] private string _gameBGMName = "GameBGM";       // 인게임 BGM

    [Header("볼륨 설정")]
    [Range(0f, 1f)]
    [SerializeField] private float _bgmVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float _defaultBGMVolume = 1f; // 기본 BGM 볼륨 배율
    [Range(0f, 1f)]
    [SerializeField] private float _gameBGMVolume = 1f;    // 인게임 BGM 볼륨 배율
    [Range(0f, 1f)]
    [SerializeField] private float _sfxVolume = 1f;

    // 음소거 상태
    private bool _isBgmMuted = false;
    private bool _isSfxMuted = false;

    // 현재 재생 중인 BGM 이름 (중복 재생 방지용)
    private string _currentBGMName = "";

    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // AudioSource 없으면 자동 생성
        if (_bgmSource == null)
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
        }

        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
        }

        // 초기 볼륨 적용
        _bgmSource.volume = _bgmVolume;
        _sfxSource.volume = _sfxVolume;

        // 시작 BGM 자동 재생
        if (_startBGM != null)
        {
            PlayBGM(_startBGM);
        }
    }

    #region BGM

    /// <summary>
    /// BGM 재생 (이름으로)
    /// </summary>
    public void PlayBGM(string name)
    {
        PlayBGM(name, 1f);
    }

    /// <summary>
    /// BGM 재생 (이름으로, 볼륨 배율 지정)
    /// </summary>
    public void PlayBGM(string name, float volumeMultiplier)
    {
        if (_soundData == null) return;

        var sound = _soundData.GetBGM(name);
        if (sound == null || sound.clip == null) return;

        _bgmSource.clip = sound.clip;
        _bgmSource.volume = _isBgmMuted ? 0f : _bgmVolume * sound.volume * volumeMultiplier;
        _bgmSource.Play();

        // 현재 재생 중인 BGM 이름 저장
        _currentBGMName = name;
    }

    /// <summary>
    /// BGM 재생 (클립으로)
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        _bgmSource.clip = clip;
        _bgmSource.volume = _isBgmMuted ? 0f : _bgmVolume;
        _bgmSource.Play();
    }

    /// <summary>
    /// BGM 정지
    /// </summary>
    public void StopBGM()
    {
        _bgmSource.Stop();
    }

    /// <summary>
    /// BGM 일시정지
    /// </summary>
    public void PauseBGM()
    {
        _bgmSource.Pause();
    }

    /// <summary>
    /// BGM 재개
    /// </summary>
    public void ResumeBGM()
    {
        _bgmSource.UnPause();
    }

    /// <summary>
    /// BGM 볼륨 설정
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        if (!_isBgmMuted)
        {
            _bgmSource.volume = _bgmVolume;
        }
    }

    /// <summary>
    /// BGM 음소거 토글
    /// </summary>
    public void ToggleBGMMute()
    {
        _isBgmMuted = !_isBgmMuted;
        _bgmSource.volume = _isBgmMuted ? 0f : _bgmVolume;
    }

    /// <summary>
    /// BGM 음소거 설정
    /// </summary>
    public void SetBGMMute(bool mute)
    {
        _isBgmMuted = mute;
        _bgmSource.volume = _isBgmMuted ? 0f : _bgmVolume;
    }

    /// <summary>
    /// 기본 BGM 재생 (Ready, Description, Puzzle, 결과창)
    /// </summary>
    public void PlayDefaultBGM()
    {
        // 이미 같은 BGM이 재생 중이면 무시
        if (_currentBGMName == _defaultBGMName)
        {
            return;
        }

        PlayBGM(_defaultBGMName, _defaultBGMVolume);
    }

    /// <summary>
    /// 인게임 BGM 재생
    /// </summary>
    public void PlayGameBGM()
    {
        // 이미 같은 BGM이 재생 중이면 무시
        if (_currentBGMName == _gameBGMName)
        {
            return;
        }

        PlayBGM(_gameBGMName, _gameBGMVolume);
    }

    #endregion

    #region SFX

    /// <summary>
    /// SFX 재생 (이름으로)
    /// </summary>
    public void PlaySFX(string name)
    {
        if (_soundData == null || _isSfxMuted) return;

        var sound = _soundData.GetSFX(name);
        if (sound == null || sound.clip == null) return;

        _sfxSource.PlayOneShot(sound.clip, _sfxVolume * sound.volume);
    }

    /// <summary>
    /// SFX 재생 (클립으로)
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || _isSfxMuted) return;
        _sfxSource.PlayOneShot(clip, _sfxVolume);
    }

    /// <summary>
    /// SFX 볼륨 설정
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// SFX 음소거 토글
    /// </summary>
    public void ToggleSFXMute()
    {
        _isSfxMuted = !_isSfxMuted;
    }

    /// <summary>
    /// SFX 음소거 설정
    /// </summary>
    public void SetSFXMute(bool mute)
    {
        _isSfxMuted = mute;
    }

    #endregion

    #region Getters

    public float GetBGMVolume() => _bgmVolume;
    public float GetSFXVolume() => _sfxVolume;
    public bool IsBGMMuted() => _isBgmMuted;
    public bool IsSFXMuted() => _isSfxMuted;
    public bool IsBGMPlaying() => _bgmSource.isPlaying;

    #endregion
}

using UnityEngine;

/// <summary>
/// 사운드 데이터 (BGM/SFX 클립 관리)
/// </summary>
[CreateAssetMenu(fileName = "SoundData", menuName = "Game/Sound Data")]
public class SoundData : ScriptableObject
{
    [System.Serializable]
    public class SoundClip
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1f;
    }

    [Header("BGM")]
    public SoundClip[] bgmList;

    [Header("SFX")]
    public SoundClip[] sfxList;

    /// <summary>
    /// 이름으로 BGM 찾기
    /// </summary>
    public SoundClip GetBGM(string name)
    {
        if (bgmList == null) return null;

        foreach (var sound in bgmList)
        {
            if (sound.name == name) return sound;
        }
        return null;
    }

    /// <summary>
    /// 이름으로 SFX 찾기
    /// </summary>
    public SoundClip GetSFX(string name)
    {
        if (sfxList == null) return null;

        foreach (var sound in sfxList)
        {
            if (sound.name == name) return sound;
        }
        return null;
    }
}

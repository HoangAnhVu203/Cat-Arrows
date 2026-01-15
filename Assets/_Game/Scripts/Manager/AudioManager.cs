using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Clips")]
    public AudioClip bgmGameplay;
    public AudioClip sfxMergeBall;
    public AudioClip sfxWin;
    public AudioClip sfxLose;
    public AudioClip sfxButton;
    public AudioClip sfxClick;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        PlayMusic(bgmGameplay);
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (!IsSoundOn()) return;

        sfxSource.PlayOneShot(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (!IsMusicOn()) return;

        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }

    // ---- helper ----

    public void PlaySoundClickLine() => PlaySFX(sfxClick);
    public void PlayMergeBall() => PlaySFX(sfxMergeBall);
    public void PlayWin() => PlaySFX(sfxWin);
    public void PlayLose() => PlaySFX(sfxLose);
    public void PlayButton() => PlaySFX(sfxButton);

    //---------------- SETTING ----------------//
    public static bool IsSoundOn() => PlayerPrefs.GetInt("SOUND_ON", 1) == 1;
    public static bool IsMusicOn() => PlayerPrefs.GetInt("MUSIC_ON", 1) == 1;
    public static bool IsVibrationOn() => PlayerPrefs.GetInt("VIBRATION_ON", 1) == 1;
}
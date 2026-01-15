using UnityEngine;
using UnityEngine.UI;

public enum SettingType { Sound, Music, Vibration }

public class ToggleSetting : MonoBehaviour
{
    public SettingType settingType;

    public Toggle toggle;
    public RectTransform checkmark;
    public Image background;

    public Vector2 leftPos = new Vector2(-30, 0);
    public Vector2 rightPos = new Vector2(200, 0);

    public Color onColor = Color.green;
    public Color offColor = Color.gray;

    void Start()
    {
        toggle.onValueChanged.AddListener(OnToggleChanged);

        // Load trạng thái từ PlayerPrefs
        bool isOn = LoadState();
        toggle.isOn = isOn;

        ApplyVisual(isOn);
    }

    bool LoadState()
    {
        switch (settingType)
        {
            case SettingType.Sound: return PlayerPrefs.GetInt("SOUND_ON", 1) == 1;
            case SettingType.Music: return PlayerPrefs.GetInt("MUSIC_ON", 1) == 1;
            case SettingType.Vibration: return PlayerPrefs.GetInt("VIBRATION_ON", 1) == 1;
        }
        return true;
    }

    void SaveState(bool isOn)
    {
        switch (settingType)
        {
            case SettingType.Sound:
                PlayerPrefs.SetInt("SOUND_ON", isOn ? 1 : 0);
                break;

            case SettingType.Music:
                PlayerPrefs.SetInt("MUSIC_ON", isOn ? 1 : 0);
                if (AudioManager.Instance.musicSource)
                    AudioManager.Instance.musicSource.mute = !isOn;
                break;

            case SettingType.Vibration:
                PlayerPrefs.SetInt("VIBRATION_ON", isOn ? 1 : 0);
                break;
        }
        PlayerPrefs.Save();
    }

    void OnToggleChanged(bool isOn)
    {
        ApplyVisual(isOn);
        SaveState(isOn);
    }

    void ApplyVisual(bool isOn)
    {
        checkmark.anchoredPosition = isOn ? rightPos : leftPos;
        background.color = isOn ? onColor : offColor;
    }
}
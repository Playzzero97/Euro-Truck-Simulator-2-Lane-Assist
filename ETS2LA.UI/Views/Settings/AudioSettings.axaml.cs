using ETS2LA.Audio;
using Avalonia.Controls;
using System.IO;
using System.Threading.Tasks;

namespace ETS2LA.UI.Views.Settings;

public partial class AudioSettings : UserControl
{
    private AudioHandler _audioHandler;
    private bool _soundPlaying = false;
    private bool _skipFirstPlay = true;

    public AudioSettings()
    {
        InitializeComponent();
        _audioHandler = AudioHandler.Current;
        VolumeSlider.Value = _audioHandler.GetVolume() * 100.0;
    }

    private async void OnVolumeChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        float volume = (float)(e.NewValue / 100.0);
        _audioHandler.SetVolume(volume);

        if (_skipFirstPlay)
        {
            _skipFirstPlay = false;
            return;
        }

        if (_soundPlaying)
            return;

        _soundPlaying = true;

        var volumeSound = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "volume.mp3");
        if (File.Exists(volumeSound))
        {
            _audioHandler.Queue(volumeSound, overrideCurrent: true);
        }

        await Task.Delay(750);
        _soundPlaying = false;
    }
}
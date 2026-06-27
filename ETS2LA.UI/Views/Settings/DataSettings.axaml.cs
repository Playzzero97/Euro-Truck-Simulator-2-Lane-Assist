using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using ETS2LA.Game.Data;
using System.Collections;

namespace ETS2LA.UI.Views.Settings;

public partial class DataSettingsPage : UserControl, INotifyPropertyChanged
{
    public ObservableCollection<TabStripItemHandler> DataFidelityOptions { get; } = new();
    public ObservableCollection<TabStripItemHandler> CurveQualityOptions { get; } = new();

    public bool BaseMapNameNeedsRestart { get; set; } = false;
    public bool ForceBaseMapName
    {
        get => DataSettings.Current.ForceBaseMapName;
        set
        {
            if (DataSettings.Current.ForceBaseMapName != value)
            {
                DataSettings.Current.ForceBaseMapName = value;
                DataSettings.Current.Save();
                BaseMapNameNeedsRestart = true;
                OnPropertyChanged(nameof(BaseMapNameNeedsRestart));
            }
        }
    }

    public int SelectedDataFidelityOption { get; set; }
    public int SelectedCurveQualityOption { get; set; }
    public bool ShowRamWarning { get; set; } = false;
    public bool ShowExtremeWarning { get; set; } = false;

    public bool ShowLowInfo { get; set; } = false;
    public bool ShowMediumInfo { get; set; } = false;
    public bool ShowHighInfo { get; set; } = false;
    public bool ShowExtremeInfo { get; set; } = false;

    private float ramAmount;

    public DataSettingsPage()
    {
        LoadDataFidelityOptions();
        LoadCurveQualityOptions();

        ramAmount = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024f * 1024f * 1024f);

        AvaloniaXamlLoader.Load(this);
        DataContext = this;
    }

    private void LoadDataFidelityOptions()
    {
        SelectedDataFidelityOption = (int)DataSettings.Current.DataFidelity;
        foreach (DataFidelity option in Enum.GetValues(typeof(DataFidelity)))
        {
            DataFidelityOptions.Add(new TabStripItemHandler(option.ToString()));
        }
    }

    private void LoadCurveQualityOptions()
    {
        SelectedCurveQualityOption = (int)DataSettings.Current.CurveQuality;
        foreach (CurveQuality option in Enum.GetValues(typeof(CurveQuality)))
        {
            CurveQualityOptions.Add(new TabStripItemHandler(option.ToString()));
        }
    }

    private void OnDataFidelityChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedDataFidelityOption >= 0)
        {
            if (SelectedDataFidelityOption == (int)DataFidelity.High
                && ramAmount < 15)
            {
                ShowRamWarning = true;
                SelectedDataFidelityOption = (int)DataFidelity.Medium;
                OnPropertyChanged(nameof(SelectedDataFidelityOption));
                OnPropertyChanged(nameof(ShowRamWarning));
                return;
            }

            if (SelectedDataFidelityOption == (int)DataFidelity.Extreme
                && ramAmount < 19)
            {
                ShowRamWarning = true;
                SelectedDataFidelityOption = (int)DataFidelity.High;
                OnPropertyChanged(nameof(SelectedDataFidelityOption));
                OnPropertyChanged(nameof(ShowRamWarning));
                return;
            }

            if (SelectedDataFidelityOption == (int)DataFidelity.Extreme)
            {
                ShowExtremeWarning = true;
                OnPropertyChanged(nameof(ShowExtremeWarning));
            }
            else
            {
                ShowExtremeWarning = false;
                OnPropertyChanged(nameof(ShowExtremeWarning));
            }

            switch ((DataFidelity)SelectedDataFidelityOption)
            {
                case DataFidelity.Low:
                    ShowLowInfo = true;
                    ShowMediumInfo = false;
                    ShowHighInfo = false;
                    ShowExtremeInfo = false;
                    break;
                case DataFidelity.Medium:
                    ShowLowInfo = false;
                    ShowMediumInfo = true;
                    ShowHighInfo = false;
                    ShowExtremeInfo = false;
                    break;
                case DataFidelity.High:
                    ShowLowInfo = false;
                    ShowMediumInfo = false;
                    ShowHighInfo = true;
                    ShowExtremeInfo = false;
                    break;
                case DataFidelity.Extreme:
                    ShowLowInfo = false;
                    ShowMediumInfo = false;
                    ShowHighInfo = false;
                    ShowExtremeInfo = true;
                    break;
            }
            
            OnPropertyChanged(nameof(ShowLowInfo));
            OnPropertyChanged(nameof(ShowMediumInfo));
            OnPropertyChanged(nameof(ShowHighInfo));
            OnPropertyChanged(nameof(ShowExtremeInfo));

            DataSettings.Current.DataFidelity = (DataFidelity)SelectedDataFidelityOption;
            DataSettings.Current.Save();
        }
    }

    private void OnCurveQualityChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedCurveQualityOption >= 0)
        {
            DataSettings.Current.CurveQuality = (CurveQuality)SelectedCurveQualityOption;
            DataSettings.Current.Save();
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
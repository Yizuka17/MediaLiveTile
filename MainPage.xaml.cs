using MediaLiveTile.Models;
using MediaLiveTile.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace MediaLiveTile
{
    public sealed partial class MainPage : Page
    {
        private const int FixedTargetSlotCount = 6;
        private const string GitHubUrl = "";

        private readonly ForegroundRefreshService _foregroundRefreshService = ForegroundRefreshService.Current;
        private readonly SecondaryTileService _secondaryTileService = new SecondaryTileService();
        private readonly BitmapImage _defaultPreviewImage = new BitmapImage();

        private bool _settingsLoaded;
        private bool _isApplyingSelections;
        private int _actionStatusVersion;

        private int _smallTileTargetIndex;
        private int _mediumTileTargetIndex;
        private int _wideTileTargetIndex;
        private int _largeTileTargetIndex;

        public ObservableCollection<MediaSessionInfo> VisibleSessions { get; } =
            new ObservableCollection<MediaSessionInfo>();

        public ObservableCollection<TileTargetOption> TargetOptions { get; } =
            new ObservableCollection<TileTargetOption>();

        public MainPage()
        {
            this.InitializeComponent();

            _defaultPreviewImage.UriSource = new Uri("ms-appx:///Assets/Square150x150Logo.png");

            InitializeFixedTargetOptions();

            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
        }

        private async void MainPage_Loaded(object _, RoutedEventArgs __)
        {
            _foregroundRefreshService.StateChanged += ForegroundRefreshService_StateChanged;

            await _foregroundRefreshService.InitializeAsync();

            LoadTileSettings();
            ApplyTargetSelectionsToUi();

            AutoRefreshToggle.IsOn = _foregroundRefreshService.IsAutoRefreshEnabled;

            _settingsLoaded = true;

            if (_foregroundRefreshService.LatestSelectionResult == null)
            {
                await _foregroundRefreshService.RefreshNowAsync();
            }
            else
            {
                UpdateUiFromServiceState();
            }
        }

        private void MainPage_Unloaded(object _, RoutedEventArgs __)
        {
            _foregroundRefreshService.StateChanged -= ForegroundRefreshService_StateChanged;
        }

        private void ForegroundRefreshService_StateChanged(object sender, EventArgs e)
        {
            UpdateUiFromServiceState();
        }

        private async void RefreshButton_Click(object _, RoutedEventArgs __)
        {
            await _foregroundRefreshService.RefreshNowAsync();
        }

        private async void AutoRefreshToggle_Toggled(object _, RoutedEventArgs __)
        {
            if (!_settingsLoaded)
                return;

            _foregroundRefreshService.SetAutoRefreshEnabled(AutoRefreshToggle.IsOn);

            if (AutoRefreshToggle.IsOn)
            {
                await _foregroundRefreshService.RefreshNowAsync();
            }
        }

        private async void SmallTargetComboBox_SelectionChanged(object _, SelectionChangedEventArgs __)
        {
            if (!_settingsLoaded || _isApplyingSelections)
                return;

            _smallTileTargetIndex = GetSelectedTargetIndex(SmallTargetComboBox);
            TileSettingsService.SetSmallTileTargetIndex(_smallTileTargetIndex);

            await RefreshMainTileAndPreviewsAsync();
        }

        private async void MediumTargetComboBox_SelectionChanged(object _, SelectionChangedEventArgs __)
        {
            if (!_settingsLoaded || _isApplyingSelections)
                return;

            _mediumTileTargetIndex = GetSelectedTargetIndex(MediumTargetComboBox);
            TileSettingsService.SetMediumTileTargetIndex(_mediumTileTargetIndex);

            await RefreshMainTileAndPreviewsAsync();
        }

        private async void WideTargetComboBox_SelectionChanged(object _, SelectionChangedEventArgs __)
        {
            if (!_settingsLoaded || _isApplyingSelections)
                return;

            _wideTileTargetIndex = GetSelectedTargetIndex(WideTargetComboBox);
            TileSettingsService.SetWideTileTargetIndex(_wideTileTargetIndex);

            await RefreshMainTileAndPreviewsAsync();
        }

        private async void LargeTargetComboBox_SelectionChanged(object _, SelectionChangedEventArgs __)
        {
            if (!_settingsLoaded || _isApplyingSelections)
                return;

            _largeTileTargetIndex = GetSelectedTargetIndex(LargeTargetComboBox);
            TileSettingsService.SetLargeTileTargetIndex(_largeTileTargetIndex);

            await RefreshMainTileAndPreviewsAsync();
        }

        private async void PinSmallTileButton_Click(object _, RoutedEventArgs __)
        {
            await PinPreviewTileAsync(PinnedTileKind.Small, _smallTileTargetIndex);
        }

        private async void PinMediumTileButton_Click(object _, RoutedEventArgs __)
        {
            await PinPreviewTileAsync(PinnedTileKind.Medium, _mediumTileTargetIndex);
        }

        private async void PinWideTileButton_Click(object _, RoutedEventArgs __)
        {
            await PinPreviewTileAsync(PinnedTileKind.Wide, _wideTileTargetIndex);
        }

        private async void PinLargeTileButton_Click(object _, RoutedEventArgs __)
        {
            await PinPreviewTileAsync(PinnedTileKind.Large, _largeTileTargetIndex);
        }

        private async void OpenLogButton_Click(object _, RoutedEventArgs __)
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    "MediaLiveTile.log",
                    CreationCollisionOption.OpenIfExists);

                var props = await file.GetBasicPropertiesAsync();
                if (props.Size == 0)
                {
                    await FileIO.AppendLinesAsync(file, new[]
                    {
                        "Media Live Tile Log",
                        "===================",
                        ""
                    });
                }

                bool launched = await Launcher.LaunchFileAsync(file);

                if (launched)
                {
                    await ShowTemporaryActionStatusAsync("已打开日志文件");
                }
                else
                {
                    SetActionStatus("无法打开日志文件");
                }
            }
            catch (Exception ex)
            {
                SetActionStatus($"打开日志失败：{ex.Message}");
            }
        }

        private async void AboutAppButton_Click(object _, RoutedEventArgs __)
        {
            if (string.IsNullOrWhiteSpace(GitHubUrl))
            {
                await ShowTemporaryActionStatusAsync("GitHub 链接尚未设置");
                return;
            }

            try
            {
                bool launched = await Launcher.LaunchUriAsync(new Uri(GitHubUrl));
                if (!launched)
                {
                    SetActionStatus("无法打开 GitHub 链接");
                }
            }
            catch (Exception ex)
            {
                SetActionStatus($"打开链接失败：{ex.Message}");
            }
        }

        private void MoreMediaButton_Click(object _, RoutedEventArgs __)
        {
            Frame.Navigate(typeof(AllMediaPage));
        }

        private void InitializeFixedTargetOptions()
        {
            TargetOptions.Clear();

            for (int i = 0; i < FixedTargetSlotCount; i++)
            {
                TargetOptions.Add(new TileTargetOption
                {
                    SessionIndex = i,
                    DisplayText = GetTargetOptionText(i)
                });
            }
        }

        private string GetTargetOptionText(int index)
        {
            if (index == 0)
                return "主媒体";

            return $"次媒体#{index}";
        }

        private void LoadTileSettings()
        {
            _smallTileTargetIndex = NormalizeTargetIndex(TileSettingsService.GetSmallTileTargetIndex());
            _mediumTileTargetIndex = NormalizeTargetIndex(TileSettingsService.GetMediumTileTargetIndex());
            _wideTileTargetIndex = NormalizeTargetIndex(TileSettingsService.GetWideTileTargetIndex());
            _largeTileTargetIndex = NormalizeTargetIndex(TileSettingsService.GetLargeTileTargetIndex());
        }

        private int NormalizeTargetIndex(int index)
        {
            if (index < 0)
                return 0;

            if (index >= FixedTargetSlotCount)
                return FixedTargetSlotCount - 1;

            return index;
        }

        private void ApplyTargetSelectionsToUi()
        {
            _isApplyingSelections = true;

            try
            {
                SmallTargetComboBox.SelectedIndex = _smallTileTargetIndex;
                MediumTargetComboBox.SelectedIndex = _mediumTileTargetIndex;
                WideTargetComboBox.SelectedIndex = _wideTileTargetIndex;
                LargeTargetComboBox.SelectedIndex = _largeTileTargetIndex;
            }
            finally
            {
                _isApplyingSelections = false;
            }
        }

        private int GetSelectedTargetIndex(ComboBox comboBox)
        {
            return comboBox.SelectedIndex >= 0 ? comboBox.SelectedIndex : 0;
        }

        private void UpdateUiFromServiceState()
        {
            var result = _foregroundRefreshService.LatestSelectionResult;

            StatusTextBlock.Text = string.IsNullOrWhiteSpace(_foregroundRefreshService.StatusText)
                ? "等待刷新"
                : _foregroundRefreshService.StatusText;

            if (_foregroundRefreshService.LastRefreshTime.HasValue)
            {
                LastRefreshTextBlock.Text = $"上次刷新：{_foregroundRefreshService.LastRefreshTime.Value:HH:mm:ss}";
            }
            else
            {
                LastRefreshTextBlock.Text = "上次刷新：--";
            }

            VisibleSessions.Clear();

            if (result != null)
            {
                int visibleCount = Math.Min(result.Count, 4);
                for (int i = 0; i < visibleCount; i++)
                {
                    VisibleSessions.Add(result.AllSessions[i]);
                }

                PrimaryMediaTextBlock.Text = BuildSummary("主媒体", result.PrimaryMedia);
                SecondaryMediaTextBlock.Text = BuildSummary("次媒体", result.SecondaryMedia);
                MoreMediaButton.Visibility = result.Count > 4
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                PrimaryMediaTextBlock.Text = "主媒体：无";
                SecondaryMediaTextBlock.Text = "次媒体：无";
                MoreMediaButton.Visibility = Visibility.Collapsed;
            }

            UpdatePreviewsFromCurrentSelection();
        }

        private async Task RefreshMainTileAndPreviewsAsync()
        {
            UpdatePreviewsFromCurrentSelection();
            await _foregroundRefreshService.UpdateTilesOnlyAsync();
        }

        private async Task PinPreviewTileAsync(PinnedTileKind kind, int targetIndex)
        {
            try
            {
                if (_secondaryTileService.Exists(kind, targetIndex))
                {
                    await ShowTemporaryActionStatusAsync("该磁贴已固定");
                    return;
                }

                string targetText = GetTargetOptionText(targetIndex);
                bool created = await _secondaryTileService.RequestCreateAsync(kind, targetIndex, targetText);

                if (!created)
                {
                    SetActionStatus("已取消固定磁贴");
                    return;
                }

                string tileId = _secondaryTileService.CreateTileId(kind, targetIndex);
                await _foregroundRefreshService.UpdateOneSecondaryTileAsync(tileId, targetIndex);

                await ShowTemporaryActionStatusAsync("已固定磁贴（默认显示为中磁贴，可在开始菜单手动调整大小）");
            }
            catch (Exception ex)
            {
                SetActionStatus($"固定磁贴失败：{ex.Message}");
            }
        }

        private string BuildSummary(string label, MediaSessionInfo info)
        {
            if (info == null)
                return $"{label}：无";

            return $"{label}：{info.SourceDisplayName} - {info.Title}";
        }

        private MediaSessionInfo ResolveMediaByIndex(int index)
        {
            var all = _foregroundRefreshService.LatestSelectionResult?.AllSessions;

            if (all == null || all.Count == 0)
                return null;

            if (index < 0 || index >= all.Count)
                return null;

            return all[index];
        }

        private void UpdatePreviewsFromCurrentSelection()
        {
            UpdateSmallPreview(ResolveMediaByIndex(_smallTileTargetIndex));
            UpdateMediumPreview(ResolveMediaByIndex(_mediumTileTargetIndex));
            UpdateWidePreview(ResolveMediaByIndex(_wideTileTargetIndex));
            UpdateLargePreview(ResolveMediaByIndex(_largeTileTargetIndex));
        }

        private void UpdateSmallPreview(MediaSessionInfo media)
        {
            SetCoverPreview(
                SmallPreviewImage,
                SmallPreviewPlaceholderText,
                media);
        }

        private void UpdateMediumPreview(MediaSessionInfo media)
        {
            SetCoverPreview(
                MediumCoverPreviewImage,
                MediumCoverPreviewPlaceholderText,
                media);

            ApplyInfoPreview(
                media,
                MediumInfoTitleText,
                MediumInfoArtistText,
                MediumInfoSourceText);
        }

        private void UpdateWidePreview(MediaSessionInfo media)
        {
            SetCoverPreview(
                WidePreviewImage,
                WidePreviewPlaceholderText,
                media);

            if (media == null)
            {
                WideTitleText.Text = "当前无媒体";
                WideArtistText.Text = string.Empty;
                WideSourceText.Text = string.Empty;
                return;
            }

            WideTitleText.Text = GetDisplayTitle(media);
            WideArtistText.Text = GetArtistText(media);
            WideSourceText.Text = GetSourceText(media);
        }

        private void UpdateLargePreview(MediaSessionInfo media)
        {
            SetCoverPreview(
                LargeCoverPreviewImage,
                LargeCoverPreviewPlaceholderText,
                media);

            ApplyInfoPreview(
                media,
                LargeInfoTitleText,
                LargeInfoArtistText,
                LargeInfoSourceText);
        }

        private void SetCoverPreview(
            Image image,
            TextBlock placeholder,
            MediaSessionInfo media)
        {
            var displayImage = media?.DisplayImage ?? _defaultPreviewImage;

            if (displayImage != null)
            {
                image.Source = displayImage;
                placeholder.Visibility = Visibility.Collapsed;
                return;
            }

            image.Source = null;
            placeholder.Text = "♪";
            placeholder.Visibility = Visibility.Visible;
        }

        private void ApplyInfoPreview(
            MediaSessionInfo media,
            TextBlock titleText,
            TextBlock artistText,
            TextBlock sourceText)
        {
            if (media == null)
            {
                titleText.Text = "当前无媒体";
                artistText.Text = string.Empty;
                sourceText.Text = string.Empty;
                return;
            }

            titleText.Text = GetDisplayTitle(media);
            artistText.Text = GetArtistText(media);
            sourceText.Text = GetSourceText(media);
        }

        private string GetDisplayTitle(MediaSessionInfo media)
        {
            if (!string.IsNullOrWhiteSpace(media?.Title) &&
                !string.Equals(media.Title, "无标题", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(media.Title, "读取失败", StringComparison.OrdinalIgnoreCase))
            {
                return media.Title;
            }

            return "当前无媒体";
        }

        private string GetArtistText(MediaSessionInfo media)
        {
            if (media == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(media.Artist) &&
                !string.Equals(media.Artist, "无艺人", StringComparison.OrdinalIgnoreCase))
            {
                return media.Artist;
            }

            return string.Empty;
        }

        private string GetSourceText(MediaSessionInfo media)
        {
            return string.IsNullOrWhiteSpace(media?.SourceDisplayName)
                ? string.Empty
                : media.SourceDisplayName;
        }

        private void SetActionStatus(string text)
        {
            _actionStatusVersion++;
            ActionStatusTextBlock.Text = text;
        }

        private async Task ShowTemporaryActionStatusAsync(string text, int milliseconds = 1800)
        {
            _actionStatusVersion++;
            int currentVersion = _actionStatusVersion;

            ActionStatusTextBlock.Text = text;

            await Task.Delay(milliseconds);

            if (currentVersion == _actionStatusVersion)
            {
                ActionStatusTextBlock.Text = string.Empty;
            }
        }
    }
}
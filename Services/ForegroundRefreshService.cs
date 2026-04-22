using MediaLiveTile.Models;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;

namespace MediaLiveTile.Services
{
    public sealed class ForegroundRefreshService
    {
        private const string AutoRefreshSettingKey = "ForegroundAutoRefreshEnabled";

        private readonly MediaSessionService _mediaSessionService = new MediaSessionService();
        private readonly LiveTileService _liveTileService = new LiveTileService();
        private readonly SecondaryTileService _secondaryTileService = new SecondaryTileService();
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        private bool _initialized;
        private bool _isRefreshing;

        public static ForegroundRefreshService Current { get; } = new ForegroundRefreshService();

        public event EventHandler StateChanged;

        public MediaSelectionResult LatestSelectionResult { get; private set; }

        public string StatusText { get; private set; } = "等待刷新";

        public DateTimeOffset? LastRefreshTime { get; private set; }

        public bool IsAutoRefreshEnabled { get; private set; }

        public bool IsRefreshing => _isRefreshing;

        private ForegroundRefreshService()
        {
            _timer.Interval = TimeSpan.FromSeconds(30);
            _timer.Tick += Timer_Tick;
        }

        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            _initialized = true;

            IsAutoRefreshEnabled = LoadAutoRefreshEnabled();
            if (IsAutoRefreshEnabled)
            {
                _timer.Start();
            }

            await Task.CompletedTask;
        }

        public void SetAutoRefreshEnabled(bool enabled)
        {
            IsAutoRefreshEnabled = enabled;
            SaveAutoRefreshEnabled(enabled);

            if (enabled)
            {
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }

            RaiseStateChanged();
        }

        public async Task RefreshNowAsync()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;
            StatusText = "正在读取系统媒体会话...";
            RaiseStateChanged();

            try
            {
                var result = await _mediaSessionService.GetSelectionAsync();

                LatestSelectionResult = result;
                MediaRuntimeStore.SetSessions(result.AllSessions);

                StatusText = result.Count == 0
                    ? "未检测到媒体会话"
                    : $"已检测到 {result.Count} 个媒体会话";

                LastRefreshTime = DateTimeOffset.Now;

                await UpdateTilesOnlyAsync();
            }
            catch (Exception ex)
            {
                StatusText = $"刷新失败：{ex.Message}";
            }
            finally
            {
                _isRefreshing = false;
                RaiseStateChanged();
            }
        }

        public async Task UpdateTilesOnlyAsync()
        {
            if (LatestSelectionResult == null)
                return;

            int smallTargetIndex = TileSettingsService.GetSmallTileTargetIndex();
            int mediumTargetIndex = TileSettingsService.GetMediumTileTargetIndex();
            int wideTargetIndex = TileSettingsService.GetWideTileTargetIndex();
            int largeTargetIndex = TileSettingsService.GetLargeTileTargetIndex();

            await _liveTileService.UpdateMainTileAsync(
                LatestSelectionResult,
                smallTargetIndex,
                mediumTargetIndex,
                wideTargetIndex,
                largeTargetIndex);

            await UpdateAllSecondaryTilesAsync(LatestSelectionResult);
        }

        public async Task UpdateOneSecondaryTileAsync(string tileId, int targetIndex)
        {
            if (LatestSelectionResult == null)
                return;

            await _liveTileService.UpdateSecondaryTileAsync(
                tileId,
                LatestSelectionResult,
                targetIndex);
        }

        private async Task UpdateAllSecondaryTilesAsync(MediaSelectionResult result)
        {
            var tiles = await _secondaryTileService.GetManagedTilesAsync();

            foreach (var tile in tiles)
            {
                await _liveTileService.UpdateSecondaryTileAsync(
                    tile.TileId,
                    result,
                    tile.TargetIndex);
            }
        }

        private async void Timer_Tick(object _, object __)
        {
            await RefreshNowAsync();
        }

        private bool LoadAutoRefreshEnabled()
        {
            object raw = ApplicationData.Current.LocalSettings.Values[AutoRefreshSettingKey];

            if (raw is bool boolValue)
                return boolValue;

            if (raw is string text && bool.TryParse(text, out bool parsed))
                return parsed;

            return false;
        }

        private void SaveAutoRefreshEnabled(bool enabled)
        {
            ApplicationData.Current.LocalSettings.Values[AutoRefreshSettingKey] = enabled;
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
using MediaLiveTile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.Media.Control;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace MediaLiveTile.Services
{
    public class MediaSessionService
    {
        public async Task<IReadOnlyList<MediaSessionInfo>> GetSessionsAsync()
        {
            return (await GetSelectionAsync()).AllSessions;
        }

        public async Task<MediaSelectionResult> GetSelectionAsync()
        {
            if (!ApiInformation.IsTypePresent("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager"))
            {
                throw new NotSupportedException("当前系统版本不支持 GlobalSystemMediaTransportControlsSessionManager。");
            }

            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = manager.GetSessions();
            var currentSession = manager.GetCurrentSession();

            string currentAppId = null;
            string currentTitle = null;
            string currentArtist = null;

            if (currentSession != null)
            {
                try
                {
                    currentAppId = currentSession.SourceAppUserModelId;
                    var currentProps = await currentSession.TryGetMediaPropertiesAsync();
                    currentTitle = currentProps?.Title;
                    currentArtist = currentProps?.Artist;
                }
                catch
                {
                }
            }

            var rawList = new List<MediaSessionInfo>();

            foreach (var session in sessions)
            {
                string appId = session.SourceAppUserModelId ?? "Unknown";

                try
                {
                    var mediaProps = await session.TryGetMediaPropertiesAsync();
                    var playbackInfo = session.GetPlaybackInfo();
                    var playbackStatus = playbackInfo?.PlaybackStatus
                        ?? GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed;

                    var title = string.IsNullOrWhiteSpace(mediaProps?.Title) ? "无标题" : mediaProps.Title;
                    var artist = string.IsNullOrWhiteSpace(mediaProps?.Artist) ? "无艺人" : mediaProps.Artist;
                    var album = string.IsNullOrWhiteSpace(mediaProps?.AlbumTitle) ? "无专辑" : mediaProps.AlbumTitle;

                    var thumbnailRef = mediaProps?.Thumbnail;
                    var thumbnailResult = await LoadAndCacheThumbnailAsync(thumbnailRef, appId, title, artist);

                    CachedImageResult appIconResult = CachedImageResult.Empty;
                    if (thumbnailResult.Image == null)
                    {
                        appIconResult = await LoadAndCacheAppIconAsync(appId);
                    }

                    var item = new MediaSessionInfo
                    {
                        SourceAppUserModelId = appId,
                        SourceDisplayName = ResolveSourceDisplayName(appId),
                        Title = title,
                        Artist = artist,
                        AlbumTitle = album,
                        PlaybackStatus = playbackStatus.ToString(),
                        IsPlaying = playbackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                        ThumbnailReference = thumbnailRef,
                        ThumbnailImage = thumbnailResult.Image,
                        ThumbnailLocalUri = thumbnailResult.LocalUri,
                        AppIconImage = appIconResult.Image,
                        AppIconLocalUri = appIconResult.LocalUri,
                        HasThumbnail = thumbnailResult.Image != null
                    };

                    item.IsCurrentSession = IsCurrentSession(
                        item.SourceAppUserModelId,
                        item.Title,
                        item.Artist,
                        currentAppId,
                        currentTitle,
                        currentArtist);

                    item.IsMusicPreferred = DetectMusicPreferred(item);
                    item.InfoCompletenessScore = CalculateInfoCompletenessScore(item);

                    rawList.Add(item);
                }
                catch (Exception ex)
                {
                    rawList.Add(new MediaSessionInfo
                    {
                        SourceAppUserModelId = appId,
                        SourceDisplayName = ResolveSourceDisplayName(appId),
                        Title = "读取失败",
                        Artist = ex.Message,
                        AlbumTitle = "",
                        PlaybackStatus = "Error",
                        IsPlaying = false,
                        IsCurrentSession = false,
                        IsMusicPreferred = false,
                        InfoCompletenessScore = 0,
                        ThumbnailReference = null,
                        ThumbnailImage = null,
                        ThumbnailLocalUri = null,
                        AppIconImage = null,
                        AppIconLocalUri = null,
                        HasThumbnail = false
                    });
                }
            }

            var deduped = rawList
                .GroupBy(x => $"{x.SourceAppUserModelId}|{x.Title}|{x.Artist}|{x.AlbumTitle}")
                .Select(g => g
                    .OrderByDescending(x => x.IsPlaying)
                    .ThenByDescending(x => x.IsMusicPreferred)
                    .ThenByDescending(x => x.InfoCompletenessScore)
                    .ThenByDescending(x => x.IsCurrentSession)
                    .ThenByDescending(x => x.HasThumbnail)
                    .First())
                .OrderByDescending(x => x.IsPlaying)
                .ThenByDescending(x => x.IsMusicPreferred)
                .ThenByDescending(x => x.InfoCompletenessScore)
                .ThenByDescending(x => x.IsCurrentSession)
                .ThenByDescending(x => x.HasThumbnail)
                .ThenBy(x => x.SourceDisplayName)
                .ToList();

            for (int i = 0; i < deduped.Count; i++)
            {
                deduped[i].Order = i + 1;

                if (i == 0)
                    deduped[i].Role = "主媒体";
                else if (i == 1)
                    deduped[i].Role = "次媒体";
                else
                    deduped[i].Role = "候选";
            }

            await TryTrimThumbnailCacheAsync();

            return new MediaSelectionResult
            {
                AllSessions = deduped,
                PrimaryMedia = deduped.Count > 0 ? deduped[0] : null,
                SecondaryMedia = deduped.Count > 1 ? deduped[1] : null
            };
        }

        private async Task<CachedImageResult> LoadAndCacheThumbnailAsync(
            IRandomAccessStreamReference thumbnail,
            string appId,
            string title,
            string artist)
        {
            if (thumbnail == null)
                return CachedImageResult.Empty;

            try
            {
                string fileName = $"thumb_{SanitizeFileName(appId)}_{Guid.NewGuid():N}.png";
                return await CacheStreamReferenceAsPngAsync(thumbnail, "SessionThumbCache", fileName);
            }
            catch
            {
                return CachedImageResult.Empty;
            }
        }

        private async Task<CachedImageResult> LoadAndCacheAppIconAsync(string appUserModelId)
        {
            if (string.IsNullOrWhiteSpace(appUserModelId))
                return CachedImageResult.Empty;

            // 1. 先尝试包应用/UWP 的真实应用图标
            try
            {
                var appInfo = AppInfo.GetFromAppUserModelId(appUserModelId);
                if (appInfo != null)
                {
                    var logoRef = appInfo.DisplayInfo.GetLogo(new Size(128, 128));
                    if (logoRef != null)
                    {
                        string fileName = $"appicon_{SanitizeFileName(appUserModelId)}.png";
                        var cached = await CacheStreamReferenceAsPngAsync(logoRef, "AppIconCache", fileName);
                        if (cached.Image != null)
                            return cached;
                    }
                }
            }
            catch
            {
            }

            // 2. 再尝试桌面程序静态映射资源
            var knownAssetResult = await TryLoadKnownAppIconAsync(appUserModelId);
            if (knownAssetResult.Image != null)
                return knownAssetResult;

            return CachedImageResult.Empty;
        }

        private async Task<CachedImageResult> TryLoadKnownAppIconAsync(string appUserModelId)
        {
            string assetUri = GetKnownAppIconUri(appUserModelId);
            if (string.IsNullOrWhiteSpace(assetUri))
                return CachedImageResult.Empty;

            try
            {
                await StorageFile.GetFileFromApplicationUriAsync(new Uri(assetUri));

                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(assetUri);

                return new CachedImageResult
                {
                    Image = bitmap,
                    LocalUri = assetUri
                };
            }
            catch
            {
                return CachedImageResult.Empty;
            }
        }

        private string GetKnownAppIconUri(string appUserModelId)
        {
            if (string.IsNullOrWhiteSpace(appUserModelId))
                return null;

            var id = appUserModelId.ToLowerInvariant();

            // 网易云 UWP / 打包应用
            if (id.StartsWith("1f8b0f94.122165ae053f")
                || id.Contains("netease")
                || id.Contains("cloudmusic"))
            {
                return "ms-appx:///Assets/AppIcons/netease.png";
            }

            // 常见桌面程序
            switch (id)
            {
                case "msedge.exe":
                    return "ms-appx:///Assets/AppIcons/msedge.png";

                case "chrome.exe":
                    return "ms-appx:///Assets/AppIcons/chrome.png";

                case "firefox.exe":
                    return "ms-appx:///Assets/AppIcons/firefox.png";

                default:
                    return null;
            }
        }

        private async Task<CachedImageResult> CacheStreamReferenceAsPngAsync(
            IRandomAccessStreamReference streamReference,
            string folderName,
            string fileName)
        {
            if (streamReference == null)
                return CachedImageResult.Empty;

            using (var stream = await streamReference.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    folderName,
                    CreationCollisionOption.OpenIfExists);

                var file = await folder.CreateFileAsync(
                    fileName,
                    CreationCollisionOption.ReplaceExisting);

                using (var output = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    await encoder.FlushAsync();
                }

                softwareBitmap.Dispose();

                string localUri = $"ms-appdata:///local/{folderName}/{file.Name}";

                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(localUri);

                return new CachedImageResult
                {
                    Image = bitmap,
                    LocalUri = localUri
                };
            }
        }

        private async Task TryTrimThumbnailCacheAsync()
        {
            try
            {
                await TrimFolderAsync("SessionThumbCache", 80);
                await TrimFolderAsync("AppIconCache", 40);
            }
            catch
            {
            }
        }

        private async Task TrimFolderAsync(string folderName, int keepCount)
        {
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                folderName,
                CreationCollisionOption.OpenIfExists);

            var files = await folder.GetFilesAsync();

            if (files.Count <= keepCount)
                return;

            var toDelete = files
                .OrderByDescending(x => x.DateCreated)
                .Skip(keepCount)
                .ToList();

            foreach (var file in toDelete)
            {
                try
                {
                    await file.DeleteAsync();
                }
                catch
                {
                }
            }
        }

        private string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var chars = value
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray();

            var result = new string(chars);
            if (result.Length > 60)
                result = result.Substring(0, 60);

            return result;
        }

        private bool IsCurrentSession(
            string appId,
            string title,
            string artist,
            string currentAppId,
            string currentTitle,
            string currentArtist)
        {
            if (string.IsNullOrWhiteSpace(currentAppId))
                return false;

            if (!string.Equals(appId, currentAppId, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(currentTitle))
                return true;

            bool sameTitle = string.Equals(Normalize(title), Normalize(currentTitle), StringComparison.OrdinalIgnoreCase);
            bool sameArtist = string.Equals(Normalize(artist), Normalize(currentArtist), StringComparison.OrdinalIgnoreCase);

            return sameTitle || sameArtist;
        }

        private bool DetectMusicPreferred(MediaSessionInfo item)
        {
            var source = $"{item.SourceDisplayName}|{item.SourceAppUserModelId}".ToLowerInvariant();

            if (source.Contains("网易云")
                || source.Contains("netease")
                || source.Contains("cloudmusic")
                || source.Contains("spotify")
                || source.Contains("qqmusic")
                || source.Contains("groove")
                || source.Contains("foobar")
                || source.Contains("musicbee")
                || source.Contains("applemusic")
                || source.Contains("apple music"))
            {
                return true;
            }

            if (HasRealValue(item.Artist, "无艺人"))
                return true;

            if (HasRealValue(item.AlbumTitle, "无专辑"))
                return true;

            return false;
        }

        private int CalculateInfoCompletenessScore(MediaSessionInfo item)
        {
            int score = 0;

            if (HasRealValue(item.Title, "无标题", "读取失败"))
                score += 2;

            if (HasRealValue(item.Artist, "无艺人"))
                score += 2;

            if (HasRealValue(item.AlbumTitle, "无专辑"))
                score += 1;

            if (item.HasThumbnail)
                score += 2;

            return score;
        }

        private bool HasRealValue(string value, params string[] placeholders)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (var p in placeholders)
            {
                if (string.Equals(value, p, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private string ResolveSourceDisplayName(string appUserModelId)
        {
            if (string.IsNullOrWhiteSpace(appUserModelId))
                return "未知来源";

            try
            {
                var appInfo = AppInfo.GetFromAppUserModelId(appUserModelId);
                var displayName = appInfo?.DisplayInfo?.DisplayName;

                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName;
            }
            catch
            {
            }

            if (appUserModelId.StartsWith("1F8B0F94.122165AE053F", StringComparison.OrdinalIgnoreCase))
                return "网易云音乐";

            if (string.Equals(appUserModelId, "msedge.exe", StringComparison.OrdinalIgnoreCase))
                return "Microsoft Edge";

            if (string.Equals(appUserModelId, "chrome.exe", StringComparison.OrdinalIgnoreCase))
                return "Google Chrome";

            return appUserModelId;
        }

        private sealed class CachedImageResult
        {
            public BitmapImage Image { get; set; }

            public string LocalUri { get; set; }

            public static CachedImageResult Empty => new CachedImageResult
            {
                Image = null,
                LocalUri = null
            };
        }
    }
}
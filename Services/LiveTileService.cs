using MediaLiveTile.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace MediaLiveTile.Services
{
    public class LiveTileService
    {
        private const string DefaultImageUri = "ms-appx:///Assets/Square150x150Logo.png";

        public Task UpdateMainTileAsync(
            MediaSelectionResult result,
            int smallTargetIndex,
            int mediumTargetIndex,
            int wideTargetIndex,
            int largeTargetIndex)
        {
            var updater = TileUpdateManager.CreateTileUpdaterForApplication();

            return UpdateTileAsync(
                updater,
                result,
                smallTargetIndex,
                mediumTargetIndex,
                wideTargetIndex,
                largeTargetIndex);
        }

        public Task UpdateSecondaryTileAsync(
            string tileId,
            MediaSelectionResult result,
            int targetIndex)
        {
            var updater = TileUpdateManager.CreateTileUpdaterForSecondaryTile(tileId);

            return UpdateTileAsync(
                updater,
                result,
                targetIndex,
                targetIndex,
                targetIndex,
                targetIndex);
        }

        public void ClearMainTile()
        {
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }

        private async Task UpdateTileAsync(
            TileUpdater updater,
            MediaSelectionResult result,
            int smallTargetIndex,
            int mediumTargetIndex,
            int wideTargetIndex,
            int largeTargetIndex)
        {
            var allSessions = result?.AllSessions;

            var smallItem = await CreateTileItemAsync(ResolveMedia(allSessions, smallTargetIndex));
            var mediumItem = await CreateTileItemAsync(ResolveMedia(allSessions, mediumTargetIndex));
            var wideItem = await CreateTileItemAsync(ResolveMedia(allSessions, wideTargetIndex));
            var largeItem = await CreateTileItemAsync(ResolveMedia(allSessions, largeTargetIndex));

            var coverXml = BuildCoverTileXml(smallItem, mediumItem, wideItem, largeItem);
            var infoXml = BuildInfoTileXml(smallItem, mediumItem, wideItem, largeItem);

            var coverDoc = new XmlDocument();
            coverDoc.LoadXml(coverXml);

            var infoDoc = new XmlDocument();
            infoDoc.LoadXml(infoXml);

            updater.Clear();

            updater.EnableNotificationQueue(false);
            updater.EnableNotificationQueueForSquare150x150(true);
            updater.EnableNotificationQueueForSquare310x310(true);
            updater.EnableNotificationQueueForWide310x150(false);

            // 先封面，后信息
            updater.Update(new TileNotification(coverDoc));
            updater.Update(new TileNotification(infoDoc));
        }

        private MediaSessionInfo ResolveMedia(IReadOnlyList<MediaSessionInfo> allSessions, int index)
        {
            if (allSessions == null || allSessions.Count == 0)
                return null;

            if (index < 0)
                index = 0;

            if (index >= allSessions.Count)
                return null;

            return allSessions[index];
        }

        private Task<TileItem> CreateTileItemAsync(MediaSessionInfo media)
        {
            return Task.FromResult(new TileItem
            {
                ImageUri = GetImageUri(media),
                Title = GetDisplayTitle(media),
                Artist = GetArtist(media),
                Source = GetSource(media)
            });
        }

        private string GetImageUri(MediaSessionInfo media)
        {
            if (!string.IsNullOrWhiteSpace(media?.EffectiveImageUri))
                return media.EffectiveImageUri;

            return DefaultImageUri;
        }

        private string BuildCoverTileXml(
            TileItem small,
            TileItem medium,
            TileItem wide,
            TileItem large)
        {
            return
$@"<tile>
    <visual version='3'>
        {BuildSmallCoverBinding(small)}
        {BuildMediumCoverBinding(medium)}
        {BuildWideStaticBinding(wide)}
        {BuildLargeCoverBinding(large)}
    </visual>
</tile>";
        }

        private string BuildInfoTileXml(
            TileItem small,
            TileItem medium,
            TileItem wide,
            TileItem large)
        {
            return
$@"<tile>
    <visual version='3'>
        {BuildSmallCoverBinding(small)}
        {BuildMediumInfoBinding(medium)}
        {BuildWideStaticBinding(wide)}
        {BuildLargeInfoBinding(large)}
    </visual>
</tile>";
        }

        private string BuildSmallCoverBinding(TileItem item)
        {
            return
$@"<binding template='TileSmall' branding='none'>
    <image src='{EscapeXml(item.ImageUri)}' hint-removeMargin='true'/>
</binding>";
        }

        private string BuildMediumCoverBinding(TileItem item)
        {
            return
$@"<binding template='TileMedium' branding='none'>
    <image src='{EscapeXml(item.ImageUri)}' placement='background'/>
</binding>";
        }

        private string BuildMediumInfoBinding(TileItem item)
        {
            return
$@"<binding template='TileMedium' branding='none'>
    <text hint-style='subtitle' hint-wrap='true' hint-maxLines='2'>{EscapeXml(item.Title)}</text>
    <text hint-style='body' hint-wrap='true' hint-maxLines='2'>{EscapeXml(item.Artist)}</text>
    <text hint-style='captionSubtle' hint-wrap='true' hint-maxLines='1'>{EscapeXml(item.Source)}</text>
</binding>";
        }

        private string BuildWideStaticBinding(TileItem item)
        {
            return
$@"<binding template='TileWide' branding='none'>
    <group>
        <subgroup hint-weight='50'>
            <image src='{EscapeXml(item.ImageUri)}' hint-removeMargin='true'/>
        </subgroup>
        <subgroup hint-weight='50'>
            <text hint-style='subtitle' hint-wrap='true' hint-maxLines='2'>{EscapeXml(item.Title)}</text>
            <text hint-style='body' hint-wrap='true' hint-maxLines='2'>{EscapeXml(item.Artist)}</text>
            <text hint-style='captionSubtle' hint-wrap='true' hint-maxLines='1'>{EscapeXml(item.Source)}</text>
        </subgroup>
    </group>
</binding>";
        }

        private string BuildLargeCoverBinding(TileItem item)
        {
            return
$@"<binding template='TileLarge' branding='none'>
    <image src='{EscapeXml(item.ImageUri)}' placement='background'/>
</binding>";
        }

        private string BuildLargeInfoBinding(TileItem item)
        {
            return
$@"<binding template='TileLarge' branding='none'>
    <text hint-style='title' hint-wrap='true' hint-maxLines='3'>{EscapeXml(item.Title)}</text>
    <text hint-style='body' hint-wrap='true' hint-maxLines='2'>{EscapeXml(item.Artist)}</text>
    <text hint-style='captionSubtle' hint-wrap='true' hint-maxLines='1'>{EscapeXml(item.Source)}</text>
</binding>";
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

        private string GetArtist(MediaSessionInfo media)
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

        private string GetSource(MediaSessionInfo media)
        {
            return string.IsNullOrWhiteSpace(media?.SourceDisplayName)
                ? string.Empty
                : media.SourceDisplayName;
        }

        private string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private class TileItem
        {
            public string ImageUri { get; set; }
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Source { get; set; }
        }
    }
}
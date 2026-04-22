using MediaLiveTile.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.StartScreen;

namespace MediaLiveTile.Services
{
    public class SecondaryTileService
    {
        private const string TileIdPrefix = "MediaLiveTile.Pinned.";

        private const string Square150LogoUri = "ms-appx:///Assets/Square150x150Logo.png";
        private const string Square71LogoUri = "ms-appx:///Assets/Square71x71Logo.png";
        private const string Wide310LogoUri = "ms-appx:///Assets/Wide310x150Logo.png";
        private const string Square310LogoUri = "ms-appx:///Assets/Square310x310Logo.png";

        public string CreateTileId(PinnedTileKind kind, int targetIndex)
        {
            return $"{TileIdPrefix}{kind.ToString().ToLowerInvariant()}.{targetIndex}";
        }

        public bool Exists(PinnedTileKind kind, int targetIndex)
        {
            return SecondaryTile.Exists(CreateTileId(kind, targetIndex));
        }

        public async Task<bool> RequestCreateAsync(PinnedTileKind kind, int targetIndex, string targetDisplayText)
        {
            string tileId = CreateTileId(kind, targetIndex);

            string displayName = $"{GetKindDisplayName(kind)} - {targetDisplayText}";
            string arguments = $"mlt;kind={kind.ToString().ToLowerInvariant()};target={targetIndex}";

            var tile = new SecondaryTile(tileId)
            {
                DisplayName = displayName,
                Arguments = arguments,
                RoamingEnabled = false
            };

            // 如果你的 SDK 支持 ShortName，这样写没问题；不支持就删掉这行
            tile.ShortName = displayName;

            tile.VisualElements.BackgroundColor = Colors.Transparent;
            tile.VisualElements.Square150x150Logo = new Uri(Square150LogoUri);
            tile.VisualElements.Square71x71Logo = new Uri(Square71LogoUri);
            tile.VisualElements.Wide310x150Logo = new Uri(Wide310LogoUri);
            tile.VisualElements.Square310x310Logo = new Uri(Square310LogoUri);

            tile.VisualElements.ShowNameOnSquare150x150Logo = false;
            tile.VisualElements.ShowNameOnWide310x150Logo = false;
            tile.VisualElements.ShowNameOnSquare310x310Logo = false;

            return await tile.RequestCreateAsync();
        }

        public async Task<IReadOnlyList<ManagedSecondaryTileInfo>> GetManagedTilesAsync()
        {
            var allTiles = await SecondaryTile.FindAllAsync();
            var result = new List<ManagedSecondaryTileInfo>();

            foreach (var tile in allTiles)
            {
                var info = ParseManagedTile(tile);
                if (info != null)
                {
                    result.Add(info);
                }
            }

            return result;
        }

        private ManagedSecondaryTileInfo ParseManagedTile(SecondaryTile tile)
        {
            if (tile == null || string.IsNullOrWhiteSpace(tile.Arguments))
                return null;

            if (!tile.Arguments.StartsWith("mlt;", StringComparison.OrdinalIgnoreCase))
                return null;

            string kindText = null;
            string targetText = null;

            var parts = tile.Arguments.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("kind=", StringComparison.OrdinalIgnoreCase))
                {
                    kindText = part.Substring("kind=".Length);
                }
                else if (part.StartsWith("target=", StringComparison.OrdinalIgnoreCase))
                {
                    targetText = part.Substring("target=".Length);
                }
            }

            if (!Enum.TryParse(kindText, true, out PinnedTileKind kind))
                return null;

            if (!int.TryParse(targetText, out int targetIndex))
                return null;

            return new ManagedSecondaryTileInfo
            {
                TileId = tile.TileId,
                Kind = kind,
                TargetIndex = targetIndex
            };
        }

        private string GetKindDisplayName(PinnedTileKind kind)
        {
            switch (kind)
            {
                case PinnedTileKind.Small:
                    return "小磁贴";
                case PinnedTileKind.Medium:
                    return "中磁贴";
                case PinnedTileKind.Wide:
                    return "宽磁贴";
                case PinnedTileKind.Large:
                    return "大磁贴";
                default:
                    return "磁贴";
            }
        }
    }
}
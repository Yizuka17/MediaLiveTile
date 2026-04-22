using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace MediaLiveTile.Models
{
    public class MediaSessionInfo
    {
        public int Order { get; set; }

        public string SourceAppUserModelId { get; set; }

        public string SourceDisplayName { get; set; }

        public string Title { get; set; }

        public string Artist { get; set; }

        public string AlbumTitle { get; set; }

        public string PlaybackStatus { get; set; }

        public bool HasThumbnail { get; set; }

        public bool IsPlaying { get; set; }

        public bool IsCurrentSession { get; set; }

        public bool IsMusicPreferred { get; set; }

        public bool IsMusicLike
        {
            get => IsMusicPreferred;
            set => IsMusicPreferred = value;
        }

        public int InfoCompletenessScore { get; set; }

        public int MetadataCompletenessScore
        {
            get => InfoCompletenessScore;
            set => InfoCompletenessScore = value;
        }

        public string Role { get; set; }

        public IRandomAccessStreamReference ThumbnailReference { get; set; }

        // 封面
        public BitmapImage ThumbnailImage { get; set; }
        public string ThumbnailLocalUri { get; set; }

        // 应用图标兜底
        public BitmapImage AppIconImage { get; set; }
        public string AppIconLocalUri { get; set; }

        // 预览统一显示图：封面优先，其次应用图标
        public BitmapImage DisplayImage => ThumbnailImage ?? AppIconImage;

        public string EffectiveImageUri =>
            !string.IsNullOrWhiteSpace(ThumbnailLocalUri)
                ? ThumbnailLocalUri
                : AppIconLocalUri;

        // 只要封面或应用图标任意一种存在，就不再显示“无封面”
        public bool HasAnyDisplayImage =>
            DisplayImage != null || !string.IsNullOrWhiteSpace(EffectiveImageUri);

        public string ThumbnailPlaceholderText => HasAnyDisplayImage ? "" : "无封面";

        public string DisplayTitle => $"#{Order} {SourceDisplayName}";
        public string TitleLine => $"标题：{Title}";
        public string ArtistLine => $"艺人：{Artist}";
        public string AlbumLine => $"专辑：{AlbumTitle}";
        public string PlaybackStatusLine => $"状态：{PlaybackStatus}";
        public string ThumbnailLine => HasThumbnail ? "封面：有" : "封面：无";

        public string RoleLine => $"角色：{Role}";
        public string MusicPreferredLine => $"音乐类优先：{(IsMusicPreferred ? "是" : "否")}";
        public string InfoScoreLine => $"信息完整度：{InfoCompletenessScore}";
        public string CurrentSessionLine => $"当前系统会话：{(IsCurrentSession ? "是" : "否")}";
        public string RankingLine =>
            $"排序因子：播放中={(IsPlaying ? "是" : "否")} / 音乐类={(IsMusicPreferred ? "是" : "否")} / 信息完整={InfoCompletenessScore} / 当前会话={(IsCurrentSession ? "是" : "否")}";
    }
}
using System.Collections.Generic;

namespace MediaLiveTile.Models
{
    public class MediaSelectionResult
    {
        public IReadOnlyList<MediaSessionInfo> AllSessions { get; set; }

        // 兼容旧属性名
        public IReadOnlyList<MediaSessionInfo> AllMedia
        {
            get => AllSessions;
            set => AllSessions = value;
        }

        public MediaSessionInfo PrimaryMedia { get; set; }

        public MediaSessionInfo SecondaryMedia { get; set; }

        public int Count => AllSessions?.Count ?? 0;
    }
}
using MediaLiveTile.Models;
using System.Collections.Generic;
using System.Linq;

namespace MediaLiveTile.Services
{
    public static class MediaRuntimeStore
    {
        private static List<MediaSessionInfo> _allSessions = new List<MediaSessionInfo>();

        public static IReadOnlyList<MediaSessionInfo> AllSessions => _allSessions;

        public static void SetSessions(IEnumerable<MediaSessionInfo> sessions)
        {
            _allSessions = sessions?.ToList() ?? new List<MediaSessionInfo>();
        }
    }
}
using MediaLiveTile.Models;
using System.Linq;
using System.Threading.Tasks;

namespace MediaLiveTile.Services
{
    public class MediaSelectionService
    {
        private readonly MediaSessionService _mediaSessionService = new MediaSessionService();

        public async Task<MediaSelectionResult> GetSelectionAsync()
        {
            var allMedia = (await _mediaSessionService.GetSessionsAsync()).ToList();

            for (int i = 0; i < allMedia.Count; i++)
            {
                if (i == 0)
                    allMedia[i].Role = "主媒体";
                else if (i == 1)
                    allMedia[i].Role = "次媒体";
                else
                    allMedia[i].Role = "候选媒体";
            }

            return new MediaSelectionResult
            {
                AllMedia = allMedia,
                PrimaryMedia = allMedia.FirstOrDefault(),
                SecondaryMedia = allMedia.Skip(1).FirstOrDefault()
            };
        }
    }
}
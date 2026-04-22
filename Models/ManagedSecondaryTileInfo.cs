namespace MediaLiveTile.Models
{
    public class ManagedSecondaryTileInfo
    {
        public string TileId { get; set; }

        public PinnedTileKind Kind { get; set; }

        public int TargetIndex { get; set; }
    }
}
namespace MediaLiveTile.Models
{
    public class TileTargetOption
    {
        public int SessionIndex { get; set; }

        public string DisplayText { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
namespace Cthangover.Tools.RpgObjectEditor
{
    using System.Text.Json.Nodes;

    /// <summary>The drag's supplied coordinates are written by the same single synchronous parent edit transaction.</summary>
    internal sealed class RpgAnchorChange
    {
        private readonly float[] anchor;
        internal RpgAnchorChange(float[] anchor)
        {
            this.anchor = anchor;
        }

        internal void Apply(JsonObject data)
        {
            data["Anchor"] = RpgObjectNumbers.ToNode(anchor);
        }
    }
}

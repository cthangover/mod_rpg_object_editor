namespace Cthangover.Tools.RpgObjectEditor
{
    using System.Text;

    /// <summary>
    /// The id of a variant made from a typed name ("Make a variant"): the mod, a slash and an ASCII slug of the name.
    /// A name without Latin letters or digits gets the parent's last id segment plus <c>_variant</c>. Pure C#.
    /// </summary>
    public static class RpgObjectVariant
    {
        /// <summary>The local part of the id: lowercase Latin letters and digits, other runs become one underscore.</summary>
        public static string LocalId(string name, string parentId)
        {
            var slug = new StringBuilder();
            var pendingSeparator = false;
            foreach (var c in (name ?? string.Empty).Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (pendingSeparator && slug.Length > 0)
                        slug.Append('_');
                    slug.Append(c);
                    pendingSeparator = false;
                }
                else
                {
                    pendingSeparator = true;
                }
            }
            return slug.Length > 0 ? slug.ToString() : LastSegment(parentId ?? string.Empty) + "_variant";
        }

        /// <summary>The wanted id of a variant of <paramref name="parentId"/> in <paramref name="modId"/> (the caller frees it).</summary>
        public static string IdFor(string modId, string name, string parentId) => modId + "/" + LocalId(name, parentId);

        private static string LastSegment(string id)
        {
            var at = id.LastIndexOf('/');
            return at < 0 ? id : id.Substring(at + 1);
        }
    }
}

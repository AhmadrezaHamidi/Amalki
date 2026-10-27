namespace Amlak.Web.Domain;

/// <summary>Photo order of a listing (ADM-09). The first photo is the cover: cards, og:image, channel posts.</summary>
public static class PhotoOrder
{
    public enum Move { First, Earlier, Later }

    /// <summary>Moves one photo and renumbers SortOrder 0..n-1 in the new order.</summary>
    /// <returns>False when the photo is not in the list.</returns>
    public static bool Apply(IList<PropertyPhoto> photos, int photoId, Move move)
    {
        var ordered = photos.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        var i = ordered.FindIndex(x => x.Id == photoId);
        if (i < 0) return false;
        var to = move switch
        {
            Move.First => 0,
            Move.Earlier => Math.Max(0, i - 1),
            _ => Math.Min(ordered.Count - 1, i + 1),
        };
        var photo = ordered[i];
        ordered.RemoveAt(i);
        ordered.Insert(to, photo);
        for (var n = 0; n < ordered.Count; n++) ordered[n].SortOrder = n;
        return true;
    }
}

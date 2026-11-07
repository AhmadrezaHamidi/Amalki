using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Read;

/// <summary>Read side for consultants: the public team page, the listing page's consultant card and the contact form.</summary>
public class ConsultantQueries(AppDbContext db)
{
    /// <summary>Visible consultants in the order staff set.</summary>
    public Task<List<Consultant>> ActiveAsync(CancellationToken ct = default) =>
        db.Consultants.AsNoTracking().Where(c => c.Active).OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync(ct);

    public Task<Consultant?> ActiveByIdAsync(int? id, CancellationToken ct = default) => id is null
        ? Task.FromResult<Consultant?>(null)
        : db.Consultants.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.Active, ct);

    /// <summary>
    /// Who answers for a listing in <paramref name="city"/>: the first consultant who names that city,
    /// else the first one who covers every city. The team is a handful of rows, so it is filtered in memory.
    /// </summary>
    public async Task<Consultant?> ForCityAsync(string city, CancellationToken ct = default) => Pick(await ActiveAsync(ct), city);

    public static Consultant? Pick(IEnumerable<Consultant> team, string city)
    {
        var list = team.ToList();
        var normalized = PersianText.Normalize(city);
        return list.FirstOrDefault(c => c.CityList.Contains(normalized)) ?? list.FirstOrDefault(c => c.CityList.Count == 0);
    }
}

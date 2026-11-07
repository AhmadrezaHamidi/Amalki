namespace Amlak.Domain;

/// <summary>
/// A real estate consultant (مشاور املاک) visitors can call or ask for advice. Each one covers some cities;
/// a listing page shows the consultant who covers its city, so a call reaches someone who knows the area.
/// </summary>
public class Consultant : AggregateRoot
{
    public const int MaxName = 80, MaxTitle = 80, MaxBio = 600, MaxCities = 200, MaxSpecialty = 120;

    private Consultant() { } // EF Core

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    /// <summary>Job title shown under the name, e.g. «مشاور ارشد فروش».</summary>
    public string Title { get; private set; } = "";
    public string Phone { get; private set; } = "";
    /// <summary>What they know best, e.g. «آپارتمان مسکونی شمال تهران».</summary>
    public string? Specialty { get; private set; }
    public string? Bio { get; private set; }
    /// <summary>Cities they cover, separated by «،». Empty means every city.</summary>
    public string Cities { get; private set; } = "";
    public int YearsOfExperience { get; private set; }
    public string? PhotoUrl { get; private set; }
    public bool Active { get; private set; } = true;
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Consultant Create(ConsultantProfile profile, DateTime nowUtc)
    {
        var c = new Consultant { CreatedAtUtc = nowUtc };
        c.Apply(profile);
        return c;
    }

    public void Update(ConsultantProfile profile) => Apply(profile);

    public void Activate() => Active = true;

    /// <summary>Hidden from the site; leads already sent to them stay.</summary>
    public void Deactivate() => Active = false;

    public IReadOnlyList<string> CityList =>
        Cities.Split(['،', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>True when they cover <paramref name="city"/> by name, or cover every city.</summary>
    public bool Covers(string city)
    {
        var list = CityList;
        return list.Count == 0 || list.Contains(PersianText.Normalize(city));
    }

    private void Apply(ConsultantProfile p)
    {
        var name = PersianText.Normalize(p.Name);
        var title = PersianText.Normalize(p.Title);
        if (name.Length is 0 or > MaxName) throw new DomainException("نام مشاور را وارد کنید (حداکثر ۸۰ نویسه)");
        if (title.Length is 0 or > MaxTitle) throw new DomainException("عنوان شغلی مشاور را وارد کنید (حداکثر ۸۰ نویسه)");
        if (p.YearsOfExperience is < 0 or > 60) throw new DomainException("سابقهٔ کار باید بین ۰ تا ۶۰ سال باشد");
        var bio = Optional(p.Bio);
        if (bio?.Length > MaxBio) throw new DomainException("معرفی حداکثر ۶۰۰ نویسه است");
        var specialty = Optional(p.Specialty);
        if (specialty?.Length > MaxSpecialty) throw new DomainException("تخصص حداکثر ۱۲۰ نویسه است");
        var cities = string.Join("، ", (p.Cities ?? "")
            .Split(['،', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(PersianText.Normalize).Distinct());
        if (cities.Length > MaxCities) throw new DomainException("فهرست شهرها طولانی است");
        var photo = Optional(p.PhotoUrl);
        if (photo is not null && !photo.StartsWith('/') && !photo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("نشانی عکس باید با / یا https:// شروع شود");

        Name = name; Title = title; Phone = p.Phone.Value; Bio = bio; Specialty = specialty;
        Cities = cities; YearsOfExperience = p.YearsOfExperience; PhotoUrl = photo; SortOrder = p.SortOrder;
    }

    private static string? Optional(string? s) => string.IsNullOrWhiteSpace(s) ? null : PersianText.Normalize(s);
}

/// <summary>What staff type in for a consultant; checked by <see cref="Consultant"/>.</summary>
public sealed record ConsultantProfile(
    string Name, string Title, PhoneNumber Phone, string? Cities = null, int YearsOfExperience = 0,
    string? Specialty = null, string? Bio = null, string? PhotoUrl = null, int SortOrder = 0);

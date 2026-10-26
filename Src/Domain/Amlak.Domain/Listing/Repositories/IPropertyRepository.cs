using Amlak.Domain.Listing.Aggregates;

namespace Amlak.Domain.Listing.Repositories;

public interface IPropertyRepository
{
    Task<Property?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<bool>      SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<bool>      IsPublishedAsync(long id, CancellationToken ct = default);
    Task            AddAsync(Property property, CancellationToken ct = default);
    Task            UpdateAsync(Property property, CancellationToken ct = default);
    Task<long>      GetNextIdAsync();
    Task<long>      GetNextPhotoIdAsync();
}

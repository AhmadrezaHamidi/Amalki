using Amlak.Domain.Listing;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Enums;
using Msg = Amlak.Application.Validators.Resources.Message;

namespace Amlak.Application.Validators.Listing;

/// <summary>قواعد مشترک مشخصات آگهی برای ساخت و ویرایش</summary>
public abstract class PropertyDetailsValidator<T> : AbstractValidator<T> where T : IPropertyDetails
{
    /// <summary>بازهٔ معقول سال ساخت شمسی</summary>
    public const int MinYearBuilt = 1300, MaxYearBuilt = 1450;

    protected PropertyDetailsValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage(Msg.TitleRequired).MaximumLength(150).WithMessage(Msg.TitleTooLong);
        RuleFor(x => x.Description).NotEmpty().WithMessage(Msg.DescriptionRequired).MaximumLength(4000).WithMessage(Msg.DescriptionTooLong);
        RuleFor(x => x.Deal).IsInEnum().WithMessage(Msg.DealInvalid);
        RuleFor(x => x.Kind).IsInEnum().WithMessage(Msg.KindInvalid);

        RuleFor(x => x.Price)
            .NotNull().WithMessage(Msg.SalePriceRequired)
            .GreaterThan(0).WithMessage(Msg.SalePriceRequired)
            .When(x => x.Deal == DealType.Sale);

        RuleFor(x => x)
            .Must(x => x.Deposit > 0 || x.MonthlyRent > 0).WithMessage(Msg.RentAmountRequired)
            .WithName(nameof(IPropertyDetails.Deposit))
            .When(x => x.Deal == DealType.Rent);

        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage(Msg.AmountNegative);
        RuleFor(x => x.Deposit).GreaterThanOrEqualTo(0).WithMessage(Msg.AmountNegative);
        RuleFor(x => x.MonthlyRent).GreaterThanOrEqualTo(0).WithMessage(Msg.AmountNegative);

        RuleFor(x => x.AreaM2).InclusiveBetween(1, 1_000_000).WithMessage(Msg.AreaInvalid);
        RuleFor(x => x.Bedrooms).InclusiveBetween(0, 50).WithMessage(Msg.BedroomsInvalid);
        RuleFor(x => x.Floor).InclusiveBetween(-5, 200).WithMessage(Msg.FloorInvalid);
        RuleFor(x => x.Parking).InclusiveBetween(0, 50).WithMessage(Msg.ParkingInvalid);
        RuleFor(x => x.YearBuilt).InclusiveBetween(MinYearBuilt, MaxYearBuilt).WithMessage(Msg.YearBuiltInvalid);

        RuleFor(x => x.City).NotEmpty().WithMessage(Msg.CityRequired).MaximumLength(50).WithMessage(Msg.CityTooLong);
        RuleFor(x => x.District).NotEmpty().WithMessage(Msg.DistrictRequired).MaximumLength(80).WithMessage(Msg.DistrictTooLong);
        RuleFor(x => x.Address).MaximumLength(300).WithMessage(Msg.AddressTooLong);

        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).WithMessage(Msg.LocationInvalid);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).WithMessage(Msg.LocationInvalid);
        RuleFor(x => x)
            .Must(x => x.Latitude.HasValue == x.Longitude.HasValue).WithMessage(Msg.LocationInvalid)
            .WithName(nameof(IPropertyDetails.Latitude));
    }
}

public sealed class CreatePropertyCommandValidator : PropertyDetailsValidator<CreatePropertyCommand>;

public sealed class UpdatePropertyCommandValidator : PropertyDetailsValidator<UpdatePropertyCommand>
{
    public UpdatePropertyCommandValidator()
        => RuleFor(x => x.Id).GreaterThan(0).WithMessage(Msg.IdInvalid);
}

public sealed class AddPropertyPhotoCommandValidator : AbstractValidator<AddPropertyPhotoCommand>
{
    public AddPropertyPhotoCommandValidator()
    {
        RuleFor(x => x.PropertyId).GreaterThan(0).WithMessage(Msg.IdInvalid);
        RuleFor(x => x.FileName).NotEmpty().WithMessage(Msg.PhotoRequired);
        RuleFor(x => x.Content).NotEmpty().WithMessage(Msg.PhotoRequired)
            .Must(c => c.LongLength <= PhotoFile.MaxBytes).WithMessage(Msg.PhotoTooLarge);
        RuleFor(x => x.Alt).MaximumLength(200).WithMessage(Msg.AltTooLong);
    }
}

public sealed class ReorderPropertyPhotosCommandValidator : AbstractValidator<ReorderPropertyPhotosCommand>
{
    public ReorderPropertyPhotosCommandValidator()
        => RuleFor(x => x.PhotoIds).NotEmpty().WithMessage(Msg.PhotoOrderRequired)
               .Must(ids => ids.Count <= Property.MaxPhotos).WithMessage(Msg.PhotoOrderRequired);
}

public sealed class ChangePhotoAltCommandValidator : AbstractValidator<ChangePhotoAltCommand>
{
    public ChangePhotoAltCommandValidator()
        => RuleFor(x => x.Alt).MaximumLength(200).WithMessage(Msg.AltTooLong);
}

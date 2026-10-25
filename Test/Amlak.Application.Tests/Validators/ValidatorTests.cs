using Amlak.Application.Validators.Lead;
using Amlak.Application.Validators.Listing;
using Amlak.Domain.Lead.Enums;
using Amlak.Domain.Listing.Enums;

namespace Amlak.Application.Tests.Validators;

public sealed class ValidatorTests
{
    private static CreatePropertyCommand Valid(DealType deal = DealType.Sale, long? price = 1_000_000_000, long? deposit = null, long? rent = null) => new(
        "آپارتمان", "توضیح", deal, PropertyKind.Apartment, price, deposit, rent,
        90, 2, 3, 1, 1398, "تهران", "ونک", null, null, null);

    [Fact]
    public void Valid_sale_and_rent_pass()
    {
        new CreatePropertyCommandValidator().Validate(Valid()).IsValid.Should().BeTrue();
        new CreatePropertyCommandValidator().Validate(Valid(DealType.Rent, null, 0, 15_000_000)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Sale_needs_price_and_rent_needs_an_amount()
    {
        new CreatePropertyCommandValidator().Validate(Valid(price: null)).Errors.Should().Contain(e => e.PropertyName == "Price");
        new CreatePropertyCommandValidator().Validate(Valid(DealType.Rent, null, null, null)).Errors.Should().Contain(e => e.PropertyName == "Deposit");
    }

    [Fact]
    public void Gregorian_year_and_half_location_are_rejected()
    {
        var r = new CreatePropertyCommandValidator().Validate(Valid() with { YearBuilt = 2020, Latitude = 35.7 });
        r.Errors.Select(e => e.PropertyName).Should().Contain(["YearBuilt", "Latitude"]);
    }

    [Fact]
    public void Update_uses_the_same_rules()
        => new UpdatePropertyCommandValidator().Validate(new UpdatePropertyCommand(5, "", "d", DealType.Sale, PropertyKind.Land,
               1, null, null, 0, null, null, null, null, "تهران", "ونک", null, null, null))
           .Errors.Select(e => e.PropertyName).Should().Contain(["Title", "AreaM2"]);

    [Theory]
    [InlineData("09121234567", true)]
    [InlineData("9121234567", false)]
    [InlineData("0912123456a", false)]
    public void Lead_phone_must_be_an_iranian_mobile(string phone, bool valid)
        => new SubmitLeadCommandValidator().Validate(new SubmitLeadCommand(null, "علی", phone, null)).IsValid.Should().Be(valid);

    [Fact]
    public void Lead_cannot_be_moved_back_to_new()
        => new ChangeLeadStatusCommandValidator().Validate(new ChangeLeadStatusCommand(1, LeadStatus.New, null)).IsValid.Should().BeFalse();
}

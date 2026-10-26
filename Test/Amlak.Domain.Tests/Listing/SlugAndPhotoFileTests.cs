using Amlak.Domain.Listing;
using Amlak.Domain.Listing.Exceptions;

namespace Amlak.Domain.Tests.Listing;

public class SlugAndPhotoFileTests
{
    [Theory]
    [InlineData("آپارتمان ۹۰ متری، ونک!", "آپارتمان-۹۰-متری-ونک")]
    [InlineData("  ويلا كردان  ", "ویلا-کردان")]
    [InlineData("Office 120m2", "office-120m2")]
    public void Slug_keeps_persian_letters_and_normalizes_arabic_forms(string title, string expected)
        => Slug.From(title).Should().Be(expected);

    [Fact]
    public void Slug_of_punctuation_only_is_rejected()
        => FluentActions.Invoking(() => Slug.From("!!! ---")).Should().Throw<InvalidSlugException>();

    [Fact]
    public void Slug_suffix_starts_at_two()
    {
        Slug.WithSuffix("x", 1).Should().Be("x");
        Slug.WithSuffix("x", 2).Should().Be("x-2");
    }

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];
    private static readonly byte[] Png  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0];

    [Fact]
    public void Photo_with_matching_signature_is_accepted()
    {
        PhotoFile.Validate("a.JPEG", Jpeg).Should().Be(".jpg");
        PhotoFile.Validate("a.png", Png).Should().Be(".png");
    }

    [Fact]
    public void Renamed_or_unknown_file_is_rejected()
    {
        FluentActions.Invoking(() => PhotoFile.Validate("evil.jpg", Png)).Should().Throw<InvalidPhotoFileException>();
        FluentActions.Invoking(() => PhotoFile.Validate("a.gif", Jpeg)).Should().Throw<InvalidPhotoFileException>();
        FluentActions.Invoking(() => PhotoFile.Validate("a.jpg", Array.Empty<byte>())).Should().Throw<InvalidPhotoFileException>();
    }

    [Fact]
    public void Photo_over_5mb_is_rejected()
    {
        var big = new byte[PhotoFile.MaxBytes + 1];
        Jpeg.CopyTo(big, 0);
        FluentActions.Invoking(() => PhotoFile.Validate("a.jpg", big)).Should().Throw<PhotoTooLargeException>();
    }
}

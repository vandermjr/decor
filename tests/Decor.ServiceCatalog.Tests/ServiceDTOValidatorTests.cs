using System.ComponentModel.DataAnnotations;
using System.Text;
using Decor.Core.DTOs;
using Decor.Core.Validation;
using Xunit;

namespace Decor.ServiceCatalog.Tests;

public sealed class ServiceDTOValidatorTests
{
    private readonly ServiceDTOValidator _validator = new();

    public static TheoryData<decimal?, bool> MoneyBoundaries => new()
    {
        { null, true },
        { 0m, true },
        { 0.01m, true },
        { 99999999.99m, true },
        { 100000000m, false },
        { decimal.MaxValue, false },
        { -0.01m, false },
        { 0.001m, false },
        { 1.234m, false }
    };

    [Theory]
    [MemberData(nameof(MoneyBoundaries))]
    public void Validate_AppliesMoneyBoundsToEveryField(decimal? value, bool accepted)
    {
        var dto = CreateService();
        Assert.Equal(accepted, !_validator.Validate(dto with { CostPrice = value }).Any());
        Assert.Equal(accepted, !_validator.Validate(dto with { SalePrice = value }).Any());
        Assert.Equal(accepted, !_validator.Validate(dto with { EmployeeCommissionValue = value }).Any());
    }

    [Theory]
    [InlineData(nameof(ServiceDTO.CostPrice))]
    [InlineData(nameof(ServiceDTO.SalePrice))]
    [InlineData(nameof(ServiceDTO.EmployeeCommissionValue))]
    public void MoneyRangeAttribute_MatchesDecimalStorageBounds(string propertyName)
    {
        var range = Assert.IsType<RangeAttribute>(
            Attribute.GetCustomAttribute(typeof(ServiceDTO).GetProperty(propertyName)!, typeof(RangeAttribute)));
        Assert.True(range.IsValid(99999999.99m));
        Assert.False(range.IsValid(100000000m));
        Assert.False(range.IsValid(-0.01m));
    }

    [Fact]
    public void Validate_RejectsObservationsAboveUtf8ByteLimit()
    {
        var observations = new string('\u00e9', 32768);
        Assert.Equal(65536, Encoding.UTF8.GetByteCount(observations));
        Assert.NotEmpty(_validator.Validate(CreateService() with { Observations = observations }));
    }

    [Fact]
    public void Validate_AcceptsObservationsAtUtf8ByteLimit()
    {
        var observations = new string('\u00e9', 32767) + "a";
        Assert.Equal(65535, Encoding.UTF8.GetByteCount(observations));
        Assert.Empty(_validator.Validate(CreateService() with { Observations = observations }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_AcceptsMissingObservations(string? observations)
    {
        Assert.Empty(_validator.Validate(CreateService() with { Observations = observations }));
    }

    private static ServiceDTO CreateService() => new(0, "Service", true, null, null, null, null);
}
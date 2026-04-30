using OrderFlow.SharedKernel;

namespace OrderFlow.Orders.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string Number { get; }
    public string? Complement { get; }
    public string Neighborhood { get; }
    public string City { get; }
    public string State { get; }
    public string ZipCode { get; }
    public string Country { get; }

    private Address(
        string street, string number, string? complement,
        string neighborhood, string city, string state,
        string zipCode, string country)
    {
        Street = street;
        Number = number;
        Complement = complement;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        ZipCode = zipCode;
        Country = country;
    }

    public static Address Create(
        string street, string number, string neighborhood,
        string city, string state, string zipCode,
        string? complement = null, string country = "Brasil")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(neighborhood);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode);

        return new Address(
            street.Trim(), number.Trim(), complement?.Trim(),
            neighborhood.Trim(), city.Trim(), state.Trim(),
            zipCode.Trim().Replace("-", ""), country.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return Number;
        yield return Complement;
        yield return Neighborhood;
        yield return City;
        yield return State;
        yield return ZipCode;
        yield return Country;
    }

    public override string ToString() =>
        $"{Street}, {Number}{(Complement is not null ? $" - {Complement}" : "")}, " +
        $"{Neighborhood}, {City}/{State}, {ZipCode}";
}

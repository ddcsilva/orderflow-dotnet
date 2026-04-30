using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Orders.Domain.Aggregates.OrderAggregate;

namespace OrderFlow.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.OwnsOne(o => o.OrderNumber, on =>
        {
            on.Property(n => n.Value)
                .HasColumnName("OrderNumber")
                .HasMaxLength(20)
                .IsRequired();

            on.HasIndex(n => n.Value).IsUnique();
        });

        builder.OwnsOne(o => o.ShippingAddress, a =>
        {
            a.Property(p => p.Street).HasColumnName("ShippingStreet").HasMaxLength(200).IsRequired();
            a.Property(p => p.Number).HasColumnName("ShippingNumber").HasMaxLength(20).IsRequired();
            a.Property(p => p.Complement).HasColumnName("ShippingComplement").HasMaxLength(100);
            a.Property(p => p.Neighborhood).HasColumnName("ShippingNeighborhood").HasMaxLength(100).IsRequired();
            a.Property(p => p.City).HasColumnName("ShippingCity").HasMaxLength(100).IsRequired();
            a.Property(p => p.State).HasColumnName("ShippingState").HasMaxLength(2).IsRequired();
            a.Property(p => p.ZipCode).HasColumnName("ShippingZipCode").HasMaxLength(8).IsRequired();
            a.Property(p => p.Country).HasColumnName("ShippingCountry").HasMaxLength(50).IsRequired();
        });

        builder.Property(o => o.Status)
            .HasConversion(
                s => s.Value,
                s => OrderStatus.FromString(s))
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(o => o.TotalAmount, m =>
        {
            m.Property(p => p.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2).IsRequired();
            m.Property(p => p.Currency).HasColumnName("TotalCurrency").HasMaxLength(3).IsRequired();
        });

        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();

        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Order.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(o => o.DomainEvents);
    }
}

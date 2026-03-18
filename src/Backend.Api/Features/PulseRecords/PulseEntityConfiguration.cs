using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Features.PulseRecords;

public sealed class PulseEntityConfiguration : IEntityTypeConfiguration<PulseEntity>
{
    public void Configure(EntityTypeBuilder<PulseEntity> builder)
    {
        builder.ToTable("pulse_records");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new PulseId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.ObservedAtUtc).IsRequired();
        builder.Property(x => x.Bpm).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(120).IsRequired();

        builder.HasIndex(x => x.ObservedAtUtc);
    }
}

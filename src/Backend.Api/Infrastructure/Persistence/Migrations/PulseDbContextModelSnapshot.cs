using Backend.Api.Features.PulseRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Backend.Api.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PulseDbContext))]
partial class PulseDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.5")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity<PulseEntity>(b =>
        {
            b.Property<PulseId>("Id")
                .HasColumnType("uuid")
                .HasConversion(new ValueConverter<PulseId, Guid>(
                    v => v.Value,
                    v => new PulseId(v)));

            b.Property<int>("Bpm")
                .HasColumnType("integer");

            b.Property<DateTimeOffset>("ObservedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("Source")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.HasKey("Id");

            b.HasIndex("ObservedAtUtc");

            b.ToTable("pulse_records");
        });
    }
}

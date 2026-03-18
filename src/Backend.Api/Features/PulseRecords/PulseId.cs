namespace Backend.Api.Features.PulseRecords;

public readonly record struct PulseId(Guid Value)
{
    public static PulseId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

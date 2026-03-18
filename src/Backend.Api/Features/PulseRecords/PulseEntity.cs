namespace Backend.Api.Features.PulseRecords;

public sealed class PulseEntity(PulseId id, DateTimeOffset observedAtUtc, int bpm, string source)
{
    public PulseId Id { get; } = id;
    public DateTimeOffset ObservedAtUtc { get; } = observedAtUtc;
    public int Bpm { get; } = bpm;
    public string Source { get; } = source;
}

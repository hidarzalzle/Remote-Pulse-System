namespace Backend.Api.Features.PulseRecords;

public sealed record PulseRecordDto(Guid Id, DateTimeOffset ObservedAtUtc, int Bpm, string Source);

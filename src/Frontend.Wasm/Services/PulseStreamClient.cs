using Microsoft.AspNetCore.SignalR.Client;

namespace Frontend.Wasm.Services;

public sealed record PulseRecordMessage(Guid Id, DateTimeOffset ObservedAtUtc, int Bpm, string Source);

public sealed class PulseStreamClient(ILogger<PulseStreamClient> logger) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<PulseRecordMessage>? PulseReceived;
    public event Action<string>? ConnectionStatusChanged;

    public async Task StartAsync(Uri backendBaseUri, CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync(cancellationToken);
                NotifyStatus(_connection.State);
            }

            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(backendBaseUri, "/hubs/pulse"))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<PulseRecordMessage>("pulse.received", pulse => PulseReceived?.Invoke(pulse));

        _connection.Reconnecting += error =>
        {
            logger.LogWarning(error, "SignalR connection reconnecting.");
            ConnectionStatusChanged?.Invoke("Reconnecting");
            return Task.CompletedTask;
        };

        _connection.Reconnected += connectionId =>
        {
            logger.LogInformation("SignalR connection restored with id {ConnectionId}", connectionId);
            ConnectionStatusChanged?.Invoke("Live");
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            logger.LogWarning(error, "SignalR connection closed.");
            ConnectionStatusChanged?.Invoke("Offline");
            return Task.CompletedTask;
        };

        logger.LogInformation("Connecting SignalR client to {Url}", backendBaseUri);
        ConnectionStatusChanged?.Invoke("Connecting");
        await _connection.StartAsync(cancellationToken);
        NotifyStatus(_connection.State);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync();
        _connection = null;
    }

    private void NotifyStatus(HubConnectionState state)
    {
        ConnectionStatusChanged?.Invoke(state switch
        {
            HubConnectionState.Connected => "Live",
            HubConnectionState.Connecting => "Connecting",
            HubConnectionState.Reconnecting => "Reconnecting",
            _ => "Offline"
        });
    }
}

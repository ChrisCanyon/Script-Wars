using System.Net.Http.Json;
using System.Text.Json;
using ArenaGame.Core;

namespace ArenaGame.Client;

// Talks to the game server: polls /state in the background into a lock-guarded field,
// and posts the player's chosen intention to /action.
sealed class ServerConnection
{
    const string ServerUrl = "http://localhost:5000";

    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    readonly HttpClient _http = new() { BaseAddress = new Uri(ServerUrl) };
    readonly object _lock = new();
    ObservationDto? _latest;

    public void StartPolling() => _ = Task.Run(PollLoop);

    // The most recent observation, or null before the first successful poll.
    public ObservationDto? Latest()
    {
        lock (_lock) return _latest;
    }

    // Submit an intention; true on a 2xx response, false on rejection or transport failure.
    public async Task<bool> Send(Intention intention)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/action", intention, JsonOpts);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    async Task PollLoop()
    {
        while (true)
        {
            try
            {
                var obs = await _http.GetFromJsonAsync<ObservationDto>("/state", JsonOpts);
                if (obs is not null) lock (_lock) _latest = obs;
            }
            catch { /* server starting up; retry */ }
            await Task.Delay(100);
        }
    }
}

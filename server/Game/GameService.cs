using Microsoft.AspNetCore.SignalR;

namespace Cs2D.Server.Game;

public class GameService(IHubContext<GameHub> hub) : BackgroundService
{
    readonly List<Room> rooms = new();
    readonly Dictionary<string, Room> byPlayer = new();
    readonly object gate = new();

    public Room Join(string id, string name)
    {
        lock (gate)
        {
            Leave(id);
            var room = rooms.Where(r => r.HasSpace).OrderByDescending(r => r.Count).FirstOrDefault();
            if (room == null) { room = new Room(); rooms.Add(room); }
            room.Add(id, name); byPlayer[id] = room; return room;
        }
    }
    public void Leave(string id)
    {
        lock (gate)
        {
            if (!byPlayer.Remove(id, out var r)) return;
            r.Remove(id); if (r.Count == 0) rooms.Remove(r);
        }
    }
    public Room? Get(string id) { lock (gate) return byPlayer.GetValueOrDefault(id); }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(33)); int n = 0;
        while (await timer.WaitForNextTickAsync(ct))
        {
            Room[] snap; lock (gate) snap = rooms.ToArray();
            n++;
            foreach (var r in snap)
            {
                r.Tick(0.033f);
                if (n % 2 == 0)
                    foreach (var id in r.HumanIds())
                        if (r.SnapshotFor(id) is { } s) await hub.Clients.User(id).SendAsync("state", s, ct);
            }
        }
    }
}

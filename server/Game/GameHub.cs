using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cs2D.Server.Game;

[Authorize]
public class GameHub(GameService game) : Hub
{
    string Uid => Context.UserIdentifier!;

    public async Task<object> FindMatch()
    {
        var name = Context.User!.FindFirstValue(ClaimTypes.Name)!;
        var room = game.Join(Uid, name);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);
        return new { you = Uid, room = room.Id, map = new { width = GameMap.Width, height = GameMap.Height, walls = GameMap.Walls, sites = GameMap.Sites }, weapons = Weapons.All };
    }
    public void Input(int keys, float angle, bool firing) => game.Get(Uid)?.SetInput(Uid, keys, angle, firing);
    public bool Buy(string weapon) => game.Get(Uid)?.Buy(Uid, weapon) ?? false;
    public void Reload() => game.Get(Uid)?.Reload(Uid);
    public async Task LeaveMatch()
    {
        var r = game.Get(Uid);
        if (r != null) await Groups.RemoveFromGroupAsync(Context.ConnectionId, r.Id);
        game.Leave(Uid);
    }
    public override Task OnDisconnectedAsync(Exception? e) { game.Leave(Uid); return base.OnDisconnectedAsync(e); }
}

namespace Cs2D.Server.Game;

public class Player
{
    public string Id = "", Name = "", Team = "T";
    public int Seq;
    public float X, Y, Angle; public int Hp, Armor, Ammo, Kills, Deaths, Money = 1000;
    public string Weapon = "glock";
    public int Keys; public bool Firing, IsBot;
    public double NextShot, ReloadEnd, RepathAt, SeenSince; public float ActionProg, PathX, PathY;
    public bool Alive => Hp > 0;
}

public record Shot(float X1, float Y1, float X2, float Y2, string Shooter);

/// <summary>One casual bomb-defusal match: 5v5, freeze time, bomb plant/defuse, economy, side swap at halftime.</summary>
public class Room
{
    public const int MaxPlayers = 20, BotTarget = 5, FreezeTime = 10, RoundTime = 115, BuyTime = 45, BombTime = 45, WinRounds = 8, MaxMoney = 10000, PlantMoney = 200;
    public const float Speed = 260, Radius = 16, ViewRange = 900, PlantTime = 3.2f, DefuseTime = 5f; // casual: every CT has a kit
    // key bits: 1=W 2=A 4=S 8=D 16=use(E) 32=walk(shift)
    public string Id = Guid.NewGuid().ToString("N")[..8];
    public Dictionary<string, Player> Players = new();
    public List<Shot> Shots = new();
    readonly List<(double T, string Killer, string KTeam, string Victim, string VTeam, string Weapon)> feed = new();
    public int ScoreT, ScoreCt, RoundNo, LossT, LossCt;
    public string Phase = "freeze", Msg = "";
    double time, phaseEnd, roundStart; bool matchOver;
    bool planted; float bombX, bombY; double bombEnd;
    string? carrier; bool dropped; float dropX, dropY;
    int botSite, botSeq;
    readonly object gate = new();
    static readonly Random Rng = new();

    public int Count { get { lock (gate) return Players.Values.Count(p => !p.IsBot); } }
    public bool HasSpace => Count < MaxPlayers;
    public string[] HumanIds() { lock (gate) return Players.Values.Where(p => !p.IsBot).Select(p => p.Id).ToArray(); }

    // ---------- membership ----------
    Player NewPlayer(string id, string name, bool bot)
    {
        var t = Players.Values.Count(p => p.Team == "T"); var ct = Players.Count - t;
        var p = new Player { Id = id, Name = name, IsBot = bot, Team = t <= ct ? "T" : "CT", Seq = ++botSeq };
        p.Weapon = Weapons.Default(p.Team);
        Spawn(p, RoundNo == 0 || Phase == "freeze");
        Players[id] = p; return p;
    }

    public Player Add(string id, string name)
    {
        lock (gate) { var p = NewPlayer(id, name, false); FillBots(); return p; }
    }

    public void Remove(string id)
    {
        lock (gate)
        {
            if (Players.TryGetValue(id, out var p) && carrier == id) { carrier = null; dropped = true; dropX = p.X; dropY = p.Y; }
            Players.Remove(id); FillBots();
        }
    }

    void FillBots()
    {
        int TeamCount(string t) => Players.Values.Count(p => p.Team == t);
        while (Players.Count > BotTarget)
        {
            var bot = Players.Values.Where(p => p.IsBot).OrderByDescending(p => TeamCount(p.Team)).FirstOrDefault();
            if (bot == null) break;
            if (carrier == bot.Id) { carrier = null; dropped = true; dropX = bot.X; dropY = bot.Y; }
            Players.Remove(bot.Id);
        }
        while (Players.Count < BotTarget) { var n = botSeq + 1; NewPlayer("bot" + n, "Bot " + n, true); }
    }

    // ---------- input ----------
    public void SetInput(string id, int keys, float angle, bool firing)
    {
        lock (gate) if (Players.TryGetValue(id, out var p)) { p.Keys = keys; p.Angle = angle; p.Firing = firing; }
    }

    public void Reload(string id)
    {
        lock (gate)
            if (Players.TryGetValue(id, out var p) && p.Alive && p.Ammo < Weapons.Get(p.Weapon).Mag && p.ReloadEnd == 0)
                p.ReloadEnd = time + Weapons.Get(p.Weapon).Reload;
    }

    public bool Buy(string id, string weaponId)
    {
        lock (gate)
        {
            if (!CanBuy() || !Players.TryGetValue(id, out var p) || !p.Alive) return false;
            var w = Weapons.All.FirstOrDefault(x => x.Id == weaponId);
            if (w == null || w.Price == 0 || !Weapons.Allowed(w, p.Team) || p.Money < w.Price) return false;
            p.Money -= w.Price; p.Weapon = w.Id; p.Ammo = w.Mag; p.ReloadEnd = 0; return true;
        }
    }

    bool CanBuy() => Phase == "freeze" || (Phase == "live" && time - roundStart <= BuyTime);

    // ---------- rounds ----------
    void Spawn(Player p, bool alive)
    {
        var spawns = p.Team == "T" ? GameMap.TSpawns : GameMap.CtSpawns;
        (p.X, p.Y) = spawns[Players.Values.Count(o => o.Team == p.Team && o != p && o.Seq < p.Seq) % spawns.Length];
        p.Hp = alive ? 100 : 0; p.Armor = alive ? 100 : 0; // casual: free armor
        p.Ammo = Weapons.Get(p.Weapon).Mag; p.ReloadEnd = 0; p.ActionProg = 0; p.Keys = 0; p.Firing = false;
    }

    void StartRound(bool fullReset)
    {
        RoundNo++; roundStart = time; Phase = "freeze"; phaseEnd = time + FreezeTime; Msg = "";
        planted = false; dropped = false; carrier = null; botSite = Rng.Next(2);
        foreach (var p in Players.Values)
        {
            if (fullReset) p.Money = 1000;
            if (fullReset || !p.Alive) p.Weapon = Weapons.Default(p.Team);
            Spawn(p, true);
            if (p.IsBot) BotBuy(p);
        }
        var ts = Players.Values.Where(p => p.Team == "T").ToArray();
        if (ts.Length > 0) carrier = ts[Rng.Next(ts.Length)].Id;
    }

    void BotBuy(Player b)
    {
        var order = b.Team == "T" ? new[] { "awp", "ak47", "mp9", "deagle" } : new[] { "awp", "m4a1", "mp9", "deagle" };
        // keep an AWP for rich bots only occasionally
        foreach (var id in order)
        {
            var w = Weapons.Get(id);
            if (id == "awp" && (b.Seq % 3 != 0 || b.Money < w.Price + 1000)) continue;
            if (b.Money >= w.Price && (id != "deagle" || b.Weapon is "glock" or "usp")) { Buy2(b, w); break; }
        }
    }
    void Buy2(Player p, Weapon w) { p.Money -= w.Price; p.Weapon = w.Id; p.Ammo = w.Mag; }

    void SwapSides()
    {
        foreach (var p in Players.Values) { p.Team = p.Team == "T" ? "CT" : "T"; p.Money = 1000; p.Weapon = Weapons.Default(p.Team); p.Hp = 0; }
        (ScoreT, ScoreCt) = (ScoreCt, ScoreT); LossT = LossCt = 0;
    }

    void EndRound(string winner, string msg, bool bombWin = false)
    {
        Phase = "end"; phaseEnd = time + 5; Msg = msg;
        int winBonus = bombWin ? 3500 : 3250;
        bool plantedLoss = planted && winner == "CT";
        if (winner == "T") { ScoreT++; LossCt++; LossT = 0; } else { ScoreCt++; LossT++; LossCt = 0; }
        foreach (var p in Players.Values)
        {
            int add = p.Team == winner ? winBonus : winBonus - 300; // casual: loser gets $300 less than winner
            p.Money = Math.Min(MaxMoney, p.Money + add);
        }
        if (Math.Max(ScoreT, ScoreCt) >= WinRounds) { matchOver = true; Msg += $" — {(ScoreT > ScoreCt ? "Terrorists" : "Counter-Terrorists")} win the match!"; phaseEnd = time + 8; }
    }

    // ---------- simulation ----------
    public void Tick(float dt)
    {
        lock (gate)
        {
            time += dt; Shots.Clear();
            if (RoundNo == 0) StartRound(true);
            if (Phase == "end" && time >= phaseEnd)
            {
                if (matchOver) { matchOver = false; ScoreT = ScoreCt = LossT = LossCt = RoundNo = 0; StartRound(true); }
                else StartRound(false); // casual: no halftime side switch
            }
            if (Phase == "freeze" && time >= phaseEnd) { Phase = "live"; phaseEnd = time + RoundTime; }
            bool combat = Phase is "live" or "planted";

            foreach (var p in Players.Values)
            {
                if (!p.Alive) { p.ActionProg = 0; continue; }
                if (p.IsBot) Think(p);
                bool moveKeys = (p.Keys & 15) != 0;
                var w = Weapons.Get(p.Weapon);
                if (Phase != "freeze")
                {
                    float mx = ((p.Keys & 8) != 0 ? 1 : 0) - ((p.Keys & 2) != 0 ? 1 : 0);
                    float my = ((p.Keys & 4) != 0 ? 1 : 0) - ((p.Keys & 1) != 0 ? 1 : 0);
                    var len = MathF.Sqrt(mx * mx + my * my);
                    if (len > 0)
                    {
                        var sp = Speed * w.Speed * ((p.Keys & 32) != 0 ? 0.5f : 1f) * dt / len;
                        mx *= sp; my *= sp;
                        if (!GameMap.Collides(p.X + mx, p.Y, Radius)) p.X += mx;
                        if (!GameMap.Collides(p.X, p.Y + my, Radius)) p.Y += my;
                    }
                }
                if (p.ReloadEnd > 0 && time >= p.ReloadEnd) { p.Ammo = w.Mag; p.ReloadEnd = 0; }
                if (combat && p.Firing && p.ReloadEnd == 0 && time >= p.NextShot)
                {
                    if (p.Ammo <= 0) p.ReloadEnd = time + w.Reload;
                    else { p.Ammo--; p.NextShot = time + w.Interval; Fire(p, w, moveKeys && (p.Keys & 32) == 0); }
                }
                if (!p.Alive) continue;

                // plant / defuse / bomb pickup
                bool use = (p.Keys & 16) != 0 && !moveKeys && combat;
                bool acting = false;
                if (p.Id == carrier && Phase == "live" && use && GameMap.InSite(p.X, p.Y))
                {
                    acting = true; p.ActionProg += dt / PlantTime;
                    if (p.ActionProg >= 1)
                    {
                        planted = true; bombX = p.X; bombY = p.Y; bombEnd = time + BombTime; carrier = null;
                        Phase = "planted"; phaseEnd = bombEnd; p.ActionProg = 0; p.Money = Math.Min(MaxMoney, p.Money + PlantMoney);
                    }
                }
                else if (planted && p.Team == "CT" && use && Dist(p.X, p.Y, bombX, bombY) < 70)
                {
                    acting = true; p.ActionProg += dt / DefuseTime;
                    if (p.ActionProg >= 1) { p.Money = Math.Min(MaxMoney, p.Money + PlantMoney); EndRound("CT", "Bomb defused — Counter-Terrorists win", true); planted = true; break; }
                }
                if (!acting) p.ActionProg = 0;
                if (dropped && p.Team == "T" && Dist(p.X, p.Y, dropX, dropY) < 40) { carrier = p.Id; dropped = false; }
            }

            if (Phase == "live" && time >= phaseEnd) EndRound("CT", "Time ran out — Counter-Terrorists win");
            else if (Phase == "planted" && time >= bombEnd) EndRound("T", "Bomb exploded — Terrorists win", true);
            else if (Phase is "live" or "planted")
            {
                int t = Players.Values.Count(p => p.Team == "T" && p.Alive), ct = Players.Values.Count(p => p.Team == "CT" && p.Alive);
                if (ct == 0) EndRound("T", "Terrorists win — all Counter-Terrorists eliminated");
                else if (t == 0 && !planted) EndRound("CT", "Counter-Terrorists win — all Terrorists eliminated");
            }
        }
    }

    static float Dist(float ax, float ay, float bx, float by) => MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    void Fire(Player s, Weapon w, bool moving)
    {
        var a = s.Angle + (float)(Rng.NextDouble() - 0.5) * 2 * (w.Spread + (moving ? 0.05f : 0));
        float dx = MathF.Cos(a), dy = MathF.Sin(a);
        var dist = GameMap.Raycast(s.X, s.Y, dx, dy, 2000);
        Player? hit = null; var hitD = dist;
        foreach (var t in Players.Values)
        {
            if (t == s || !t.Alive || t.Team == s.Team) continue; // no friendly fire
            var rx = t.X - s.X; var ry = t.Y - s.Y;
            var proj = rx * dx + ry * dy;
            if (proj < 0 || proj > hitD) continue;
            var px = rx - dx * proj; var py = ry - dy * proj;
            if (px * px + py * py <= Radius * Radius) { hit = t; hitD = proj; }
        }
        if (hit != null)
        {
            var dmg = w.Dmg;
            if (hit.Armor > 0) { hit.Armor = Math.Max(0, hit.Armor - (int)(dmg * 0.5f)); dmg *= 0.57f; }
            hit.Hp -= (int)MathF.Ceiling(dmg);
            if (hit.Hp <= 0)
            {
                hit.Hp = 0; feed.Add((time, s.Name, s.Team, hit.Name, hit.Team, w.Name)); hit.Deaths++; s.Kills++; s.Money = Math.Min(MaxMoney, s.Money + w.Reward / 2);
                if (carrier == hit.Id) { carrier = null; dropped = true; dropX = hit.X; dropY = hit.Y; }
            }
        }
        Shots.Add(new Shot(s.X, s.Y, s.X + dx * hitD, s.Y + dy * hitD, s.Id));
    }

    // ---------- bots ----------
    void Think(Player b)
    {
        b.Firing = false; b.Keys = 0;
        Player? tgt = null; const float BotView = 650; float best = BotView * BotView;
        foreach (var e in Players.Values)
        {
            if (!e.Alive || e.Team == b.Team) continue;
            var dx = e.X - b.X; var dy = e.Y - b.Y; var d2 = dx * dx + dy * dy;
            if (d2 >= best) continue;
            var d = MathF.Sqrt(d2);
            if (d < 1 || GameMap.Raycast(b.X, b.Y, dx / d, dy / d, d) >= d - 1) { best = d2; tgt = e; }
        }
        // After a plant, the CT bot closest to the bomb is the defuser: it ignores enemies that are not close.
        bool defuser = planted && b.Team == "CT" && IsDefuser(b);
        if (defuser && tgt != null && best > 350 * 350) tgt = null;
        if (tgt == null) b.SeenSince = 0;
        if (Phase == "freeze") return;
        float gx, gy; bool stopNear = false;
        if (tgt != null)
        {
            var d = MathF.Sqrt(best);
            b.Angle = MathF.Atan2(tgt.Y - b.Y, tgt.X - b.X) + (float)(Rng.NextDouble() - 0.5) * 0.35f;
            if (b.SeenSince == 0) b.SeenSince = time;
            // reaction delay, then fire in bursts
            b.Firing = time - b.SeenSince > 0.5 && (time * 2 + b.Seq) % 1 < 0.55;
             gx = tgt.X; gy = tgt.Y; stopNear = d < 450;
            if (stopNear) return;
        }
        else if (b.Team == "T")
        {
            if (planted) { gx = bombX; gy = bombY; stopNear = Dist(b.X, b.Y, gx, gy) < 250; }
            else if (b.Id == carrier)
            {
                (gx, gy) = GameMap.SiteCenter(botSite);
                if (GameMap.InSite(b.X, b.Y) && Dist(b.X, b.Y, gx, gy) < 120) { b.Keys = 16; return; }
            }
            else if (dropped) { gx = dropX; gy = dropY; }
            else { (gx, gy) = GameMap.SiteCenter(botSite); stopNear = Dist(b.X, b.Y, gx, gy) < 200; }
        }
        else
        {
            if (planted)
            {
                gx = bombX; gy = bombY;
                var bd = Dist(b.X, b.Y, gx, gy);
                if (defuser && bd < 45) { b.Keys = 16; return; }
                stopNear = !defuser && bd < 250; // teammates cover the defuser
            }
            else { (gx, gy) = GameMap.SiteCenter(b.Seq % 2); stopNear = Dist(b.X, b.Y, gx, gy) < 250; }
        }
        if (stopNear && tgt == null) return;
        if (time >= b.RepathAt || Dist(b.X, b.Y, b.PathX, b.PathY) < 15)
        {
            var n = GameMap.NextStep(b.X, b.Y, gx, gy) ?? (gx, gy);
            b.PathX = n.Item1; b.PathY = n.Item2; b.RepathAt = time + 0.25;
        }
        var step = (b.PathX, b.PathY);
        var sx = step.Item1 - b.X; var sy = step.Item2 - b.Y;
        b.Keys = (sx > 12 ? 8 : sx < -12 ? 2 : 0) | (sy > 12 ? 4 : sy < -12 ? 1 : 0);
        if (tgt == null && (sx != 0 || sy != 0)) b.Angle = MathF.Atan2(sy, sx);
    }

    bool IsDefuser(Player b)
    {
        var d = Dist(b.X, b.Y, bombX, bombY);
        return !Players.Values.Any(o => o != b && o.IsBot && o.Alive && o.Team == "CT" && Dist(o.X, o.Y, bombX, bombY) < d);
    }

    // ---------- snapshots ----------
    bool Sees(Player v, Player o)
    {
        if (v == o) return true;
        float dx = o.X - v.X, dy = o.Y - v.Y; var d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > ViewRange) return false;
        return d < 1 || GameMap.Raycast(v.X, v.Y, dx / d, dy / d, d) >= d - 1;
    }

    /// <summary>Only what the viewer sees (range + line of sight). Dead players share their living teammates' vision; everyone sees all at round end.</summary>
    public object? SnapshotFor(string id)
    {
        lock (gate)
        {
            if (!Players.TryGetValue(id, out var v)) return null;
            var viewers = v.Alive ? [v] : Players.Values.Where(p => p.Team == v.Team && p.Alive).Append(v).ToList();
            var vis = Phase == "end" ? Players.Values.ToArray() : Players.Values.Where(o => viewers.Any(w => (w.Alive || w == o) && Sees(w, o))).ToArray();
            var ids = vis.Select(p => p.Id).ToHashSet();
            var showDrop = dropped && v.Team == "T";
            return new
            {
                t = ScoreT, ct = ScoreCt, phase = Phase, left = Math.Max(0, phaseEnd - time), round = RoundNo, msg = Msg,
                bomb = new { planted, x = planted ? bombX : showDrop ? dropX : (float?)null, y = planted ? bombY : showDrop ? dropY : (float?)null, dropped = showDrop },
                me = new { v.Money, v.Hp, v.Armor, v.Ammo, v.Weapon, v.ActionProg, hasBomb = carrier == v.Id, canBuy = CanBuy() && v.Alive, reloading = v.ReloadEnd > 0 },
                feed = feed.Where(f => time - f.T < 6).TakeLast(5).Select(f => new { killer = f.Killer, kTeam = f.KTeam, victim = f.Victim, vTeam = f.VTeam, weapon = f.Weapon }).ToArray(),
                board = Players.Values.Select(p => new { p.Id, p.Name, p.Team, p.Kills, p.Deaths, p.Alive }).ToArray(),
                players = vis.Select(p => new { p.Id, p.Name, p.Team, p.X, p.Y, p.Angle, p.Hp, p.Weapon, p.ActionProg }).ToArray(),
                shots = Shots.Where(s => ids.Contains(s.Shooter)).Select(s => new[] { s.X1, s.Y1, s.X2, s.Y2 }).ToArray(),
            };
        }
    }
}

namespace Cs2D.Server.Game;

/// <param name="Speed">Movement multiplier while holding the weapon.</param>
/// <param name="Team">"T", "CT" or "" (both).</param>
public record Weapon(string Id, string Name, int Price, float Dmg, double Interval, int Mag, double Reload, float Speed, float Spread, int Reward, string Team);

public static class Weapons
{
    public static readonly Weapon[] All =
    [
        new("glock", "Glock-18", 0, 30, 0.15, 20, 2.2, 1.00f, 0.025f, 300, "T"),
        new("usp", "USP-S", 0, 33, 0.17, 12, 2.2, 1.00f, 0.020f, 300, "CT"),
        new("deagle", "Desert Eagle", 700, 53, 0.35, 7, 2.2, 0.95f, 0.015f, 300, ""),
        new("mp9", "MP9", 1250, 26, 0.07, 30, 2.1, 0.95f, 0.045f, 600, ""),
        new("ak47", "AK-47", 2700, 36, 0.10, 30, 2.5, 0.88f, 0.030f, 300, "T"),
        new("m4a1", "M4A1-S", 2900, 33, 0.09, 25, 2.3, 0.90f, 0.025f, 300, "CT"),
        new("awp", "AWP", 4750, 115, 1.45, 5, 3.7, 0.80f, 0.004f, 100, ""),
    ];
    public static Weapon Get(string id) => All.First(w => w.Id == id);
    public static string Default(string team) => team == "T" ? "glock" : "usp";
    public static bool Allowed(Weapon w, string team) => w.Team == "" || w.Team == team;
}

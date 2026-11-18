namespace Cs2D.Server.Game;

public record Wall(float X, float Y, float W, float H);

/// <summary>
/// Dust II top-down collision map generated from the official CS2 radar at 32 world units per cell.
/// Map-local coordinates match the radar (0..4505.6), so spawn/site/door coordinates from the
/// CS2 game files line up directly.
/// </summary>
public static class GameMap
{
    public const int Cell = Dust2Data.Cell, NC = Dust2Data.NC, NR = Dust2Data.NR;
    public const float Width = NC * Cell, Height = NR * Cell;

    /// <summary>Bomb sites A and B (world units).</summary>
    public static readonly Wall[] Sites = Dust2Data.Sites;
    public static readonly (float X, float Y)[] TSpawns = Dust2Data.TSpawns;
    public static readonly (float X, float Y)[] CtSpawns = Dust2Data.CtSpawns;
    public static readonly DoorDef[] DoorDefs = Dust2Data.DoorDefs;

    static readonly bool[,] open = new bool[NC, NR];
    public static readonly Wall[] Walls;

    static GameMap()
    {
        var bits = Convert.FromBase64String(Dust2Data.GridB64);
        for (int i = 0; i < NC * NR; i++)
            open[i % NC, i / NC] = (bits[i >> 3] & (1 << (i & 7))) != 0;

        // merge blocked cells into rectangles (horizontal runs, merged vertically when identical)
        var rects = new List<int[]>(); var active = new Dictionary<(int, int), int[]>();
        for (int r = 0; r < NR; r++)
        {
            var next = new Dictionary<(int, int), int[]>();
            for (int c = 0; c < NC;)
            {
                if (open[c, r]) { c++; continue; }
                int s = c; while (c < NC && !open[c, r]) c++;
                if (active.TryGetValue((s, c), out var rc)) { rc[3] = r + 1; next[(s, c)] = rc; }
                else { rc = [s, r, c, r + 1]; rects.Add(rc); next[(s, c)] = rc; }
            }
            active = next;
        }
        Walls = rects.Select(a => new Wall(a[0] * Cell, a[1] * Cell, (a[2] - a[0]) * Cell, (a[3] - a[1]) * Cell)).ToArray();
    }

    public static (float X, float Y) SiteCenter(int i) => (Sites[i].X + Sites[i].W / 2, Sites[i].Y + Sites[i].H / 2);
    public static bool InSite(float x, float y) => Sites.Any(s => x >= s.X && x <= s.X + s.W && y >= s.Y && y <= s.Y + s.H);

    static bool Blocked(int c, int r) => c < 0 || r < 0 || c >= NC || r >= NR || !open[c, r];

    /// <summary>Circle vs. grid collision: only the cells under the circle are tested.</summary>
    public static bool Collides(float x, float y, float r)
    {
        int c0 = (int)MathF.Floor((x - r) / Cell), c1 = (int)MathF.Floor((x + r) / Cell);
        int r0 = (int)MathF.Floor((y - r) / Cell), r1 = (int)MathF.Floor((y + r) / Cell);
        for (int cy = r0; cy <= r1; cy++)
            for (int cx = c0; cx <= c1; cx++)
            {
                if (!Blocked(cx, cy)) continue;
                var nx = Math.Clamp(x, cx * Cell, (cx + 1) * Cell); var ny = Math.Clamp(y, cy * Cell, (cy + 1) * Cell);
                var dx = x - nx; var dy = y - ny;
                if (dx * dx + dy * dy < r * r) return true;
            }
        return false;
    }

    /// <summary>Next waypoint (world coords) on a BFS path from one point to another, or null if none.</summary>
    public static (float X, float Y)? NextStep(float fx, float fy, float tx, float ty)
    {
        int sc = Math.Clamp((int)(fx / Cell), 0, NC - 1), sr = Math.Clamp((int)(fy / Cell), 0, NR - 1);
        int gc = Math.Clamp((int)(tx / Cell), 0, NC - 1), gr = Math.Clamp((int)(ty / Cell), 0, NR - 1);
        if (sc == gc && sr == gr) return (tx, ty);
        var prev = new int[NC * NR]; Array.Fill(prev, -1);
        var q = new Queue<int>(); int goal = gr * NC + gc; prev[goal] = goal; q.Enqueue(goal); // search from goal
        int start = sr * NC + sc;
        while (q.Count > 0 && prev[start] < 0)
        {
            var cur = q.Dequeue(); int cc = cur % NC, cr = cur / NC;
            foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nc = cc + dc, nr = cr + dr;
                if (nc < 0 || nr < 0 || nc >= NC || nr >= NR || !open[nc, nr]) continue;
                int ni = nr * NC + nc; if (prev[ni] >= 0) continue;
                prev[ni] = cur; q.Enqueue(ni);
            }
        }
        if (prev[start] < 0) return null;
        int n = prev[start]; return ((n % NC + 0.5f) * Cell, (n / NC + 0.5f) * Cell);
    }

    /// <summary>Distance along the ray to the first blocked cell (grid DDA), or max.</summary>
    public static float Raycast(float ox, float oy, float dx, float dy, float max)
    {
        int cx = (int)MathF.Floor(ox / Cell), cy = (int)MathF.Floor(oy / Cell);
        if (Blocked(cx, cy)) return 0;
        int sx = dx > 0 ? 1 : -1, sy = dy > 0 ? 1 : -1;
        float tdx = Math.Abs(dx) < 1e-6f ? float.MaxValue : Math.Abs(Cell / dx), tdy = Math.Abs(dy) < 1e-6f ? float.MaxValue : Math.Abs(Cell / dy);
        float tx = Math.Abs(dx) < 1e-6f ? float.MaxValue : ((dx > 0 ? (cx + 1) * Cell - ox : ox - cx * Cell) / Math.Abs(dx));
        float ty = Math.Abs(dy) < 1e-6f ? float.MaxValue : ((dy > 0 ? (cy + 1) * Cell - oy : oy - cy * Cell) / Math.Abs(dy));
        while (true)
        {
            float t;
            if (tx < ty) { t = tx; tx += tdx; cx += sx; } else { t = ty; ty += tdy; cy += sy; }
            if (t >= max) return max;
            if (Blocked(cx, cy)) return t;
        }
    }
}

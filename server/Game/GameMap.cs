namespace Cs2D.Server.Game;

public record Wall(float X, float Y, float W, float H);

/// <summary>Dust II top-down layout extracted from the cs2pulse callouts radar (dark-grey walkable area), 64x60 cells.</summary>
public static class GameMap
{
    const int Cell = 48, NC = 64, NR = 60, PxScale = 3; // radar px * 3 = world units (16px per cell)
    public const float Width = NC * Cell, Height = NR * Cell;

    // '#' = walkable
    static readonly string[] Rows =
    [
        "................................................................",
        "................................................................",
        "................................................................",
        "................................................................",
        "................................................................",
        "......##..........................................######........",
        "......####........................................######........",
        "......####........................................#######.......",
        "......#########..................................########.......",
        "......######.###........................####..###########.......",
        "......##############...................##################.......",
        "......#########.####...................##################.......",
        "......########...##########.###.....############.##.#####.......",
        "........########.##############.....#####################.......",
        "......####.#####.##############.....#####################.......",
        "......############.############.....#######################.....",
        ".....###########################.###########################....",
        ".....#######################################################....",
        ".....#############..#####################################.##....",
        "......##########...........####..#.....###.........######.##....",
        "......##########...........####........###..........########....",
        "......#########............####.......####..........######......",
        ".......##..####............####.......####.........######.......",
        ".......##..##..............###.........###.........######.......",
        ".......##...#..............###.........###.........#####........",
        ".......##...................###..###..####.........#####........",
        ".......##........#########################..........####........",
        ".......######....######.#################...........####........",
        ".......######.....###.......######..................####........",
        "......###########.###.......#####..................#####........",
        "......###############.......#####........####.##########........",
        "......###########.##.......######........##################.....",
        "...........###.............######........##################.....",
        "............#..............######........##################.....",
        "............#...............#####........##################.....",
        "...........###..............#####.........##...############.....",
        ".........#######...........######........##....############.....",
        ".......##########.........#######........###...############.....",
        ".......##########........########........####..############.....",
        ".......##########........####################..############.....",
        ".......##########........####################..############.....",
        ".......###########.......#####.....#########...#########........",
        ".......###########.........###......########....................",
        "........##########..........#.......########....................",
        "........#######.............#.......########....................",
        ".......#########............#........######.....................",
        ".......###...###....##......#.......#########...................",
        ".......####.##########......#.......########....................",
        ".......###############......#.......########....................",
        ".......###############......##......####.###....................",
        ".......#############.#......##......####........................",
        "......##############.#......############........................",
        "......################......############........................",
        "......###################################.......................",
        "......###################################.......................",
        "......#..################################.......................",
        "......###################################.......................",
        "......###############################...........................",
        "..............................#######...........................",
        "................................................................"
    ];

    /// <summary>Bomb sites A and B (world units).</summary>
    public static readonly Wall[] Sites = [new(745 * PxScale, 155 * PxScale, 115 * PxScale, 100 * PxScale), new(150 * PxScale, 110 * PxScale, 125 * PxScale, 120 * PxScale)];
    public static (float X, float Y) SiteCenter(int i) => (Sites[i].X + Sites[i].W / 2, Sites[i].Y + Sites[i].H / 2);
    public static bool InSite(float x, float y) => Sites.Any(s => x >= s.X && x <= s.X + s.W && y >= s.Y && y <= s.Y + s.H);

    static readonly bool[,] open = new bool[NC, NR];
    public static readonly Wall[] Walls;
    public static readonly (float X, float Y)[] TSpawns = Enumerable.Range(0, 10).Select(i => ((330f + (i % 5) * 30) * PxScale, (880f + (i / 5) * 30) * PxScale)).ToArray();
    public static readonly (float X, float Y)[] CtSpawns = Enumerable.Range(0, 10).Select(i => ((590f + (i % 5) * 20) * PxScale, (230f + (i / 5) * 30) * PxScale)).ToArray();

    static GameMap()
    {
        for (int r = 0; r < NR; r++)
            for (int c = 0; c < NC; c++) open[c, r] = Rows[r][c] == '#';
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

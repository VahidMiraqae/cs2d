namespace Cs2D.Server.Game;

/// <summary>Per-room state for one swinging door leaf.</summary>
public class Door
{
    public readonly string Set;
    public readonly float HingeX, HingeY, Length, ClosedDeg, OpenDeg;
    public bool Open;
    public float Anim; // 0 = closed, 1 = open

    public Door(DoorDef d)
    {
        Set = d.Set; HingeX = d.HingeX; HingeY = d.HingeY;
        Length = d.Length; ClosedDeg = d.ClosedDeg; OpenDeg = d.OpenDeg;
    }

    public float AngleRad => (ClosedDeg + (OpenDeg - ClosedDeg) * Anim) * MathF.PI / 180f;
    public float EndX => HingeX + MathF.Cos(AngleRad) * Length;
    public float EndY => HingeY + MathF.Sin(AngleRad) * Length;

    /// <summary>A door blocks movement and vision until it is mostly open.</summary>
    public bool Blocking => Anim < 0.55f;

    public void Step(float dt)
    {
        var target = Open ? 1f : 0f;
        if (Anim == target) return;
        var step = dt * 3f;
        Anim = target > Anim ? MathF.Min(target, Anim + step) : MathF.Max(target, Anim - step);
    }

    public DoorView View() => new(HingeX, HingeY, EndX, EndY, Anim);
}

public record DoorView(float X1, float Y1, float X2, float Y2, float Open);

public static class DoorGeo
{
    public static bool SegmentCircle(float ax, float ay, float bx, float by, float cx, float cy, float r)
    {
        var vx = bx - ax; var vy = by - ay;
        var len2 = vx * vx + vy * vy;
        var t = len2 < 1e-6f ? 0 : Math.Clamp(((cx - ax) * vx + (cy - ay) * vy) / len2, 0, 1);
        var px = ax + vx * t; var py = ay + vy * t;
        var dx = cx - px; var dy = cy - py;
        return dx * dx + dy * dy <= r * r;
    }

    /// <summary>True when the ray (ox,oy + t*dx,dy, 0..max) crosses the segment.</summary>
    public static bool SegmentRay(float ax, float ay, float bx, float by, float ox, float oy, float dx, float dy, float max)
    {
        var ex = bx - ax; var ey = by - ay;
        var den = dx * ey - dy * ex;
        if (MathF.Abs(den) < 1e-6f) return false;
        var wx = ax - ox; var wy = ay - oy;
        var t = (wx * ey - wy * ex) / den;
        var u = (wx * dy - wy * dx) / den;
        return t >= 0 && t <= max && u >= 0 && u <= 1;
    }
}

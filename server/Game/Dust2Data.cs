namespace Cs2D.Server.Game;

/// <summary>One swinging door leaf: hinge, length, and closed/open angles in degrees.</summary>
public record DoorDef(string Set, float HingeX, float HingeY, float Length, float ClosedDeg, float OpenDeg);

/// <summary>
/// Dust II geometry generated from the official CS2 radar (docs/dust2/de_dust2_radar.png,
/// 1024 px, pos_x -2476, pos_y 3239, scale 4.4). World coordinates here are radar-local:
/// x = radar_x * 4.4, y = radar_y * 4.4, so the map spans 0..4505.6 units (grid 141 x 141 at 32 u).
/// Spawns are the exact CS2 entity-lump spawn origins converted to the same space.
/// </summary>
public static class Dust2Data
{
    public const int Cell = 32, NC = 141, NR = 141;

    // Row-major 1-bit walkability grid, 141*141 bits, base64. '#' = walkable.
    public const string GridB64 = """
AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA4A8AAAAAAAAAAAAAAAAAAAAAfAAAAAAAAAAAAAAAAAAAAACADwAA
AAAAAAAAAAAAAAAAAADwAQAAAAAAAAAAAADA//8AAAA+AAAAAAAAAAAAAAD4/x8AAMAfAAAAAAAAAAAAAAD//wMAAPgHAAAAAAAA
AAAAAOD/fwAAAP8AAAAAAAAAAAAAAPz/DwAA4B8AAAAAAAAAAAAAgP//AQAA/B8AAAAAAAAAAAAA8P8/AACA/+MDAAAAAAAAAAAA
/v8HAADwf/wDAAAAAAAAAADA//8AAAD+/38AAAAAAACAH/z//z8AAMD//w8AAAAAAADwg////wcAAPj//wEAAAAAAAD//////wAA
AP//PwAAAAAAAPj/////HwAA4P//BwAAAAAAAP//////AwAA/P//AAAAAAAA4P//8f9/AACA//8PAAAAAAAA/P8//v8PAADwf/AB
AAAAAAD+///H//8BAAD+DzwYAAAAAOD////4/z8AAMD/gQcDAAAAAAT//x///wcAAPgP8GAAAAAAgOD//+P//wAAAP/w/w8AAAAA
EPgHfPz/HwAA+B/+jwEAAAAAAv+A////AwAA/8P/MQAAAAAA4B/wD/9/AADgf/g/BgAAAADg/wP+4f8PAAD8////AAAAAAD8P8AA
/P8BAID///8fAAAAAOD/BwCA//8HAPD///8DAAAAAP7/AADw//8AAP7//38AAAAAwP8fAAD//x8AwP///w8AAAAA+P8HAOD//wMA
+P///wEAAAAA//8AAPz/YwAA////PwAAAADg/x8AgP9/DADg////BwAAAAD4/wEA8P+PAQD8//8PAAAAAACAPwAA/v8hAID///8B
AAAAAADwBwAA/D8EAPD//z8AAAAAAAD+AACA/48AAP7//wcAAAAAAMAfAADw/xEAwP9/+AAAAAAAAPgDAAD+/wMA+B8HDwAAAAAA
gH8AAMD//wAA/+PgAAAAAAAA8A8AAPj/DwDgfxwPAAAAAAAA/gEAAP9/AADAB/8AAAAAAADAPwAA4P8DAAD44AgAAAAAAAD4BwAA
/n8AAAAfHAAAAAAAAAD/AADA/wcAAOCBAwAAAAAAAOAfAAD4/wAAADzwAAAAAAAAAPwDAAD/HwAAgAccAAAAAAAAgH8AAOD/AQAA
8AAAAAAAAAA/8A8AAPw/AAAAHgAAAAAAAOAH/gEAgP8HAADAAwAAAAAAAP//PwAA4P8AAAB4AAAAAAAA4P//BwAA/B8AAAAPAAAA
AAAA/P//AACA/wMAAOADAAAAAACA//8DAADwfwAAAPg/AAAAAADw/38AAAD+DwAAAP8HAAAAAAD+/w8AAMD/AQAA4P8AAAAAAMAD
AAAAAPg/AAAA/h8AAAAAADgAAAAAAP8HAAD8//8HBgAAAAMAAAAA4P8AAID////gAQAAYAAAAAAA/B8AAPD///8/AAAADAAAMID/
/wMAAP7///8PAACAAQDgB/D/fwAAwP////8DAAAwAAD8AP7/DwAA+P///z8AAAAGAIAf8P//AQAA/////wcAAMAAAPAH//8/AADg
////PwAAABgAAP7///8fAAAA8D8AAAAAAAMAwP////8HAAAA/AcAAAAAYAAA+P////8HAAAAPAAAAAAADAAA//////8AAACABwAA
AACAAQDg/////x8AAADwAAAAAAAwAAD8/////wMAAAAeAAAAAD8GAID/////fwAAAMADAAAA8McAAPD/////DwAAAHgAAAAA/h8A
APzx////AQAAAA8AAADA/wMAgA/4//8/AAAA4AEAAAD4fwAA8AD+//8HAADg/x8AAAD/DwAADsB/APgAAAD+/wMAAOD/AQDAAfgP
AB8AAMD/fwAAgP8/AAA4AP8B4AMAgP//fwAA+P8HAAAP4D8AfAAA8P//DwAA//8AAOAP/AeADwAA/v//AQDg/z8AAPyB/wDwAQDA
//8/AID//w8AgD/wHwA+AAD4//8HAPD//////wf+A8AHAAD///8AAPz//////8B/APgAAOD//x8AgP//////H/gPAB8AAPz//wMA
8P//////A/8B4AMAgP///wAA/v////9/4D8AfAAA8P//HwDA//////8P/AeADwAA/v//AwDwfwDw//+B/wAAAADA//9/AAD8BwD8
/z/wHwAAAAD4//8PAIB/AID//wf+AwAAAAD///8BAAAOAPD//wAAAAAAAOD//z8AAMABAP7/HwAAAAAAAPz//wcAADgAwP//AwAA
AAAAgP//xwAAAAcA8P9/AAAAAAAA8P9/CAAA4AAA/H8MAAAAAAAA/v8PAAAAHAAA/48BAAAAAADA//8BAACAAwDg/zEAAAAAAAD4
/z8AAABAAAD8PwQAAAAAAAD//wcAAAAIAID/hwAAAAAAAOD//wAAAAABAPD/EQAAAAAAAP4HHwAAADAAAP4/AgAAAAAAwP/gD+AA
AA4A+P9/AAAAAAAA+B/8AT4A4AEA//8PAAAAAAAA/+N/4AcAPADg//8BAAAAAADgf/z//wCABwD+/z8AAAAAAAD8j///HwDwAMD/
/wcAAAAAAID/////AwAeAPj//wAAAAAAAPD///9/AMADAP//DwAAAAAAAP7///8PAPgA4D/4AAAAAAAAwP///z8AAD8A/AcAAAAA
AAAA+P///wcA4P/j/wAAAAAAAAAA/////wAA/P//HwAAAAAAAADw////HwCA////AwAAAAAAAAD/////AwDw//9/AAAAAAAAAPD/
////AAD+//8PAAAAAAAAAP////9/AMD///8BAAAAAAAA4P////////////8AAAAAAAAA/P///////////x8AAAAAAACA////////
/////wMAAAAAAADw////////////fwAAAAAAAAD+////////////DwAAAAAAAMD/////////////AQAAAAAAAPj///////////9/
AAAAAAAAAP////////////8PAAAAAAAA4P////////////8BAAAAAAAA/P///////////z8AAAAAAACA/////////////wMAAAAA
AADw////////////fwAAAAAAAAD+//////////8PAAAAAAAAAAAA+P//BwAA/v8BAAAAAAAAAAAAAAAAAADA/z8AAAAAAAAAAAAA
AAAAAAD4/wcAAAAAAAAAAAAAAAAAAAD//wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
AAAAAAAAAAAAAAA=
""";

    /// <summary>Bomb sites from the radar's orange site outlines (map-local world units).</summary>
    public static readonly Wall[] Sites =
    [
        new(3432, 418, 352, 462), // A
        new(748, 396, 352, 396),  // B
    ];

    // Exact CS2 info_player_terrorist origins, converted from world (x + 2476, 3239 - y).
    public static readonly (float X, float Y)[] TSpawns =
    [
        (1653.6f, 4034.6f), (1618.5f, 3977.4f), (1715.3f, 4075.2f), (1779.2f, 4045.6f), (1818.7f, 3994.9f),
        (1335.0f, 4047.0f), (1295.0f, 3993.0f), (1400.0f, 4082.0f), (1461.0f, 4047.0f), (1496.0f, 3993.0f),
    ];

    // Exact CS2 info_player_counterterrorist origins, converted the same way.
    public static readonly (float X, float Y)[] CtSpawns =
    [
        (2858.1f, 1136.6f), (2930.8f, 1140.0f), (2798.4f, 1074.0f), (2823.9f, 988.3f), (2897.5f, 950.9f),
        (2698.9f, 952.7f), (2629.6f, 930.6f), (2740.5f, 1028.6f), (2693.6f, 1104.8f), (2612.8f, 1121.8f),
    ];

    /// <summary>
    /// The three Dust II door pairs. Each pair is two leaves that meet in the middle when closed.
    /// Doorways were carved from the radar gaps: Long (x 3680..4096 at y 2464),
    /// B (x 1088..1216 at y 1150), Mid (x 2640..2750 at y 1144).
    /// </summary>
    public static readonly DoorDef[] DoorDefs =
    [
        new("long", 3680, 2464, 208, 0, 90), new("long", 4096, 2464, 208, 180, 90),
        new("b", 1088, 1150, 64, 0, 90), new("b", 1216, 1150, 64, 180, 90),
        new("mid", 2640, 1144, 55, 0, -90), new("mid", 2750, 1144, 55, 180, -90),
    ];
}

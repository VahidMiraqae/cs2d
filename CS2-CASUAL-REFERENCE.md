# Counter-Strike 2 — Casual Mode Reference

Research date: **2026-11-18**. Everything here is for **CS2** (Source 2, current build), not CS:GO.
Primary data comes from Valve's shipped game files (mirrored by SteamDB's `GameTracking-CS2`) and
config-derived write-ups of the live mode. Valve runs a separate, unshipped
`gamemode_casual_server.cfg` on official servers, so live matchmaking can override a shipped value.

## 1. What "Casual" is in CS2

| Rule | Casual |
|---|---|
| Players | Up to 20 (10v10 when full) |
| Match length | Max 15 rounds, first to 8 wins, no side swap |
| Round clock | 2:15 (135 s) on bomb maps; 2:00 on hostage maps |
| Freeze time | 5 s |
| Buy time | 45 s |
| Start money / cap | $1,000 / $10,000 |
| Round win (bomb map) | $2,700, any win condition |
| Round loss | $2,400 flat, no loss streak |
| Kill reward | 50% of the Competitive award |
| Armor | Free Kevlar + helmet every round |
| Defuse kit | Free for every CT |
| Grenades | 3 total, max 1 flashbang |
| Friendly fire | OFF |
| Team collision | OFF |
| Rank | None |
| Warmup | 60 s (6 s once everyone is connected; ends at 8 players) |
| Map selection | Queue by map group, not individual map |

The in-game rules text is four lines: *"Friendly fire is OFF", "Team collision is OFF",
"50% kill reward amounts", "Best out of 15 rounds"*. Internally the mode is game type 0 / mode 0.

**No halftime.** `mp_halftime 0`; teams stay on the same side for the whole match. The match clinches
as soon as a team reaches 8 (`mp_maxrounds 15`, `mp_match_can_clinch 1`).

### Map groups (as of Oct 2026; rotates)

| Group | Maps |
|---|---|
| Defusal Group Alpha | Dust II, Mirage, Inferno, Vertigo, Cache |
| Defusal Group Delta | Train, Anubis, Ancient (night), Overpass, Nuke |
| Community Map Group | Rotating community maps |
| Hostage Group | Office, Italy |

Offline/bot play exposes Casual's map list individually instead of by group.

## 2. Casual economy (exact config values)

| Event | Casual | Competitive (for contrast) |
|---|---|---|
| Start money | $1,000 | $800 |
| Max money | $10,000 | $16,000 |
| Win by elimination or CT time-out (bomb map) | $2,700 | $3,250 |
| Win by detonation or defuse | $2,700 | $3,500 |
| Loss | $2,400 flat | $1,400, +$500 per consecutive loss |
| Plant / defuse (that player) | $200 | $300 |
| T team when planted bomb is defused | $200 each | $600 each |
| Kill reward multiplier | 0.5 | 1.0 |
| Team-kill penalty | -$300 | -$300 |
| Short-handed bonus | $0 | $1,000 |

Hostage-map values differ (T elimination $2,000, CT elimination $2,300, rescue $3,000, etc.), but the
current 2D game is bomb-only.

### Casual kill rewards (weapon award × 0.5)

| Weapon class | Competitive | Casual |
|---|---|---|
| Knife | $1,500 | $750 |
| Nova / MAG-7 / Sawed-Off | $900 | $450 |
| XM1014, all SMGs except P90 | $600 | $300 |
| Rifles, pistols, P90, SSG 08, autosnipers, Negev, M249 | $300 | $150 |
| AWP, Zeus x27 | $100 | $50 |

## 3. Round, bomb and defuse timings

| Action | Time |
|---|---|
| Plant the C4 | 3.2 s (planter is crouched/locked in place) |
| C4 fuse | 40 s (`mp_c4timer` default) |
| Defuse without kit | 10 s |
| Defuse with kit | 5 s |
| Round timer after plant | Stops; only the C4 clock matters |
| Round-restart delay | 10 s (`mp_round_restart_delay`) |
| Win panel | 3 s |

Rules:

- Defuse progress is **lost**, not paused, if USE is released or the view leaves the bomb.
- If the defuse and the explosion would happen on the same instant, the **CT wins**.
- Since Oct 2025, leaving a defuse lowers the viewmodel and blocks firing for **150 ms** (no free fake defuse).
- Casual gives every CT a kit, so the practical defuse time is always 5 s.

### CS2's current C4 blast (important: changed July 2026)

On **2026-07-08** Valve replaced the old spherical "damage through walls" bomb with a **shockwave
simulated outward from the explosion**, with values precomputed per compiled map:

- Walls/cover block the wave; distance and geometry matter.
- Very close = lethal regardless; farther away, cover can save you.
- The health bar shows a damage preview as soon as the bomb becomes audible (2026-07-20).
- The explosion now interacts with smoke and molotov/incendiary fire.
- Old figures (1750-unit radius, 500 max damage, 788/1046-unit survival thresholds) are **obsolete**.

For a 2D clone, model this as a radial wave with line-of-sight blocking rather than a plain circle.

## 4. Movement and shooting model

### Movement

| Value | CS2 |
|---|---|
| Base run speed | 250 units/s (weapons override it) |
| Walk (Shift) | 52% of max speed (≈130 u/s), silent |
| Crouch | 34% of max speed (≈85 u/s), silent |
| Weapon max speed | AK 215, M4A4/M4A1-S 225, AWP 200 (100 scoped), knife 250 |
| Jump | ~302 u/s initial, gravity 800 |
| Tagging | Getting shot slows the victim briefly |

Running footsteps are audible; walking and crouching are not. No stamina.

### Accuracy

Each weapon has separate inaccuracy values for standing, crouching, moving, jumping, landing and
firing, plus a base spread. The practical CS2 rule: **you must stop to shoot**. Example AK-47:
standing inaccuracy 0.00641 rad (≈0.37°), moving inaccuracy 0.17506 rad (≈10°). Crouching is more
accurate than standing; moving or jumping is wildly inaccurate.

Recoil is a climb pattern with horizontal variance, recovered over ~0.37 s (standing) / ~0.31 s
(crouching); spray control means pulling against the pattern.

### Damage

- Hitscan; damage falls off with distance: `damage × rangeModifier^(distance / 500 units)`.
- Headshot: ×4 for most weapons (M4A1-S ×3.475, Desert Eagle ×3.9). Helmet required to mitigate.
- Armor: with armor, health damage ≈ `damage × armorRatio × 0.5`; armor absorbs the remainder.
  Armor has 100 HP; AK-47 does ~28 to an armored body, AWP ~112, Deagle ~49.
- Wall penetration is per-weapon (`m_flPenetration`: rifles 2.0, AWP/autosnipers 2.5, SMGs/pistols 1.0).
- Shotguns fire multiple pellets (Nova 9, XM1014 6, MAG-7/Sawed-Off 8), each with its own falloff.

## 5. CS2 weapon values (current build)

Read from `scripts/weapons.vdata` (price/damage/cycle/mag/speed/armor ratio/headshot/penetration/
range modifier/inaccuracy). RPM is 60/cycle. Inaccuracy is standing/moving, unscoped, in degrees.
"Move" is the weapon's max movement speed in units/s. Kill $ is the **Casual** reward.

### Pistols

| Weapon | Side | $ | Dmg | RPM | Mag | Move | ArmorRatio | HS× | Pen | RangeMod | Stand° | Move° | Kill $ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Glock-18 | T | 200 | 30 | 400 | 20 | 240 | 0.94 | 4.0 | 1.0 | 0.85 | 0.32 | 0.57 | 150 |
| USP-S | CT | 200 | 35 | 353 | 12 | 240 | 1.01 | 4.0 | 1.0 | 0.91 | 0.28 | 0.79 | 150 |
| P2000 | CT | 200 | 35 | 353 | 13 | 240 | 1.01 | 4.0 | 1.0 | 0.91 | 0.28 | 0.74 | 150 |
| P250 | both | 300 | 38 | 400 | 13 | 240 | 1.28 | 4.0 | 1.0 | 0.90 | 0.52 | 1.15 | 150 |
| Five-SeveN | CT | 500 | 32 | 400 | 20 | 240 | 1.823 | 4.0 | 1.0 | 0.81 | 0.52 | 2.29 | 150 |
| Tec-9 | T | 500 | 33 | 500 | 18 | 240 | 1.812 | 4.0 | 1.0 | 0.79 | 0.28 | 0.22 | 150 |
| CZ75-Auto | both | 500 | 31 | 600 | 12 | 240 | 1.553 | 4.0 | 1.0 | 0.85 | 0.60 | 0.77 | 150 |
| Dual Berettas | both | 300 | 38 | 500 | 30 | 240 | 1.15 | 4.0 | 1.0 | 0.79 | 0.40 | 1.02 | 150 |
| Desert Eagle | both | 700 | 53 | 267 | 7 | 230 | 1.864 | 3.9 | 2.0 | 0.85 | 0.24 | 2.76 | 150 |
| R8 Revolver | both | 600 | 86 | 120 | 8 | 180 | 1.864 | 4.0 | 2.0 | 0.94 | 0.11 | 0.37 | 150 |

### SMGs

| Weapon | Side | $ | Dmg | RPM | Mag | Move | ArmorRatio | HS× | Pen | RangeMod | Stand° | Move° | Kill $ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| MAC-10 | T | 1050 | 29 | 800 | 30 | 240 | 1.15 | 4.0 | 1.0 | 0.80 | 0.76 | 0.80 | 300 |
| MP9 | CT | 1250 | 26 | 857 | 30 | 240 | 1.20 | 4.0 | 1.0 | 0.87 | 0.52 | 1.66 | 300 |
| MP7 | both | 1400 | 30 | 750 | 30 | 220 | 1.25 | 4.0 | 1.0 | 0.87 | 0.57 | 1.14 | 300 |
| MP5-SD | both | 1400 | 28 | 750 | 30 | 235 | 1.25 | 4.0 | 1.0 | 0.87 | 0.57 | 1.72 | 300 |
| UMP-45 | both | 1200 | 35 | 667 | 25 | 230 | 1.30 | 4.0 | 1.0 | 0.75 | 0.77 | 1.65 | 300 |
| P90 | both | 2350 | 26 | 857 | 50 | 230 | 1.38 | 4.0 | 1.0 | 0.86 | 0.78 | 1.78 | 150 |
| PP-Bizon | both | 1300 | 27 | 750 | 64 | 240 | 1.26 | 4.0 | 1.0 | 0.80 | 0.80 | 1.58 | 300 |

### Shotguns

| Weapon | Side | $ | Dmg × pellets | RPM | Mag | Move | ArmorRatio | HS× | Pen | RangeMod | Stand° | Move° | Kill $ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Nova | both | 1050 | 26 × 9 | 68 | 8 | 220 | 1.0 | 4.0 | 1.0 | 0.70 | 0.40 | 2.11 | 450 |
| XM1014 | both | 2000 | 20 × 6 | 171 | 7 | 215 | 1.6 | 4.0 | 1.0 | 0.70 | 0.40 | 2.06 | 300 |
| MAG-7 | CT | 1300 | 30 × 8 | 71 | 5 | 225 | 1.5 | 4.0 | 1.0 | 0.45 | 0.40 | 0.92 | 450 |
| Sawed-Off | T | 1100 | 32 × 8 | 71 | 7 | 210 | 1.5 | 4.0 | 1.0 | 0.45 | 0.40 | 0.96 | 450 |

### LMGs

| Weapon | Side | $ | Dmg | RPM | Mag | Move | ArmorRatio | HS× | Pen | RangeMod | Stand° | Move° | Kill $ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| M249 | both | 5200 | 32 | 750 | 100 | 195 | 1.60 | 4.0 | 2.0 | 0.97 | 0.44 | 8.95 | 150 |
| Negev | both | 1700 | 35 | 800 | 150 | 150 | 1.42 | 4.0 | 2.0 | 0.97 | 0.58 | 9.12 | 150 |

### Rifles

| Weapon | Side | $ | Dmg | RPM | Mag | Move | ArmorRatio | HS× | Pen | RangeMod | Stand° | Move° | Kill $ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Galil AR | T | 1800 | 30 | 667 | 35 | 215 | 1.55 | 4.0 | 2.0 | 0.98 | 0.50 | 7.08 | 150 |
| FAMAS | CT | 1950 | 30 | 667 | 25 | 220 | 1.40 | 4.0 | 2.0 | 0.96 | 0.43 | 5.69 | 150 |
| AK-47 | T | 2700 | 36 | 600 | 30 | 215 | 1.55 | 4.0 | 2.0 | 0.98 | 0.37 | 10.03 | 150 |
| M4A4 | CT | 2900 | 33 | 667 | 30 | 225 | 1.40 | 4.0 | 2.0 | 0.97 | 0.28 | 7.90 | 150 |
| M4A1-S | CT | 2900 | 38 | 600 | 20 | 225 | 1.40 | 3.475 | 2.0 | 0.94 | 0.28 | 5.32 | 150 |
| AUG | CT | 3300 | 28 | 600 | 30 | 220 | 1.80 | 4.0 | 2.0 | 0.98 | 0.28 | 7.76 | 150 |
| SG 553 | T | 3000 | 30 | 545 | 30 | 210 | 2.00 | 4.0 | 2.0 | 0.98 | 0.33 | 7.79 | 150 |

### Snipers

| Weapon | Side | $ | Dmg | RPM | Mag | Move | ArmorRatio | HS× | Pen | RangeMod | Stand° | Move° | Kill $ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| SSG 08 | both | 1700 | 88 | 48 | 10 | 230 | 1.70 | 4.0 | 2.5 | 0.98 | 1.82 | 7.07 | 150 |
| AWP | both | 4750 | 115 | 41 | 5 | 200 | 1.95 | 4.0 | 2.5 | 0.99 | 4.63 | 10.11 | 50 |
| G3SG1 | T | 5000 | 80 | 240 | 20 | 215 | 1.65 | 4.0 | 2.5 | 0.98 | 1.48 | 8.62 | 150 |
| SCAR-20 | CT | 5000 | 80 | 240 | 20 | 215 | 1.65 | 4.0 | 2.5 | 0.98 | 1.48 | 8.62 | 150 |

Scoped sniper accuracy is far better than the unscoped values above. Knife: $0, 50 damage,
250 u/s, kill $750. Zeus x27: $200, 500 damage, 1 round, kill $50; Casual uses
`mp_weapons_allow_zeus 2` (more permissive than Competitive's `5`).

## 6. Equipment and utility

| Item | Price | Notes |
|---|---|---|
| Kevlar | $650 | 100 armor |
| Kevlar + helmet | $1,000 | Helmet mitigates headshots |
| Defuse kit | $400 | CT only; 10 s → 5 s defuse; **free in Casual** |
| Zeus x27 | $200 | One shot, 500 damage, short range |
| Flashbang | $200 | Blinds by distance + view angle |
| HE grenade | $300 | ~98 max damage, ~350-unit radius, armor reduces |
| Smoke grenade | $300 | ~18 s vision-blocking cloud |
| Molotov | $400 | T only; ~7 s fire area |
| Incendiary | $500 | CT only; ~7 s fire area |
| Decoy | $50 | ~15 s of fake gunfire, then a small explosion |

Casual limits: **3 grenades total, at most 1 flashbang**. On death players drop their best gun, all
grenades, and the defuse kit (`mp_death_drop_gun 1`, `mp_death_drop_grenade 3`, `mp_death_drop_defuser 1`).

CS2 utility behavior that matters for a clone:

- Smokes are **volumetric**: they block vision in 3D (not just a flat circle), and bullets/HE can
  briefly displace or open them. In 2D, use a vision-blocking circle and let HE/molly interact.
- Flashes depend on line of sight, distance, and whether the target is looking at the flash; turning
  away shortens the blind. Maximum blind is ~5 s.
- HE damage falls off with distance and is reduced by armor.
- Molotov/incendiary creates a burning area for ~7 s and denies a position; it can be smoked out.
- Decoys mimic gunfire sounds and explode weakly at the end.

Exact grenade radii/durations are engine constants, not in the shipped weapon script; treat the values
above as the standard CS2 tuning to verify in-game.

## 7. CS2-specific behavior vs CS:GO

- **Sub-tick**: movement and shooting are not quantized to the server tick; input is timestamped.
  A 2D clone should use a high-tick authoritative sim plus client prediction/interpolation.
- **MR12 Competitive** (first to 13, overtime) — **not** Casual. Casual is still best-of-15 / first to 8.
- **Volumetric smokes** and the **July 2026 C4 shockwave** are CS2-only mechanics.
- Casual is 10v10, $1,000 start, $10,000 cap, free armor/helmet and kits, flat $2,400 loss,
  half kill rewards, no halftime, no rank.
- Casual social rules: free spectating (`mp_forcecamera 0`), dead players cannot talk to living
  teammates (`sv_deadtalk 0`) but can talk to enemies (`sv_talk_enemy_dead 1`).
- Bots: `bot_quota 10`, `bot_quota_mode fill`, `bot_difficulty 1` for local Casual games.

## 8. Gap analysis: current CS2D vs CS2 Casual

Current code: `server/Game/Room.cs`, `server/Game/Weapons.cs`, `server/Game/GameMap.cs`,
`client/src/Game.tsx`.

| Area | Current CS2D | CS2 Casual target | Where |
|---|---|---|---|
| Players | `BotTarget = 5` fills rooms to 5 **total** | 20 players (10v10), bots fill to 10/side | `Room.cs:19,63-74` |
| Freeze time | 10 s | 5 s | `Room.cs:19` |
| Round time | 115 s | 135 s (bomb maps) | `Room.cs:19` |
| Buy window | freeze + 45 s of live | 45 s buy time | `Room.cs:19,100` |
| C4 timer | 45 s | 40 s | `Room.cs:19` |
| Round-end delay | 5 s | 10 s | `Room.cs:147` |
| Win money | $3,250 / $3,500 bomb win | $2,700 flat | `Room.cs:148` |
| Loss money | winner − $300 | $2,400 flat | `Room.cs:153` |
| Money cap / start | $10,000 / $1,000 | same | `Room.cs:19`, `Player.Money` |
| Kill reward | `Reward / 2` | half award (already correct) | `Room.cs:257` |
| Plant/defuse reward | $200 | $200 | `Room.cs:209,216` |
| Armor | 100 free, no helmet/headshot model | free Kevlar+helmet, armor ratio + 4× headshot | `Room.cs:107` |
| Defuse | 5 s for everyone (effectively kit) | 5 s with free kit; 10 s kitless | `Room.cs:20,213-217` |
| No halftime | already no side swap | correct | `Room.cs:169` |
| Weapons | 7 guns; M4A1-S has 33 dmg / 25 mag | 34-weapon CS2 arsenal; M4A1-S 38 dmg / 20 mag; USP 35 | `Weapons.cs` |
| Grenades | none | HE/flash/smoke/molly/inc/decoy; 3 max, 1 flash | missing |
| Knife / Zeus | none | knife + Zeus | missing |
| Headshots | none (uniform body circle) | 4× headshot, helmet mitigation | `Room.cs:235-265` |
| Armor formula | flat 50% + ×0.57 | armor ratio, armor HP, range falloff | `Room.cs:253` |
| Recoil | none | spray pattern, recovery, movement inaccuracy | missing |
| Movement | base 260, walk 50%, no crouch/jump | 250 base, walk 52%, crouch 34%, weapon speeds | `Room.cs:20,178-192` |
| Weapon speed | multipliers on 260 | direct max speeds (AK 215, AWP 200) | `Weapons.cs` |
| Drops | only the bomb | guns, grenades, kit drop on death | missing |
| Free spectate | teammates only | free camera / any player in Casual | `Room.cs:361-390` |
| Map pool | one hard-coded Dust II grid | map groups; multiple maps | `GameMap.cs` |
| Warmup | none | 60 s warmup, 8-player start | missing |
| Bots | 4-gun buy list, basic pathing | 10v10, utility, economy, holds/retakes | `Room.cs:126-137,268-352` |
| Net rate | 30 Hz sim / ~15 Hz snapshots | 60 Hz+ sim, 20-30 Hz snapshots + interpolation | `GameService.cs:31-46` |

Bugs worth fixing while touching this code: `planted` is set back to `true` after a defuse
(`Room.cs:216`), and the `Room` summary comment says 5v5/side swap while the mode is 10v10/no swap.

## 9. Turning it into a faithful 2D version

1. **Aim + accuracy cone.** Top-down mouse aim maps well to CS2's yaw-only shooting. Replace the flat
   spread with stance/speed-based inaccuracy and a recoil offset that recovers; require stopping to
   shoot accurately (moving inaccuracy ~10° for rifles).
2. **Headshots in 2D.** Use a small inner hit circle (≈25-30% of body radius) as the head; apply the
   4× multiplier and helmet check. This preserves one-tap AK/AWP feel without a Z axis.
3. **Wall penetration.** Raycast through thin walls; reduce damage per wall using the weapon's
   penetration value. This enables CS-style wallbangs on Dust II.
4. **Utility.** Flash = LOS + facing dot product + distance (blind duration scaled); smoke = circle
   that blocks LOS and bot vision; molotov = area denial; HE = radial damage with armor; decoy = sound.
5. **Sound is mandatory.** Footsteps (run vs walk/crouch), gunshots by weapon, reloads, bomb beeps,
   plant/defuse, flash ring. The client currently has no audio code at all.
6. **Bomb.** 3.2 s plant, 40 s fuse, 5 s kit defuse, progress reset on release, CT wins ties, and the
   new shockwave blast with LOS blocking instead of a plain radius.
7. **Round flow.** 5 s freeze, 45 s buy, 135 s round, $1,000 start, $2,700 win / $2,400 loss, free
   armor/kit, half kill rewards, first to 8, no side swap, 10v10.
8. **Netcode.** With 20 players, 30 Hz sim / 15 Hz snapshots will feel rough. Move to 60 Hz server
   sim and ~20-30 Hz snapshots with client interpolation and local prediction for movement.
9. **Map.** Keep Dust II as the 2D map but add proper callouts, chokepoints and site geometry; later
   add the Casual map groups as separate 2D layouts.
10. **Bots.** Scale to 10v10 with economy-aware buys, utility usage, site splits, holds, retakes and
    difficulty levels so a solo player gets a Casual-like match.

## 10. Dust II map data (exact calibration, not yet an exact collision map)

The current `server/Game/GameMap.cs` is a **rough 64×60 ASCII trace** (48 units/cell =
3,072×2,880 units). It is not in CS2 world coordinates and is smaller than the real map, so it is a
recognisable Dust II sketch rather than an exact copy.

Valve ships the official top-down calibration in `resource/overviews/de_dust2.txt`:

| Field | Value |
|---|---|
| Radar image | `overviews/de_dust2_v2`, 1024×1024 px |
| `pos_x` (upper-left world X) | -2476 |
| `pos_y` (upper-left world Y) | 3239 |
| `scale` | 4.4 world units per pixel |
| `rotate` | 1 |
| `zoom` | 1.1 |

World conversion (matches the loading-screen icon positions and known Dust II coordinates):

```
world_x = -2476 + pixel_x * 4.4
world_y =  3239 - pixel_y * 4.4
pixel_x = (world_x + 2476) / 4.4
pixel_y = ( 3239 - world_y) / 4.4
```

The 1024 px image therefore covers **X −2476 … 2030, Y −1267 … 3239** (4,505.6 × 4,505.6 units).

Valve's anchor positions (normalised image coordinates → world coordinates):

| Anchor | Normalised (x, y) | World (x, y) |
|---|---|---|
| Bombsite A | (0.80, 0.16) | (1128, 2518) |
| Bombsite B | (0.21, 0.12) | (-1530, 2698) |
| CT spawn | (0.62, 0.21) | (317, 2293) |
| T spawn | (0.39, 0.91) | (-719, -861) |

These are Valve's loading-screen anchor points, not the exact bombsite trigger polygons; the trigger
shapes live in the map entity data inside `de_dust2.vpk`.

### Getting a truly exact 2D map

1. **Rasterise the official radar.** The radar is a 1024×1024 top-down projection; floor pixels are
   teal/olive, walls/props are grey, background is transparent. Sampling it at 16 or 32 world units per
   cell (282×282 or 141×141 cells) gives a far more accurate walkable grid than the current 64×60 map,
   with the exact scale/origin above. This is the practical route for the 2D renderer.
2. **Extract the real collision mesh.** The local CS2 install contains
   `game/csgo/maps/de_dust2.vpk` (≈262 MB). Tools such as ValveResourceFormat/Source2Viewer can export
   the compiled map/geometry; rasterising that gives exact brushes, crates, thin walls and elevations.
   Heavier, but the only way to get collision-exact geometry rather than a radar trace.
3. **Hand-author from callouts** (what the current code does) — fastest, least exact.

The radar image and calibration are saved in the workspace:

- `docs/dust2/de_dust2_radar.png` (1024×1024 RGBA)
- `docs/dust2/de_dust2.txt` (Valve overview calibration)

Source of the extracted image: https://github.com/2mlml/cs2-radar-images (base asset is Valve's
`overviews/de_dust2_v2` radar; the green/orange site/spawn outlines are community annotations).

## 11. Sources

- Valve shipped config: `gamemode_casual.cfg` — https://raw.githubusercontent.com/SteamTracking/GameTracking-CS2/master/game/csgo/cfg/gamemode_casual.cfg
- Valve shipped config: `gamemode_competitive.cfg` — https://raw.githubusercontent.com/SteamTracking/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg
- CS2 weapon data: `scripts/weapons.vdata` — https://raw.githubusercontent.com/SteamTracking/GameTracking-CS2/master/game/csgo/pak01_dir/scripts/weapons.vdata
- CS2 item/equipment prices: `scripts/items/items_game.txt` — https://raw.githubusercontent.com/SteamTracking/GameTracking-CS2/master/game/csgo/pak01_dir/scripts/items/items_game.txt
- "CS2 Casual Explained: Money, Free Armor, Maps and Rules" (config-derived, Oct 2026) — https://gamertagmythras.com/blog/counter-strike-2/cs2-casual-mode-explained
- "CS2 Bomb Timer and Defuse Time: The New Blast Rules" (Sep 2026) — https://gamertagmythras.com/blog/counter-strike-2/cs2-bomb-timer-defuse-blast-guide
- Official CS2 site — https://www.counter-strike.net/cs2
- Valve release notes via Steam news API (`appid=730`).

Caveats: SteamDB's `GameTracking-CS2` mirrors the shipped client files; official matchmaking runs an
additional server-side config that is not shipped. Grenade radii/durations and movement multipliers
are engine constants rather than weapon-script values, so verify those in-game before hard-coding.

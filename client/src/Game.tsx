import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import { BASE } from './config'

type P = { id: string; name: string; team: string; x: number; y: number; angle: number; hp: number; weapon: string; actionProg: number }
type B = { id: string; name: string; team: string; kills: number; deaths: number; alive: boolean }
type Me = { money: number; hp: number; armor: number; ammo: number; weapon: string; actionProg: number; hasBomb: boolean; canBuy: boolean; reloading: boolean }
type Feed = { killer: string; kTeam: string; victim: string; vTeam: string; weapon: string }
type State = { t: number; ct: number; phase: string; left: number; round: number; msg: string; bomb: { planted: boolean; x: number | null; y: number | null; dropped: boolean }; doors: D[]; me: Me; players: P[]; board: B[]; feed: Feed[]; shots: number[][] }
type Rect = { x: number; y: number; w: number; h: number }
type Map = { width: number; height: number; walls: Rect[]; sites: Rect[] }
type Weapon = { id: string; name: string; price: number; team: string; mag: number }
type D = { x1: number; y1: number; x2: number; y2: number; open: number }

const CT = '#5b9bf0', T = '#f0b340'
const LABELS: [string, number, number][] = [
  ['A SITE', 3604, 721], ['B SITE', 946, 541], ['CT SPAWN', 2793, 946], ['T SPAWN', 1757, 4035], ['MID', 2400, 2400], ['LONG A', 3900, 1800],
  ['SHORT', 2900, 1300], ['B TUNNELS', 800, 1900], ['LOWER TUNNEL', 1600, 2400], ['TOP MID', 2400, 1500], ['OUTSIDE TUNNEL', 900, 3000],
  ['T RAMP', 1200, 3400], ['PIT', 3900, 2900], ['LOWER MID', 2200, 1400], ['B DOORS', 1150, 1150], ['LONG DOORS', 3890, 2464],
  ['MID DOORS', 2695, 1144],
]

function roundRect(c: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  c.beginPath(); c.moveTo(x + r, y); c.arcTo(x + w, y, x + w, y + h, r); c.arcTo(x + w, y + h, x, y + h, r)
  c.arcTo(x, y + h, x, y, r); c.arcTo(x, y, x + w, y, r); c.closePath()
}

function buildMap(map: Map, radar: HTMLImageElement): HTMLCanvasElement {
  const k = 0.5; const cv = document.createElement('canvas'); cv.width = map.width * k; cv.height = map.height * k
  const c = cv.getContext('2d')!; c.scale(k, k)
  c.fillStyle = '#0b0b0d'; c.fillRect(0, 0, map.width, map.height)
  if (radar.complete && radar.naturalWidth) {
    c.filter = 'brightness(1.28) saturate(1.05)'
    c.drawImage(radar, 0, 0, map.width, map.height)
    c.filter = 'none'
  } else {
    const g = c.createLinearGradient(0, 0, map.width, map.height); g.addColorStop(0, '#d2b27a'); g.addColorStop(1, '#bf9d62')
    c.fillStyle = g; c.fillRect(0, 0, map.width, map.height)
  }
  c.fillStyle = 'rgba(230,150,20,0.08)'; map.sites.forEach(w => c.fillRect(w.x, w.y, w.w, w.h))
  c.strokeStyle = 'rgba(255,170,30,0.55)'; c.lineWidth = 4; map.sites.forEach(w => c.strokeRect(w.x, w.y, w.w, w.h))
  c.font = 'bold 24px system-ui, sans-serif'; c.textAlign = 'center'; c.fillStyle = 'rgba(255,240,200,0.55)'
  LABELS.forEach(([t, x, y]) => c.fillText(t, x, y))
  c.fillStyle = 'rgba(255,190,40,0.9)'; c.font = 'bold 80px system-ui, sans-serif'
  const [a, b] = map.sites; c.fillText('A', a.x + a.w / 2, a.y + a.h / 2 + 28); c.fillText('B', b.x + b.w / 2, b.y + b.h / 2 + 28)
  return cv
}

export default function Game({ token, onExit }: { token: string; onExit: () => void }) {
  const ref = useRef<HTMLCanvasElement>(null)
  const [buy, setBuy] = useState(false); const [list, setList] = useState<Weapon[]>([]); const [team, setTeam] = useState('T'); const [money, setMoney] = useState(0)
  const [status, setStatus] = useState('Searching for a match…')

  useEffect(() => {
    const cv = ref.current!; const ctx = cv.getContext('2d')!
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE}/hub/game`, { accessTokenFactory: () => token }).build()
    let mapCv: HTMLCanvasElement | null = null; const radar = new Image(); radar.src = '/dust2_radar.png'; radar.onload = () => { mapCv = null }; const disp: Record<string, { x: number; y: number }> = {}; let lastHud = 0
    let me = ''; let myName = ''; let map: Map | null = null; let st: State | null = null; let weapons: Weapon[] = []; let myTeam = 'T'; let buyOpen = false
    let keys = 0; let firing = false; let mouse = { x: 0, y: 0 }; let tab = false
    let lastHp = 100; let hurt = 0
    const shots: { s: number[]; t: number }[] = []
    let raf = 0; let alive = true

    let sc = 1, ox = 0, oy = 0, camX = 0, camY = 0, camInit = false
    const VIEW_W = 1800, VIEW_H = 1000 // world units visible on screen
    const layout = () => {
      if (!map) return
      sc = Math.max(cv.width / VIEW_W, cv.height / VIEW_H)
      const hw = cv.width / (2 * sc), hh = cv.height / (2 * sc)
      camX = map.width > hw * 2 ? Math.min(Math.max(camX, hw), map.width - hw) : map.width / 2
      camY = map.height > hh * 2 ? Math.min(Math.max(camY, hh), map.height - hh) : map.height / 2
      ox = cv.width / 2 - camX * sc; oy = cv.height / 2 - camY * sc
    }
    const resize = () => { cv.width = innerWidth; cv.height = innerHeight; layout() }
    const bit: Record<string, number> = { KeyW: 1, KeyA: 2, KeyS: 4, KeyD: 8, KeyE: 16, ShiftLeft: 32 }
    const angle = () => {
      const you = st?.players.find(p => p.id === me)
      return you ? Math.atan2((mouse.y - oy) / sc - you.y, (mouse.x - ox) / sc - you.x) : 0
    }
    const send = () => conn.state === signalR.HubConnectionState.Connected && me && conn.send('Input', keys, angle(), firing)
    resize()
    const buyList = () => weapons.filter(w => w.price > 0 && (w.team === '' || w.team === myTeam))
    const kd = (e: KeyboardEvent) => {
      if (e.code === 'Tab') { e.preventDefault(); tab = true; return }
      if (bit[e.code]) { keys |= bit[e.code]; send() }
      else if (e.code === 'KeyR') conn.send('Reload')
      else if (e.code === 'KeyB') { buyOpen = !buyOpen; setBuy(buyOpen) }
      else if (buyOpen && /^Digit[1-9]$/.test(e.code)) { const w = buyList()[+e.code.slice(5) - 1]; if (w) conn.invoke('Buy', w.id) }
    }
    const ku = (e: KeyboardEvent) => { if (e.code === 'Tab') tab = false; if (bit[e.code]) { keys &= ~bit[e.code]; send() } }
    const mm = (e: MouseEvent) => { mouse = { x: e.clientX, y: e.clientY }; send() }
    const md = (e: MouseEvent) => { if (e.button === 0) { firing = true; send() } }
    const mu = (e: MouseEvent) => { if (e.button === 0) { firing = false; send() } }
    addEventListener('keydown', kd); addEventListener('keyup', ku); addEventListener('mousemove', mm)
    addEventListener('mousedown', md); addEventListener('mouseup', mu); addEventListener('resize', resize)
    const iv = setInterval(send, 50)

    conn.on('state', (s: State) => { st = s; myTeam = s.players.find(p => p.id === me)?.team ?? myTeam; setTeam(myTeam); s.shots.forEach(x => shots.push({ s: x, t: performance.now() })) })
    conn.start().then(() => conn.invoke('FindMatch')).then(r => {
      if (!alive) return; me = r.you; map = r.map; weapons = r.weapons; setList(weapons.filter(w => w.price > 0)); layout(); camInit = false; setStatus('')
    }).catch(() => setStatus('Connection failed'))

    const teamCol = (t: string) => (t === 'T' ? T : CT)
    const panel = (x: number, y: number, w: number, h: number, a = 0.6) => { ctx.fillStyle = `rgba(10,12,16,${a})`; roundRect(ctx, x, y, w, h, 6); ctx.fill() }
    const text = (s: string, x: number, y: number, size: number, color = '#fff', align: CanvasTextAlign = 'left', weight = '700') => {
      ctx.font = `${weight} ${size}px system-ui, sans-serif`; ctx.fillStyle = color; ctx.textAlign = align; ctx.fillText(s, x, y)
    }

    const draw = () => {
      raf = requestAnimationFrame(draw)
      ctx.fillStyle = '#0b0b0d'; ctx.fillRect(0, 0, cv.width, cv.height)
      if (!map || !st) return
      const you = st.players.find(p => p.id === me); if (!you) return
      myName = you.name
      const m = st.me; const now = performance.now()
      if (m.hp < lastHp) hurt = 1
      lastHp = m.hp; hurt = Math.max(0, hurt - 0.03)

      const focus = you.hp > 0 ? you : (st.players.find(p => p.team === myTeam && p.hp > 0) ?? you)
      if (!camInit) { camX = focus.x; camY = focus.y; camInit = true }
      else { camX += (focus.x - camX) * 0.25; camY += (focus.y - camY) * 0.25 }
      layout()

      ctx.save(); ctx.translate(ox, oy); ctx.scale(sc, sc)
      if (!mapCv) mapCv = buildMap(map, radar)
      ctx.drawImage(mapCv, 0, 0, map.width, map.height)

      // doors (world-space leaves)
      ;(st.doors ?? []).forEach(d => {
        ctx.strokeStyle = d.open > 0.5 ? 'rgba(120,85,50,0.75)' : '#6b4a2a'
        ctx.lineWidth = 8; ctx.beginPath(); ctx.moveTo(d.x1, d.y1); ctx.lineTo(d.x2, d.y2); ctx.stroke()
        ctx.strokeStyle = '#b08a5a'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(d.x1, d.y1); ctx.lineTo(d.x2, d.y2); ctx.stroke()
        ctx.fillStyle = '#d0b070'; ctx.beginPath(); ctx.arc(d.x2, d.y2, 3, 0, 7); ctx.fill()
      })

      // tracers
      while (shots.length && now - shots[0].t > 120) shots.shift()
      shots.forEach(({ s, t }) => {
        const a = 1 - (now - t) / 120
        ctx.strokeStyle = `rgba(255,230,120,${a})`; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(s[0], s[1]); ctx.lineTo(s[2], s[3]); ctx.stroke()
        ctx.fillStyle = `rgba(255,255,255,${a})`; ctx.beginPath(); ctx.arc(s[2], s[3], 5, 0, 7); ctx.fill()
      })

      // bomb
      if (st.bomb.x != null && st.bomb.y != null) {
        const blink = st.bomb.planted && Math.floor(now / (st.left < 10 ? 120 : 400)) % 2
        ctx.save(); ctx.translate(st.bomb.x, st.bomb.y)
        if (st.bomb.planted) { ctx.strokeStyle = `rgba(255,60,60,${blink ? 0.7 : 0.15})`; ctx.lineWidth = 4; ctx.beginPath(); ctx.arc(0, 0, 50, 0, 7); ctx.stroke() }
        ctx.fillStyle = '#222'; roundRect(ctx, -14, -9, 28, 18, 4); ctx.fill()
        ctx.fillStyle = blink ? '#ff3030' : '#661010'; ctx.fillRect(-8, -4, 16, 8)
        ctx.restore()
      }

      // players
      st.players.forEach(p => {
        const d = disp[p.id] ?? (disp[p.id] = { x: p.x, y: p.y })
        d.x += (p.x - d.x) * 0.4; d.y += (p.y - d.y) * 0.4
        if (Math.abs(p.x - d.x) > 300 || Math.abs(p.y - d.y) > 300) { d.x = p.x; d.y = p.y }
        const ally = p.team === myTeam
        if (p.hp <= 0) { ctx.fillStyle = 'rgba(0,0,0,0.45)'; ctx.beginPath(); ctx.arc(d.x, d.y, 8, 0, 7); ctx.fill(); return }
        ctx.save(); ctx.translate(d.x, d.y)
        ctx.fillStyle = 'rgba(0,0,0,0.3)'; ctx.beginPath(); ctx.ellipse(3, 5, 17, 15, 0, 0, 7); ctx.fill()
        ctx.rotate(p.angle)
        ctx.fillStyle = '#26282c'; roundRect(ctx, 6, -3.5, 26, 7, 2); ctx.fill() // gun
        ctx.fillStyle = teamCol(p.team); ctx.strokeStyle = '#101010'; ctx.lineWidth = 2
        ctx.beginPath(); ctx.arc(0, 0, 16, 0, 7); ctx.fill(); ctx.stroke()
        ctx.fillStyle = 'rgba(255,255,255,0.35)'; ctx.beginPath(); ctx.arc(5, 0, 5.5, 0, 7); ctx.fill() // head
        ctx.restore()
        if (p.id === me) { ctx.strokeStyle = '#7cff7c'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(d.x, d.y, 21, 0, 7); ctx.stroke() }
        if (ally && p.id !== me) {
          text(p.name, d.x, d.y - 26, 18, '#cfe6ff', 'center', '600')
          ctx.fillStyle = 'rgba(0,0,0,0.6)'; ctx.fillRect(d.x - 22, d.y - 21, 44, 5)
          ctx.fillStyle = p.hp > 30 ? '#6bd46b' : '#e05050'; ctx.fillRect(d.x - 22, d.y - 21, 44 * p.hp / 100, 5)
        }
        if (p.actionProg > 0) { ctx.fillStyle = 'rgba(0,0,0,0.6)'; ctx.fillRect(d.x - 24, d.y + 22, 48, 6); ctx.fillStyle = '#fff'; ctx.fillRect(d.x - 24, d.y + 22, 48 * p.actionProg, 6) }
        p.x = d.x; p.y = d.y
      })

      // view-range vignette around you
      const yd = disp[me] ?? you
      const gr = ctx.createRadialGradient(yd.x, yd.y, 500, yd.x, yd.y, 900)
      gr.addColorStop(0, 'rgba(0,0,0,0)'); gr.addColorStop(1, 'rgba(0,0,0,0.5)')
      ctx.fillStyle = gr; ctx.fillRect(0, 0, map.width, map.height)
      ctx.restore()

      // minimap (world is now larger than the viewport, so keep an overview)
      const mm = 178, mmx = 16, mmy = 56
      if (mapCv) {
        panel(mmx, mmy, mm, mm, 0.55)
        ctx.save(); ctx.globalAlpha = 0.85; ctx.drawImage(mapCv, mmx, mmy, mm, mm); ctx.restore()
        ctx.strokeStyle = 'rgba(255,255,255,0.35)'; ctx.lineWidth = 1; ctx.strokeRect(mmx, mmy, mm, mm)
        const kx = mm / map.width, ky = mm / map.height
        const hw = cv.width / (2 * sc), hh = cv.height / (2 * sc)
        ctx.strokeStyle = 'rgba(255,255,255,0.22)'; ctx.strokeRect(mmx + (camX - hw) * kx, mmy + (camY - hh) * ky, hw * 2 * kx, hh * 2 * ky)
        st.players.forEach(p => {
          if (p.hp <= 0) return
          ctx.fillStyle = p.id === me ? '#7cff7c' : teamCol(p.team)
          ctx.beginPath(); ctx.arc(mmx + p.x * kx, mmy + p.y * ky, 2.2, 0, 7); ctx.fill()
        })
        if (st.bomb.x != null && st.bomb.y != null) { ctx.fillStyle = '#ff4040'; ctx.beginPath(); ctx.arc(mmx + st.bomb.x * kx, mmy + st.bomb.y * ky, 3, 0, 7); ctx.fill() }
        text('DUST II', mmx + 6, mmy + mm - 6, 10, 'rgba(255,255,255,0.65)', 'left', '700')
      }

      const W = cv.width, H = cv.height
      const dead = m.hp <= 0
      if (hurt > 0) { const g = ctx.createRadialGradient(W / 2, H / 2, H * 0.35, W / 2, H / 2, H * 0.85); g.addColorStop(0, 'rgba(200,0,0,0)'); g.addColorStop(1, `rgba(200,0,0,${hurt * 0.55})`); ctx.fillStyle = g; ctx.fillRect(0, 0, W, H) }
      if (dead) { ctx.fillStyle = 'rgba(0,0,0,0.25)'; ctx.fillRect(0, 0, W, H) }

      // top scoreboard (CT left, T right)
      const ctAlive = st.board.filter(b => b.team === 'CT' && b.alive).length, tAlive = st.board.filter(b => b.team === 'T' && b.alive).length
      const secs = Math.ceil(st.left)
      const clock = st.bomb.planted ? String(secs) : `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`
      panel(W / 2 - 170, 10, 340, 64)
      ctx.fillStyle = CT; ctx.fillRect(W / 2 - 170, 10, 6, 64); ctx.fillStyle = T; ctx.fillRect(W / 2 + 164, 10, 6, 64)
      text(String(st.ct), W / 2 - 120, 50, 34, CT, 'center', '800'); text(String(st.t), W / 2 + 120, 50, 34, T, 'center', '800')
      text(clock, W / 2, 46, 30, st.bomb.planted || (st.phase === 'live' && secs <= 10) ? '#ff5050' : '#fff', 'center', '800')
      text(`${ctAlive} alive`, W / 2 - 120, 68, 12, '#9ab', 'center', '600'); text(`${tAlive} alive`, W / 2 + 120, 68, 12, '#9ab', 'center', '600')
      text(st.phase === 'freeze' ? 'FREEZE TIME' : st.bomb.planted ? 'BOMB PLANTED' : `ROUND ${st.round}`, W / 2, 22, 11, '#9ab', 'center', '700')

      // kill feed
      st.feed.forEach((f, i) => {
        const w = 300; const x = W - w - 14, y = 14 + i * 32
        panel(x, y, w, 26, 0.65)
        if (f.killer === myName || f.victim === myName) { ctx.strokeStyle = '#e04040'; ctx.lineWidth = 2; roundRect(ctx, x, y, w, 26, 6); ctx.stroke() }
        text(f.killer, x + 8, y + 18, 14, teamCol(f.kTeam)); text(f.weapon, x + w / 2, y + 18, 12, '#aaa', 'center', '500'); text(f.victim, x + w - 8, y + 18, 14, teamCol(f.vTeam), 'right')
      })

      // bottom-left health + armor
      panel(16, H - 86, 250, 70)
      text('✚', 30, H - 40, 28, m.hp > 30 ? '#fff' : '#ff5050'); text(String(Math.max(0, m.hp)), 66, H - 36, 44, m.hp > 30 ? '#fff' : '#ff5050', 'left', '800')
      ctx.fillStyle = 'rgba(255,255,255,0.15)'; ctx.fillRect(160, H - 62, 90, 6); ctx.fillStyle = '#9fc4ff'; ctx.fillRect(160, H - 62, 90 * m.armor / 100, 6)
      text(`🛡 ${m.armor}`, 160, H - 36, 22, '#d8e6ff')

      // bottom-right weapon + ammo
      const wp = weapons.find(w => w.id === m.weapon)
      panel(W - 266, H - 86, 250, 70)
      text(wp?.name ?? '', W - 30, H - 58, 14, '#bbb', 'right', '600')
      text(m.reloading ? 'RELOADING' : String(m.ammo), W - 30, H - 24, m.reloading ? 22 : 40, m.ammo === 0 ? '#ff5050' : '#fff', 'right', '800')
      if (!m.reloading) text(`/ ${wp?.mag ?? ''}`, W - 100, H - 24, 18, '#999', 'right', '600')

      // money
      text(`$${m.money}`, 24, H / 2, 30, '#8fe066', 'left', '800')
      if (m.hasBomb) { panel(16, H / 2 + 16, 210, 28); text('💣 Carrying bomb — E in site', 26, H / 2 + 36, 13, '#ffd070') }
      if (m.canBuy && !buyOpen) text('Press B to buy', 24, H / 2 + 70, 13, '#bbb', 'left', '600')

      // action bar
      if (m.actionProg > 0) { panel(W / 2 - 110, H - 110, 220, 22); ctx.fillStyle = '#fff'; ctx.fillRect(W / 2 - 104, H - 104, 208 * m.actionProg, 10) }

      // door hint
      if (!dead) {
        const nearDoor = (st.doors ?? []).some(d => {
          const vx = d.x2 - d.x1, vy = d.y2 - d.y1, len2 = vx * vx + vy * vy
          const t = len2 < 1e-6 ? 0 : Math.max(0, Math.min(1, ((you.x - d.x1) * vx + (you.y - d.y1) * vy) / len2))
          const dx = you.x - (d.x1 + vx * t), dy = you.y - (d.y1 + vy * t)
          return dx * dx + dy * dy < 80 * 80
        })
        if (nearDoor) text('E — open/close door', W / 2, H - 146, 14, '#ddd', 'center', '600')
      }

      // round-end / center messages
      if (st.msg) {
        const col = st.msg.startsWith('Terror') || st.msg.startsWith('Bomb exploded') ? T : CT
        panel(W / 2 - 280, 100, 560, 70, 0.7); ctx.fillStyle = col; ctx.fillRect(W / 2 - 280, 100, 560, 5)
        text(st.msg, W / 2, 144, 22, '#fff', 'center', '800')
      }
      if (dead) text('YOU ARE DEAD — spectating teammates', W / 2, H - 120, 18, '#ddd', 'center', '700')

      // scoreboard (hold Tab)
      if (tab) {
        const bw = 640, bh = 70 + 22 * 11; const bx = W / 2 - bw / 2, by = H / 2 - bh / 2
        panel(bx, by, bw, bh, 0.82)
        ;(['CT', 'T'] as const).forEach((tm, ci) => {
          const x = bx + 16 + ci * 312
          text(tm === 'CT' ? 'COUNTER-TERRORISTS' : 'TERRORISTS', x, by + 30, 14, teamCol(tm), 'left', '800')
          text('K', x + 230, by + 30, 12, '#999', 'right'); text('D', x + 285, by + 30, 12, '#999', 'right')
          st!.board.filter(b => b.team === tm).sort((a, b) => b.kills - a.kills).forEach((p, i) => {
            const y = by + 56 + i * 22; const c = p.alive ? '#fff' : '#666'
            text(p.name, x, y, 14, p.id === me ? '#7cff7c' : c, 'left', '600'); text(String(p.kills), x + 230, y, 14, c, 'right'); text(String(p.deaths), x + 285, y, 14, c, 'right')
          })
        })
      }

      // crosshair
      const cx = mouse.x, cy = mouse.y
      ctx.strokeStyle = '#3dff6a'; ctx.lineWidth = 2; ctx.beginPath()
      ctx.moveTo(cx - 14, cy); ctx.lineTo(cx - 5, cy); ctx.moveTo(cx + 5, cy); ctx.lineTo(cx + 14, cy)
      ctx.moveTo(cx, cy - 14); ctx.lineTo(cx, cy - 5); ctx.moveTo(cx, cy + 5); ctx.lineTo(cx, cy + 14); ctx.stroke()

      if (now - lastHud > 250) { lastHud = now; setMoney(m.money) }
    }
    draw()

    return () => {
      alive = false; cancelAnimationFrame(raf); clearInterval(iv)
      removeEventListener('keydown', kd); removeEventListener('keyup', ku); removeEventListener('mousemove', mm)
      removeEventListener('mousedown', md); removeEventListener('mouseup', mu); removeEventListener('resize', resize)
      conn.stop()
    }
  }, [token])

  return <>
    <canvas ref={ref} />
    {status && <div className="center" style={{ position: 'fixed', inset: 0 }}>{status}</div>}
    {buy && <div className="buy">
      <h3>BUY MENU <span>${money}</span></h3>
      {list.filter(w => w.team === '' || w.team === team).map((w, i) =>
        <div key={w.id} className="row" style={{ opacity: money >= w.price ? 1 : 0.4 }}><b>{i + 1}</b><span>{w.name}</span><em>${w.price}</em></div>)}
      <small>Press the number to buy. Buying closes 45s into the round. Armor &amp; kit are free.</small></div>}
    <button className="leave" onClick={onExit}>Leave</button>
  </>
}

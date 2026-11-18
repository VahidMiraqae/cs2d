import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'

type P = { id: string; name: string; team: string; x: number; y: number; angle: number; hp: number; weapon: string; actionProg: number }
type B = { id: string; name: string; team: string; kills: number; deaths: number; alive: boolean }
type Me = { money: number; hp: number; armor: number; ammo: number; weapon: string; actionProg: number; hasBomb: boolean; canBuy: boolean; reloading: boolean }
type State = { t: number; ct: number; phase: string; left: number; round: number; msg: string; bomb: { planted: boolean; x: number | null; y: number | null; dropped: boolean }; me: Me; players: P[]; board: B[]; shots: number[][] }
type Rect = { x: number; y: number; w: number; h: number }
type Map = { width: number; height: number; walls: Rect[]; sites: Rect[] }
type Weapon = { id: string; name: string; price: number; team: string; mag: number }

export default function Game({ token, onExit }: { token: string; onExit: () => void }) {
  const ref = useRef<HTMLCanvasElement>(null)
  const [buy, setBuy] = useState(false); const [list, setList] = useState<Weapon[]>([]); const [team, setTeam] = useState('T'); const [money, setMoney] = useState(0)
  const [status, setStatus] = useState('Searching for a matchâ€¦')

  useEffect(() => {
    const cv = ref.current!; const ctx = cv.getContext('2d')!
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5275/hub/game', { accessTokenFactory: () => token }).build()
    let mapCv: HTMLCanvasElement | null = null; const disp: Record<string, { x: number; y: number }> = {}; let lastHud = 0
    let me = ''; let map: Map | null = null; let st: State | null = null; let weapons: Weapon[] = []; let myTeam = 'T'; let buyOpen = false
    let keys = 0; let firing = false; let mouse = { x: 0, y: 0 }
    const shots: { s: number[]; t: number }[] = []
    let raf = 0; let alive = true

    const resize = () => { cv.width = innerWidth; cv.height = innerHeight; layout() }
    const bit: Record<string, number> = { KeyW: 1, KeyA: 2, KeyS: 4, KeyD: 8, KeyE: 16, ShiftLeft: 32 }
    let sc = 1, ox = 0, oy = 0
    const layout = () => { if (!map) return; sc = Math.min(cv.width / map.width, cv.height / map.height); ox = (cv.width - map.width * sc) / 2; oy = (cv.height - map.height * sc) / 2 }
    const angle = () => {
      const you = st?.players.find(p => p.id === me)
      return you ? Math.atan2((mouse.y - oy) / sc - you.y, (mouse.x - ox) / sc - you.x) : 0
    }
    const send = () => conn.state === signalR.HubConnectionState.Connected && me && conn.send('Input', keys, angle(), firing)
    resize()
    const buyList = () => weapons.filter(w => w.price > 0 && (w.team === '' || w.team === myTeam))
    const kd = (e: KeyboardEvent) => {
      if (bit[e.code]) { keys |= bit[e.code]; send() }
      else if (e.code === 'KeyR') conn.send('Reload')
      else if (e.code === 'KeyB') { buyOpen = !buyOpen; setBuy(buyOpen) }
      else if (buyOpen && /^Digit[1-9]$/.test(e.code)) { const w = buyList()[+e.code.slice(5) - 1]; if (w) conn.invoke('Buy', w.id) }
    }
    const ku = (e: KeyboardEvent) => { if (bit[e.code]) { keys &= ~bit[e.code]; send() } }
    const mm = (e: MouseEvent) => { mouse = { x: e.clientX, y: e.clientY }; send() }
    const md = (e: MouseEvent) => { if (e.button === 0) { firing = true; send() } }
    const mu = (e: MouseEvent) => { if (e.button === 0) { firing = false; send() } }
    addEventListener('keydown', kd); addEventListener('keyup', ku); addEventListener('mousemove', mm)
    addEventListener('mousedown', md); addEventListener('mouseup', mu); addEventListener('resize', resize)
    const iv = setInterval(send, 50)

    conn.on('state', (s: State) => { st = s; myTeam = s.players.find(p => p.id === me)?.team ?? myTeam; setTeam(myTeam); s.shots.forEach(x => shots.push({ s: x, t: performance.now() })) })
    conn.start().then(() => conn.invoke('FindMatch')).then(r => { if (!alive) return; me = r.you; map = r.map; weapons = r.weapons; setList(weapons.filter(w => w.price > 0)); layout(); setStatus('') })
      .catch(() => setStatus('Connection failed'))

    const draw = () => {
      raf = requestAnimationFrame(draw)
      ctx.fillStyle = '#111'; ctx.fillRect(0, 0, cv.width, cv.height)
      if (!map || !st) return
      const you = st.players.find(p => p.id === me); if (!you) return
      const m = st.me
      ctx.save(); ctx.translate(ox, oy); ctx.scale(sc, sc)
            if (!mapCv) {
        const k = 0.5; mapCv = document.createElement('canvas'); mapCv.width = map.width * k; mapCv.height = map.height * k
        const c = mapCv.getContext('2d')!; c.scale(k, k)
        c.fillStyle = '#c9a76a'; c.fillRect(0, 0, map.width, map.height)
        c.fillStyle = 'rgba(217,142,4,0.25)'; map.sites.forEach(w => c.fillRect(w.x, w.y, w.w, w.h))
        c.fillStyle = '#0c0c0c'; map.walls.forEach(w => c.fillRect(w.x - 0.5, w.y - 0.5, w.w + 1, w.h + 1))
      }
      ctx.drawImage(mapCv, 0, 0, map.width, map.height)
      const now = performance.now()
      while (shots.length && now - shots[0].t > 80) shots.shift()
      ctx.strokeStyle = '#ffe066'; ctx.lineWidth = 4
      shots.forEach(({ s }) => { ctx.beginPath(); ctx.moveTo(s[0], s[1]); ctx.lineTo(s[2], s[3]); ctx.stroke() })
      if (st.bomb.x != null && st.bomb.y != null) {
        ctx.fillStyle = st.bomb.planted && Math.floor(now / 250) % 2 ? '#ff2222' : '#aa0000'
        ctx.fillRect(st.bomb.x - 14, st.bomb.y - 10, 28, 20)
      }
      st.players.forEach(p => {
        const d = disp[p.id] ?? (disp[p.id] = { x: p.x, y: p.y })
        d.x += (p.x - d.x) * 0.4; d.y += (p.y - d.y) * 0.4
        if (Math.abs(p.x - d.x) > 300 || Math.abs(p.y - d.y) > 300) { d.x = p.x; d.y = p.y }
        p.x = d.x; p.y = d.y
        if (p.hp <= 0) return
        if (p.actionProg > 0) { ctx.fillStyle = '#fff'; ctx.fillRect(p.x - 30, p.y + 28, 60 * p.actionProg, 8) }
        ctx.save(); ctx.translate(p.x, p.y)
        ctx.fillStyle = p.team === 'T' ? '#e0a030' : '#3a7bd5'
        ctx.beginPath(); ctx.arc(0, 0, p.id === me ? 24 : 20, 0, 7); ctx.fill()
        ctx.rotate(p.angle); ctx.fillStyle = '#222'; ctx.fillRect(10, -4, 36, 8); ctx.restore()
        ctx.fillStyle = '#fff'; ctx.font = '28px sans-serif'; ctx.textAlign = 'center'; ctx.fillText(p.name, p.x, p.y - 34)
      })
      ctx.restore()
      if (now - lastHud > 250) { lastHud = now; setMoney(m.money) }
      const W = weapons.find(w => w.id === m.weapon)
      ctx.fillStyle = '#fff'; ctx.font = '20px sans-serif'; ctx.textAlign = 'left'
      ctx.fillText(m.hp > 0 ? `HP ${m.hp}   Armor ${m.armor}   ${W?.name ?? ''} ${m.reloading ? 'reloading…' : `${m.ammo}/${W?.mag ?? ''}`}` : 'You are dead — spectating teammates', 20, cv.height - 52)
      ctx.fillStyle = '#7ec850'; ctx.fillText(`$${m.money}${m.hasBomb ? '   [BOMB: hold E in a site to plant]' : ''}`, 20, cv.height - 24)
      if (m.actionProg > 0) { ctx.fillStyle = '#fff'; ctx.fillRect(cv.width / 2 - 100, cv.height - 80, 200 * m.actionProg, 10) }
      ctx.fillStyle = '#fff'; ctx.textAlign = 'center'
      const secs = Math.ceil(st.left)
      ctx.fillText(`T ${st.t}  –  CT ${st.ct}    Round ${st.round}`, cv.width / 2, 28)
      ctx.fillStyle = st.bomb.planted ? '#ff4444' : '#fff'
      ctx.fillText(st.phase === 'freeze' ? `Freeze ${secs}s — B: buy` : st.bomb.planted ? `BOMB ${secs}s` : `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`, cv.width / 2, 54)
      if (st.msg) { ctx.fillStyle = '#ffd84d'; ctx.font = '28px sans-serif'; ctx.fillText(st.msg, cv.width / 2, 100) }
      ctx.font = '14px sans-serif'; ctx.textAlign = 'right'
      ;[...st.board].sort((a, b) => b.kills - a.kills).forEach((p, i) => {
        ctx.fillStyle = p.alive ? '#fff' : '#777'
        ctx.fillText(`${p.name} [${p.team}] ${p.kills}/${p.deaths}`, cv.width - 16, 24 + i * 18)
      })
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
    {buy && <div style={{ position: 'fixed', left: 16, top: 60, background: '#000c', padding: 12, borderRadius: 6, minWidth: 240 }}>
      <b>Buy (${money}) — press number</b>
      {list.filter(w => w.team === '' || w.team === team).map((w, i) => <div key={w.id} style={{ opacity: money >= w.price ? 1 : 0.4 }}>{i + 1}. {w.name} — ${w.price}</div>)}
      <small>Buying closes 45s into the round. Armor &amp; kit are free in casual.</small></div>}
    <button style={{ position: 'fixed', left: 16, top: 16 }} onClick={onExit}>Leave</button>
  </>
}

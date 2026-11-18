import { useState } from 'react'
import Game from './Game'
import { BASE as API } from './config'


export default function App() {
  const [auth, setAuth] = useState<{ token: string; name: string } | null>(() => {
    const s = localStorage.getItem('auth'); return s ? JSON.parse(s) : null
  })
  const [u, setU] = useState(''); const [p, setP] = useState('')
  const [err, setErr] = useState(''); const [playing, setPlaying] = useState(false)

  async function submit(kind: 'login' | 'register') {
    setErr('')
    const r = await fetch(`${API}/api/${kind}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username: u, password: p }) })
    if (!r.ok) { setErr(r.status === 401 ? 'Invalid credentials' : await r.text()); return }
    const a = await r.json(); localStorage.setItem('auth', JSON.stringify(a)); setAuth(a)
  }

  if (!auth) return (
    <div className="center"><h1>CS2D<small>DUST II · CASUAL</small></h1>
      <input placeholder="Username" value={u} onChange={e => setU(e.target.value)} />
      <input placeholder="Password" type="password" value={p} onChange={e => setP(e.target.value)} />
      <div><button onClick={() => submit('login')}>Log in</button> <button onClick={() => submit('register')}>Register</button></div>
      <div className="err">{err}</div></div>)

  if (playing) return <Game token={auth.token} onExit={() => setPlaying(false)} />

  return (
    <div className="center"><h1>Welcome, {auth.name}</h1>
      <button onClick={() => setPlaying(true)}>Find casual match · Dust II</button>
      <button className="ghost" onClick={() => { localStorage.removeItem("auth"); setAuth(null) }}>Log out</button></div>)
}

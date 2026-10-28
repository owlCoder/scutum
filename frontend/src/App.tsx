import { useEffect, useState } from 'react'

export default function App() {
  const [health, setHealth] = useState('Proveravanje API-ja…')
  useEffect(() => {
    fetch('/api/health').then(r => r.ok ? r.json() : Promise.reject(r.status))
      .then(data => setHealth(data.status === 'ok' ? 'API dostupan' : 'API nije spreman'))
      .catch(() => setHealth('API nije dostupan (pokrenite backend).'))
  }, [])
  return <main>
    <p className="eyebrow">Security Lab / OIB</p>
    <h1>Security Lab</h1>
    <p>Platforma za interaktivne laboratorijske vežbe. Početni projektni šablon. Prave funkcionalnosti se razvijaju kroz dodeljene projektne celine.</p>
    <section><h2>Razvojno stanje</h2><p>{health}</p><p>KT1: započeta implementacija, arhitektura dela i DFD.</p></section>
  </main>
}

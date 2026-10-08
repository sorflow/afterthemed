import { useRef, useState, type DragEvent } from 'react'
import { FileUp, FolderOpen, ShieldCheck, X } from 'lucide-react'

export type AepItem = {
  id: string; name: string; folder: string; size: number; version: string
  status: 'ready' | 'current' | 'converting' | 'done' | 'error'
  detail: string; output: string; changes: string[]
}
export type AepState = { target: number; targets: number[]; busy: boolean; items: AepItem[] }

const statusLabels: Record<AepItem['status'], string> = {
  ready: 'Ready', current: 'No change needed', converting: 'Converting…', done: 'Done', error: "Can't convert",
}
const formatSize = (bytes: number) => bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`

export default function AepDowngrader({ state, send }: { state?: AepState; send: (type: string, value?: string) => void }) {
  const aep = state ?? { target: 24, targets: [24, 23, 22, 18], busy: false, items: [] }
  const input = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [note, setNote] = useState('')
  const ready = aep.items.filter(item => item.status === 'ready').length

  // Files go to the desktop app as File objects; WebView2 hands it their real paths, so nothing is copied.
  const addFiles = (files: FileList | null) => {
    const all = [...(files ?? [])]
    const projects = all.filter(file => /\.aep$/i.test(file.name))
    const webview = window.chrome?.webview
    if (!projects.length) { setNote(all.length ? 'Only After Effects projects (.aep) can be downgraded.' : ''); return }
    if (!webview?.postMessageWithAdditionalObjects) { setNote('Open the desktop app to downgrade projects.'); return }
    webview.postMessageWithAdditionalObjects({ type: 'aepAdd' }, projects)
    setNote(projects.length < all.length ? `${all.length - projects.length} file(s) skipped: only .aep projects can be downgraded.` : '')
  }
  const drop = (event: DragEvent) => {
    event.preventDefault()
    setDragging(false)
    if (!aep.busy) addFiles(event.dataTransfer.files)
  }

  return <main className="downgrader">
    <section className="downgrader-card surface" aria-labelledby="downgrader-title">
      <div className="section-heading panel-heading">
        <h2 id="downgrader-title">AEP Downgrader</h2>
        <p>Open projects from a newer After Effects in an older one. Your original file is never changed.</p>
      </div>

      <div className={`aep-drop${dragging ? ' dragging' : ''}`} role="button" tabIndex={0} aria-disabled={aep.busy}
        aria-label="Add After Effects projects: drop .aep files here or press Enter to choose files"
        onClick={() => !aep.busy && input.current?.click()}
        onKeyDown={event => { if ((event.key === 'Enter' || event.key === ' ') && !aep.busy) { event.preventDefault(); input.current?.click() } }}
        onDragEnter={event => { event.preventDefault(); setDragging(true) }}
        onDragOver={event => { event.preventDefault(); event.dataTransfer.dropEffect = aep.busy ? 'none' : 'copy' }}
        onDragLeave={event => { if (!event.currentTarget.contains(event.relatedTarget as Node)) setDragging(false) }}
        onDrop={drop}>
        <FileUp size={28} aria-hidden="true" />
        <strong>Drop .aep projects here</strong>
        <span>or click to choose files</span>
        <input ref={input} type="file" accept=".aep" multiple hidden onChange={event => { addFiles(event.target.files); event.target.value = '' }} />
      </div>
      {note && <p className="tool-note" role="status">{note}</p>}

      <div className="aep-target">
        <span className="field-label" id="aep-target-label">Save for</span>
        <div className="aep-target-options" role="radiogroup" aria-labelledby="aep-target-label">
          {aep.targets.map(target => <button key={target} type="button" role="radio" aria-checked={aep.target === target}
            disabled={aep.busy} onClick={() => send('aepTarget', String(target))}
            title={target === 18 ? 'Most compatible: also opens in After Effects 19.x, 20.x and 21.x' : undefined}>
            After Effects {target}.x</button>)}
        </div>
      </div>

      {aep.items.length > 0 && <ul className="aep-list" aria-label="Projects">
        {aep.items.map(item => <li className="aep-row" data-status={item.status} key={item.id}>
          <span className="installation-symbol" aria-hidden="true">Ae</span>
          <span className="aep-copy">
            <strong title={`${item.folder}\\${item.name}`}>{item.name}</strong>
            <small>{[item.version && `Made with After Effects ${item.version}`, item.size ? formatSize(item.size) : ''].filter(Boolean).join(' · ')}</small>
            {item.detail && <small className="aep-detail">{item.detail}{item.output ? ` as ${item.output}` : ''}</small>}
            {item.changes.length > 1 && <small className="aep-detail">{item.changes.slice(1).join(' · ')}</small>}
          </span>
          <span className={`aep-status ${item.status}`} role="status">{statusLabels[item.status]}</span>
          {item.status === 'done' && <button type="button" className="aep-row-action" onClick={() => send('aepReveal', item.id)}><FolderOpen size={14} /> Show</button>}
          {item.status !== 'converting' && <button type="button" className="aep-remove" aria-label={`Remove ${item.name}`} disabled={aep.busy}
            onClick={() => send('aepRemove', item.id)}><X size={14} /></button>}
        </li>)}
      </ul>}

      <div className="dialog-actions aep-actions">
        <button className="dialog-button ghost" disabled={aep.busy || !aep.items.length} onClick={() => send('aepClear')}>Clear list</button>
        <span className="dialog-action-spacer" />
        <button className="dialog-button accent" disabled={aep.busy || ready === 0} onClick={() => send('aepConvert')}>
          {aep.busy ? 'Converting…' : ready ? `Downgrade ${ready} ${ready === 1 ? 'project' : 'projects'}` : 'Downgrade'}</button>
      </div>
    </section>

    <aside className="downgrader-info surface">
      <div className="section-heading panel-heading"><h2>What changes</h2><p>The same edits After Effects makes with “Save As”.</p></div>
      <ul className="restore-steps">
        <li>The version header is rewritten for the release you pick.</li>
        <li>23.x and older: Material Options › Shadow Color (added in 24) is removed.</li>
        <li>22.x and older: Light Transmission is removed and layer records lose the fields added in 23.</li>
        <li>18.x is the fallback format: 19.x, 20.x and 21.x open it too.</li>
        <li>Everything else is copied byte for byte, and the copy is saved beside the original.</li>
      </ul>
      <div className="safety-note"><ShieldCheck size={18} /><span>Your original project is never modified or replaced.</span></div>
      <p className="downgrader-footnote">Effects and features that only exist in newer releases may still be missing when the older version opens the project.</p>
    </aside>
  </main>
}

import { useRef, useState, type DragEvent, type KeyboardEvent } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { FileUp, FolderOpen, Plus, X } from 'lucide-react'
import { duration, ease, leave } from './motion'

export type AepItem = {
  id: string; name: string; folder: string; size: number; version: string
  status: 'ready' | 'current' | 'converting' | 'done' | 'error'
  detail: string; output: string; changes: string[]
  path?: string; plannedOutput?: string
  /** Dry-run changes for the current target; null while the project is still being inspected. */
  preview?: string[] | null
}
export type AepState = { target: number; targets: number[]; busy: boolean; items: AepItem[] }

const statusLabels: Record<AepItem['status'], string> = {
  ready: 'Ready', current: 'Already compatible', converting: 'Converting…', done: 'Saved', error: "Can't convert",
}
// What each target changes, from the rules in AepDowngrader.cs.
const targetNotes: Record<number, string> = {
  24: 'Header only', 23: 'Drops Shadow Color', 22: 'Also converts layers', 18: 'Opens in 19.x–21.x too',
}
const layoutMove = { duration: duration.panel, ease: ease.move }
const formatSize = (bytes: number) => bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
const isLoss = (change: string) => change.startsWith('Removed') || change.startsWith('Could not')
const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? '' : 's'}`

export default function AepDowngrader({ state, send }: { state?: AepState; send: (type: string, value?: string) => void }) {
  const aep = state ?? { target: 24, targets: [24, 23, 22, 18], busy: false, items: [] }
  const input = useRef<HTMLInputElement>(null)
  const list = useRef<HTMLUListElement>(null)
  const [dragging, setDragging] = useState(false)
  const [note, setNote] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const items = aep.items
  const selected = items.find(item => item.id === selectedId) ?? items[0]
  const ready = items.filter(item => item.status === 'ready').length
  const count = (status: AepItem['status']) => items.filter(item => item.status === status).length

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
  const choose = () => { if (!aep.busy) input.current?.click() }
  const convert = () => { if (!aep.busy && ready > 0) send('aepConvert') }
  const drop = (event: DragEvent) => {
    event.preventDefault()
    setDragging(false)
    if (!aep.busy) addFiles(event.dataTransfer.files)
  }
  const keys = (event: KeyboardEvent) => {
    if (!event.ctrlKey) return
    if (event.key === 'Enter') { event.preventDefault(); convert() }
    else if (event.key.toLowerCase() === 'o') { event.preventDefault(); choose() }
  }
  const rowKeys = (event: KeyboardEvent<HTMLLIElement>, index: number) => {
    const move = event.key === 'ArrowDown' ? 1 : event.key === 'ArrowUp' ? -1 : 0
    if (move) {
      event.preventDefault()
      const next = items[Math.min(items.length - 1, Math.max(0, index + move))]
      setSelectedId(next.id)
      list.current?.querySelector<HTMLElement>(`[data-id="${next.id}"]`)?.focus()
    } else if ((event.key === 'Delete' || event.key === 'Backspace') && !aep.busy && items[index].status !== 'converting') {
      event.preventDefault()
      send('aepRemove', items[index].id)
    }
  }

  return <main className="downgrader" onKeyDown={keys}>
    <section className="downgrader-card surface" aria-labelledby="downgrader-title" data-dragging={dragging || undefined}
      onDragEnter={event => { event.preventDefault(); setDragging(true) }}
      onDragOver={event => { event.preventDefault(); event.dataTransfer.dropEffect = aep.busy ? 'none' : 'copy' }}
      onDragLeave={event => { if (!event.currentTarget.contains(event.relatedTarget as Node)) setDragging(false) }}
      onDrop={drop}>
      <header className="downgrader-head">
        <div className="section-heading panel-heading">
          <h1 id="downgrader-title" className="downgrader-title">AEP Downgrader</h1>
          <p>Save copies of newer projects that older After Effects releases can open.</p>
        </div>
        {items.length > 0 && <button type="button" className="downgrader-add" onClick={choose} disabled={aep.busy}>
          <Plus size={15} aria-hidden="true" /> Add projects</button>}
      </header>
      <input ref={input} type="file" accept=".aep" multiple hidden onChange={event => { addFiles(event.target.files); event.target.value = '' }} />

      <div className="aep-targets" role="radiogroup" aria-labelledby="aep-target-label">
        <span className="field-label" id="aep-target-label">Save for</span>
        {aep.targets.map(target => <button key={target} type="button" role="radio" aria-checked={aep.target === target}
          disabled={aep.busy} onClick={() => send('aepTarget', String(target))}>
          <strong>AE {target}.x</strong><small>{targetNotes[target] ?? ''}</small>
        </button>)}
      </div>

      {/* Continuity: the drop zone contracts into the list, and its file icon becomes the first row's icon. */}
      <AnimatePresence initial={false}>
        {items.length === 0 ? <motion.div key="drop" layoutId="aep-surface" className="aep-drop" role="button" tabIndex={0} aria-disabled={aep.busy}
          aria-label="Add After Effects projects: drop .aep files here or press Enter to choose files"
          onClick={choose} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); choose() } }}
          transition={{ layout: layoutMove }}>
          <motion.span layoutId="aep-lead" className="aep-drop-icon" transition={{ layout: layoutMove }}><FileUp size={28} aria-hidden="true" /></motion.span>
          <strong>Drop .aep projects here</strong>
          <span>or click to choose files <kbd>Ctrl+O</kbd></span>
          <small>The original is never changed. The copy is saved beside it.</small>
        </motion.div>
          : <motion.ul key="list" layoutId="aep-surface" ref={list} className="aep-list" role="listbox" aria-label="Projects"
            transition={{ layout: layoutMove }}>
            <AnimatePresence initial={false}>
              {items.map((item, index) => <motion.li key={item.id} data-id={item.id} className="aep-row" data-status={item.status}
                role="option" aria-selected={item === selected} tabIndex={item === selected ? 0 : -1}
                onClick={() => setSelectedId(item.id)} onKeyDown={event => rowKeys(event, index)}
                layout="position" initial={{ opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0 }}
                transition={{ duration: duration.row, ease: ease.enter }}>
                <motion.span layoutId={index === 0 ? 'aep-lead' : undefined} className="installation-symbol" aria-hidden="true" transition={{ layout: layoutMove }}>Ae</motion.span>
                <span className="aep-name"><strong title={item.path ?? item.name}>{item.name}</strong><small title={item.folder}>{item.folder}</small></span>
                <span className="aep-versions">{item.version || '—'}{item.status !== 'current' && item.status !== 'error' && <><i aria-hidden="true">→</i>{aep.target}.x</>}</span>
                <span className={`aep-status ${item.status}`}>{statusLabels[item.status]}
                  {item.status === 'ready' && item.preview && item.preview.filter(isLoss).length > 0 && <em> · {item.preview.filter(isLoss).length} removed</em>}</span>
                <span className="aep-size">{item.size ? formatSize(item.size) : ''}</span>
                {item.status !== 'converting' && <button type="button" className="aep-remove" aria-label={`Remove ${item.name}`} disabled={aep.busy}
                  onClick={event => { event.stopPropagation(); send('aepRemove', item.id) }}><X size={14} /></button>}
              </motion.li>)}
            </AnimatePresence>
          </motion.ul>}
      </AnimatePresence>
      {note && <p className="tool-note" role="status">{note}</p>}

      {items.length > 0 && <footer className="downgrader-bar">
        <span className="downgrader-summary">{plural(items.length, 'project')} · {formatSize(items.reduce((sum, item) => sum + item.size, 0))}
          {count('current') > 0 && ` · ${count('current')} already compatible`}{count('done') > 0 && ` · ${count('done')} saved`}{count('error') > 0 && ` · ${count('error')} failed`}</span>
        <span className="dialog-action-spacer" />
        <button type="button" className="dialog-button ghost" disabled={aep.busy} onClick={() => send('aepClear')}>Clear</button>
        <button type="button" className="dialog-button accent" disabled={aep.busy || ready === 0} onClick={convert}>
          {aep.busy ? 'Converting…' : ready ? `Downgrade ${plural(ready, 'project')}` : 'Nothing to convert'}
          {!aep.busy && ready > 0 && <kbd>Ctrl+Enter</kbd>}</button>
      </footer>}
    </section>

    <aside className="downgrader-inspector surface" aria-label="Compatibility">
      <AnimatePresence mode="wait" initial={false}>
        <motion.div key={selected?.id ?? 'none'} className="inspector-body" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0, transition: leave(duration.fast) }} transition={{ duration: duration.inspector, ease: ease.enter }}>
          {selected ? <Inspector item={selected} target={aep.target} send={send} /> : <HowItWorks />}
        </motion.div>
      </AnimatePresence>
    </aside>
  </main>
}

function Inspector({ item, target, send }: { item: AepItem; target: number; send: (type: string, value?: string) => void }) {
  const changes = item.status === 'done' ? item.changes : item.preview
  return <>
    <div className="inspector-title">
      <h2>{item.name}</h2>
      <small>{[item.version && `Made with After Effects ${item.version}`, item.size ? formatSize(item.size) : ''].filter(Boolean).join(' · ')}</small>
    </div>

    {item.status !== 'error' && <div className="version-rail" data-status={item.status}>
      <span>AE {item.version || '?'}</span><i aria-hidden="true" /><span>AE {target}.x</span>
    </div>}

    <section>
      <h3>Compatibility</h3>
      {item.status === 'current' ? <p className="inspector-text">Nothing to convert. This project already opens in After Effects {target}.x.</p>
        : item.status === 'error' ? <p className="inspector-text warn">{item.detail}</p>
        : !changes ? <p className="inspector-text">Inspecting the project…</p>
        : <ul className="change-list">{changes.map(change => <li key={change} data-loss={isLoss(change) || undefined}>{change}</li>)}</ul>}
    </section>

    {(item.path || item.plannedOutput) && <section>
      <h3>Files</h3>
      <dl className="path-list">
        {item.path && <><dt>Source</dt><dd>{item.path}</dd></>}
        {item.plannedOutput && <><dt>{item.status === 'done' ? 'Saved as' : 'Output'}</dt><dd>{item.plannedOutput}</dd></>}
      </dl>
      {item.status === 'done' && <button type="button" className="dialog-button ghost" onClick={() => send('aepReveal', item.id)}>
        <FolderOpen size={15} /> Show in folder</button>}
    </section>}

    <p className="inspector-footnote">The original is never modified. Effects that only exist in newer releases may still be missing in the older version.</p>
  </>
}

function HowItWorks() {
  return <>
    <div className="inspector-title"><h2>How it works</h2><small>The same edits After Effects makes with “Save As”.</small></div>
    <ul className="change-list">
      <li>The version header is rewritten for the release you pick.</li>
      <li data-loss>23.x and older: Material Options › Shadow Color is removed.</li>
      <li data-loss>22.x and older: Light Transmission is removed and layer records lose the fields added in 23.</li>
      <li>18.x is the fallback format: 19.x, 20.x and 21.x open it too.</li>
      <li>Everything else is copied byte for byte, beside the original.</li>
    </ul>
    <p className="inspector-footnote">Add projects to see exactly what changes in each one before anything is written.</p>
  </>
}

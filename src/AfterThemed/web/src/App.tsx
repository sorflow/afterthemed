import { useEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import {
  Activity, ArrowDownToLine, ArrowUpRight, Bug, Check, CheckCircle2, ChevronDown, CircleHelp,
  Copy, Download, ExternalLink, FileDown, FileSearch, FolderOpen, History, ImageUp, Info, Layers3, Leaf, Lock, LockOpen, Minus, Moon,
  Palette, Plus, RefreshCw, RotateCcw, ScanSearch, Settings2, Share2, ShieldCheck, Shuffle,
  Sparkles, Store, Sun, TriangleAlert, Type, Wand2, X,
} from 'lucide-react'
import AePreview from './AePreview'
import LogoMark from './LogoMark'
import AboutSocialIcon from './AboutSocialIcon'
import AepDowngrader, { type AepState } from './AepDowngrader'
import InstallThemeButton, { type InstallStatus } from './InstallThemeButton'
import PixelField from './PixelField'
import AppleColorPicker from './AppleColorPicker'
import Dropdown from './Dropdown'
import PaletteBrowser, { PaletteMiniature, PaletteStrip, type PresetPreview } from './PaletteBrowser'
import demoPresets from './demoPresets.json'
import { contrastReport, fixAll, generateFromColor, highlightFor, paletteFromImage, shuffle, themeThumbnail } from './paletteTools'

type EditorState = {
  type: 'state'
  installStatus?: InstallStatus
  installDetail?: string
  themeName: string
  presetIndex: number
  presets: string[]
  presetPreviews?: PresetPreview[]
  colors: Record<string, string>
  cutoff: number
  source: string
  target: string
  fonts: string[]
  font: string
  themePanels: boolean
  textReplacements: string
  importStatus: string
  panelStatus: string
  panelDetails: string
  log: string
  version: string
  installations: Array<{
    path: string
    name: string
    version: string
    hasCompanion: boolean
    source: string
  }>
  bugReport: null | {
    summary: string
    bundlePath: string
  }
  shareCode?: string | null
  installAll?: boolean
  history?: Array<{ id: string; name: string; installedAt: string; targets: string[]; colors: string[] }>
  replaced?: Array<{ target: string; install: string; name: string }>
  aep?: AepState
  navigate?: { tab: 'themes' | 'downgrader'; id: number } | null
  gallery?: { status: 'idle' | 'loading' | 'ready' | 'error'; error: string; items: Array<{ name: string; author: string; colors: string[] }> }
}

type UiTheme = 'dark' | 'light' | 'mocha' | 'fall'
const appearanceLabels: Record<UiTheme, string> = { dark: 'Blue', light: 'Ice', mocha: 'Midnight', fall: 'Fall' }
type DialogName = 'install' | 'bug' | 'about' | 'palettes' | 'share' | 'history' | 'gallery' | 'restore' | null
const roleVars = ['bg', 'panel', 'raised', 'text', 'primary', 'secondary', 'danger']
// Fall appearance: the Install sweep cycles through leaf colors, and these AE palettes are suggested.
const leafColors = ['#A43700', '#E08A1E', '#B5541B', '#F5C761', '#8A2E12', '#D9A441', '#C2541B']
const fallPicks = ['Harvest', 'Maple', 'Forest Floor', 'Golden Hour', 'Copper', 'Warm Paper', 'Graphite Amber', 'Gruvbox Dark', 'Sunset Dusk']
const desktopOnly = 'Open the desktop app to use this.'

type Command = {
  type: string
  value?: string
  key?: string
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: Command) => void
        postMessageWithAdditionalObjects?: (message: Command, objects: File[]) => void
        addEventListener: (type: 'message', listener: (event: MessageEvent<EditorState>) => void) => void
        removeEventListener: (type: 'message', listener: (event: MessageEvent<EditorState>) => void) => void
      }
    }
  }
}

const colorNames = [
  'App Background', 'Panel Color', 'Raised Surface', 'UI Text Color',
  'Primary Accent', 'Secondary Accent', 'Danger Accent',
] as const

const colorGroups = [
  { label: 'Surfaces', names: colorNames.slice(0, 3) },
  { label: 'Text', names: colorNames.slice(3, 4) },
  { label: 'Accents', names: colorNames.slice(4) },
]
const colorLabels: Record<string, string> = {
  'App Background': 'Background', 'Panel Color': 'Panels', 'Raised Surface': 'Raised',
  'UI Text Color': 'Interface text', 'Primary Accent': 'Primary',
  'Secondary Accent': 'Secondary', 'Danger Accent': 'Danger',
}

const demoState: EditorState = {
  type: 'state',
  installStatus: 'idle',
  installDetail: '',
  themeName: 'Hatsune-Miku-Accessible',
  presetIndex: 5,
  presets: demoPresets.map(preset => preset.label),
  presetPreviews: demoPresets,
  colors: Object.fromEntries(colorNames.map((name, index) => [name, demoPresets[5].colors[index]])),
  cutoff: demoPresets[5].cutoff,
  source: 'C:\\Users\\you\\AppData\\Local\\AfterThemed\\Originals\\dvaui.dll',
  target: 'C:\\Program Files\\Adobe\\Adobe After Effects 2026\\Support Files\\dvaui.dll',
  fonts: ['Adobe Clean · original', 'SF Pro Display', 'Inter'],
  font: 'Adobe Clean · original',
  themePanels: true,
  textReplacements: '',
  importStatus: 'BUILT-IN PRESET  ·  LIVE PREVIEW',
  panelStatus: '5 CEP · 1 SIGNED · CEP 12 DEBUG AUTO-ENABLE · 2 SCRIPTUI',
  panelDetails: 'CEP HTML/CSS · every detected panel is themed from a verified original backup\nSigned bundles use Adobe CEP developer mode.\n\nTHEME  Animation Composer  ·  4 HTML/CSS',
  log: '[12:06:42]  Detected one After Effects installation.\n[12:06:43]  Saved immutable original.\n[12:06:44]  Ready · Your theme is safe to edit.\n',
  version: '1.3.13',
  installations: [{
    path: 'C:\\Program Files\\Adobe\\Adobe After Effects 2026\\Support Files\\dvaui.dll',
    name: 'After Effects 2026',
    version: '26.0.0.0',
    hasCompanion: true,
    source: 'Program Files',
  }],
  bugReport: null,
}

const send = (type: string, value?: string, key?: string) =>
  window.chrome?.webview?.postMessage({ type, value, key })

function validColor(value: string | undefined, fallback: string) {
  return value && /^#[0-9a-fA-F]{6}$/.test(value) ? value : fallback
}

function IconButton({ label, children, onClick, className = '' }: {
  label: string, children: ReactNode, onClick: () => void, className?: string
}) {
  return <motion.button className={`icon-button ${className}`} type="button" title={label} aria-label={label} onClick={onClick} whileHover={{ scale: 1.045 }} whileTap={{ scale: .94 }} transition={{ type: 'spring', stiffness: 420, damping: 24 }}>{children}</motion.button>
}

function Modal({ title, description, icon, onClose, children, wide = false }: {
  title: string
  description: string
  icon: ReactNode
  onClose: () => void
  children: ReactNode
  wide?: boolean
}) {
  const dialog = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    const node = dialog.current
    node?.querySelector<HTMLElement>('button, input, textarea, select, [tabindex]:not([tabindex="-1"])')?.focus()
    const keydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        return
      }
      if (event.key !== 'Tab' || !node) return
      const focusable = Array.from(node.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'))
      if (!focusable.length) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', keydown)
    return () => {
      document.removeEventListener('keydown', keydown)
      previous?.focus()
    }
  }, [onClose])

  return <motion.div className="dialog-backdrop" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: .16 }} onMouseDown={event => {
    if (event.target === event.currentTarget) onClose()
  }}>
    <motion.div ref={dialog} className={`app-dialog ${wide ? 'app-dialog-wide' : ''}`} role="dialog" aria-modal="true" aria-labelledby="dialog-title" aria-describedby="dialog-description" initial={{ opacity: 0, y: 12, scale: .975 }} animate={{ opacity: 1, y: 0, scale: 1 }} exit={{ opacity: 0, y: 8, scale: .985 }} transition={{ type: 'spring', stiffness: 360, damping: 28 }}>
      <div className="dialog-heading">
        <span className="dialog-icon" aria-hidden="true">{icon}</span>
        <div><h2 id="dialog-title">{title}</h2><p id="dialog-description">{description}</p></div>
        <IconButton label="Close dialog" onClick={onClose}><X size={17} /></IconButton>
      </div>
      {children}
    </motion.div>
  </motion.div>
}

function HexField({ name, value, onChange }: { name: string, value: string, onChange: (value: string) => void }) {
  const [draft, setDraft] = useState(value)
  useEffect(() => setDraft(value), [value])
  const valid = /^#[0-9a-fA-F]{6}$/.test(draft)
  return <input id={`color-${name}`} aria-label={`${name} hex value`} aria-invalid={!valid}
    title={valid ? 'Hex color · #RRGGBB' : 'Enter # followed by six hexadecimal characters'}
    className="hex-field" value={draft} maxLength={7} spellCheck={false}
    onChange={event => { const next = event.target.value; setDraft(next); if (/^#[0-9a-fA-F]{6}$/.test(next)) onChange(next.toUpperCase()) }}
    onBlur={() => { if (!valid) setDraft(value) }}
    onKeyDown={event => { if (event.key === 'Escape') setDraft(value); if (event.key === 'Enter') event.currentTarget.blur() }} />
}

function App() {
  const [workspaceView, setWorkspaceView] = useState('preview')
  const [state, setState] = useState<EditorState>(demoState)
  const [page, setPage] = useState<'colors' | 'text' | 'panels'>('colors')
  const [activityOpen, setActivityOpen] = useState(false)
  const [activityPage, setActivityPage] = useState(0)
  const [moreOpen, setMoreOpen] = useState(false)
  const [appearanceOpen, setAppearanceOpen] = useState(false)
  const [dialog, setDialog] = useState<DialogName>(null)
  const [selectedInstall, setSelectedInstall] = useState('')
  const [mode, setMode] = useState<'themes' | 'downgrader'>(() => {
    try { return localStorage.getItem('afterthemed-mode') === 'downgrader' ? 'downgrader' : 'themes' } catch { return 'themes' }
  })
  const switchMode = (next: 'themes' | 'downgrader') => {
    setMode(next)
    try { localStorage.setItem('afterthemed-mode', next) } catch { /* the choice is only a convenience */ }
  }
  const [fallFilter, setFallFilter] = useState(false)
  // The desktop app asks for a tab when Explorer opens a file with AfterThemed.
  const handledNavigation = useRef(0)
  useEffect(() => {
    if (state.navigate && state.navigate.id !== handledNavigation.current) {
      handledNavigation.current = state.navigate.id
      switchMode(state.navigate.tab)
    }
  }, [state.navigate])
  const [highlight, setHighlight] = useState<number | null>(null)
  const [locked, setLocked] = useState<boolean[]>(() => colorNames.map(() => false))
  const [toolNote, setToolNote] = useState('')
  const [shareInput, setShareInput] = useState('')
  const imageInput = useRef<HTMLInputElement>(null)
  const [uiTheme, setUiTheme] = useState<UiTheme>(() => {
    const saved = localStorage.getItem('afterthemed-ui-theme')
    return saved === 'light' || saved === 'mocha' || saved === 'fall' ? saved : 'dark'
  })

  useEffect(() => {
    const webview = window.chrome?.webview
    if (!webview) return
    const receive = (event: MessageEvent<EditorState>) => {
      if (event.data?.type === 'state') setState(event.data)
    }
    webview.addEventListener('message', receive)
    webview.postMessage({ type: 'ready' })
    return () => webview.removeEventListener('message', receive)
  }, [])

  useEffect(() => {
    // Fall shares Blue's layout and swaps only the base colors (Palette.css).
    document.documentElement.dataset.uiTheme = uiTheme === 'fall' ? 'dark' : uiTheme
    if (uiTheme === 'fall') document.documentElement.dataset.uiPalette = 'fall'
    else delete document.documentElement.dataset.uiPalette
    localStorage.setItem('afterthemed-ui-theme', uiTheme)
  }, [uiTheme])

  useEffect(() => {
    if (!selectedInstall && state.installations.length) {
      setSelectedInstall(state.installations.find(item => item.path === state.target)?.path ?? state.installations[0].path)
    }
  }, [selectedInstall, state.installations, state.target])


  const edit = (field: keyof EditorState, value: string | number | boolean, type: string, key?: string) => {
    setState(previous => ({ ...previous, [field]: value }))
    send(type, String(value), key)
  }
  const editColor = (name: string, value: string) => {
    if (!/^#[0-9a-fA-F]{6}$/.test(value)) return
    setState(previous => ({ ...previous, presetIndex: previous.presets.length,
      colors: { ...previous.colors, [name]: value } }))
    send('color', value, name)
  }
  const installTheme = () => {
    if (window.chrome?.webview) send('install')
    else setState(previous => ({ ...previous, installStatus: 'idle', installDetail: 'Open the desktop app to install this theme.' }))
  }
  const selectPreset = (index: number) => {
    if (window.chrome?.webview) send('preset', String(index))
    else {
      const preset = demoPresets[index]
      if (preset) setState(previous => ({ ...previous, presetIndex: index, themeName: preset.label,
        colors: Object.fromEntries(colorNames.map((name, colorIndex) => [name, preset.colors[colorIndex]])),
        cutoff: preset.cutoff, importStatus: 'Built-in palette' }))
    }
    setDialog(null)
  }
  const resetPalette = () => {
    if (window.chrome?.webview) send('reset')
    else if (state.presetIndex < state.presets.length) selectPreset(state.presetIndex)
  }
  const focusColor = (name: string) => {
    setPage('colors')
    setWorkspaceView('customize')
    requestAnimationFrame(() => document.getElementById(`color-${name}`)?.focus())
  }
  const switchUiTheme = (next: UiTheme) => {
    const style = document.createElement('style')
    style.textContent = '*,*::before,*::after{transition:none!important}'
    document.head.append(style)
    setUiTheme(next)
    setAppearanceOpen(false)
    requestAnimationFrame(() => {
      void document.body.offsetHeight
      requestAnimationFrame(() => style.remove())
    })
  }
  const openDialog = (name: Exclude<DialogName, null>) => {
    setMoreOpen(false)
    setAppearanceOpen(false)
    setDialog(name)
    if (name === 'bug') send('reportBug')
    if (name === 'gallery' && state.gallery?.status !== 'ready' && state.gallery?.status !== 'loading') send('galleryLoad')
  }
  const swatches = state.colors
  const paletteColors = colorNames.map(name => validColor(swatches[name], '#000000'))
  const activePaletteName = state.presets[state.presetIndex] ?? 'Custom palette'
  const activeInstallation = state.installations.find(item => item.path === state.target)
  const applyColors = (next: string[]) => next.forEach((color, index) => {
    if (color !== paletteColors[index]) editColor(colorNames[index], color)
  })
  const report = contrastReport(paletteColors)
  const issues = report.filter(result => result.ratio < result.min)
  const fixRole = (role: number) => applyColors(fixAll(paletteColors, colorNames.map((_, index) => index !== role)))
  const importImage = async (file?: File) => {
    if (!file?.type.startsWith('image/')) { setToolNote('Choose an image file, such as a PNG or JPEG.'); return }
    try {
      applyColors(await paletteFromImage(file))
      setToolNote(`Palette taken from ${file.name}.`)
    } catch {
      setToolNote('That image could not be read. Try a PNG or JPEG.')
    }
  }
  const exportTheme = () => {
    if (window.chrome?.webview) send('exportTheme', themeThumbnail(paletteColors))
    else setState(previous => ({ ...previous, importStatus: desktopOnly }))
  }
  const highlightProps = (index: number) => ({
    onMouseEnter: () => setHighlight(index), onMouseLeave: () => setHighlight(null),
    onFocus: () => setHighlight(index), onBlur: () => setHighlight(null),
  })
  const previewStyle = {
    '--theme-bg': validColor(swatches['App Background'], '#10171D'),
    '--theme-panel': validColor(swatches['Panel Color'], '#1C2930'),
    '--theme-raised': validColor(swatches['Raised Surface'], '#2A3B43'),
    '--theme-text': validColor(swatches['UI Text Color'], '#EFFCFB'),
    '--theme-primary': validColor(swatches['Primary Accent'], '#44E0D2'),
    '--theme-secondary': validColor(swatches['Secondary Accent'], '#80B2FF'),
    '--theme-danger': validColor(swatches['Danger Accent'], '#FF7891'),
  } as CSSProperties

  return <motion.div className="app-shell" initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: .24, ease: [.2, 0, 0, 1] }}>
    <header className="titlebar">
      <div className="window-controls">
        <IconButton label="Close" onClick={() => send('close')} className="window-close"><X size={12} strokeWidth={2.5} /></IconButton>
        <IconButton label="Minimize" onClick={() => send('minimize')} className="window-minimize"><Minus size={12} strokeWidth={2.5} /></IconButton>
        <IconButton label="Maximize" onClick={() => send('maximize')} className="window-maximize"><Plus size={12} strokeWidth={2.5} /></IconButton>
      </div>
      <div className="brand" onMouseDown={event => { if (event.button === 0) send('drag') }}>
        <span className="brand-mark" aria-hidden="true"><LogoMark /></span>
        <span><strong>AfterThemed</strong><small>Theme studio</small></span>
      </div>
      <div className="mode-switch" role="tablist" aria-label="AfterThemed tools">
        <button type="button" role="tab" aria-selected={mode === 'themes'} onClick={() => switchMode('themes')}><Palette size={15} /><span>Themes</span></button>
        <button type="button" role="tab" aria-selected={mode === 'downgrader'} onClick={() => switchMode('downgrader')}><FileDown size={15} /><span>AEP Downgrader</span></button>
      </div>
      <div className="titlebar-drag" onMouseDown={event => { if (event.button === 0) send('drag') }} />
      <div className="title-actions">
        <div className="appearance-menu">
          <motion.button className="quiet-button appearance-trigger" whileHover={{ y: -1 }} whileTap={{ scale: .97 }} transition={{ duration: .12 }} aria-label={`Appearance: ${appearanceLabels[uiTheme]}`} aria-haspopup="menu" aria-expanded={appearanceOpen} onClick={() => setAppearanceOpen(!appearanceOpen)}>
            {uiTheme === 'light' ? <Sun size={15} /> : uiTheme === 'mocha' ? <Moon size={15} /> : uiTheme === 'fall' ? <Leaf size={15} /> : <Palette size={15} />}
            <span>{appearanceLabels[uiTheme]}</span>
            <ChevronDown size={13} />
          </motion.button>
          <AnimatePresence>
          {appearanceOpen && <motion.div className="appearance-popover" role="menu" aria-label="Appearance" initial={{ opacity: 0, y: -4, scale: .97 }} animate={{ opacity: 1, y: 0, scale: 1 }} exit={{ opacity: 0, y: -3, scale: .98 }} transition={{ duration: .14 }}>
            {([['dark', 'Blue', <Palette size={16} />], ['light', 'Ice', <Sun size={16} />], ['mocha', 'Midnight', <Moon size={16} />], ['fall', 'Fall', <Leaf size={16} />]] as const).map(([value, label, icon]) =>
              <button role="menuitemradio" aria-checked={uiTheme === value} key={value} onClick={() => switchUiTheme(value)}>{icon}<span>{label}</span>{uiTheme === value && <Check size={14} />}</button>)}
          </motion.div>}
          </AnimatePresence>
        </div>
        <motion.button className="quiet-button" whileHover={{ y: -1 }} whileTap={{ scale: .97 }} transition={{ duration: .12 }} onClick={() => openDialog('about')}><CircleHelp size={15} /> About</motion.button>
        <motion.button className="quiet-button" whileHover={{ y: -1 }} whileTap={{ scale: .97 }} transition={{ duration: .12 }} onClick={() => openDialog('bug')}><Bug size={15} /> Report bug</motion.button>
        {mode === 'themes' && <><span className="title-divider" />
        <motion.button className="secondary-button pixel-generate-button" whileHover={{ y: -1 }} whileTap={{ scale: .97 }} transition={{ duration: .12 }} onClick={() => send('generate')}><PixelField /><span className="generate-button-content"><Download size={14} /> Generate</span></motion.button>
        <InstallThemeButton onInstall={installTheme} status={state.installStatus} detail={state.installDetail} colors={uiTheme === 'fall' ? leafColors : paletteColors} /></>}
        <div className="more-menu"><button className="quiet-button" aria-label="More actions" aria-expanded={moreOpen} onClick={() => setMoreOpen(!moreOpen)}><Settings2 size={17} /></button>{moreOpen && <div className="more-popover"><button onClick={() => { send('generate'); setMoreOpen(false) }}>Generate</button><button onClick={() => openDialog('about')}>About</button><button onClick={() => openDialog('bug')}>Report bug</button></div>}</div>
      </div>
    </header>

    {mode === 'themes' ? <>
    <nav className="workspace-switcher" aria-label="Editor sections">
      {['project', 'preview', 'customize'].map(section => <button key={section} aria-pressed={workspaceView === section} onClick={() => setWorkspaceView(section)}>{section === 'project' ? <FolderOpen size={16} /> : section === 'preview' ? <Layers3 size={16} /> : <Settings2 size={16} />}{section[0].toUpperCase() + section.slice(1)}</button>)}
    </nav>
    <main className="workspace" data-view={workspaceView}>
      <aside className="project-card surface">
        <div className="section-heading panel-heading"><h2>Your theme</h2><p>A workspace that feels like you.</p></div>
        <label className="field-label" htmlFor="theme-name">Theme name</label>
        <input id="theme-name" className="text-field" value={state.themeName} onChange={e => edit('themeName', e.target.value, 'name')} spellCheck={false} />
        <span className="field-label palette-field-label">Starting palette</span>
        <button className="current-palette" onClick={() => openDialog('palettes')} aria-haspopup="dialog" aria-label={`Browse palettes. Current: ${activePaletteName}`}>
          <PaletteMiniature colors={paletteColors} />
          <span className="current-palette-name">{activePaletteName}</span>
          <PaletteStrip colors={paletteColors} />
          <span className="browse-palette-label">Browse palettes <ChevronDown size={15} /></span>
        </button>
        {uiTheme === 'fall' && !fallPicks.includes(activePaletteName) &&
          <button className="fall-suggestion" onClick={() => { setFallFilter(true); openDialog('palettes') }}><Leaf size={15} /> Pair with a fall palette</button>}
        <button className="import-palette" onClick={() => send('import')}><ArrowDownToLine size={16} /> Import theme file</button>
        <div className="theme-actions">
          <button onClick={() => openDialog('share')}><Share2 size={15} /> Share</button>
          <button onClick={exportTheme}><Download size={15} /> Export</button>
          <button onClick={() => openDialog('history')}><History size={15} /> History</button>
          <button onClick={() => openDialog('gallery')}><Store size={15} /> Gallery</button>
        </div>
        <details className="installation-details">
          <summary><span className="installation-symbol">Ae</span><span><strong>{activeInstallation?.name || (state.target ? 'Selected installation' : 'Choose installation')}</strong><small>Installation & original files</small></span><ChevronDown size={14} /></summary>
          <div className="installation-content">
            <div className="path-block"><span className="field-label">Preserved original</span><p title={state.source}>{state.source || 'No original selected yet'}</p><IconButton label="Open originals folder" onClick={() => send('openOriginals')}><FolderOpen size={16} /></IconButton></div>
            <div className="path-block"><span className="field-label">After Effects installation</span><p title={state.target}>{state.target || 'Choose an installation'}</p></div>
            <button className="wide-action" onClick={() => openDialog('install')}><ScanSearch size={16} /> Choose installation</button>
          </div>
        </details>
        <div className="project-spacer" />
        <div className="safety-note"><ShieldCheck size={18} /><span>Originals are preserved before a theme is installed.</span></div>
        <button className="restore-stock" onClick={() => openDialog('restore')}><RotateCcw size={16} /> Restore stock After Effects</button>
        <div className="utility-row">
          <button onClick={() => send('inventory')}>Inventory</button>
          <button onClick={() => send('openData')}>Files</button>
        </div>
      </aside>

      <section className="preview-card surface" aria-label="Live theme preview">
        <div className="preview-heading">
          <div className="panel-heading"><div className="preview-caption"><span className="live-indicator" />Live preview</div><h1 title={state.themeName}>{state.themeName || 'Untitled theme'}</h1><p>Your colors, in context.</p></div>
          <div className="preview-tools"><button onClick={resetPalette} disabled={state.presetIndex >= state.presets.length} title="Reset the selected built-in palette"><RotateCcw size={15} /> Reset palette</button></div>
        </div>
        {state.replaced?.map(item => <div className="reapply-banner" role="alert" key={item.target}>
          <RefreshCw size={16} aria-hidden="true" />
          <span><strong>{item.install} changed</strong> since you installed “{item.name}”. An After Effects update usually replaces the theme.</span>
          <button onClick={() => send('reapply', item.target)}>Re-apply</button>
          <button className="reapply-dismiss" aria-label={`Dismiss re-apply for ${item.install}`} onClick={() => send('dismissReapply', item.target)}><X size={14} /></button>
        </div>)}
        <div className="preview-stage" style={highlight === null ? previewStyle : { ...previewStyle, '--flash': highlightFor(paletteColors[highlight]) } as CSSProperties}
          data-highlight={highlight === null ? undefined : roleVars[highlight]}>
          <AePreview themeName={state.themeName} />
        </div>
        <div className="palette-ribbon" aria-label="Theme color roles">{colorNames.map((name, index) => <button key={name} {...highlightProps(index)} onClick={() => focusColor(name)} aria-label={`Edit ${name}`} title={`${name}: ${paletteColors[index]}`}><span style={{ backgroundColor: paletteColors[index] }} /><small>{colorLabels[name]}</small></button>)}</div>
        <div className="preview-footer"><span title={state.importStatus} role="status">{state.importStatus || 'Live preview'}</span><button onClick={() => send('openOutput')}>Generated files <ArrowUpRight size={14} /></button></div>
      </section>

      <aside className="inspector-card surface">
        <div className="section-heading panel-heading"><h2>Customize</h2><p>Make every detail yours.</p></div>
        <div className="segmented" data-selected={page} role="tablist" aria-label="Customize theme" onKeyDown={event => {
          const tabs = ['colors', 'text', 'panels'] as const
          if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
          event.preventDefault()
          const next = event.key === 'Home' ? 0 : event.key === 'End' ? 2 : (tabs.indexOf(page) + (event.key === 'ArrowRight' ? 1 : 2)) % 3
          setPage(tabs[next])
          event.currentTarget.querySelectorAll<HTMLButtonElement>('[role="tab"]')[next]?.focus()
        }}>
          <button type="button" role="tab" id="customize-tab-colors" aria-controls="customize-panel-colors" tabIndex={page === 'colors' ? 0 : -1} aria-selected={page === 'colors'} className={page === 'colors' ? 'selected' : ''} onClick={() => setPage('colors')}><Palette size={15} /> Colors</button>
          <button type="button" role="tab" id="customize-tab-text" aria-controls="customize-panel-text" tabIndex={page === 'text' ? 0 : -1} aria-selected={page === 'text'} className={page === 'text' ? 'selected' : ''} onClick={() => setPage('text')}><Type size={15} /> Text</button>
          <button type="button" role="tab" id="customize-tab-panels" aria-controls="customize-panel-panels" tabIndex={page === 'panels' ? 0 : -1} aria-selected={page === 'panels'} className={page === 'panels' ? 'selected' : ''} onClick={() => setPage('panels')}><Layers3 size={15} /> Panels</button>
        </div>
        {page === 'colors' && <div className="inspector-content" role="tabpanel" id="customize-panel-colors" aria-labelledby="customize-tab-colors"
          onDragOver={event => { if (event.dataTransfer.types.includes('Files')) event.preventDefault() }}
          onDrop={event => { event.preventDefault(); void importImage(event.dataTransfer.files[0]) }}>
          <div className="palette-tools" role="group" aria-label="Palette tools">
            <button type="button" onClick={() => applyColors(generateFromColor(paletteColors))} title="Match primary: build Background, Panels, Raised and Interface text from your Primary color" aria-label="Match surfaces to primary"><Wand2 size={15} /> Match</button>
            <button type="button" onClick={() => applyColors(shuffle(paletteColors, locked))} title="Shuffle: new colors for every unlocked role" aria-label="Shuffle unlocked colors"><Shuffle size={15} /> Shuffle</button>
            <button type="button" onClick={() => imageInput.current?.click()} title="From image: take a palette from an image. You can also drop an image here." aria-label="Palette from image"><ImageUp size={15} /> Image</button>
            <input ref={imageInput} type="file" accept="image/*" hidden onChange={event => { void importImage(event.target.files?.[0]); event.target.value = '' }} />
          </div>
          {toolNote && <p className="tool-note" role="status">{toolNote}</p>}
          <div className="color-groups">{colorGroups.map(group => <fieldset className="color-group" key={group.label}>
            <legend>{group.label}</legend>
            <div className="color-list">{group.names.map(name => {
              const index = colorNames.indexOf(name)
              const result = report.find(item => item.role === index)
              return <div className="color-row" key={name} data-locked={locked[index] || undefined} {...highlightProps(index)}>
                <AppleColorPicker label={name} value={validColor(swatches[name], '#000000')} onChange={value => editColor(name, value)} />
                <span className="color-label-stack">
                  <label htmlFor={`color-${name}`}>{colorLabels[name]}</label>
                  {result && <small className={`contrast-badge${result.ratio < result.min ? ' failing' : ''}`}
                    title={`${colorLabels[name]} on ${colorLabels[colorNames[result.against]]}: ${result.ratio.toFixed(2)}:1 (needs ${result.min}:1)`}>
                    {result.ratio < result.min && <TriangleAlert size={10} aria-hidden="true" />}{result.ratio.toFixed(1)}:1{result.ratio < result.min ? ` · needs ${result.min}` : ''}</small>}
                </span>
                <HexField name={name} value={swatches[name] || ''} onChange={value => editColor(name, value)} />
                <button type="button" className="lock-toggle" aria-pressed={locked[index]} aria-label={`${locked[index] ? 'Unlock' : 'Lock'} ${colorLabels[name]}`}
                  title={locked[index] ? 'Locked: Shuffle keeps this color' : 'Lock to keep this color when shuffling'}
                  onClick={() => setLocked(previous => previous.map((value, item) => item === index ? !value : value))}>
                  {locked[index] ? <Lock size={13} /> : <LockOpen size={13} />}</button>
              </div>
            })}</div>
          </fieldset>)}</div>
          {issues.length > 0 && <div className="contrast-warnings" role="status">
            <div className="contrast-warnings-heading"><TriangleAlert size={15} aria-hidden="true" /><strong>{issues.length} contrast {issues.length === 1 ? 'issue' : 'issues'}</strong>
              <button type="button" onClick={() => applyColors(fixAll(paletteColors, locked))}>Fix all</button></div>
            <ul>{issues.map(issue => <li key={issue.role}>
              <span>{colorLabels[colorNames[issue.role]]} on {colorLabels[colorNames[issue.against]]} is {issue.ratio.toFixed(1)}:1 · needs {issue.min}:1</span>
              <button type="button" onClick={() => fixRole(issue.role)}>Fix</button>
            </li>)}</ul>
          </div>}
          <div className="cutoff-control"><div><label htmlFor="cutoff">Text contrast cutoff</label><strong>{(state.cutoff / 100).toFixed(2)}</strong></div><input id="cutoff" type="range" min="20" max="80" value={state.cutoff} onChange={e => edit('cutoff', Number(e.target.value), 'cutoff')} /><p>Adjust how the patcher balances foreground contrast.</p></div>
        </div>}
        {page === 'text' && <div className="inspector-content" role="tabpanel" id="customize-panel-text" aria-labelledby="customize-tab-text"><p className="content-intro">Replace exact interface strings. Each new value must be the same length or shorter.</p><label className="field-label" htmlFor="replacements">One replacement per line</label><textarea id="replacements" className="text-area" value={state.textReplacements} onChange={e => edit('textReplacements', e.target.value, 'text')} placeholder={'AdobeClean-Regular => SFProDisplay-Regular\n# Lines beginning with # are ignored'} spellCheck={false} /><p className="field-help">Format: original text =&gt; new text</p></div>}
        {page === 'panels' && <div className="inspector-content" role="tabpanel" id="customize-panel-panels" aria-labelledby="customize-tab-panels"><p className="content-intro">Extend your palette to compatible CEP panels and choose a safe installed font.</p><label className="field-label" htmlFor="font">DVAUI font</label><Dropdown id="font" label="DVAUI font" value={state.font} onChange={value => edit('font', value, 'font')} options={state.fonts.map(font => ({ value: font, label: font }))} /><label className="toggle-row"><span><strong>Theme extension panels</strong><small>Apply the palette when installing a theme</small></span><input type="checkbox" checked={state.themePanels} onChange={e => edit('themePanels', e.target.checked, 'themePanels')} /><span className="toggle-visual" /></label><div className="panel-actions"><button onClick={() => send('scanPanels')}><RefreshCw size={15} /> Rescan</button><button onClick={() => send('applyPanels')}><Sparkles size={15} /> Apply now</button></div><div className="panel-report"><strong>{state.panelStatus}</strong><pre>{state.panelDetails}</pre></div></div>}
        <div className="activity-anchor">
          <button className="activity-trigger" onClick={() => { setActivityOpen(!activityOpen); setActivityPage(0) }} aria-expanded={activityOpen}><span><Activity className="activity-wave" size={17} aria-hidden="true" /> Activity log</span><ChevronDown size={16} className={activityOpen ? '' : 'rotated'} /></button>
          <AnimatePresence initial={false}>{activityOpen && <motion.div className="activity-popover" initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: 6 }} transition={{ duration: .16 }}>
            <div className="activity-heading"><strong>Activity log</strong><button aria-label="Close activity log" onClick={() => setActivityOpen(false)}><X size={16} /></button></div>
            <pre>{state.log.trim().split('\n').reverse().slice(activityPage * 4, activityPage * 4 + 4).join('\n') || 'No activity yet.'}</pre>
            <div className="activity-pagination"><button disabled={activityPage === 0} onClick={() => setActivityPage(activityPage - 1)}>Newer</button><span>Page {activityPage + 1}</span><button disabled={(activityPage + 1) * 4 >= state.log.trim().split('\n').length} onClick={() => setActivityPage(activityPage + 1)}>Older</button></div>
          </motion.div>}</AnimatePresence>
        </div>
      </aside>
    </main>
    </> : <AepDowngrader state={state.aep} send={send} />}

    {dialog === 'palettes' && <Modal title="Find your starting palette" description="A new atmosphere for your After Effects workspace. Choose a palette, then make it yours." icon={<Palette size={20} />} onClose={() => { setDialog(null); setFallFilter(false) }} wide>
      <PaletteBrowser names={state.presets} previews={state.presetPreviews ?? []} selected={state.presetIndex} onSelect={selectPreset}
        featured={uiTheme === 'fall' ? { label: 'Fall picks', names: fallPicks } : undefined} featuredOnly={fallFilter} onFeaturedOnlyChange={setFallFilter} />
      <div className="dialog-actions palette-dialog-actions"><button className="dialog-button ghost" onClick={() => { setDialog(null); send('import') }}><ArrowDownToLine size={16} /> Import theme file</button><span className="dialog-action-spacer" /><button className="dialog-button ghost" onClick={() => setDialog(null)}>Done</button></div>
    </Modal>}

    {dialog === 'install' && <Modal title="Choose installation" description="Select the After Effects release AfterThemed should update." icon={<ScanSearch size={20} />} onClose={() => setDialog(null)}>
      <div className="dialog-section-label">Detected on this PC</div>
      <div className="install-list" role="radiogroup" aria-label="Detected After Effects installations">
        {state.installations.length ? state.installations.map(install => <label className={`install-option ${selectedInstall === install.path ? 'selected' : ''}`} key={install.path}>
          <input type="radio" name="installation" value={install.path} checked={selectedInstall === install.path} onChange={() => setSelectedInstall(install.path)} />
          <span className="install-radio" aria-hidden="true" />
          <span className="install-copy"><strong>{install.name}</strong><small>{install.path}</small><span className="install-meta">DVAUI {install.version}<i />{install.hasCompanion ? 'Native companion included' : 'DVAUI colors only'}<i />{install.source}</span></span>
          {selectedInstall === install.path && <CheckCircle2 size={19} aria-hidden="true" />}
        </label>) : <div className="dialog-empty"><FileSearch size={26} /><strong>No installation detected</strong><p>Browse to the installed <code>dvaui.dll</code> file to continue.</p></div>}
      </div>
      <label className="toggle-row install-all-row"><span><strong>Install to every detected version</strong>
        <small>{state.installations.length > 1 ? `Install theme also updates the other ${state.installations.length - 1} version${state.installations.length > 2 ? 's' : ''}.` : 'Only one After Effects version was detected.'}</small></span>
        <input type="checkbox" checked={!!state.installAll} disabled={state.installations.length < 2} onChange={event => edit('installAll', event.target.checked, 'installAll')} /><span className="toggle-visual" /></label>
      <div className="dialog-note"><ShieldCheck size={17} /><span>The Adobe original is preserved before this target is changed.</span></div>
      <div className="dialog-actions">
        <button className="dialog-button ghost" onClick={() => send('browseInstall')}><FolderOpen size={16} /> Browse for dvaui.dll</button>
        <span className="dialog-action-spacer" />
        <button className="dialog-button ghost" onClick={() => setDialog(null)}>Cancel</button>
        <button className="dialog-button accent" disabled={!selectedInstall} onClick={() => { send('chooseInstall', selectedInstall); setDialog(null) }}><Check size={16} /> Use installation</button>
      </div>
    </Modal>}

    {dialog === 'share' && <Modal title="Share theme" description="Anyone with AfterThemed can paste this code to get your exact palette." icon={<Share2 size={20} />} onClose={() => setDialog(null)}>
      <label className="dialog-section-label" htmlFor="share-code">Your share code</label>
      <div className="share-row">
        <input id="share-code" className="text-field share-code" readOnly value={state.shareCode ?? desktopOnly} onFocus={event => event.currentTarget.select()} />
        <button className="dialog-button accent" disabled={!state.shareCode} onClick={() => send('copyShareCode')}><Copy size={16} /> Copy</button>
      </div>
      <label className="dialog-section-label" htmlFor="share-paste">Use someone else's code</label>
      <div className="share-row">
        <input id="share-paste" className="text-field" placeholder="AT1-…" value={shareInput} spellCheck={false} onChange={event => setShareInput(event.target.value)} />
        <button className="dialog-button ghost" disabled={!shareInput.trim()} onClick={() => { send('applyShareCode', shareInput.trim()); setShareInput(''); setDialog(null) }}>Apply</button>
      </div>
      <div className="dialog-note"><Info size={17} /><span>Codes carry colors and contrast settings. Fonts and text replacements stay on your PC; use Export to share those.</span></div>
    </Modal>}

    {dialog === 'history' && <Modal title="Theme history" description="Your last 10 installed themes. Load one to keep editing, or reinstall it in one click." icon={<History size={20} />} onClose={() => setDialog(null)} wide>
      {state.history?.length ? <div className="history-list">{state.history.map(item => <div className="history-item" key={item.id}>
        <PaletteStrip colors={item.colors} />
        <span className="history-copy"><strong>{item.name}</strong><small>{new Date(item.installedAt).toLocaleString()} · {item.targets.join(', ')}</small></span>
        <button className="dialog-button ghost" onClick={() => { send('historyLoad', item.id); setDialog(null) }}>Load</button>
        <button className="dialog-button accent" onClick={() => { send('historyInstall', item.id); setDialog(null) }}><ArrowDownToLine size={15} /> Reinstall</button>
      </div>)}</div> : <div className="dialog-empty"><History size={26} /><strong>No installs yet</strong><p>Themes appear here after you install them.</p></div>}
    </Modal>}

    {dialog === 'gallery' && <Modal title="Community gallery" description="Themes shared by AfterThemed users. Pick one to preview it, then make it yours." icon={<Store size={20} />} onClose={() => setDialog(null)} wide>
      {!window.chrome?.webview ? <div className="dialog-empty"><Store size={26} /><strong>Gallery unavailable here</strong><p>{desktopOnly}</p></div>
        : state.gallery?.status === 'error' ? <div className="dialog-empty"><TriangleAlert size={26} /><strong>Gallery unavailable</strong><p>{state.gallery.error}</p><button className="dialog-button ghost" onClick={() => send('galleryLoad')}><RefreshCw size={15} /> Try again</button></div>
        : state.gallery?.status !== 'ready' ? <div className="dialog-loading" role="status"><span className="loading-ring" /><strong>Loading community themes…</strong></div>
        : state.gallery.items.length ? <div className="palette-grid gallery-grid">{state.gallery.items.map((item, index) =>
          <button type="button" className="palette-option" key={`${item.name}-${index}`} aria-label={`${item.name} by ${item.author || 'Anonymous'}`} onClick={() => { send('galleryUse', String(index)); setDialog(null) }}>
            <PaletteMiniature colors={item.colors} />
            <span className="palette-option-name"><span>{item.name}<small className="gallery-author">by {item.author || 'Anonymous'}</small></span></span>
            <PaletteStrip colors={item.colors} />
            <span className="palette-preview-action" aria-hidden="true">Preview theme<ArrowUpRight size={14} /></span>
          </button>)}</div>
        : <div className="dialog-empty"><Store size={26} /><strong>No community themes yet</strong><p>Be the first: submit your current theme.</p></div>}
      <div className="dialog-actions"><span className="dialog-action-spacer" />
        <button className="dialog-button ghost" disabled={!window.chrome?.webview} onClick={() => send('gallerySubmit')}><ExternalLink size={15} /> Submit current theme</button>
        <button className="dialog-button ghost" onClick={() => setDialog(null)}>Done</button></div>
    </Modal>}

    {dialog === 'restore' && <Modal title="Restore stock After Effects" description="Put Adobe's original interface files back for this installation." icon={<RotateCcw size={20} />} onClose={() => setDialog(null)}>
      <div className="restore-summary"><span className="installation-symbol">Ae</span><span><strong>{activeInstallation?.name ?? 'Selected installation'}</strong><small title={state.target}>{state.target || 'No installation selected'}</small></span></div>
      <ul className="restore-steps">
        <li>Your verified Adobe originals replace the themed files.</li>
        <li>Themed CEP panels go back to their backups.</li>
        <li>Your themes stay in History, so you can reinstall any of them later.</li>
      </ul>
      <div className="dialog-note"><ShieldCheck size={17} /><span>Close After Effects first. Windows will ask for permission.</span></div>
      <div className="dialog-actions"><span className="dialog-action-spacer" />
        <button className="dialog-button ghost" onClick={() => setDialog(null)}>Cancel</button>
        <button className="dialog-button accent" disabled={!state.target} onClick={() => { send('restore'); setDialog(null) }}><RotateCcw size={16} /> Restore</button></div>
    </Modal>}

    {dialog === 'bug' && <Modal title="Report a bug" description="Review the diagnostics before opening an issue. Nothing is uploaded automatically." icon={<Bug size={20} />} onClose={() => setDialog(null)} wide>
      {state.bugReport ? <>
        <div className="diagnostic-summary"><span><Info size={16} /> Diagnostics ready</span><small>No Adobe binaries are included. Your dvaui.dll stays on this PC.</small></div>
        <label className="dialog-section-label" htmlFor="bug-report-preview">Report preview</label>
        <textarea id="bug-report-preview" className="report-preview" readOnly value={state.bugReport.summary} />
        <p className="bundle-path" title={state.bugReport.bundlePath}>Bundle saved to <span>{state.bugReport.bundlePath}</span></p>
        <div className="dialog-actions">
          <button className="dialog-button ghost" onClick={() => send('showBugBundle')}><FolderOpen size={16} /> Show bundle</button>
          <button className="dialog-button ghost" onClick={() => send('copyBugReport')}><Copy size={16} /> Copy report</button>
          <span className="dialog-action-spacer" />
          <button className="dialog-button accent" onClick={() => send('openBugIssue')}><ExternalLink size={16} /> Open GitHub issue</button>
        </div>
      </> : <div className="dialog-loading" role="status"><span className="loading-ring" /><strong>Preparing diagnostics…</strong><p>Collecting versions, recent activity, and installation details.</p></div>}
    </Modal>}

    {dialog === 'about' && <Modal title="About AfterThemed" description={`Version ${state.version} · Created by Drerachi`} icon={<Info size={20} />} onClose={() => setDialog(null)}>
      <div className="about-hero">
        <span className="about-mark" aria-hidden="true"><LogoMark tile /></span>
        <div><strong>Make After Effects feel like yours.</strong><p>An independent community theme editor built around safe originals and reversible changes.</p></div>
      </div>
      <div className="about-credit">
        <p>Thanks to my family in the Blank server!</p>
        <div><span>Special thanks to</span><strong>Dallas · Jaidon · Ito · Star</strong></div>
        <p>and especially Tewzy for pushing me to do this fun project!<br />You're the best loser :D</p>
        <strong className="about-hashtags">#Blank2026 &nbsp; #bringbackrealprogramming</strong>
      </div>
      <div className="about-links" aria-label="AfterThemed links">
        <button onClick={() => send('openLink', 'x')}><AboutSocialIcon platform="x" /><span>X / @shonenvii</span><ExternalLink size={14} aria-hidden="true" /></button>
        <button onClick={() => send('openLink', 'youtube')}><AboutSocialIcon platform="youtube" /><span>YouTube / shonenshwty</span><ExternalLink size={14} aria-hidden="true" /></button>
        <button onClick={() => send('openLink', 'instagram')}><AboutSocialIcon platform="instagram" /><span>Instagram / @ripshonen</span><ExternalLink size={14} aria-hidden="true" /></button>
        <button onClick={() => send('openLink', 'discord')}><AboutSocialIcon platform="discord" /><span>Discord / Blank</span><ExternalLink size={14} aria-hidden="true" /></button>
      </div>
      <div className="dialog-actions about-actions">
        <button className="dialog-button ghost" onClick={() => send('openLink', 'legal')}>EULA & legal notices <ExternalLink size={14} /></button>
        <span className="dialog-action-spacer" />
        <button className="dialog-button accent" onClick={() => setDialog(null)}>Done</button>
      </div>
    </Modal>}
  </motion.div>
}

export default App

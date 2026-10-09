import { useEffect, useRef, useState, type CSSProperties, type FocusEvent, type KeyboardEvent as ReactKeyboardEvent, type ReactNode, type RefObject } from 'react'
import { AnimatePresence, MotionConfig, motion } from 'framer-motion'
import {
  Activity, ArrowDownToLine, ArrowUpRight, Bug, Check, CheckCircle2, ChevronDown, CircleHelp,
  Copy, Download, ExternalLink, FileCog, FileDown, FileSearch, FolderOpen, History, ImageUp, Info, Layers3, Leaf, Lock, LockOpen, Moon,
  Palette, RefreshCw, RotateCcw, ScanSearch, Settings2, Share2, ShieldCheck, Shuffle,
  ChevronsUpDown, MoreHorizontal, Search, Sparkles, Store, Sun, TriangleAlert, Type, X, Blend,
} from 'lucide-react'
import AePreview from './AePreview'
import CommandPalette, { type Command as PaletteCommand } from './CommandPalette'
import { duration, ease, leave } from './motion'
import LogoMark from './LogoMark'
import AboutSocialIcon from './AboutSocialIcon'
import AepDowngrader, { type AepState } from './AepDowngrader'
import InstallThemeButton, { InstallRail, type InstallStage, type InstallStatus } from './InstallThemeButton'
import PixelField from './PixelField'
import AppleColorPicker from './AppleColorPicker'
import Dropdown from './Dropdown'
import PaletteBrowser, { PaletteMiniature, PaletteStrip, type PresetPreview } from './PaletteBrowser'
import demoPresets from './demoPresets.json'
import { contrast, contrastReport, fixAll, generateFromColor, highlightFor, paletteFromImage, shuffle, themeThumbnail } from './paletteTools'

type EditorState = {
  type: 'state'
  ack?: number
  installStatus?: InstallStatus
  installDetail?: string
  installStage?: InstallStage
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
  maximized?: boolean
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
  seq?: number
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
  version: '2.1.0',
  installations: [{
    path: 'C:\\Program Files\\Adobe\\Adobe After Effects 2026\\Support Files\\dvaui.dll',
    name: 'After Effects 2026',
    version: '26.0.0.0',
    hasCompanion: true,
    source: 'Program Files',
  }],
  bugReport: null,
}

// Every command is numbered; the desktop app echoes the last number it handled as `ack`.
let sentSeq = 0
const send = (type: string, value?: string, key?: string) =>
  window.chrome?.webview?.postMessage({ type, value, key, seq: ++sentSeq })

function validColor(value: string | undefined, fallback: string) {
  return value && /^#[0-9a-fA-F]{6}$/.test(value) ? value : fallback
}

function IconButton({ label, children, onClick, className = '' }: {
  label: string, children: ReactNode, onClick: () => void, className?: string
}) {
  return <button className={`icon-button ${className}`} type="button" title={label} aria-label={label} onClick={onClick}>{children}</button>
}

/** Popup menu behavior: focus the checked or first item, arrows move, Escape returns to the trigger, leaving closes. */
function menuProps(close: () => void, trigger: RefObject<HTMLButtonElement | null>) {
  // Only visible items: some entries are hidden at certain window widths.
  const items = (menu: HTMLElement) => [...menu.querySelectorAll<HTMLElement>('[role^="menuitem"]')].filter(item => item.getClientRects().length > 0)
  return {
    ref: (menu: HTMLDivElement | null) => {
      if (menu && !menu.contains(document.activeElement))
        (items(menu).find(item => item.getAttribute('aria-checked') === 'true') ?? items(menu)[0])?.focus()
    },
    onKeyDown: (event: ReactKeyboardEvent<HTMLDivElement>) => {
      const list = items(event.currentTarget)
      const index = list.indexOf(document.activeElement as HTMLElement)
      const next = { ArrowDown: index + 1, ArrowUp: index - 1, Home: 0, End: list.length - 1 }[event.key]
      if (next !== undefined) { event.preventDefault(); list[(next + list.length) % list.length]?.focus() }
      else if (event.key === 'Escape') { event.preventDefault(); close(); trigger.current?.focus() }
      else if (event.key === 'Tab') close()
    },
    onBlur: (event: FocusEvent<HTMLDivElement>) => {
      const to = event.relatedTarget as Node | null
      if (!event.currentTarget.contains(to) && to !== trigger.current) close()
    },
  }
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

  return <motion.div className="dialog-backdrop" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0, transition: leave(duration.popover) }} transition={{ duration: duration.popover, ease: ease.enter }} onMouseDown={event => {
    if (event.target === event.currentTarget) onClose()
  }}>
    <motion.div ref={dialog} className={`app-dialog ${wide ? 'app-dialog-wide' : ''}`} role="dialog" aria-modal="true" aria-labelledby="dialog-title" aria-describedby="dialog-description" initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: 4, transition: leave(duration.inspector) }} transition={{ duration: duration.inspector, ease: ease.enter }}>
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
  const appearanceTrigger = useRef<HTMLButtonElement>(null)
  const moreTrigger = useRef<HTMLButtonElement>(null)
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
  const [commandOpen, setCommandOpen] = useState(false)
  // Motion follows the Windows animation setting unless the user asks to always animate.
  const [alwaysAnimate, setAlwaysAnimateState] = useState(() => {
    try { return localStorage.getItem('afterthemed-motion') === 'on' } catch { return false }
  })
  const setAlwaysAnimate = (on: boolean) => {
    setAlwaysAnimateState(on)
    try { localStorage.setItem('afterthemed-motion', on ? 'on' : 'system') } catch { /* only a convenience */ }
  }
  useEffect(() => {
    if (alwaysAnimate) document.documentElement.dataset.motion = 'on'
    else delete document.documentElement.dataset.motion
  }, [alwaysAnimate])
  // The install rail stays after a finished install until clicked, or for a few seconds after success.
  const [railFor, setRailFor] = useState<string | null>(null)
  useEffect(() => {
    setRailFor(null)
    if (state.installStatus !== 'installed') return
    const timer = setTimeout(() => setRailFor('installed'), 6000)
    return () => clearTimeout(timer)
  }, [state.installStatus])
  // A colored surround shifts how colors are perceived, so the preview sits on neutral gray unless turned off.
  const [neutralPreview, setNeutralPreviewState] = useState(() => {
    try { return localStorage.getItem('afterthemed-neutral-preview') !== 'false' } catch { return true }
  })
  const setNeutralPreview = (on: boolean) => {
    setNeutralPreviewState(on)
    try { localStorage.setItem('afterthemed-neutral-preview', String(on)) } catch { /* only a convenience */ }
  }
  // The desktop app asks for a tab when Explorer opens a file with AfterThemed.
  const handledNavigation = useRef(0)
  useEffect(() => {
    if (state.navigate && state.navigate.id !== handledNavigation.current) {
      handledNavigation.current = state.navigate.id
      switchMode(state.navigate.tab)
    }
  }, [state.navigate])
  const [highlight, setHighlight] = useState<number | null>(null)
  const stage = useRef<HTMLDivElement>(null)
  const [comparing, setComparing] = useState(false)
  const [base, setBase] = useState<{ name: string; colors: string[] } | null>(null)
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
      const next = event.data
      if (next?.type !== 'state') return
      // A state older than the latest command would undo edits still in flight (a color drag would
      // snap back), so keep the editor's own values until the desktop app has caught up.
      setState(previous => (next.ack ?? 0) < sentSeq ? {
        ...next, colors: previous.colors, presetIndex: previous.presetIndex, themeName: previous.themeName,
        cutoff: previous.cutoff, textReplacements: previous.textReplacements,
      } : next)
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

  // The comparison base is the palette as it was loaded: a preset, an import, a share code, history or gallery.
  const loadedPreset = state.presetIndex < state.presets.length ? state.presetIndex : -1
  const baseStatus = useRef<string | null>(null)
  useEffect(() => {
    // Editing turns the palette custom without a new load; only a load (new status or preset) moves the base.
    const loaded = baseStatus.current !== state.importStatus || loadedPreset >= 0
    baseStatus.current = state.importStatus
    if (!loaded) return
    setBase({ name: loadedPreset >= 0 ? state.presets[loadedPreset] : state.themeName || 'the loaded theme', colors: colorNames.map(name => validColor(state.colors[name], '#000000')) })
  }, [state.importStatus, loadedPreset])

  // Hold B to compare; Ctrl+K opens the command center; Ctrl+1 and Ctrl+2 switch tools.
  useEffect(() => {
    const typing = (target: EventTarget | null) => target instanceof HTMLElement && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName))
    const down = (event: KeyboardEvent) => {
      if (event.ctrlKey && event.key.toLowerCase() === 'k') { event.preventDefault(); setCommandOpen(open => !open); return }
      if (event.ctrlKey && (event.key === '1' || event.key === '2')) { event.preventDefault(); switchMode(event.key === '1' ? 'themes' : 'downgrader'); return }
      if (event.key.toLowerCase() === 'b' && !event.ctrlKey && !event.altKey && !event.repeat && !typing(event.target)) setComparing(true)
    }
    const up = (event: KeyboardEvent) => { if (event.key.toLowerCase() === 'b') setComparing(false) }
    window.addEventListener('keydown', down)
    window.addEventListener('keyup', up)
    const blur = () => setComparing(false)
    window.addEventListener('blur', blur)
    return () => { window.removeEventListener('keydown', down); window.removeEventListener('keyup', up); window.removeEventListener('blur', blur) }
  }, [])

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
  // Hold to compare shows the palette this theme started from instead of the edited one.
  const comparable = !!base && base.colors.join() !== paletteColors.join()
  const shown = comparing && comparable ? base!.colors : paletteColors
  const previewStyle = Object.fromEntries(roleVars.map((role, index) => [`--theme-${role}`, shown[index]])) as CSSProperties
  const lensRole = highlight === null || comparing ? null : roleVars[highlight]
  const lensFlash = highlight === null ? '' : highlightFor(paletteColors[highlight])
  const lensCount = lensRole ? stage.current?.querySelectorAll(`[data-role~="${lensRole}"]`).length ?? 0 : 0

  const themes = mode === 'themes'
  const commands: PaletteCommand[] = [
    { id: 'themes', group: 'Go to', label: 'Themes', shortcut: 'Ctrl+1', run: () => switchMode('themes') },
    { id: 'downgrader', group: 'Go to', label: 'AEP Downgrader', shortcut: 'Ctrl+2', run: () => switchMode('downgrader') },
    { id: 'install', group: 'Theme', label: 'Install theme', disabled: !themes, run: installTheme },
    { id: 'generate', group: 'Theme', label: 'Generate theme files', disabled: !themes, run: () => send('generate') },
    { id: 'palettes', group: 'Theme', label: 'Browse palettes', run: () => { switchMode('themes'); openDialog('palettes') } },
    { id: 'import', group: 'Theme', label: 'Import theme file', run: () => send('import') },
    { id: 'theme-dll', group: 'Theme', label: 'Theme a DLL file', run: () => send('themeDll') },
    { id: 'share', group: 'Theme', label: 'Share theme', run: () => openDialog('share') },
    { id: 'export', group: 'Theme', label: 'Export theme', run: exportTheme },
    { id: 'history', group: 'Theme', label: 'Theme history', run: () => openDialog('history') },
    { id: 'gallery', group: 'Theme', label: 'Community gallery', run: () => openDialog('gallery') },
    { id: 'match', group: 'Colors', label: 'Match surfaces to primary', disabled: !themes, run: () => applyColors(generateFromColor(paletteColors)) },
    { id: 'shuffle', group: 'Colors', label: 'Shuffle unlocked colors', disabled: !themes, run: () => applyColors(shuffle(paletteColors, locked)) },
    { id: 'fix-contrast', group: 'Colors', label: 'Fix all contrast issues', disabled: !themes || issues.length === 0, run: () => applyColors(fixAll(paletteColors, locked)) },
    { id: 'image', group: 'Colors', label: 'Palette from image', disabled: !themes, run: () => imageInput.current?.click() },
    { id: 'neutral', group: 'View', label: neutralPreview ? 'Show preview on the theme surface' : 'Show preview on neutral gray', run: () => setNeutralPreview(!neutralPreview) },
    ...(['dark', 'light', 'mocha', 'fall'] as const).map(value => ({ id: `appearance-${value}`, group: 'View', label: `Appearance: ${appearanceLabels[value]}`, disabled: uiTheme === value, run: () => switchUiTheme(value) })),
    { id: 'motion', group: 'View', label: alwaysAnimate ? 'Follow the Windows animation setting' : 'Animate even when Windows animations are off', run: () => setAlwaysAnimate(!alwaysAnimate) },
    { id: 'install-target', group: 'After Effects', label: 'Choose installation', run: () => openDialog('install') },
    { id: 'restore', group: 'After Effects', label: 'Restore stock After Effects', run: () => openDialog('restore') },
    { id: 'originals', group: 'Files', label: 'Open originals folder', run: () => send('openOriginals') },
    { id: 'data', group: 'Files', label: 'Open AfterThemed data folder', run: () => send('openData') },
    { id: 'inventory', group: 'Files', label: 'DLL color inventory', run: () => send('inventory') },
    { id: 'about', group: 'Help', label: 'About AfterThemed', run: () => openDialog('about') },
    { id: 'bug', group: 'Help', label: 'Report a bug', run: () => openDialog('bug') },
  ]

  return <MotionConfig reducedMotion={alwaysAnimate ? 'never' : 'user'}><motion.div className="app-shell" initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ duration: duration.panel, ease: ease.enter }}>
    <header className="titlebar" onDoubleClick={event => { if ((event.target as HTMLElement).closest('.titlebar-drag, .brand')) send('maximize') }}>
      <div className="brand" onMouseDown={event => { if (event.button === 0 && event.detail === 1) send('drag') }}>
        <span className="brand-mark" aria-hidden="true"><LogoMark /></span>
        <span><strong>AfterThemed</strong><small>Theme studio</small></span>
      </div>
      <nav className="mode-switch" aria-label="AfterThemed tools">
        <button type="button" aria-label="Themes" title="Themes" aria-current={mode === 'themes' ? 'page' : undefined} onClick={() => switchMode('themes')}><Palette size={15} aria-hidden="true" /><span>Themes</span></button>
        <button type="button" aria-label="AEP Downgrader" title="AEP Downgrader" aria-current={mode === 'downgrader' ? 'page' : undefined} onClick={() => switchMode('downgrader')}><FileDown size={15} aria-hidden="true" /><span>AEP Downgrader</span></button>
      </nav>
      <div className="titlebar-drag" onMouseDown={event => { if (event.button === 0 && event.detail === 1) send('drag') }} />
      <div className="title-actions">
        <div className="appearance-menu">
          <button ref={appearanceTrigger} className="quiet-button appearance-trigger" aria-label={`Appearance: ${appearanceLabels[uiTheme]}`} aria-haspopup="menu" aria-expanded={appearanceOpen} onClick={() => setAppearanceOpen(!appearanceOpen)}>
            {uiTheme === 'light' ? <Sun size={15} /> : uiTheme === 'mocha' ? <Moon size={15} /> : uiTheme === 'fall' ? <Leaf size={15} /> : <Palette size={15} />}
            <span>{appearanceLabels[uiTheme]}</span>
            <ChevronDown size={13} />
          </button>
          <AnimatePresence>
          {appearanceOpen && <motion.div className="appearance-popover" role="menu" aria-label="Appearance" {...menuProps(() => setAppearanceOpen(false), appearanceTrigger)} initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, transition: leave(duration.popover) }} transition={{ duration: duration.popover, ease: ease.enter }}>
            {([['dark', 'Blue', <Palette size={16} />], ['light', 'Ice', <Sun size={16} />], ['mocha', 'Midnight', <Moon size={16} />], ['fall', 'Fall', <Leaf size={16} />]] as const).map(([value, label, icon]) =>
              <button role="menuitemradio" aria-checked={uiTheme === value} tabIndex={-1} key={value} onClick={() => { switchUiTheme(value); appearanceTrigger.current?.focus() }}>{icon}<span>{label}</span>{uiTheme === value && <Check size={14} />}</button>)}
            <span className="menu-separator" role="separator" />
            <button role="menuitemcheckbox" aria-checked={alwaysAnimate} tabIndex={-1} className="menu-check"
              title="Windows animation effects are respected unless this is on" onClick={() => setAlwaysAnimate(!alwaysAnimate)}>
              <Sparkles size={16} /><span>Animate even when Windows animations are off</span>{alwaysAnimate && <Check size={14} />}</button>
          </motion.div>}
          </AnimatePresence>
        </div>
        <button className="quiet-button command-trigger" onClick={() => setCommandOpen(true)} aria-keyshortcuts="Control+K" title="Search every command (Ctrl+K)">
          <Search size={15} aria-hidden="true" /><span>Commands</span><kbd aria-hidden="true">Ctrl K</kbd></button>
        {mode === 'themes' && <><span className="title-divider" />
        <button className="secondary-button pixel-generate-button" onClick={() => send('generate')}><PixelField /><span className="generate-button-content"><Download size={14} aria-hidden="true" /> Generate</span></button>
        <span className="install-anchor">
          <InstallThemeButton onInstall={installTheme} status={state.installStatus} detail={state.installDetail} colors={uiTheme === 'fall' ? leafColors : paletteColors} />
          <AnimatePresence>{state.installStage && state.installStatus !== 'idle' && state.installStatus !== 'cancelled' && railFor !== state.installStatus &&
            <InstallRail key="rail" status={state.installStatus} stage={state.installStage} onDismiss={() => setRailFor(state.installStatus ?? null)} />}</AnimatePresence>
        </span></>}
        <div className="more-menu"><button ref={moreTrigger} className="quiet-button" aria-label="More" title="More" aria-haspopup="menu" aria-expanded={moreOpen} onClick={() => setMoreOpen(!moreOpen)}><MoreHorizontal size={18} /></button>
          {moreOpen && <div className="more-popover" role="menu" aria-label="More" {...menuProps(() => setMoreOpen(false), moreTrigger)}>
            {mode === 'themes' && <button role="menuitem" tabIndex={-1} className="menu-narrow-only" onClick={() => { send('generate'); setMoreOpen(false) }}><Download size={15} aria-hidden="true" />Generate theme files</button>}
            <button role="menuitem" tabIndex={-1} onClick={() => { send('openOriginals'); setMoreOpen(false) }}><FolderOpen size={15} aria-hidden="true" />Open originals folder</button>
            <button role="menuitem" tabIndex={-1} onClick={() => { send('openData'); setMoreOpen(false) }}><FolderOpen size={15} aria-hidden="true" />Open AfterThemed data folder</button>
            <button role="menuitem" tabIndex={-1} onClick={() => { send('inventory'); setMoreOpen(false) }}><ScanSearch size={15} aria-hidden="true" />DLL color inventory</button>
            <span className="menu-separator" role="separator" />
            <button role="menuitem" tabIndex={-1} onClick={() => openDialog('about')}><CircleHelp size={15} aria-hidden="true" />About AfterThemed</button>
            <button role="menuitem" tabIndex={-1} onClick={() => openDialog('bug')}><Bug size={15} aria-hidden="true" />Report a bug</button>
          </div>}</div>
      </div>
      {/* Windows caption buttons: the app runs on Windows, so it uses Windows window controls. */}
      <div className="caption-buttons">
        <button type="button" aria-label="Minimize" title="Minimize" onClick={() => send('minimize')}><span aria-hidden="true">{''}</span></button>
        <button type="button" aria-label={state.maximized ? 'Restore' : 'Maximize'} title={state.maximized ? 'Restore down' : 'Maximize'} onClick={() => send('maximize')}><span aria-hidden="true">{state.maximized ? '' : ''}</span></button>
        <button type="button" className="caption-close" aria-label="Close" title="Close" onClick={() => send('close')}><span aria-hidden="true">{''}</span></button>
      </div>
    </header>

    {mode === 'themes' ? <>
    <nav className="workspace-switcher" aria-label="Editor sections">
      {['project', 'preview', 'customize'].map(section => <button key={section} aria-pressed={workspaceView === section} onClick={() => setWorkspaceView(section)}>{section === 'project' ? <FolderOpen size={16} /> : section === 'preview' ? <Layers3 size={16} /> : <Settings2 size={16} />}{section[0].toUpperCase() + section.slice(1)}</button>)}
    </nav>
    <main className="workspace" data-view={workspaceView}>
      <aside className="project-card surface" aria-labelledby="theme-column-title">
        <header className="column-head"><h2 id="theme-column-title">Theme</h2></header>
        <label className="field-label" htmlFor="theme-name">Name</label>
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
        {/* Theme files as a quiet list: bordered buttons of equal weight hid the hierarchy. */}
        <nav className="theme-library" aria-label="Theme files">
          <button onClick={() => send('import')}><ArrowDownToLine size={16} aria-hidden="true" /><span>Import theme file</span></button>
          <button onClick={() => send('themeDll')} title="Theme a dvaui.dll (and AfterFXLib.dll) copied from another PC. The files you pick are never changed."><FileCog size={16} aria-hidden="true" /><span>Theme a DLL file</span></button>
          <span className="library-separator" aria-hidden="true" />
          <button onClick={() => openDialog('share')}><Share2 size={16} aria-hidden="true" /><span>Share code</span></button>
          <button onClick={exportTheme}><Download size={16} aria-hidden="true" /><span>Export file</span></button>
          <button onClick={() => openDialog('history')}><History size={16} aria-hidden="true" /><span>History</span></button>
          <button onClick={() => openDialog('gallery')}><Store size={16} aria-hidden="true" /><span>Community gallery</span></button>
        </nav>
        <div className="project-spacer" />
        {/* Where the theme goes stays in view: it is the one fact every install depends on. */}
        <section className="target-block" aria-label="Install target">
          <button className="target-row" onClick={() => openDialog('install')} title={state.target || 'Choose the After Effects installation to theme'}>
            <span className="installation-symbol" aria-hidden="true">Ae</span>
            <span className="target-copy"><small>Installs into</small><strong>{activeInstallation?.name || (state.target ? 'Selected installation' : 'Choose installation')}</strong></span>
            <ChevronsUpDown size={15} className="target-change" aria-hidden="true" />
          </button>
          <div className="target-foot">
            {state.source && <span className="target-safety" title="The Adobe-signed original is preserved, so every change can be undone"><ShieldCheck size={14} aria-hidden="true" /> Original preserved</span>}
            <button className="restore-link" aria-label="Restore stock After Effects" title="Restore stock After Effects" onClick={() => openDialog('restore')}>Restore stock</button>
          </div>
        </section>
      </aside>

      <section className="preview-card surface" aria-label="Live theme preview" data-neutral={neutralPreview || undefined}>
        <div className="preview-heading">
          <div className="panel-heading"><div className="preview-caption">{activeInstallation?.name ?? 'After Effects'} preview</div><h1 title={state.themeName}>{state.themeName || 'Untitled theme'}</h1><p>Based on {activePaletteName}</p></div>
          <div className="preview-tools"><button type="button" aria-pressed={neutralPreview} onClick={() => setNeutralPreview(!neutralPreview)}
            title="Neutral gray around the preview, so the surrounding color does not shift how you judge the theme">Neutral surround</button><button type="button" className="compare-button" disabled={!comparable} aria-pressed={comparing && comparable}
              title={comparable ? `Hold to compare with ${base!.name} (or hold B)` : 'Edit a color to compare with the starting palette'}
              onPointerDown={() => setComparing(true)} onPointerUp={() => setComparing(false)} onPointerLeave={() => setComparing(false)} onPointerCancel={() => setComparing(false)}
              onKeyDown={event => { if (event.key === ' ' || event.key === 'Enter') { event.preventDefault(); setComparing(true) } }}
              onKeyUp={() => setComparing(false)} onBlur={() => setComparing(false)}>Compare</button><button onClick={resetPalette} disabled={state.presetIndex >= state.presets.length} title="Reset the selected built-in palette"><RotateCcw size={15} /> Reset palette</button></div>
        </div>
        {state.replaced?.map(item => <div className="reapply-banner" role="alert" key={item.target}>
          <RefreshCw size={16} aria-hidden="true" />
          <span><strong>{item.install} changed</strong> since you installed “{item.name}”. An After Effects update usually replaces the theme.</span>
          <button onClick={() => send('reapply', item.target)}>Re-apply</button>
          <button className="reapply-dismiss" aria-label={`Dismiss re-apply for ${item.install}`} onClick={() => send('dismissReapply', item.target)}><X size={14} /></button>
        </div>)}
        <div ref={stage} className="preview-stage" style={lensRole ? { ...previewStyle, '--flash': lensFlash } as CSSProperties : previewStyle}
          data-highlight={lensRole ?? undefined}>
          <AePreview themeName={state.themeName} />
          {lensRole && <span className="lens-label" aria-hidden="true" style={{ background: lensFlash, color: contrast(lensFlash, '#000000') > contrast(lensFlash, '#FFFFFF') ? '#000000' : '#FFFFFF' }}>
            {colorLabels[colorNames[highlight!]]} · {lensCount} {lensCount === 1 ? 'place' : 'places'}</span>}
          {comparing && comparable && <span className="lens-label compare-label">Before · {base!.name}</span>}
        </div>
        <div className="palette-ribbon" aria-label="Theme color roles">{colorNames.map((name, index) => <button key={name} {...highlightProps(index)} onClick={() => focusColor(name)} aria-label={`Edit ${name}`} title={`${name}: ${paletteColors[index]}`}><span style={{ backgroundColor: paletteColors[index] }} /><small>{colorLabels[name]}</small><code>{paletteColors[index].slice(1)}</code></button>)}</div>
        <div className="preview-footer"><span title={state.importStatus} role="status">{state.importStatus || 'Live preview'}</span><button onClick={() => send('openOutput')}>Generated files <ArrowUpRight size={14} /></button></div>
      </section>

      <aside className="inspector-card surface" aria-labelledby="customize-column-title">
        <header className="column-head"><h2 id="customize-column-title">Customize</h2></header>
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
            <button type="button" onClick={() => applyColors(generateFromColor(paletteColors))} title="Match primary: build Background, Panels, Raised and Interface text from your Primary color" aria-label="Match surfaces to primary"><Blend size={15} /> Match</button>
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
        {page === 'panels' && <div className="inspector-content" role="tabpanel" id="customize-panel-panels" aria-labelledby="customize-tab-panels"><p className="content-intro">Extend your palette to compatible CEP panels and choose a safe installed font.</p><label className="field-label" htmlFor="font">DVAUI font</label><Dropdown id="font" label="DVAUI font" value={state.font} onChange={value => edit('font', value, 'font')} options={state.fonts.map(font => ({ value: font, label: font }))} /><label className="toggle-row"><span><strong>Theme extension panels</strong><small>Apply the palette when installing a theme</small></span><input type="checkbox" checked={state.themePanels} onChange={e => edit('themePanels', e.target.checked, 'themePanels')} /><span className="toggle-visual" /></label><div className="panel-actions"><button onClick={() => send('scanPanels')}><RefreshCw size={15} /> Rescan</button><button onClick={() => send('applyPanels')}>Apply now</button></div><div className="panel-report"><strong>{state.panelStatus}</strong><pre>{state.panelDetails}</pre></div></div>}
        <div className="activity-anchor">
          <button className="activity-trigger" onClick={() => { setActivityOpen(!activityOpen); setActivityPage(0) }} aria-expanded={activityOpen}><span><Activity className="activity-wave" size={17} aria-hidden="true" /> Activity log</span><ChevronDown size={16} className={activityOpen ? '' : 'rotated'} /></button>
          <AnimatePresence initial={false}>{activityOpen && <motion.div className="activity-popover" initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: 6, transition: leave(duration.popover) }} transition={{ duration: duration.popover, ease: ease.enter }}>
            <div className="activity-heading"><strong>Activity log</strong><button aria-label="Close activity log" onClick={() => setActivityOpen(false)}><X size={16} /></button></div>
            <pre>{state.log.trim().split('\n').reverse().slice(activityPage * 4, activityPage * 4 + 4).join('\n') || 'No activity yet.'}</pre>
            <div className="activity-pagination"><button disabled={activityPage === 0} onClick={() => setActivityPage(activityPage - 1)}>Newer</button><span>Page {activityPage + 1}</span><button disabled={(activityPage + 1) * 4 >= state.log.trim().split('\n').length} onClick={() => setActivityPage(activityPage + 1)}>Older</button></div>
          </motion.div>}</AnimatePresence>
        </div>
      </aside>
    </main>
    </> : <AepDowngrader state={state.aep} send={send} />}

    <AnimatePresence>{commandOpen && <CommandPalette key="commands" commands={commands} onClose={() => setCommandOpen(false)} />}</AnimatePresence>

    {dialog === 'palettes' && <Modal title="Palettes" description={`${state.presets.length} built-in palettes. Pick a base; every color stays editable.`} icon={<Palette size={20} />} onClose={() => { setDialog(null); setFallFilter(false) }} wide>
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

    {dialog === 'gallery' && <Modal title="Community gallery" description="Themes shared by AfterThemed users. Pick one to load it into the editor." icon={<Store size={20} />} onClose={() => setDialog(null)} wide>
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
  </motion.div></MotionConfig>
}

export default App

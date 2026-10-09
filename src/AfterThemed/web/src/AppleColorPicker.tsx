import { useEffect, useId, useLayoutEffect, useRef, useState, type CSSProperties, type KeyboardEvent, type PointerEvent } from 'react'
import { createPortal } from 'react-dom'
import { Pipette, X } from 'lucide-react'
import opacityChecker from './assets/figma-color-picker/opacity-checker.png'
import materialPointer from './assets/figma-color-picker/material-pointer.svg'
import addButton from './assets/figma-color-picker/add-button.svg'

type PickerTab = 'grid' | 'spectrum' | 'sliders'
type Hsv = { h: number; s: number; v: number }

type AppleColorPickerProps = {
  label: string
  value: string
  onChange: (value: string) => void
}

const colorGrid = [
  ['#FEFFFE','#EBEBEB','#D6D6D6','#C2C2C2','#ADADAD','#999999','#858585','#707070','#5C5C5C','#474747','#333333','#000000'],
  ['#00374A','#011D57','#11053B','#2E063D','#3C071B','#5C0701','#5A1C00','#583300','#563D00','#666100','#4F5504','#263E0F'],
  ['#004D65','#012F7B','#1A0A52','#450D59','#551029','#831100','#7B2900','#7A4A00','#785800','#8D8602','#6F760A','#38571A'],
  ['#016E8F','#0042A9','#2C0977','#61187C','#791A3D','#B51A00','#AD3E00','#A96800','#A67B01','#C4BC00','#9BA50E','#4E7A27'],
  ['#008CB4','#0056D6','#371A94','#7A219E','#99244F','#E22400','#DA5100','#D38301','#D19D01','#F5EC00','#C3D117','#669D34'],
  ['#00A1D8','#0061FD','#4D22B2','#982ABC','#B92D5D','#FF4015','#FF6A00','#FFAB01','#FCC700','#FEFB41','#D9EC37','#76BB40'],
  ['#01C7FC','#3A87FD','#5E30EB','#BE38F3','#E63B7A','#FE6250','#FE8648','#FEB43F','#FECB3E','#FFF76B','#E4EF65','#96D35F'],
  ['#52D6FC','#74A7FF','#864FFD','#D357FE','#EE719E','#FF8C82','#FEA57D','#FEC777','#FED977','#FFF994','#EAF28F','#B1DD8B'],
  ['#93E3FC','#A7C6FF','#B18CFE','#E292FE','#F4A4C0','#FFB5AF','#FFC5AB','#FED9A8','#FDE4A8','#FFFBB9','#F1F7B7','#CDE8B5'],
  ['#CBF0FF','#D2E2FE','#D8C9FE','#EFCAFE','#F9D3E0','#FFDAD8','#FFE2D6','#FEECD4','#FEF1D5','#FDFBDD','#F6FADB','#DEEED4'],
]

const defaultSwatches = ['#000000','#007AFF','#34C759','#FFCC00','#FF3B30','#7AC6F5','#AF52DE','#5856D6','#FF2D55']

function clamp(value: number, min = 0, max = 1) {
  return Math.min(max, Math.max(min, value))
}

function normalizeHex(value: string) {
  const hex = value.trim().replace(/^#/, '')
  return /^[0-9a-f]{6}$/i.test(hex) ? `#${hex.toUpperCase()}` : '#000000'
}

function hexToRgb(value: string) {
  const hex = normalizeHex(value)
  return {
    r: Number.parseInt(hex.slice(1, 3), 16),
    g: Number.parseInt(hex.slice(3, 5), 16),
    b: Number.parseInt(hex.slice(5, 7), 16),
  }
}

function rgbToHex(r: number, g: number, b: number) {
  return `#${[r, g, b].map(channel => Math.round(clamp(channel, 0, 255)).toString(16).padStart(2, '0')).join('').toUpperCase()}`
}

function hexToHsv(value: string): Hsv {
  const { r: red, g: green, b: blue } = hexToRgb(value)
  const r = red / 255, g = green / 255, b = blue / 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b), delta = max - min
  let h = 0
  if (delta) {
    if (max === r) h = 60 * (((g - b) / delta) % 6)
    else if (max === g) h = 60 * ((b - r) / delta + 2)
    else h = 60 * ((r - g) / delta + 4)
  }
  return { h: h < 0 ? h + 360 : h, s: max ? delta / max : 0, v: max }
}

function hsvToHex({ h, s, v }: Hsv) {
  const chroma = v * s, section = h / 60, x = chroma * (1 - Math.abs((section % 2) - 1)), match = v - chroma
  let [r, g, b] = [0, 0, 0]
  if (section < 1) [r, g, b] = [chroma, x, 0]
  else if (section < 2) [r, g, b] = [x, chroma, 0]
  else if (section < 3) [r, g, b] = [0, chroma, x]
  else if (section < 4) [r, g, b] = [0, x, chroma]
  else if (section < 5) [r, g, b] = [x, 0, chroma]
  else [r, g, b] = [chroma, 0, x]
  return rgbToHex((r + match) * 255, (g + match) * 255, (b + match) * 255)
}

export default function AppleColorPicker({ label, value, onChange }: AppleColorPickerProps) {
  const color = normalizeHex(value)
  const [open, setOpen] = useState(false)
  const [tab, setTab] = useState<PickerTab>('grid')
  const [hsv, setHsv] = useState(() => hexToHsv(color))
  const [swatchPage, setSwatchPage] = useState<0 | 1>(0)
  const [customSwatches, setCustomSwatches] = useState<string[]>(() => {
    try {
      const stored = JSON.parse(localStorage.getItem('afterthemed-picker-swatches') ?? '[]')
      return Array.isArray(stored) ? stored.filter(item => typeof item === 'string').slice(0, 9) : []
    } catch { return [] }
  })
  const [position, setPosition] = useState({ left: 0, top: 0, width: 360 })
  const trigger = useRef<HTMLButtonElement>(null)
  const popover = useRef<HTMLDivElement>(null)
  const pickerId = useId()

  useEffect(() => setHsv(hexToHsv(color)), [color])

  const measure = () => {
    const rect = trigger.current?.getBoundingClientRect()
    if (!rect) return
    // Include the pointer and edge clearance when fitting short windows.
    const width = Math.max(1, Math.min(360, window.innerWidth - 24, (window.innerHeight - 24) * 402 / 628))
    const estimatedHeight = width * (628 / 402)
    const left = clamp(rect.right - width, 12, Math.max(12, window.innerWidth - width - 12))
    const top = clamp((window.innerHeight - estimatedHeight) / 2, 12, Math.max(12, window.innerHeight - estimatedHeight - 12))
    setPosition({ left, top, width })
  }

  useLayoutEffect(() => {
    if (!open) return
    measure()
    const closeOutside = (event: globalThis.PointerEvent) => {
      const target = event.target as Node
      if (!trigger.current?.contains(target) && !popover.current?.contains(target)) setOpen(false)
    }
    const closeEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') { setOpen(false); trigger.current?.focus() }
    }
    document.addEventListener('pointerdown', closeOutside)
    document.addEventListener('keydown', closeEscape)
    window.addEventListener('resize', measure)
    return () => {
      document.removeEventListener('pointerdown', closeOutside)
      document.removeEventListener('keydown', closeEscape)
      window.removeEventListener('resize', measure)
    }
  }, [open])

  const apply = (next: string) => {
    const normalized = normalizeHex(next)
    setHsv(hexToHsv(normalized))
    onChange(normalized)
  }

  const applyHsv = (next: Hsv) => {
    const safe = { h: (next.h + 360) % 360, s: clamp(next.s), v: clamp(next.v) }
    setHsv(safe)
    onChange(hsvToHex(safe))
  }

  const pointerValue = (event: PointerEvent<HTMLDivElement>, kind: 'spectrum' | 'hue') => {
    const rect = event.currentTarget.getBoundingClientRect()
    if (kind === 'spectrum') applyHsv({ ...hsv, s: clamp((event.clientX - rect.left) / rect.width), v: clamp(1 - (event.clientY - rect.top) / rect.height) })
    else applyHsv({ ...hsv, h: clamp((event.clientX - rect.left) / rect.width) * 359.999 })
  }

  const startPointer = (event: PointerEvent<HTMLDivElement>, kind: 'spectrum' | 'hue') => {
    event.currentTarget.setPointerCapture(event.pointerId)
    pointerValue(event, kind)
  }

  const movePointer = (event: PointerEvent<HTMLDivElement>, kind: 'spectrum' | 'hue') => {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) pointerValue(event, kind)
  }

  const spectrumKeys = (event: KeyboardEvent<HTMLDivElement>) => {
    const step = event.shiftKey ? .1 : .02
    if (event.key === 'ArrowLeft') applyHsv({ ...hsv, s: hsv.s - step })
    else if (event.key === 'ArrowRight') applyHsv({ ...hsv, s: hsv.s + step })
    else if (event.key === 'ArrowUp') applyHsv({ ...hsv, v: hsv.v + step })
    else if (event.key === 'ArrowDown') applyHsv({ ...hsv, v: hsv.v - step })
    else return
    event.preventDefault()
  }

  const hueKeys = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'ArrowLeft' || event.key === 'ArrowDown') applyHsv({ ...hsv, h: hsv.h - 3 })
    else if (event.key === 'ArrowRight' || event.key === 'ArrowUp') applyHsv({ ...hsv, h: hsv.h + 3 })
    else return
    event.preventDefault()
  }

  const setRgb = (channel: 'r' | 'g' | 'b', next: number) => {
    const rgb = hexToRgb(color)
    apply(rgbToHex(channel === 'r' ? next : rgb.r, channel === 'g' ? next : rgb.g, channel === 'b' ? next : rgb.b))
  }

  const saveCurrent = () => {
    const next = [color, ...customSwatches.filter(item => item !== color)].slice(0, 9)
    setCustomSwatches(next)
    setSwatchPage(1)
    localStorage.setItem('afterthemed-picker-swatches', JSON.stringify(next))
  }

  const useEyedropper = async () => {
    const EyeDropper = (window as unknown as { EyeDropper?: new () => { open: () => Promise<{ sRGBHex: string }> } }).EyeDropper
    if (!EyeDropper) { setTab('spectrum'); return }
    try { apply((await new EyeDropper().open()).sRGBHex) } catch { /* cancelled */ }
  }

  const rgb = hexToRgb(color)
  const tabKeys = (event: KeyboardEvent<HTMLButtonElement>, current: PickerTab) => {
    const tabs: PickerTab[] = ['grid', 'spectrum', 'sliders']
    let index = tabs.indexOf(current)
    if (event.key === 'ArrowRight') index = (index + 1) % tabs.length
    else if (event.key === 'ArrowLeft') index = (index + tabs.length - 1) % tabs.length
    else if (event.key === 'Home') index = 0
    else if (event.key === 'End') index = tabs.length - 1
    else return
    event.preventDefault()
    setTab(tabs[index])
    event.currentTarget.parentElement?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[index]?.focus()
  }
  const style = { left: position.left, top: position.top, '--picker-scale': position.width / 402, '--picker-color': color, '--picker-hue': `hsl(${hsv.h} 100% 50%)` } as CSSProperties
  const saved = swatchPage === 0 ? defaultSwatches : customSwatches

  return <>
    <button ref={trigger} type="button" className="apple-color-trigger" aria-label={`Pick ${label}`} aria-haspopup="dialog" aria-expanded={open} onClick={() => setOpen(previous => !previous)} style={{ backgroundColor: color }} />
    {open && createPortal(<div ref={popover} className="apple-color-picker" role="dialog" aria-label={`${label} color picker`} style={style} data-node-id="515:56319" onPointerDown={event => event.stopPropagation()}>
      <div className="apple-picker-material" />
      <div className="apple-picker-content">
        <div className="apple-picker-toolbar">
          <span className="apple-picker-grabber" />
          <div className="apple-picker-title-row">
            <button type="button" aria-label="Eyedropper" onClick={useEyedropper}><Pipette size={22} strokeWidth={2} /></button>
            <strong>Colors</strong>
            <button type="button" aria-label="Close color picker" onClick={() => setOpen(false)}><X size={22} strokeWidth={2} /></button>
          </div>
        </div>
        <div className="apple-picker-segmented" role="tablist" aria-label="Color selection mode">
          {(['grid','spectrum','sliders'] as const).map(item => <button key={item} id={`${pickerId}-${item}`} type="button" role="tab" aria-controls={`${pickerId}-panel`} aria-selected={tab === item} tabIndex={tab === item ? 0 : -1} className={tab === item ? 'selected' : ''} onKeyDown={event => tabKeys(event, item)} onClick={() => setTab(item)}>{item[0].toUpperCase() + item.slice(1)}</button>)}
        </div>

        <div id={`${pickerId}-panel`} role="tabpanel" aria-labelledby={`${pickerId}-${tab}`}>
        {tab === 'grid' && <div className="apple-picker-grid" role="group" aria-label="Color grid">
          {colorGrid.flat().map(gridColor => <button key={gridColor} type="button" aria-label={gridColor} aria-pressed={color === gridColor} className={color === gridColor ? 'selected' : ''} style={{ backgroundColor: gridColor }} onClick={() => apply(gridColor)} />)}
        </div>}

        {tab === 'spectrum' && <div className="apple-picker-spectrum apple-picker-main">
          <div className="apple-spectrum-plane" role="slider" tabIndex={0} aria-label="Saturation and brightness" aria-valuetext={`${Math.round(hsv.s * 100)}% saturation, ${Math.round(hsv.v * 100)}% brightness`} onKeyDown={spectrumKeys} onPointerDown={event => startPointer(event, 'spectrum')} onPointerMove={event => movePointer(event, 'spectrum')}>
            <span style={{ left: `${hsv.s * 100}%`, top: `${(1 - hsv.v) * 100}%` }} />
          </div>
          <div className="apple-spectrum-hue" role="slider" tabIndex={0} aria-label="Hue" aria-valuemin={0} aria-valuemax={360} aria-valuenow={Math.round(hsv.h)} onKeyDown={hueKeys} onPointerDown={event => startPointer(event, 'hue')} onPointerMove={event => movePointer(event, 'hue')}><span style={{ left: `${hsv.h / 3.6}%` }} /></div>
        </div>}

        {tab === 'sliders' && <div className="apple-picker-sliders apple-picker-main">
          {([['r','Red',rgb.r],['g','Green',rgb.g],['b','Blue',rgb.b]] as const).map(([channel, name, amount]) => <label key={channel}><span>{name}</span><input type="range" min="0" max="255" value={amount} onChange={event => setRgb(channel, Number(event.target.value))} /><input type="number" min="0" max="255" value={amount} onChange={event => setRgb(channel, Number(event.target.value))} /></label>)}
          <div className="apple-picker-hex"><span>Hex</span><strong>{color}</strong></div>
        </div>}

        </div>
        <div className="apple-picker-opacity">
          <span>Opacity</span>
          <div className="apple-picker-opacity-row"><div className="apple-picker-opacity-track" style={{ backgroundImage: `linear-gradient(90deg, transparent 12%, ${color} 88%), url(${opacityChecker})` }}><i /></div><output>100%</output></div>
        </div>
        <div className="apple-picker-swatches">
          <button className="apple-picker-current" type="button" aria-label={`Current color ${color}`} style={{ backgroundColor: color }} />
          <div className="apple-picker-saved">
            <div>{saved.slice(0, 5).map(swatch => <button key={swatch} type="button" aria-label={`Use ${swatch}`} style={{ backgroundColor: swatch }} onClick={() => apply(swatch)} />)}</div>
            <div>{saved.slice(5, 9).map(swatch => <button key={swatch} type="button" aria-label={`Use ${swatch}`} className={swatch === color ? 'selected' : ''} style={{ backgroundColor: swatch }} onClick={() => apply(swatch)} />)}<button type="button" className="apple-picker-add" aria-label="Save current color" onClick={saveCurrent}><img src={addButton} width="30" height="30" alt="" /></button></div>
            <div className="apple-picker-pages" aria-label="Saved color pages"><button type="button" aria-label="Standard colors" aria-current={swatchPage === 0} onClick={() => setSwatchPage(0)} /><button type="button" aria-label="Custom colors" aria-current={swatchPage === 1} onClick={() => setSwatchPage(1)} /></div>
          </div>
        </div>
      </div>
      <span className="apple-picker-pointer" style={{ maskImage: `url(${materialPointer})` }} />
    </div>, document.body)}
  </>
}

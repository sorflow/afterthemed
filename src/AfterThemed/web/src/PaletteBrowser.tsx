import { useState, type CSSProperties } from 'react'
import { ArrowUpRight, Check, Leaf, Search } from 'lucide-react'

export type PresetPreview = { label: string; colors: string[]; cutoff?: number }

export function PaletteStrip({ colors }: { colors: string[] }) {
  return <span className="palette-strip" aria-hidden="true">
    {colors.map((color, index) => <i key={index} style={{ backgroundColor: color }} />)}
  </span>
}

export function PaletteMiniature({ colors }: { colors: string[] }) {
  const [background, panel, raised, text, primary, secondary] = colors
  return <span className="palette-miniature" aria-hidden="true" style={{
    '--mini-bg': background, '--mini-panel': panel, '--mini-raised': raised,
    '--mini-text': text, '--mini-primary': primary, '--mini-secondary': secondary,
  } as CSSProperties}>
    <span className="mini-toolbar"><i /><i /><i /></span>
    <span className="mini-workspace"><span className="mini-sidebar"><i /><i /><i /></span><span className="mini-canvas"><i /><b /></span></span>
    <span className="mini-timeline"><i /><i /><i /><b /></span>
  </span>
}

export default function PaletteBrowser({ names, previews, selected, onSelect, featured, featuredOnly = false, onFeaturedOnlyChange }: {
  names: string[]; previews: PresetPreview[]; selected: number; onSelect: (index: number) => void
  featured?: { label: string; names: string[] }; featuredOnly?: boolean; onFeaturedOnlyChange?: (on: boolean) => void
}) {
  const [query, setQuery] = useState('')
  const options = names.map((label, index) => ({ label, index, preview: previews.find(item => item.label === label) }))
    .filter(item => item.label.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase()))
    .filter(item => !featured || !featuredOnly || featured.names.includes(item.label))

  return <div className="palette-browser">
    <div className="palette-search"><Search size={17} aria-hidden="true" />
      <input aria-label="Search palettes" placeholder="Search by name" value={query} onChange={event => setQuery(event.target.value)} />
      {featured && <button type="button" className="palette-featured-toggle" aria-pressed={featuredOnly}
        onClick={() => onFeaturedOnlyChange?.(!featuredOnly)}><Leaf size={14} /> {featured.label}</button>}
      <span aria-live="polite">{options.length} {options.length === 1 ? 'palette' : 'palettes'}</span>
    </div>
    <div className="palette-grid" aria-label="Built-in palettes">
      {options.map(({ label, index, preview }) => <button type="button" className="palette-option" key={label}
        aria-label={label} aria-pressed={selected === index} onClick={() => onSelect(index)}>
        {preview ? <PaletteMiniature colors={preview.colors} /> : <span className="palette-unavailable">Choose to preview</span>}
        <span className="palette-option-name"><span>{label}</span>{selected === index && <Check size={16} aria-hidden="true" />}</span>
        {preview && <PaletteStrip colors={preview.colors} />}
        <span className="palette-preview-action" aria-hidden="true">{selected === index ? 'Current palette' : 'Preview palette'}<ArrowUpRight size={14} /></span>
      </button>)}
      {options.length === 0 && <p className="palette-empty">No palettes match “{query}”. Try another name.</p>}
    </div>
  </div>
}

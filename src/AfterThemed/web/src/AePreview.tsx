import {
  ChevronDown, ChevronRight, Eye, Hand, MousePointer2, Move, Play,
  RotateCw, Search, Square, Type, ZoomIn,
} from 'lucide-react'

const effects = ['Animation Presets', 'Blur & Sharpen', 'Color Correction', 'Distort']

const layers = [
  { number: '1', name: 'Accent circle', kind: 'shape', bar: 'accent' },
  { number: '2', name: 'Inner circle', kind: 'shape', bar: 'secondary' },
  { number: '3', name: 'Background', kind: 'solid', bar: 'background' },
]

export default function AePreview({ themeName }: { themeName: string }) {
  return <div className="ae2-window" role="img" aria-label="After Effects style interface showing your selected theme colors in panels, a composition viewer, and a three-layer timeline">
    <div className="ae2-titlebar">
      <span className="ae2-app-icon">Ae</span>
      <span className="ae2-app-name">After Effects</span>
      <span className="ae2-title-filename">{themeName || 'Untitled theme'} · Preview.aep</span>
      <span className="ae2-window-actions"><i /><i /><i /></span>
    </div>
    <div className="ae2-menubar"><span>File</span><span>Edit</span><span>Composition</span><span>Layer</span><span>Effect</span><span>Animation</span><span>View</span><span>Window</span><span>Help</span></div>
    <div className="ae2-toolbar">
      <MousePointer2 className="ae2-tool-active" /><Hand /><ZoomIn />
      <span className="ae2-tool-separator" /><RotateCw /><Move /><Square /><Type />
      <span className="ae2-toolbar-spacer" /><span className="ae2-workspace-name">Default <ChevronDown /></span>
    </div>
    <div className="ae2-editors">
      <div className="ae2-left-column">
        <div className="ae2-project ae2-panel">
          <div className="ae2-panel-tabs"><span className="ae2-tab-active">Project</span><span>Effect Controls</span></div>
          <div className="ae2-project-info"><div className="ae2-project-thumb"><span /></div><div><strong>Preview Comp</strong><small>1920 × 1080 · 30 fps</small></div></div>
          <div className="ae2-project-row ae2-selected"><ChevronDown /><span className="ae2-project-folder">▣</span><span>Preview Comp</span></div>
        </div>
        <div className="ae2-effects ae2-panel">
          <div className="ae2-panel-tabs"><span className="ae2-tab-active">Effects & Presets</span><span className="ae2-panel-more">≡</span></div>
          <div className="ae2-project-filter"><Search /><span>Search effects</span></div>
          {effects.map(effect => <div className="ae2-effect-row" key={effect}><ChevronRight />{effect}</div>)}
        </div>
      </div>
      <div className="ae2-composition ae2-panel">
        <div className="ae2-panel-tabs"><span className="ae2-tab-active">Composition: Preview Comp</span><span className="ae2-panel-more">≡</span></div>
        <div className="ae2-viewer">
          <div className="ae2-canvas" aria-hidden="true">
            <div className="ae2-safe-frame" />
            <div className="ae2-shape-group">
              <div className="ae2-selection-box"><i /><i /><i /><i /></div>
              <div className="ae2-disc-outer" /><div className="ae2-disc-inner" />
              <div className="ae2-anchor">+</div>
            </div>
          </div>
        </div>
        <div className="ae2-viewer-controls"><span>50% <ChevronDown /></span><span>Full <ChevronDown /></span><span className="ae2-control-time">0:00:02:12</span><span>RGB</span></div>
      </div>
    </div>
    <div className="ae2-timeline ae2-panel">
      <div className="ae2-panel-tabs"><span className="ae2-tab-active">Preview Comp</span><span>Render Queue</span><span className="ae2-panel-more">≡</span></div>
      <div className="ae2-timeline-meta"><strong>0:00:02:12</strong><span>30.00 fps</span><span className="ae2-timeline-play"><Play fill="currentColor" /></span></div>
      <div className="ae2-timeline-content">
        <div className="ae2-layer-list">
          <div className="ae2-layer-head"><span>◉</span><span>Layer Name</span></div>
          {layers.map(layer => <div className="ae2-layer" key={layer.number}>
            <Eye /><span className="ae2-layer-number">{layer.number}</span><span className={`ae2-layer-kind ${layer.kind}`} />
            <span className="ae2-layer-name">{layer.name}</span>
            {layer.number === '1' && <span className="ae2-layer-error" title="Expression error">▲</span>}
          </div>)}
        </div>
        <div className="ae2-tracks">
          <div className="ae2-ruler"><span>0s</span><span>1s</span><span>2s</span><span>3s</span><span>4s</span><span>5s</span></div>
          <div className="ae2-playhead" />
          {layers.map(layer => <div className="ae2-track" key={layer.number}><div className={`ae2-track-bar ${layer.bar}`} /><i className="ae2-keyframe" /></div>)}
        </div>
      </div>
      <div className="ae2-timeline-bottom"><span>◀</span><div /><span>▶</span></div>
    </div>
  </div>
}

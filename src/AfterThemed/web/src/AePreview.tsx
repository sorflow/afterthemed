import {
  ChevronDown, ChevronRight, Eye, Hand, MousePointer2, Move, Play,
  RotateCw, Search, Square, Type, ZoomIn,
} from 'lucide-react'

const effects = ['Animation Presets', 'Blur & Sharpen', 'Color Correction', 'Distort']

const layers = [
  { number: '1', name: 'Accent circle', kind: 'shape', bar: 'accent', role: 'primary' },
  { number: '2', name: 'Inner circle', kind: 'shape', bar: 'secondary', role: 'secondary' },
  { number: '3', name: 'Background', kind: 'solid', bar: 'background', role: 'raised' },
]

// data-role names the theme roles each region paints (AePreview.css), so Theme Lens can outline them.
export default function AePreview({ themeName }: { themeName: string }) {
  return <div className="ae2-window" data-role="bg" role="img" aria-label="After Effects style interface showing your selected theme colors in panels, a composition viewer, and a three-layer timeline">
    <div className="ae2-titlebar" data-role="bg">
      <span className="ae2-app-icon">Ae</span>
      <span className="ae2-app-name" data-role="text">After Effects</span>
      <span className="ae2-title-filename" data-role="text">{themeName || 'Untitled theme'} · Preview.aep</span>
      <span className="ae2-window-actions"><i /><i /><i /></span>
    </div>
    <div className="ae2-menubar" data-role="bg text"><span>File</span><span>Edit</span><span>Composition</span><span>Layer</span><span>Effect</span><span>Animation</span><span>View</span><span>Window</span><span>Help</span></div>
    <div className="ae2-toolbar" data-role="raised">
      <MousePointer2 className="ae2-tool-active" data-role="primary" /><Hand /><ZoomIn />
      <span className="ae2-tool-separator" /><RotateCw /><Move /><Square /><Type />
      <span className="ae2-toolbar-spacer" /><span className="ae2-workspace-name">Default <ChevronDown /></span>
    </div>
    <div className="ae2-editors" data-role="bg">
      <div className="ae2-left-column">
        <div className="ae2-project ae2-panel" data-role="panel">
          <div className="ae2-panel-tabs" data-role="raised"><span className="ae2-tab-active" data-role="primary">Project</span><span>Effect Controls</span></div>
          <div className="ae2-project-info" data-role="text"><div className="ae2-project-thumb"><span data-role="primary secondary" /></div><div><strong>Preview Comp</strong><small>1920 × 1080 · 30 fps</small></div></div>
          <div className="ae2-project-row ae2-selected" data-role="primary"><ChevronDown /><span className="ae2-project-folder" data-role="secondary">▣</span><span>Preview Comp</span></div>
        </div>
        <div className="ae2-effects ae2-panel" data-role="panel">
          <div className="ae2-panel-tabs" data-role="raised"><span className="ae2-tab-active" data-role="primary">Effects & Presets</span><span className="ae2-panel-more">≡</span></div>
          <div className="ae2-project-filter" data-role="bg"><Search /><span>Search effects</span></div>
          {effects.map(effect => <div className="ae2-effect-row" data-role="text" key={effect}><ChevronRight />{effect}</div>)}
        </div>
      </div>
      <div className="ae2-composition ae2-panel" data-role="panel">
        <div className="ae2-panel-tabs" data-role="raised"><span className="ae2-tab-active" data-role="primary">Composition: Preview Comp</span><span className="ae2-panel-more">≡</span></div>
        <div className="ae2-viewer" data-role="bg">
          <div className="ae2-canvas" aria-hidden="true">
            <div className="ae2-safe-frame" />
            <div className="ae2-shape-group">
              <div className="ae2-selection-box" data-role="secondary"><i /><i /><i /><i /></div>
              <div className="ae2-disc-outer" data-role="primary" /><div className="ae2-disc-inner" data-role="secondary" />
              <div className="ae2-anchor">+</div>
            </div>
          </div>
        </div>
        <div className="ae2-viewer-controls" data-role="raised"><span>50% <ChevronDown /></span><span>Full <ChevronDown /></span><span className="ae2-control-time" data-role="text">0:00:02:12</span><span>RGB</span></div>
      </div>
    </div>
    <div className="ae2-timeline ae2-panel" data-role="panel">
      <div className="ae2-panel-tabs" data-role="raised"><span className="ae2-tab-active" data-role="primary">Preview Comp</span><span>Render Queue</span><span className="ae2-panel-more">≡</span></div>
      <div className="ae2-timeline-meta"><strong data-role="primary">0:00:02:12</strong><span>30.00 fps</span><span className="ae2-timeline-play"><Play fill="currentColor" /></span></div>
      <div className="ae2-timeline-content">
        <div className="ae2-layer-list">
          <div className="ae2-layer-head" data-role="raised"><span>◉</span><span>Layer Name</span></div>
          {layers.map(layer => <div className="ae2-layer" key={layer.number} data-role={layer.number === '2' ? 'primary' : undefined}>
            <Eye /><span className="ae2-layer-number">{layer.number}</span><span className={`ae2-layer-kind ${layer.kind}`} data-role={layer.kind === 'solid' ? 'raised' : 'primary'} />
            <span className="ae2-layer-name" data-role="text">{layer.name}</span>
            {layer.number === '1' && <span className="ae2-layer-error" data-role="danger" title="Expression error">▲</span>}
          </div>)}
        </div>
        <div className="ae2-tracks" data-role="panel">
          <div className="ae2-ruler" data-role="raised"><span>0s</span><span>1s</span><span>2s</span><span>3s</span><span>4s</span><span>5s</span></div>
          <div className="ae2-playhead" data-role="primary" />
          {layers.map(layer => <div className="ae2-track" key={layer.number}><div className={`ae2-track-bar ${layer.bar}`} data-role={layer.role} /><i className="ae2-keyframe" data-role="text" /></div>)}
        </div>
      </div>
      <div className="ae2-timeline-bottom"><span>◀</span><div data-role="raised" /><span>▶</span></div>
    </div>
  </div>
}

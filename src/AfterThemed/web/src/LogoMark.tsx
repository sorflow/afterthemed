// The AfterThemed logo (brand/afterthemed-logo.svg): a ring filling with dissolving pixels, crowned by
// the Install button's sparkle. Without a tile the glyph uses currentColor and the star's knockout
// stroke uses --logo-knockout, which must match the surface behind it.
const pixels = [[158, 206], [218, 236], [308, 236], [128, 266], [188, 266], [218, 266], [278, 266], [338, 266],
  [128, 296], [158, 296], [218, 296], [248, 296], [278, 296], [308, 296], [338, 296], [158, 326], [188, 326],
  [218, 326], [248, 326], [278, 326], [308, 326], [188, 356], [218, 356], [248, 356], [278, 356]]
const leaf = 'M6 22C3.5 10.5 12 2 26 2C26.5 15.5 17.5 24.5 6 22Z'
const star = 'M14 1.5C14.9 10.8 17.2 13.1 26.5 14C17.2 14.9 14.9 17.2 14 26.5C13.1 17.2 10.8 14.9 1.5 14C10.8 13.1 13.1 10.8 14 1.5Z'

export default function LogoMark({ tile = false, className }: { tile?: boolean; className?: string }) {
  // Colors follow the appearance palette (Palette.css); the fallbacks are the Blue values.
  const ink = tile ? 'var(--paper, #EDF5FF)' : 'currentColor'
  const knockout = tile ? 'var(--ink, #100BEA)' : 'var(--logo-knockout, #100BEA)'
  return <svg className={className} viewBox={tile ? '0 0 512 512' : '64 48 384 384'} aria-hidden="true" focusable="false">
    {tile && <rect width="512" height="512" rx="116" style={{ fill: 'var(--ink, #100BEA)' }} />}
    <g style={{ fill: ink }}>{pixels.map(([x, y]) => <rect key={`${x}-${y}`} x={x} y={y} width="23" height="23" rx="3" />)}</g>
    <circle cx="244" cy="268" r="150" fill="none" style={{ stroke: ink }} strokeWidth="18" />
    <path className="logo-star" transform="translate(266.1 77.9) scale(6)" d={star} style={{ fill: ink, stroke: knockout }} strokeWidth="4.333" strokeLinejoin="round" paintOrder="stroke" />
    {/* Fall swaps the sparkle for a leaf (Fall.css toggles which one shows). */}
    <g className="logo-leaf" transform="translate(266.1 77.9) scale(6)" strokeLinecap="round">
      <path d={leaf} style={{ fill: ink, stroke: knockout }} strokeWidth="4.333" strokeLinejoin="round" paintOrder="stroke" />
      <path d="M3.5 24.5L6.5 21.5" style={{ stroke: knockout }} strokeWidth="5" />
      <path d="M3.5 24.5L7 21" style={{ stroke: ink }} strokeWidth="2" />
      <path d="M8.5 19.5L21 7" style={{ stroke: knockout }} strokeWidth="1.3" />
    </g>
  </svg>
}

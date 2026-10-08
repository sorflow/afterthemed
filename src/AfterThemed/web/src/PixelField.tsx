import type { CSSProperties } from 'react'

// Full 42 x 12 grid of 3px cells so hover can dither-fill the pill; only even cells show at rest.
const columns = 42, rows = 12
const pixels = Array.from({ length: columns * rows }, (_, index) => {
  const column = index % columns
  const row = Math.floor(index / columns)
  const seed = ((index * 137 + 53) % 997) / 997
  const resting = column % 2 === 0 && row % 2 === 0 && index % 7 !== 0
  const alpha = (0.03 + seed * 0.14) * Math.pow(1 - row / rows, 1.7)
  return { column, row, seed, opacity: resting && alpha > 0.025 ? alpha : 0,
    delay: -(column * 0.07 + seed * 5), duration: 3.6 + seed * 3 }
})

export default function PixelField({ colors = [] }: { colors?: string[] }) {
  return <svg className="install-pixel-field" viewBox={`0 0 ${columns * 3} ${rows * 3}`} preserveAspectRatio="none" shapeRendering="crispEdges" aria-hidden="true">
    {pixels.map((pixel, index) => <rect key={index} className={pixel.opacity ? 'install-pixel' : 'install-pixel install-pixel-fill'}
      x={pixel.column * 3} y={pixel.row * 3} width="3.05" height="3.05"
      style={{ '--pixel-alpha': pixel.opacity, '--pixel-delay': `${pixel.delay}s`, '--pixel-duration': `${pixel.duration}s`,
        '--pixel-fill-delay': `${pixel.column * 13 + pixel.seed * 140}ms`, '--pixel-scan-delay': `${pixel.column * 22}ms`,
        '--pixel-tint': colors.length ? colors[(pixel.column + pixel.row * 3) % colors.length] : 'currentColor' } as CSSProperties} />)}
  </svg>
}

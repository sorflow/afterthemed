import { clampChroma, converter, formatHex, wcagContrast, type Oklab, type Oklch } from 'culori'

// Role order everywhere: Background, Panels, Raised, Text, Primary, Secondary, Danger.
const toOklch = converter('oklch')
const toOklab = converter('oklab')
const hex = (color: Oklch) => (formatHex(clampChroma(color, 'oklch')) ?? '#000000').toUpperCase()
const lch = (value: string) => toOklch(value) ?? { mode: 'oklch' as const, l: 0, c: 0, h: 0 }

export const contrast = (a: string, b: string) => wcagContrast(a, b)

/** Text must read on every surface (4.5:1); accents must stand out on panels (3:1). */
const checks = [
  { role: 3, against: [0, 1, 2], min: 4.5 },
  { role: 4, against: [1], min: 3 },
  { role: 5, against: [1], min: 3 },
  { role: 6, against: [1], min: 3 },
]

export type ContrastResult = { role: number; against: number; ratio: number; min: number }

/** The weakest pairing for each checked role. */
export function contrastReport(colors: string[]): ContrastResult[] {
  return checks.map(({ role, against, min }) => {
    const worst = against.map(index => ({ against: index, ratio: contrast(colors[role], colors[index]) }))
      .sort((a, b) => a.ratio - b.ratio)[0]
    return { role, min, ...worst }
  })
}

/** Moves only lightness, in small steps, until the color passes against every background. */
export function fixContrast(color: string, backgrounds: string[], min: number): string {
  const base = lch(color)
  const darkBackground = backgrounds.reduce((sum, value) => sum + lch(value).l, 0) / backgrounds.length < .6
  for (const direction of darkBackground ? [1, -1] : [-1, 1]) {
    for (let step = 1; step <= 100; step++) {
      const l = base.l + direction * step * .01
      if (l < 0 || l > 1) break
      const candidate = hex({ ...base, l })
      if (backgrounds.every(background => contrast(candidate, background) >= min + .05)) return candidate
    }
  }
  return darkBackground ? '#FFFFFF' : '#000000'
}

export function fixAll(colors: string[], locked: boolean[] = []): string[] {
  const next = [...colors]
  for (const { role, against, min } of checks) {
    if (locked[role]) continue
    const backgrounds = against.map(index => next[index])
    if (backgrounds.some(background => contrast(next[role], background) < min)) next[role] = fixContrast(next[role], backgrounds, min)
  }
  return next
}

export const isDark = (colors: string[]) => lch(colors[0]).l < .6

/** Background, Panels, Raised and Text as lightness steps of one hue. */
export function surfacesFrom(seed: string, dark: boolean): string[] {
  const { h = 0, c } = lch(seed)
  const tint = Math.min(c * .2, .035)
  const steps = dark ? [.2, .245, .3, .93] : [.94, .97, .995, .3]
  return steps.map((l, index) => hex({ mode: 'oklch', l, c: index === 3 ? Math.min(tint, .015) : tint, h }))
}

export const generateFromColor = (colors: string[], seed = colors[4]) =>
  fixAll([...surfacesFrom(seed, isDark(colors)), ...colors.slice(4)], [true, true, true, false, false, false, false])

/** A new harmonious palette that keeps locked roles and the current light/dark direction. */
export function shuffle(colors: string[], locked: boolean[], random = Math.random): string[] {
  const dark = isDark(colors)
  const hue = locked[4] ? lch(colors[4]).h ?? random() * 360 : random() * 360
  const l = dark ? .76 : .52
  const chroma = .11 + random() * .07
  const primary = hex({ mode: 'oklch', l, c: chroma, h: hue })
  const secondary = hex({ mode: 'oklch', l: l + (random() - .5) * .08, c: chroma * .85, h: hue + 120 + random() * 120 })
  const danger = hex({ mode: 'oklch', l: dark ? .7 : .52, c: .16, h: 18 + random() * 14 })
  const proposal = [...surfacesFrom(locked[4] ? colors[4] : primary, dark), primary, secondary, danger]
  return fixAll(proposal.map((color, index) => locked[index] ? colors[index] : color), locked)
}

/** k-means in OKLab over a downscaled image, then roles from the dominant and most colorful clusters. */
export async function paletteFromImage(file: Blob): Promise<string[]> {
  const bitmap = await createImageBitmap(file)
  const scale = Math.min(1, 96 / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.max(1, Math.round(bitmap.width * scale))
  canvas.height = Math.max(1, Math.round(bitmap.height * scale))
  const context = canvas.getContext('2d', { willReadFrequently: true })!
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  const data = context.getImageData(0, 0, canvas.width, canvas.height).data
  const samples: Oklab[] = []
  for (let i = 0; i < data.length; i += 4)
    if (data[i + 3] > 127) samples.push(toOklab({ mode: 'rgb', r: data[i] / 255, g: data[i + 1] / 255, b: data[i + 2] / 255 }))
  if (samples.length === 0) throw new Error('The image has no visible pixels.')
  return paletteFromSamples(samples)
}

export function paletteFromSamples(samples: Oklab[], k = 8): string[] {
  const sorted = [...samples].sort((a, b) => a.l - b.l)
  let centers = Array.from({ length: Math.min(k, samples.length) }, (_, i) => ({ ...sorted[Math.floor((i + .5) * sorted.length / k)] }))
  let counts: number[] = []
  for (let iteration = 0; iteration < 10; iteration++) {
    const sums = centers.map(() => ({ l: 0, a: 0, b: 0, n: 0 }))
    for (const sample of samples) {
      let best = 0, bestDistance = Infinity
      centers.forEach((center, index) => {
        const distance = (sample.l - center.l) ** 2 + (sample.a - center.a) ** 2 + (sample.b - center.b) ** 2
        if (distance < bestDistance) { best = index; bestDistance = distance }
      })
      const sum = sums[best]
      sum.l += sample.l; sum.a += sample.a; sum.b += sample.b; sum.n++
    }
    counts = sums.map(sum => sum.n)
    centers = centers.map((center, index) => sums[index].n
      ? { mode: 'oklab' as const, l: sums[index].l / sums[index].n, a: sums[index].a / sums[index].n, b: sums[index].b / sums[index].n }
      : center)
  }
  const clusters = centers.map((center, index) => ({ color: lch(formatHex(center) ?? '#000000'), weight: counts[index] / samples.length }))
    .filter(cluster => cluster.weight > 0)
  const meanL = clusters.reduce((sum, cluster) => sum + cluster.color.l * cluster.weight, 0)
  const dark = meanL < .55
  const dominant = [...clusters].sort((a, b) => b.weight - a.weight)[0].color
  // The dominant cluster becomes the surfaces; accents favor vivid color over coverage.
  const vivid = clusters.filter(cluster => cluster.color.c > .04)
  const colorful = (vivid.some(cluster => cluster.color !== dominant) ? vivid.filter(cluster => cluster.color !== dominant) : vivid)
    .sort((a, b) => b.color.c * b.weight ** .25 - a.color.c * a.weight ** .25).map(cluster => cluster.color)
  const accentL = (color: Oklch) => dark ? Math.min(.85, Math.max(.68, color.l)) : Math.min(.58, Math.max(.42, color.l))
  const accent = (color: Oklch) => hex({ ...color, l: accentL(color), c: Math.min(.2, Math.max(.09, color.c)) })
  const hueGap = (a = 0, b = 0) => Math.abs(((a - b + 540) % 360) - 180)
  const primary = colorful[0] ?? { mode: 'oklch' as const, l: .7, c: .12, h: (dominant.h ?? 220) }
  const secondary = colorful.find(color => hueGap(color.h, primary.h) > 35) ?? { ...primary, h: (primary.h ?? 0) + 150 }
  const red = colorful.find(color => hueGap(color.h, 25) < 30) ?? { mode: 'oklch' as const, l: .68, c: .16, h: 25 }
  const surfaces = surfacesFrom(hex({ ...dominant, c: Math.max(dominant.c, .02) }), dark)
  return fixAll([...surfaces, accent(primary), accent(secondary), accent(red)])
}

/** A vivid color far from the role's own, used to flash where that role appears in the preview. */
export function highlightFor(color: string) {
  const { l, h = 0 } = lch(color)
  return hex({ mode: 'oklch', l: l > .55 ? .45 : .82, c: .22, h: h + 180 })
}

/** A small PNG of the palette as an After Effects workspace, embedded in exported .afterthemed files. */
export function themeThumbnail(colors: string[]): string {
  const [bg, panel, raised, text, primary, secondary, danger] = colors
  const canvas = document.createElement('canvas')
  canvas.width = 320
  canvas.height = 180
  const g = canvas.getContext('2d')!
  const box = (color: string, x: number, y: number, w: number, h: number) => { g.fillStyle = color; g.fillRect(x, y, w, h) }
  box(bg, 0, 0, 320, 180)
  box(raised, 0, 0, 320, 16)
  box(panel, 4, 20, 86, 96); box(panel, 94, 20, 222, 96); box(panel, 4, 120, 312, 56)
  for (let i = 0; i < 4; i++) box(text, 10, 30 + i * 14, 50 - i * 6, 3)
  box(bg, 98, 24, 214, 88)
  g.fillStyle = primary; g.beginPath(); g.arc(205, 68, 24, 0, Math.PI * 2); g.fill()
  g.fillStyle = secondary; g.beginPath(); g.arc(205, 68, 10, 0, Math.PI * 2); g.fill()
  box(primary, 70, 132, 180, 8); box(secondary, 70, 146, 140, 8); box(raised, 70, 160, 230, 8)
  box(danger, 300, 6, 10, 4)
  return canvas.toDataURL('image/png')
}

// Run with: node checks/paletteTools.check.ts
import assert from 'node:assert/strict'
import { converter } from 'culori'
import { contrast, contrastReport, fixAll, fixContrast, generateFromColor, isDark, paletteFromSamples, shuffle } from '../src/paletteTools.ts'

const miku = ['#1F2527', '#242F31', '#29383A', '#BEC8D1', '#86CECB', '#59C9CC', '#FF9ACC']
const passes = (colors: string[]) => contrastReport(colors).every(result => result.ratio >= result.min)

// A failing text color is nudged until it passes, without leaving its hue.
const fixed = fixContrast('#555555', ['#1F2527', '#242F31', '#29383A'], 4.5)
assert.ok(['#1F2527', '#242F31', '#29383A'].every(bg => contrast(fixed, bg) >= 4.5), fixed)
assert.ok(passes(fixAll(['#1F2527', '#242F31', '#29383A', '#333333', '#202020', '#2A2A2A', '#301010'])))

// Locked roles never change; generated palettes pass and keep dark/light direction.
const locked = [false, false, false, false, true, false, false]
for (let i = 0; i < 50; i++) {
  const next = shuffle(miku, locked)
  assert.equal(next[4], miku[4])
  assert.ok(passes(next), next.join())
  assert.ok(isDark(next))
}
const light = ['#EFF1F5', '#E6E9EF', '#CCD0DA', '#4C4F69', '#8839EF', '#1E66F5', '#D20F39']
assert.ok(!isDark(generateFromColor(light)) && passes(generateFromColor(light)))
assert.deepEqual(generateFromColor(miku).slice(4, 7).filter((c, i) => c === miku[4 + i]).length >= 1, true)

// Image extraction: a mostly dark blue image with orange highlights gives a dark palette with a warm accent.
const toOklab = converter('oklab')
const samples = [...Array(900).fill('#0B1630'), ...Array(80).fill('#F28C28'), ...Array(20).fill('#E6E6E6')].map(c => toOklab(c)!)
const image = paletteFromSamples(samples)
assert.ok(isDark(image) && passes(image), image.join())
const hue = converter('oklch')(image[4])!.h!
assert.ok(hue > 30 && hue < 90, `primary hue ${hue}`)
console.log('paletteTools checks passed')

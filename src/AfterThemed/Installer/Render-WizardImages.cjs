// Renders the installer artwork from brand/afterthemed-logo.svg into Installer/Images as 24-bit BMPs
// (100/150/200% scale). Run: node Render-WizardImages.cjs <path-to-playwright>
// will-change forces grayscale antialiasing so text has no ClearType color fringes on the bitmap.
// The wizard panel is the app's electric blue with the logo, wordmark and a dissolving pixel field.
const { chromium } = require(process.argv[2] || 'playwright');
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..', '..', '..');
const logo = fs.readFileSync(path.join(root, 'brand', 'afterthemed-logo.svg'), 'utf8');
const glyph = logo.replace('<rect width="512" height="512" rx="116" fill="#100BEA"/>', '').replace('viewBox="0 0 512 512"', 'viewBox="64 48 384 384"');
const out = path.join(__dirname, 'Images');
fs.mkdirSync(out, { recursive: true });

// Rows of the pixel field, bottom-up: solid, then dissolving (same hand-placed rhythm as the logo).
const rows = ['##########', '#.###.##.#', '.#..#..#.#', '...#......'];
const pixels = rows.map((row, r) => [...row].map((cell, c) => cell === '#'
  ? `<rect x="${7 + c * 15.5}" y="${300 - (r + 1) * 15.5}" width="11.5" height="11.5" rx="1.6"/>` : '').join('')).join('');

const wizard = `<body style="margin:0;width:164px;height:314px;background:#100BEA;font-family:'Segoe UI Variable Display','Segoe UI',sans-serif;overflow:hidden">
  <style>div{will-change:transform}</style>
  <div style="position:absolute;left:32px;top:46px;width:100px;height:100px;color:#EDF5FF">${glyph.replace('<svg ', '<svg width="100" height="100" ')}</div>
  <div style="position:absolute;left:0;right:0;top:160px;text-align:center;color:#EDF5FF;font-size:17px;font-weight:400;letter-spacing:-.02em">AfterThemed</div>
  <div style="position:absolute;left:0;right:0;top:183px;text-align:center;color:#D0DAFF;font-size:9.5px">Theme studio</div>
  <svg width="164" height="314" style="position:absolute;inset:0" fill="#EDF5FF" opacity=".55">${pixels}</svg>
</body>`;
const small = `<body style="margin:0;width:55px;height:55px;background:#EDF5FF">
  ${logo.replace('<svg ', '<svg width="55" height="55" style="display:block" ')}</body>`;

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  for (const [name, html, w, h] of [['WizardImage', wizard, 164, 314], ['WizardSmallImage', small, 55, 55]]) {
    for (const scale of [1, 1.5, 2]) {
      const page = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: scale });
      await page.setContent(html);
      await page.screenshot({ path: path.join(out, `${name}-${Math.round(scale * 100)}.png`) });
      await page.close();
    }
  }
  await browser.close();
})();

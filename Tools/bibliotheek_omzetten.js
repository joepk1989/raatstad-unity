// Zet de standaardbibliotheek van de webversie (libStandard in 08e/08d9) om naar JSON-bestanden voor Unity.
// Gebruik: node Tools/bibliotheek_omzetten.js [pad naar raatstad-web] [uitvoermap]
// De webcode draait hier zonder browser: onbekende namen (THREE, document, scene …) worden nepobjecten.
const fs = require('fs'), path = require('path');
const web = process.argv[2] || 'D:/raatstad-web', out = process.argv[3] || path.join(__dirname, '..', 'Assets', 'Resources', 'Modules');
const files = ['01-raster-data.js', '03-gebouwen.js', '08-schets-modules.js', '08c-modules-v2.js', '08d9-bibliotheek-uitbreiding.js', '08e-modules-bibliotheek.js'];
const code = files.map(f => fs.readFileSync(path.join(web, 'src', 'js', f), 'utf8')).join('\n');
const stub = new Proxy(function () {}, {
  get: (t, k) => k === Symbol.toPrimitive ? () => 0 : k === Symbol.iterator ? function* () {} : k === 'length' ? 0 : stub,
  apply: () => stub, construct: () => stub, set: () => true, has: () => false,
});
const scope = new Proxy({}, { has: (t, k) => typeof k === 'string' && !(k in globalThis) && k !== 'eval', get: (t, k) => k === Symbol.unscopables ? undefined : (k === 'localStorage' ? { getItem: () => '1', setItem() {}, removeItem() {} } : stub), set: () => true });
const run = new Function('scope', 'with (scope) { ' + code + '\n; return { libStandard, modV2Codes, modMeta }; }');
const api = run(scope);
const mods = api.libStandard();
fs.mkdirSync(out, { recursive: true });
const slug = s => s.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const index = [], seen = new Set();
for (const m of mods) {
  let id = slug(m.name); while (seen.has(id)) id += '-2'; seen.add(id);
  m.id = id; m.lib = 15; try { m.codes = api.modV2Codes(m); } catch (e) { }
  delete m._sig;
  fs.writeFileSync(path.join(out, id + '.json'), JSON.stringify(m));
  index.push(id);
}
fs.writeFileSync(path.join(out, 'index.txt'), index.join('\n'));
console.log(mods.length + ' modules naar ' + out);

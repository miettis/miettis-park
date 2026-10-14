const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const html = fs.readFileSync(require('node:path').join(__dirname, 'index.html'), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
new vm.Script(script);
const nodes = new Map();
const context = vm.createContext({
  structuredClone,
  document: {
    getElementById(id) {
      if (!nodes.has(id)) nodes.set(id, { innerHTML: '', textContent: '', hidden: true });
      return nodes.get(id);
    },
    querySelectorAll: () => [],
    addEventListener() {},
  },
  window: { addEventListener() {} },
  location: { hash: '#map' },
  history: { replaceState() {} },
  confirm: () => true,
  setTimeout: () => 1,
  clearTimeout() {},
  setInterval() {},
});
vm.runInContext(script, context);
function run(code) { return vm.runInContext(code, context); }
assert.ok(nodes.get('app').innerHTML.includes('Löydä vapaa paikka'));
assert.equal(run('totals(garages[0]).total'), 16);
assert.equal(run('totals(garages[2]).free'), 0);
assert.equal(run('summary(garages[3])'), '0/0 varattu');
assert.equal(run('garages[0].visits'), 4);
run("draft=structuredClone(garages[0]); brush='erase'; paint(11)");
assert.equal(run("draft.cells[11].state"), 'Occupied');
run("brush='lot'; paint(0)");
assert.equal(run('draft.cells[0].number'), 17);
assert.equal(run('garages[0].cells[0]'), null);
run("selectedLot=draft.cells[0].id; brush='select'; paint(9)");
assert.equal(run('draft.cells[0]'), null);
assert.equal(run('draft.cells[9].id===selectedLot'), true);
assert.ok(run('resizeDraft(3,3)').includes('Siirrä'));
assert.equal(run('resizeDraft(12,6)'), '');
assert.equal(run('draft.cells[9].id===selectedLot'), true);
assert.equal(run('draft.cells.length'), 72);
assert.ok(run("esc('<img onerror=x>')").startsWith('&lt;'));
for (const hash of ['#garage/1','#admin','#form/new','#form/1','#edit/1','#garage/999']) {
  context.location.hash = hash;
  run('render()');
  assert.ok(nodes.get('app').innerHTML.length > 300);
}
context.location.hash = '#map';
run('mapOff=true;render()');
assert.ok(nodes.get('app').innerHTML.includes('Karttaa ei voitu ladata'));
assert.ok(nodes.get('app').innerHTML.includes('Hallit lähialueella'));
console.log('PASS: JS syntax, all views, counters, unknown/empty halls, editor guards, draft isolation, stable move IDs, resizing, escaping, map fallback');

context.location.hash = '#map';
run('mapOff=false;liveConnected=true;render()');
const before = run('liveSequence');
assert.equal(run('simulateLiveUpdate()'), true);
assert.equal(run('liveSequence'), before + 1);
assert.ok(nodes.get('app').innerHTML.includes('SSE-simulaatio'));
run('liveConnected=false');
assert.equal(run('simulateLiveUpdate()'), false);
assert.equal(run('liveSequence'), before + 1);
context.location.hash = '#edit/1';
run('liveConnected=true');
assert.equal(run('simulateLiveUpdate()'), false);
console.log('PASS: simulated SSE refresh, offline pause, editor draft protection');
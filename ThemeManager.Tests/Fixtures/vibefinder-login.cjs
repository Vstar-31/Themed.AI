// Execute the actual C#-generated scripts with browser storage/network doubles.
const assert = require('node:assert/strict');
const vm = require('node:vm');
const scripts = JSON.parse(require('node:fs').readFileSync(0, 'utf8'));
const jwt = exp => `header.${Buffer.from(JSON.stringify({ exp })).toString('base64url')}.signature`;
const current = jwt(Date.now() / 1000 + 3600);
const expired = jwt(Date.now() / 1000 - 1);
function browser(token, { origin = 'https://vfaithemed.netlify.app', status = 200, response = current, networkError = false } = {}) {
  const state = { calls: [], messages: [], reloads: 0 };
  const values = new Map(token ? [['vf_token', token]] : []);
  const context = vm.createContext({
    URLSearchParams, atob,
    window: { chrome: { webview: { postMessage: message => state.messages.push(JSON.parse(message)) } } },
    location: { origin, reload: () => state.reloads++ },
    localStorage: { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) },
    // A previous successful login must not block renewal in a new document.
    sessionStorage: { getItem: () => '1', setItem: () => {} },
    fetch: async (url, options) => {
      state.calls.push({ url, options });
      if (networkError) throw new Error('offline');
      return { ok: status === 200, status, json: async () => ({ access_token: response }) };
    }
  });
  return { state, values, context, run: kind => vm.runInContext(scripts[kind], context) };
}
(async () => {
  for (const token of [undefined, expired, 'malformed', jwt('tomorrow'), jwt(undefined), jwt(Date.now() / 1000 + 10)]) {
    const b = browser(token);
    assert.equal(b.run('ready'), false);
    await Promise.all([b.run('automatic'), b.run('automatic')]);
    assert.equal(b.state.calls.length, 1, 'only one renewal per document');
    assert.equal(b.state.reloads, 1);
    assert.equal(b.values.get('vf_token'), current);
    assert.equal(b.run('ready'), false, 'old SPA cannot start analysis during reload');
    assert.equal(b.state.calls[0].options.body.get('username'), "test'user");
    assert.equal(b.state.calls[0].options.body.get('password'), 'test\\password');
    const reloaded = browser(b.values.get('vf_token'));
    assert.equal(reloaded.run('ready'), true);
    await reloaded.run('automatic');
    assert.equal(reloaded.state.calls.length, 0, 'no reload loop');
  }
  const valid = browser(current);
  assert.equal(valid.run('ready'), true);
  await valid.run('automatic');
  assert.equal(valid.state.calls.length, 0);
  await valid.run('forced');
  assert.equal(valid.state.calls.length, 1, 'Save & Apply may replace a current session');
  for (const options of [{ status: 401 }, { status: 500 }, { networkError: true }, { response: expired }]) {
    const b = browser(expired, options);
    await b.run('automatic');
    await b.run('automatic');
    assert.equal(b.state.calls.length, 1, 'failed credentials/server must not loop');
    assert.equal(b.state.reloads, 0);
    assert.equal(b.state.messages[0].success, false);
    assert.equal(b.run('ready'), false);
    await b.run('forced');
    assert.equal(b.state.calls.length, 2, 'explicit retry remains possible');
  }
  const foreign = browser(expired, { origin: 'https://evil.example' });
  await foreign.run('automatic');
  await foreign.run('forced');
  assert.equal(foreign.run('ready'), false);
  assert.equal(foreign.state.calls.length, 0);
  console.log('Expired, missing, malformed, near-expiry, current, concurrent, reload, failure, explicit retry, and origin scenarios passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });

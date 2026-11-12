// Execute the actual C#-generated scripts with browser storage/network doubles.
const assert = require('node:assert/strict');
const vm = require('node:vm');
const scripts = JSON.parse(require('node:fs').readFileSync(0, 'utf8'));
const jwt = exp => `header.${Buffer.from(JSON.stringify({ exp })).toString('base64url')}.signature`;
const current = jwt(Date.now() / 1000 + 3600);
const expired = jwt(Date.now() / 1000 - 1);
function browser(token, { origin = 'https://vfaithemed.netlify.app', status = 200, response = current, networkError = false, beforeResponse = () => {} } = {}) {
  const state = { calls: [], messages: [], events: [], reloads: 0 };
  const values = new Map(token ? [['vf_token', token]] : []);
  const context = vm.createContext({
    URLSearchParams, atob, AbortController, setTimeout, clearTimeout, CustomEvent,
    window: { dispatchEvent: event => state.events.push(event), chrome: { webview: { postMessage: message => state.messages.push(JSON.parse(message)) } } },
    location: { origin, reload: () => state.reloads++ },
    localStorage: { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) },
    // A previous successful login must not block renewal in a new document.
    sessionStorage: { getItem: () => '1', setItem: () => {} },
    fetch: async (url, options) => {
      state.calls.push({ url, options });
      if (networkError) throw new Error('offline');
      return { ok: status === 200, status, json: async () => { await beforeResponse(); return { access_token: response }; } };
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
  valid.context.window._themed_session_renewal = 'pending-renewal';
  await valid.run('forced');
  assert.equal(valid.state.calls.length, 1, 'Save & Apply may replace a current session');
  assert.equal(valid.context.window._themed_session_renewal, null, 'explicit account apply supersedes a pending renewal');
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
  const renewalId = '11111111-1111-4111-8111-111111111111';
  const live = browser(expired);
  live.context.window._themed_session_renewal = renewalId;
  await Promise.all([live.run('renewal'), live.run('renewal')]);
  assert.equal(live.state.calls.length, 1);
  assert.equal(live.state.reloads, 0, 'in-place renewal preserves the SPA and prompt');
  assert.equal(live.values.get('vf_token'), current);
  assert.equal(live.state.events[0].detail.success, true);
  assert.equal(live.state.events[0].detail.id, renewalId);
  assert.deepEqual(Object.keys(live.state.events[0].detail).sort(), ['id', 'success'], 'no token in host event');
  const cancelled = browser(expired, { beforeResponse: () => {
    cancelled.context.window._themed_session_renewal = null;
    cancelled.values.delete('vf_token');
  } });
  cancelled.context.window._themed_session_renewal = renewalId;
  await cancelled.run('renewal');
  assert.equal(cancelled.values.has('vf_token'), false, 'late response cannot undo Sign Out');
  assert.equal(cancelled.state.events[0].detail.success, false);
  for (const options of [{ status: 401 }, { networkError: true }, { response: expired }]) {
    const failed = browser(expired, options);
    failed.context.window._themed_session_renewal = renewalId;
    await failed.run('renewal');
    assert.equal(failed.state.events[0].detail.success, false);
    assert.equal(failed.state.reloads, 0);
    assert.equal(failed.context.window._themed_renewal_in_flight, false);
  }
  const unsolicited = browser(expired);
  await unsolicited.run('renewal');
  assert.equal(unsolicited.state.calls.length, 0);
  foreign.context.window._themed_session_renewal = renewalId;
  await foreign.run('renewal');
  assert.equal(foreign.state.calls.length, 0);
  console.log('Expired, missing, malformed, near-expiry, current, concurrent, reload, failure, explicit retry, and origin scenarios passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });

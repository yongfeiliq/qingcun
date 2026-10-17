'use strict';

/** Separate browser clients never overwrite each other's account checks. */
function createAuthWorker({
  chrome,
  native,
  auth,
  uuid = () => crypto.randomUUID(),
  browser = /Edg\//.test(globalThis.navigator?.userAgent || '') ? 'Edge' : 'Chrome / Chromium',
  now = Date.now,
}) {
  const alarm = 'qingcun.account-check';
  const interval = 5 * 60 * 1000;
  const dirty = new Map();
  const recent = new Map();
  const tabPlatforms = new Map();
  let busy = false;
  const mark = (platform) => dirty.set(platform, (dirty.get(platform) || 0) + 1);
  function clearObserved(ids, revisions) {
    for (const platform of ids)
      if ((dirty.get(platform) || 0) === (revisions.get(platform) || 0)) dirty.delete(platform);
  }

  async function client() {
    const saved = (await chrome.storage.local.get('accountClient')).accountClient;
    if (saved) return saved;
    const id = uuid();
    await chrome.storage.local.set({ accountClient: id });
    return id;
  }

  async function poll() {
    if (busy) return;
    busy = true;
    try {
      const id = await client();
      const response = await native({
        action: 'platformAuthPoll',
        client: id,
        browser,
        extensionVersion: chrome.runtime.getManifest().version,
      });
      if (chrome.tabs?.query) {
        for (const tab of await chrome.tabs.query({})) {
          if (tab.incognito) continue;
          const platform = auth.platforms.find((platform) => auth.matches(tab.url || '', platform));
          if (platform) tabPlatforms.set(tab.id, platform.id);
        }
      }
      for (const request of response.requests || []) {
        const revisions = new Map(dirty);
        const results = await auth.checkAll(chrome, request.platforms);
        await native({ action: 'platformAuthResult', client: id, id: request.id, results });
        clearObserved(request.platforms, revisions);
        if (request.platforms.length === auth.platforms?.length)
          await chrome.storage.local.set({ accountAutoCheckedAt: now() });
      }
      // The existing browser session is reused. Never open pages or run a login flow.
      const last = (await chrome.storage.local.get('accountAutoCheckedAt')).accountAutoCheckedAt;
      const due = !last || now() < last || now() - last >= interval;
      const ids = due ? auth.platforms?.map((platform) => platform.id) : [...dirty.keys()];
      if (ids?.length) {
        const revisions = new Map(dirty);
        const results = await auth.checkAll(chrome, ids);
        await native({ action: 'platformAuthUpdate', client: id, results });
        clearObserved(ids, revisions);
        if (due) await chrome.storage.local.set({ accountAutoCheckedAt: now() });
      }
    } catch {
      // Eagle displays the heartbeat age; disconnected is never "signed out".
    } finally {
      busy = false;
    }
  }

  async function start() {
    if (!(await chrome.alarms.get(alarm)))
      await chrome.alarms.create(alarm, { periodInMinutes: 0.5 });
    await poll();
  }

  function wake(tab) {
    if (tab?.incognito) return;
    const platform = auth.platforms.find((platform) => auth.matches(tab?.url || '', platform));
    const previous = tabPlatforms.get(tab?.id);
    if (previous && previous !== platform?.id) mark(previous);
    if (platform && tab?.id != null) tabPlatforms.set(tab.id, platform.id);
    else tabPlatforms.delete(tab?.id);
    if (!platform) {
      if (previous) poll();
      return;
    }
    if (!platform || now() - (recent.get(platform.id) || 0) < 10000) return;
    recent.set(platform.id, now());
    mark(platform.id);
    poll();
  }

  async function check(platform = '') {
    const id = await client();
    await native({
      action: 'platformAuthPoll',
      client: id,
      browser,
      extensionVersion: chrome.runtime.getManifest().version,
    });
    const ids = platform ? [platform] : auth.platforms.map((platform) => platform.id);
    const results = await auth.checkAll(chrome, ids);
    await native({ action: 'platformAuthUpdate', client: id, results });
    return results;
  }

  async function snapshot() {
    const id = await client();
    const response = await native({ action: 'platformAuthStatus' });
    return response.clients.find((record) => record.client === id) || { results: [] };
  }

  async function invalidate() {
    for (const platform of auth.platforms) mark(platform.id);
    await chrome.storage.local.remove('accountAutoCheckedAt');
    await poll();
  }

  function removed(id) {
    const platform = tabPlatforms.get(id);
    tabPlatforms.delete(id);
    if (platform) {
      mark(platform);
      poll();
    }
  }

  return { alarm, poll, start, wake, check, snapshot, invalidate, removed };
}
if (typeof module !== 'undefined') module.exports = { createAuthWorker };

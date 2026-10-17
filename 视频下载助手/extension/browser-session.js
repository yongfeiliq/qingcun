/* Only cookies for the user's requested platform are sent to the local host.
   The host encrypts them for the current Windows user and never logs values. */
globalThis.QingcunBrowserSession = (() => {
  const roots = Object.freeze({
    youtube: ['youtube.com', 'google.com'],
    instagram: ['instagram.com'],
    pinterest: ['pinterest.com'],
    behance: ['behance.net'],
    vimeo: ['vimeo.com'],
    bilibili: ['bilibili.com'],
    douyin: ['douyin.com', 'iesdouyin.com'],
    tiktok: ['tiktok.com'],
    xinpianchang: ['xinpianchang.com'],
    x: ['x.com', 'twitter.com']
  });
  const platformRoots = Object.freeze({
    youtube: ['youtube.com', 'youtu.be'], instagram: ['instagram.com'],
    pinterest: ['pinterest.com'], behance: ['behance.net'], vimeo: ['vimeo.com'],
    bilibili: ['bilibili.com'], douyin: ['douyin.com', 'iesdouyin.com'],
    tiktok: ['tiktok.com'], xinpianchang: ['xinpianchang.com'],
    x: ['x.com', 'twitter.com']
  });
  const domainMatches = (domain, root) => domain === root || domain.endsWith('.' + root);
  function platform(url) {
    try {
      const parsed = new URL(url);
      if (parsed.protocol !== 'https:' || parsed.username || parsed.password) return '';
      const host = parsed.hostname.toLowerCase();
      return Object.keys(platformRoots).find(key => platformRoots[key].some(root => domainMatches(host, root))) || '';
    } catch { return ''; }
  }
  function cookieAllowed(cookie, key) {
    if (cookie.partitionKey || !cookie.value || !roots[key].some(root => domainMatches(cookie.domain.replace(/^\./, '').toLowerCase(), root))) return false;
    // YouTube needs the Google sign-in cookies, not all Google preferences.
    if (key === 'youtube' && domainMatches(cookie.domain.replace(/^\./, '').toLowerCase(), 'google.com'))
      return /^(?:SID|HSID|SSID|APISID|SAPISID|SIDCC|__Secure-[A-Za-z0-9_-]+)$/.test(cookie.name);
    return true;
  }
  let serial = Promise.resolve();
  async function sync(key, native) {
    if (!roots[key] || !chrome.cookies) return false;
    const unique = new Map();
    for (const root of roots[key]) {
      for (const cookie of await chrome.cookies.getAll({domain: root})) {
        if (!cookieAllowed(cookie, key)) continue;
        const value = {domain:cookie.domain, name:cookie.name, value:cookie.value,
          path:cookie.path, secure:cookie.secure, httpOnly:cookie.httpOnly,
          hostOnly:cookie.hostOnly, expirationDate:cookie.expirationDate || 0};
        unique.set([value.domain, value.path, value.name].join('\n'), value);
      }
    }
    const cookies = [...unique.values()].slice(0, 300);
    const browser = /\bEdg\//.test(globalThis.navigator?.userAgent || '') ? 'edge' : 'chrome';
    await native({action:'saveBrowserSession', platform:key, browser, cookies});
    return cookies.length > 0;
  }
  function enqueue(key, native) {
    const next = serial.catch(() => {}).then(() => sync(key, native));
    serial = next;
    return next;
  }
  async function syncUrls(urls, native) {
    for (const key of new Set((urls || []).map(platform).filter(Boolean))) await enqueue(key, native);
  }
  async function syncOpenTabs(native) {
    if (!chrome.cookies) return;
    const tabs = await chrome.tabs.query({});
    await syncUrls(tabs.map(tab => tab.url), native);
  }
  async function syncAll(native) {
    if (!chrome.cookies) return;
    for (const key of Object.keys(roots)) await enqueue(key, native);
  }
  function changed(cookie, native) {
    const host = String(cookie?.domain || '').replace(/^\./, '').toLowerCase();
    const key = Object.keys(roots).find(k => roots[k].some(root => domainMatches(host, root)));
    if (key) enqueue(key, native).catch(() => {});
  }
  return {platform, syncUrls, syncOpenTabs, syncAll, changed};
})();

'use strict';

/** Read-only account checks. Never read passwords, cookies or access tokens. */
globalThis.QingcunAuth = (() => {
  const platforms = [
    {
      id: 'youtube',
      name: 'YouTube',
      url: 'https://www.youtube.com/',
      hosts: ['youtube.com'],
      account: ['#avatar-btn'],
      login: ['a[href*="accounts.google.com/ServiceLogin"]'],
    },
    {
      id: 'vimeo',
      name: 'Vimeo',
      url: 'https://vimeo.com/',
      hosts: ['vimeo.com'],
      account: ['[data-testid="user-menu"]', '[data-testid="user-avatar"]', 'a[href="/settings"]'],
      login: ['a[href="/log_in"]'],
    },
    {
      id: 'bilibili',
      name: 'bilibili',
      url: 'https://www.bilibili.com/',
      hosts: ['bilibili.com'],
      account: ['.header-avatar-wrap .bili-avatar-img', '.header-avatar-wrap img'],
      login: ['.header-login-entry'],
    },
    {
      id: 'douyin',
      name: '抖音',
      url: 'https://www.douyin.com/',
      hosts: ['douyin.com'],
      account: ['[data-e2e="user-avatar"]', '[data-e2e="user-info"]'],
      login: ['[data-e2e="login-button"]'],
      loginText: ['登录', 'Log in', 'Sign in'],
    },
    {
      id: 'tiktok',
      name: 'TikTok',
      url: 'https://www.tiktok.com/',
      hosts: ['tiktok.com'],
      account: ['[data-e2e="profile-icon"]'],
      login: [
        '[data-e2e="top-login-button"]',
        '[data-e2e="login-button"]',
        '#header-login-button',
        '#top-right-action-bar-login-button',
      ],
    },
    {
      id: 'instagram',
      name: 'Instagram',
      url: 'https://www.instagram.com/',
      hosts: ['instagram.com'],
      account: ['a[href="/accounts/edit/"]', 'a[href="/direct/inbox/"]'],
      login: ['form[action*="/accounts/login"]', 'a[href*="/accounts/login/"]'],
    },
    {
      id: 'pinterest',
      name: 'Pinterest',
      url: 'https://www.pinterest.com/',
      hosts: ['pinterest.com'],
      account: [
        '[data-test-id="header-profile"]',
        '[data-test-id="header-accounts-options-button"]',
        'a[aria-label="你的个人资料"]',
        'a[aria-label="Your profile"]',
      ],
      login: ['[data-test-id="simple-login-button"]', '[data-test-id="login-button"]'],
    },
    {
      id: 'behance',
      name: 'Behance',
      url: 'https://www.behance.net/',
      hosts: ['behance.net'],
      account: ['[data-testid="user-menu"]', 'a[href="/settings"]', '[class*="UserMenu-avatar"]'],
      login: ['a[href*="/auth/login"]', 'a[href*="/login"]'],
    },
    {
      id: 'xinpianchang',
      name: '新片场',
      url: 'https://www.xinpianchang.com/',
      hosts: ['xinpianchang.com'],
      account: ['.user-info .avatar', '.header-user .avatar', 'a[href="/user/settings"]'],
      login: ['a[href*="passport.xinpianchang.com/login"]', '.login-btn'],
      accountText: ['退出登录', 'Log out', 'Sign out'],
    },
  ];

  function matches(url, platform) {
    try {
      const value = new URL(url);
      return (
        value.protocol === 'https:' &&
        !value.username &&
        !value.password &&
        (!value.port || value.port === '443') &&
        platform.hosts.some(
          (host) => value.hostname === host || value.hostname.endsWith('.' + host),
        )
      );
    } catch {
      return false;
    }
  }

  // Self-contained because Chrome serializes the function into an isolated world.
  function probe(platform) {
    const visibleNode = (node) => {
      if (!node.getClientRects().length) return false;
      for (let ancestor = node; ancestor; ancestor = ancestor.parentElement) {
        const style = getComputedStyle(ancestor);
        if (
          style.visibility === 'hidden' ||
          style.display === 'none' ||
          style.opacity === '0' ||
          ancestor.getAttribute?.('aria-hidden') === 'true'
        )
          return false;
      }
      return true;
    };
    const visible = (selector) => [...document.querySelectorAll(selector)].some(visibleNode);
    const text = document.title + '\n' + (document.body?.innerText || '').slice(0, 6000);
    if (
      /verify you are human|security verification|use another device to continue|尝试使用另一台设备继续|无法将你当前使用的设备|安全验证|人机验证|滑块验证|访问过于频繁|access denied/i.test(
        text,
      )
    ) {
      return { status: 'verification_required', evidence: 'challenge' };
    }
    // Only known boolean fields from the current page's own bootstrap data.
    let signedIn = null;
    for (const script of document.querySelectorAll('script:not([src])')) {
      const source = script.textContent || '';
      if (platform.id === 'youtube' && /"LOGGED_IN"\s*:\s*true/.test(source)) signedIn = true;
      if (platform.id === 'youtube' && /"LOGGED_IN"\s*:\s*false/.test(source)) signedIn = false;
    }
    const labelledControl = (labels) =>
      labels?.some((label) =>
        [...document.querySelectorAll('button, a[role="button"]')].some(
          (node) => node.textContent.trim() === label && visibleNode(node),
        ),
      );
    const account = platform.account.some(visible) || labelledControl(platform.accountText);
    const login = platform.login.some(visible) || labelledControl(platform.loginText);
    if ((account && login) || (account && signedIn === false) || (login && signedIn === true))
      return { status: 'unknown', evidence: 'conflicting_controls' };
    if (account || signedIn === true)
      return { status: 'signed_in', evidence: account ? 'account_control' : 'page_boolean' };
    if (login || signedIn === false)
      return { status: 'signed_out', evidence: login ? 'login_control' : 'page_boolean' };
    return { status: 'unknown', evidence: 'no_account_signal' };
  }

  function bounded(work, timeout = 6000) {
    let timer;
    return Promise.race([
      work,
      new Promise((_, reject) => {
        timer = setTimeout(() => reject(new Error('CHECK_TIMEOUT')), timeout);
      }),
    ]).finally(() => clearTimeout(timer));
  }

  async function check(chrome, platform) {
    const origins = platform.hosts.map((host) => 'https://*.' + host + '/*');
    if (!(await chrome.permissions.contains({ origins })))
      return { status: 'permission_required', evidence: 'host_permission' };
    const tabs = (await chrome.tabs.query({})).filter(
      (tab) => !tab.incognito && matches(tab.url || '', platform),
    );
    if (!tabs.length) return { status: 'not_open', evidence: 'no_platform_tab' };
    const results = [];
    for (const tab of tabs.slice(0, 3)) {
      try {
        const result = await bounded(
          chrome.scripting.executeScript({
            target: { tabId: tab.id },
            func: probe,
            args: [platform],
          }),
        );
        const current = await chrome.tabs.get(tab.id);
        if (current.url !== tab.url || !matches(current.url, platform)) continue;
        results.push(result[0]?.result || { status: 'unknown', evidence: 'empty_result' });
      } catch {
        results.push({ status: 'unknown', evidence: 'page_unreadable' });
      }
    }
    if (!results.length) return { status: 'unknown', evidence: 'page_changed' };
    const known = new Set(
      results.filter((r) => ['signed_in', 'signed_out'].includes(r.status)).map((r) => r.status),
    );
    if (known.size > 1) return { status: 'unknown', evidence: 'conflicting_tabs' };
    return (
      results.find((r) => r.status === 'signed_in') ||
      results.find((r) => r.status === 'signed_out') ||
      results[0]
    );
  }

  async function checkAll(chrome, ids = platforms.map((p) => p.id)) {
    return Promise.all(
      platforms
        .filter((p) => ids.includes(p.id))
        .map(async (platform) => ({
          platform: platform.id,
          ...(await check(chrome, platform).catch(() => ({
            status: 'unknown',
            evidence: 'browser_error',
          }))),
        })),
    );
  }

  return { platforms, matches, probe, check, checkAll };
})();
if (typeof module !== 'undefined') module.exports = QingcunAuth;

'use strict';

/** Account metadata only; account checks never mutate Eagle assets. */
(() => {
  const platforms = [
    ['youtube', 'YouTube', 'https://www.youtube.com/'],
    ['vimeo', 'Vimeo', 'https://vimeo.com/'],
    ['bilibili', 'bilibili', 'https://www.bilibili.com/'],
    ['douyin', '抖音', 'https://www.douyin.com/'],
    ['tiktok', 'TikTok', 'https://www.tiktok.com/'],
    ['instagram', 'Instagram', 'https://www.instagram.com/'],
    ['pinterest', 'Pinterest', 'https://www.pinterest.com/'],
    ['behance', 'Behance', 'https://www.behance.net/'],
    ['xinpianchang', '新片场', 'https://www.xinpianchang.com/'],
  ];
  const $ = (id) => document.getElementById(id);
  const rows = new Map();
  let refreshPromise;
  let requestId = '';
  let requestedAt = 0;
  let checking = false;
  const element = (tag, text, className = '') => {
    const node = document.createElement(tag);
    node.textContent = text;
    node.className = className;
    return node;
  };

  function setChecking(value) {
    checking = value;
    $('checkAccounts').disabled = value;
    for (const row of rows.values()) row.button.disabled = value;
  }

  function render(response) {
    const clients = [...(response.clients || [])].sort(
      (a, b) => Date.parse(b.heartbeat) - Date.parse(a.heartbeat),
    );
    $('accountBrowser').replaceChildren();
    let previous = $('accountBrowser').dataset.selected;
    try {
      previous ||= window.localStorage?.getItem('qingcun.accountBrowser');
    } catch {}
    for (const [index, client] of clients.entries()) {
      const live = Date.now() - Date.parse(client.heartbeat) < 90000;
      const option = element(
        'option',
        `${client.browser === 'Chrome / Chromium' ? 'Chrome' : client.browser || '浏览器'}${clients.length > 1 ? ' · 连接 ' + (index + 1) : ''}${live ? '' : ' · 未连接'}`,
      );
      option.value = client.client;
      $('accountBrowser').append(option);
    }
    if (clients.some((client) => client.client === previous)) $('accountBrowser').value = previous;
    const selected = clients.find((client) => client.client === $('accountBrowser').value);
    const live = selected && Date.now() - Date.parse(selected.heartbeat) < 90000;
    $('accountBrowser').dataset.selected = selected?.client || '';
    $('accountConnection').textContent = live ? '已自动连接' : '等待浏览器连接';
    for (const [id, row] of rows) {
      const result = selected?.results?.find((result) => result.platform === id);
      const known = selected?.lastKnown?.find((result) => result.platform === id);
      const presentation = QingcunAuthStatus.present(result, known, { live: !!live });
      row.status.textContent = presentation.label;
      row.detail.textContent = presentation.detail;
      row.status.title = row.detail.textContent;
    }
    if (requestId) {
      const complete = selected?.requestId === requestId;
      const timeout = Date.now() - requestedAt > 90000;
      $('accountMessage').textContent = complete
        ? '检查完成'
        : timeout
          ? '浏览器未在限定时间内完成检查。请确认扩展已启用并重新检查。'
          : '检查已提交，等待浏览器回报…';
      if (complete || timeout) {
        requestId = '';
        setChecking(false);
      }
    }
  }

  async function refresh() {
    if (refreshPromise) return refreshPromise;
    refreshPromise = window.Qingcun.call({ action: 'platformAuthStatus' })
      .then(render)
      .catch((error) => {
        $('accountMessage').textContent = error.message;
      })
      .finally(() => {
        refreshPromise = null;
      });
    return refreshPromise;
  }

  async function check(platform = '') {
    if (checking) return;
    setChecking(true);
    try {
      const response = await window.Qingcun.call({
        action: 'requestPlatformAuth',
        platform,
        client: $('accountBrowser').value,
      });
      requestId = response.id;
      requestedAt = Date.now();
      await refresh();
    } catch (error) {
      $('accountMessage').textContent = error.message;
      setChecking(false);
    }
  }

  for (const [id, name, url] of platforms) {
    const card = element('article', '', 'panel account-card');
    const status = element('p', '待自动确认', 'account-status');
    const detail = element('p', '在浏览器完成平台登录，再检查。', 'hint');
    detail.hidden = true;
    const actions = element('div', '', 'actions');
    const open = element('button', '打开 / 登录 ↗');
    open.onclick = async () => {
      try {
        await window.Qingcun.openExternal(url);
      } catch (error) {
        $('accountMessage').textContent = error.message;
      }
    };
    const button = element('button', '刷新');
    button.onclick = () => check(id);
    actions.append(open, button);
    card.append(element('h2', name), status, detail, actions);
    $('accountCards').append(card);
    rows.set(id, { status, detail, button });
  }
  $('checkAccounts').onclick = () => check();
  $('accountBrowser').onchange = () => {
    $('accountBrowser').dataset.selected = $('accountBrowser').value;
    try {
      window.localStorage?.setItem('qingcun.accountBrowser', $('accountBrowser').value);
    } catch {}
    refresh();
  };
  window.QingcunAccounts = { refresh };
  setInterval(() => {
    if (!$('accountsPage').hidden) refresh();
  }, 2500);
})();

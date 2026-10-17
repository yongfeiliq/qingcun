'use strict';

const accountRows = new Map();
let refreshing = false;
const create = (tag, text, className = '') => {
  const node = document.createElement(tag);
  node.textContent = text;
  node.className = className;
  return node;
};

async function check(platform = '') {
  const buttons = [...document.querySelectorAll('button')];
  buttons.forEach((button) => {
    button.disabled = true;
  });
  document.getElementById('message').textContent = '正在读取本浏览器的平台页面…';
  try {
    const response = await chrome.runtime.sendMessage({
      type: 'qingcun.accountCheck',
      ticket: crypto.randomUUID(),
      platform,
    });
    if (!response?.ok) throw new Error(response?.error || '浏览器后台未连接。');
    await refresh();
    document.getElementById('message').textContent = '检查完成。';
  } catch (error) {
    document.getElementById('message').textContent = error.message;
  } finally {
    buttons.forEach((button) => {
      button.disabled = false;
    });
  }
}

async function refresh() {
  if (refreshing) return;
  refreshing = true;
  try {
    const record = await chrome.runtime.sendMessage({
      type: 'qingcun.accountStatus',
      ticket: crypto.randomUUID(),
    });
    if (!record?.ok) throw new Error(record?.error || '等待浏览器连接');
    for (const [id, row] of accountRows) {
      const presentation = QingcunAuthStatus.present(
        record.results?.find((result) => result.platform === id),
        record.lastKnown?.find((result) => result.platform === id),
      );
      row.status.textContent = presentation.label;
      row.detail.textContent = presentation.detail;
    }
  } catch (error) {
    document.getElementById('message').textContent = error.message;
  } finally {
    refreshing = false;
  }
}

for (const platform of QingcunAuth.platforms) {
  const card = create('article', '');
  const status = create('p', '待自动确认', 'status');
  const detail = create('p', '', 'detail');
  detail.hidden = true;
  const actions = create('div', '', 'account-actions');
  const link = create('a', '打开 / 登录');
  link.href = platform.url;
  link.target = '_blank';
  link.rel = 'noopener';
  const permission = create('button', '允许访问');
  permission.onclick = () => {
    chrome.permissions
      .request({ origins: platform.hosts.map((host) => 'https://*.' + host + '/*') })
      .then((granted) => {
        document.getElementById('message').textContent = granted
          ? '网站权限已允许，将自动更新。'
          : '网站权限未允许。';
      })
      .catch((error) => {
        document.getElementById('message').textContent = error.message;
      });
  };
  const update = create('button', '刷新');
  update.onclick = () => check(platform.id);
  actions.append(link, permission, update);
  card.append(create('h2', platform.name), status, detail, actions);
  document.getElementById('accounts').append(card);
  accountRows.set(platform.id, { status, detail });
}
document.getElementById('checkAll').onclick = () => check();
refresh();
setInterval(() => {
  if (!document.hidden) refresh();
}, 5000);

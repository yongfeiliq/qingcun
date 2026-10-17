'use strict';
(() => {
  const extensionPage = location.protocol === 'chrome-extension:' && !!globalThis.chrome?.runtime?.id;
  const available = extensionPage && typeof globalThis.chrome.runtime.sendNativeMessage === 'function';
  // Opening a page must never reload the extension or depend on a release number.
  globalThis.QingcunContext = Object.freeze({ available });
  if (available) return;
  const isHelp = location.pathname.endsWith('/help.html');
  function make(tag, text, className) {
    const node = document.createElement(tag);
    if (text) node.textContent = text;
    if (className) node.className = className;
    return node;
  }
  const guide = make('section', '', 'qingcun-opening-guide');
  guide.id = 'qingcun-opening-guide';
  guide.setAttribute('aria-labelledby', 'qingcun-guide-title');
  const title = make('h1', extensionPage ? '请重新加载轻存扩展' : '请从浏览器扩展打开轻存');
  title.id = 'qingcun-guide-title';
  guide.append(make('p', '轻存 · 打开方式', 'qingcun-guide-eyebrow'), title,
    make('p', extensionPage
      ? '扩展的本地通信权限尚未就绪。请到扩展管理页重新加载“轻存”；仍有问题时，重新加载安装目录中的扩展。'
      : '当前页面以普通网页或本地文件方式打开，无法连接电脑上的下载组件。请按下面的步骤打开素材库。'));
  const steps = make('ol');
  for (const text of [
    '点击 Chrome / Edge 右上角的拼图图标，找到“轻存 · 视频下载助手”。',
    '点击“轻存”，再点击“打开视频素材库”。',
    '如果没有找到“轻存”，打开扩展管理页，开启“开发者模式”，点击“加载已解压的扩展程序”，选择下方文件夹。'
  ]) steps.append(make('li', text));
  guide.append(steps);
  const status = make('p', '', 'qingcun-guide-status');
  status.setAttribute('role', 'status');
  status.setAttribute('aria-live', 'polite');
  function copyRow(label, value) {
    const row = make('div', '', 'qingcun-guide-row');
    const fieldLabel = make('label', label);
    const input = make('input');
    input.type = 'text'; input.value = value; input.readOnly = true;
    input.setAttribute('aria-label', label);
    input.addEventListener('click', () => input.select());
    const button = make('button', '复制');
    button.type = 'button';
    button.addEventListener('click', async () => {
      try {
        if (!navigator.clipboard?.writeText) throw Error('clipboard unavailable');
        await navigator.clipboard.writeText(value);
        status.textContent = '已复制：' + label + '。';
      } catch {
        input.focus(); input.select();
        status.textContent = '已选中内容，请按 Ctrl+C 复制。';
      }
    });
    fieldLabel.append(input); row.append(fieldLabel, button); guide.append(row);
  }
  copyRow('Chrome 扩展管理地址（粘贴到浏览器地址栏）', 'chrome://extensions');
  copyRow('Edge 扩展管理地址（粘贴到浏览器地址栏）', 'edge://extensions');
  copyRow('安装后的扩展文件夹（粘贴到文件夹选择窗口）', '%LOCALAPPDATA%\\QingcunVideo\\extension');
  copyRow('素材库地址（已加载扩展时，粘贴到浏览器地址栏）',
    'chrome-extension://ikbihjphodgkkollcgcocjibbgkehldo/library.html');
  guide.append(status, make('p', '升级后，请先在扩展管理页点击“重新加载”，再从工具栏打开轻存。直接双击 library.html、settings.html 或 popup.html 无法使用下载功能。', 'qingcun-guide-tip'));
  if (isHelp) {
    for (const link of document.querySelectorAll('a[href]')) {
      const target = new URL(link.getAttribute('href'), location.href);
      if (target.origin === location.origin && /\/(library|settings|popup)\.html$/.test(target.pathname)) {
        link.href = '#qingcun-opening-guide';
        link.removeAttribute('target');
        link.title = '请按下方步骤从扩展打开';
      }
    }
    document.querySelector('main').prepend(guide);
  } else {
    document.body.classList.add('qingcun-context-page');
    for (const node of [...document.body.children]) {
      if (node.tagName !== 'SCRIPT') node.hidden = true;
    }
    const help = make('a', '查看完整安装说明');
    help.href = 'help.html';
    guide.append(help);
    document.body.prepend(guide);
    document.title = '轻存 · 请从浏览器扩展打开';
  }
})();

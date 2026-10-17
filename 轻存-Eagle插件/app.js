'use strict';
const path = require('path'),
  native = require(path.join(__dirname, 'native.cjs')),
  { Sync } = require(path.join(__dirname, 'sync.cjs')),
  adapt = require(path.join(__dirname, 'eagle-adapter.cjs'));
const { ExistingVideos } = require(path.join(__dirname, 'existing.cjs'));
const $ = (id) => document.getElementById(id);
let sync,
  existing,
  api,
  ready = false,
  started = false;
const el = (tag, text, cl) => {
  const n = document.createElement(tag);
  if (text) n.textContent = text;
  if (cl) n.className = cl;
  return n;
};
function message(t, bad = false) {
  $('message').textContent = t;
  $('message').classList.toggle('error', bad);
}
const openExternal = (url) => {
  const u = new URL(url);
  if (!['https:', 'http:'].includes(u.protocol)) throw Error('链接格式不支持');
  return eagle.shell.openExternal(u.href);
};
window.Qingcun = {
  call: async (q) => {
    const r = await native.call(q);
    if (
      [
        'editAsset',
        'editFilmTypeAnalysis',
        'startFilmTypeAnalysis',
        'editImageAnalysis',
        'editAnalysis',
        'editCredit',
        'editSource',
        'organizeBrands',
        'startAnalysis',
        'refreshSource',
      ].includes(q.action) &&
      q.id &&
      existing
    ) {
      await existing.edited(q.id);
      setTimeout(() => existing.tick(), 100);
    }
    return r;
  },
  openExternal,
  openAsset: (id) => {
    showPage('library');
    const a = native.readAssets().find((a) => a.id === id);
    $('workspace').src =
      'views/' +
      (a?.mediaType === 'image' ? 'image' : 'film-type') +
      '.html#asset=' +
      encodeURIComponent(id);
  },
};
function report(r) {
  $('syncState').textContent = r.message;
  $('syncState').classList.toggle('error', !!r.error);
  const s = r.state || sync?.state;
  if (!s) return;
  $('libraryName').textContent = s.libraryName || '等待打开资源库';
  $('libraryPath').textContent = s.libraryPath || '';
  $('autoSync').checked = s.enabled;
  const rows = Object.values(s.entries).filter((x) => x.status === '已入库');
  $('imported').textContent = rows.length;
  $('pending').textContent = s.errors.length;
  $('connection').textContent = r.error ? '等待连接' : s.enabled ? '后台已开启' : '已暂停';
  $('recent').replaceChildren();
  for (const r of rows.sort((a, b) => b.updated.localeCompare(a.updated)).slice(0, 6)) {
    const b = el('button', '', 'recent-item');
    b.append(el('span', r.title), el('small', 'Eagle 播放 ↗'));
    b.onclick = () => eagle.item.open(r.itemId).catch((e) => message(e.message, true));
    $('recent').append(b);
  }
  if (!rows.length) $('recent').append(el('p', '视频下载完成后会自动出现在这里。', 'empty'));
  $('errors').replaceChildren();
  for (const e of s.errors.slice(0, 5)) $('errors').append(el('p', e.title + '：' + e.message));
}
async function refresh() {
  if (!ready) return;
  try {
    const r = await native.call({ action: 'list' });
    $('jobs').replaceChildren();
    for (const j of r.jobs.slice(0, 5)) {
      const box = el('article', '', 'job'),
        row = el('div', '', 'row');
      row.append(
        el('strong', j.title || '视频下载'),
        el(
          'span',
          {
            queued: '等待下载',
            running: '下载中',
            completed: '已下载',
            partial: '部分完成',
            failed: '未完成',
          }[j.status] || j.status,
        ),
      );
      box.append(row, el('p', j.detail || ''));
      if (['running', 'queued'].includes(j.status)) {
        const p = el('progress');
        p.max = 100;
        p.value = j.percent || 0;
        box.append(p);
      }
      $('jobs').append(box);
    }
    if (!r.jobs.length)
      $('jobs').append(el('p', '还没有下载任务。添加一条喜欢的参考片吧。', 'empty'));
  } catch (e) {
    message(e.message, true);
  }
}
function review(r) {
  const dialog = $('duplicate');
  $('duplicateRows').replaceChildren();
  const fields = [];
  for (const row of r.items) {
    if (!row.known && !row.active) continue;
    const box = el('div', '', 'duplicate-row'),
      select = el('select'),
      target = el('select');
    box.append(el('strong', row.title), el('small', row.url));
    for (const [v, t] of [
      ['skip', row.active ? '等待现有任务完成' : '跳过，保留当前视频'],
      ...(!row.active ? [['copy', '再次下载，另存一份']] : []),
      ...((row.files || []).some((f) => f.canReplace)
        ? [['replace', '替换原片，保留 Eagle 分类与标签']]
        : []),
    ]) {
      const o = el('option', t);
      o.value = v;
      select.append(o);
    }
    for (const f of row.files || []) {
      if (!f.canReplace) continue;
      const o = el('option', path.basename(f.path));
      o.value = f.path;
      target.append(o);
    }
    target.hidden = true;
    select.onchange = () => (target.hidden = select.value !== 'replace');
    box.append(select, target);
    $('duplicateRows').append(box);
    fields.push({ row, select, target });
  }
  return new Promise((resolve) => {
    dialog.addEventListener(
      'close',
      () =>
        resolve(
          dialog.returnValue === 'confirm'
            ? fields.map((x) => ({
                url: x.row.url,
                decision: x.select.value,
                target: x.target.value,
              }))
            : null,
        ),
      { once: true },
    );
    dialog.showModal();
  });
}
$('downloadForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  if (!ready) return;
  $('submit').disabled = true;
  try {
    const urls = $('urls').value.trim().split(/\s+/).filter(Boolean);
    if (!urls.length || urls.length > 20) throw Error('请输入 1 至 20 条链接，每行一条。');
    let q = {
      urls,
      quality: $('quality').value,
      brandOverride: $('brand').value.trim(),
      subtitles: {
        mode: $('subtitleMode').value,
        language: $('language').value,
        automatic: true,
        size: 24,
      },
    };
    message('正在检查链接与下载记录…');
    let checked = await native.call({ action: 'checkDownloads', ...q });
    for (let attempt = 0; attempt < 5; attempt++) {
      if (checked.requiresConfirmation) {
        const choices = await review(checked.review);
        if (!choices) {
          message('已取消本次下载。');
          return;
        }
        q = { ...q, snapshot: checked.review.snapshot, choices };
      }
      const result = await native.call({ action: 'download', ...q });
      if (result.review) {
        checked = result;
        continue;
      }
      message(result.count === 0 ? '所选视频已跳过。' : '任务已添加。完成后自动进入 Eagle。');
      if (result.count) $('urls').value = '';
      await refresh();
      return;
    }
    throw Error('下载记录持续变化，请刷新后重试。');
  } catch (e) {
    message(e.message, true);
  } finally {
    $('submit').disabled = false;
  }
});
function showPage(page) {
  document
    .querySelectorAll('[data-page]')
    .forEach((n) => n.classList.toggle('active', n.dataset.page === page));
  $('home').hidden = page !== 'download';
  $('existingPageWrap').hidden = page !== 'existing';
  $('frameWrap').hidden = !['library', 'settings'].includes(page);
  if (['library', 'settings'].includes(page)) {
    const src = 'views/' + page + '.html';
    if (!$('workspace').src.split('#')[0].endsWith(src)) $('workspace').src = src;
  }
}
document
  .querySelectorAll('[data-page]')
  .forEach((b) => (b.onclick = () => showPage(b.dataset.page)));
showPage('download');
$('sync').onclick = async () => {
  if (!sync) return;
  $('sync').disabled = true;
  try {
    await sync.run();
  } finally {
    $('sync').disabled = false;
  }
};
$('autoSync').onchange = () => {
  if (!sync) return;
  sync.state.enabled = $('autoSync').checked;
  sync.save();
  report({ message: sync.state.enabled ? '自动入库已开启' : '自动入库已暂停', state: sync.state });
  if (sync.state.enabled) sync.run();
};
function choiceMessage(text, bad = false) {
  $('libraryChoiceMessage').textContent = text;
  $('libraryChoiceMessage').classList.toggle('error', bad);
}
function addLibraryChoice(libraryPath, label) {
  const resolved = path.resolve(libraryPath),
    compare = (p) =>
      process.platform === 'darwin' ? path.resolve(p) : path.resolve(p).toLowerCase();
  let option = [...$('librarySelect').options].find((o) => compare(o.value) === compare(resolved));
  if (!option) {
    option = el('option', label || path.basename(resolved).replace(/\.library$/i, ''));
    option.value = resolved;
    option.title = resolved;
    $('librarySelect').append(option);
  }
  return option;
}
function showChoicePath() {
  $('libraryChoicePath').textContent = $('librarySelect').value;
}
$('chooseLibrary').onclick = async () => {
  if (!sync || sync.busy) {
    message('正在同步，请完成后再选择资源库。', true);
    return;
  }
  $('librarySelect').replaceChildren();
  choiceMessage('正在读取资源库…');
  $('libraryChooser').showModal();
  try {
    const current = await api.library();
    let history = [];
    try {
      history = await api.libraries();
    } catch {
      choiceMessage('最近使用列表暂不可用，可使用 Eagle 当前库或浏览选择。');
    }
    for (const l of Object.values(sync.state.libraries || {}))
      addLibraryChoice(l.libraryPath, l.libraryName);
    for (const p of history) addLibraryChoice(p);
    if (current.path)
      addLibraryChoice(
        current.path,
        (current.name || path.basename(current.path)) + '（Eagle 当前库）',
      );
    if (sync.state.libraryPath) {
      addLibraryChoice(sync.state.libraryPath, sync.state.libraryName);
      $('librarySelect').value = path.resolve(sync.state.libraryPath);
    } else if (current.path) $('librarySelect').value = path.resolve(current.path);
    showChoicePath();
    if (!history.length && $('librarySelect').options.length === 0)
      choiceMessage('请打开 Eagle 资源库，或浏览选择已有 .library 目录。');
    else if (history.length) choiceMessage('选择后点击“设为入库资源库”。');
  } catch (e) {
    choiceMessage(e.message, true);
  }
};
$('librarySelect').onchange = showChoicePath;
$('browseLibrary').onclick = async () => {
  try {
    const result = await eagle.dialog.showOpenDialog({
      title: '选择 Eagle 资源库',
      buttonLabel: '选择资源库',
      properties: ['openDirectory'],
      defaultPath: sync.state.libraryPath || require('os').homedir(),
    });
    if (result.canceled || !result.filePaths?.length) return;
    const file = result.filePaths[0];
    if (!/\.library$/i.test(path.resolve(file))) throw Error('请选择已有的 .library 资源库目录。');
    const option = addLibraryChoice(file);
    $('librarySelect').value = option.value;
    showChoicePath();
    choiceMessage('点击“设为入库资源库”完成选择。');
  } catch (e) {
    choiceMessage(e.message, true);
  }
};
async function applyLibrary(libraryPath) {
  if (!ready) return;
  for (const id of ['applyLibrary', 'currentLibrary', 'browseLibrary']) $(id).disabled = true;
  try {
    choiceMessage('正在切换资源库…');
    await sync.selectLibrary(libraryPath);
    $('libraryChooser').close();
    window.dispatchEvent(new Event('qingcun-library-changed'));
    message('入库资源库已选择：' + sync.state.libraryName + '。旧素材保留在原库。');
    await sync.run();
    existing?.tick();
  } catch (e) {
    choiceMessage(e.message, true);
  } finally {
    for (const id of ['applyLibrary', 'currentLibrary', 'browseLibrary']) $(id).disabled = false;
  }
}
$('applyLibrary').onclick = () => applyLibrary($('librarySelect').value);
$('currentLibrary').onclick = async () => {
  try {
    const current = await api.library();
    await applyLibrary(current.path);
  } catch (e) {
    choiceMessage(e.message, true);
  }
};
$('refresh').onclick = refresh;
$('openRoot').onclick = async () => {
  try {
    await sync.guard();
    const id = await sync.folder(['轻存']);
    await eagle.folder.open(id);
  } catch (e) {
    message(e.message, true);
  }
};
async function boot(plugin) {
  if (started) return;
  started = true;
  try {
    await native.ensureEngine(plugin?.path || __dirname);
    api = adapt(eagle);
    sync = new Sync({
      api,
      statePath: path.join(native.base, 'eagle/state.json'),
      readAssets: native.readAssets,
      isBusy: native.jobsBusy,
      notify: report,
      storage: require(path.join(__dirname, 'storage.cjs'))(native.call),
    });
    existing = new ExistingVideos({
      api,
      native,
      statePath: path.join(native.base, 'eagle/existing.json'),
      notify: (r) => window.QingcunExistingReport?.(r),
    });
    window.QingcunExisting = existing;
    ready = true;
    $('submit').disabled = false;
    $('chooseLibrary').disabled = false;
    await refresh();
    await sync.run();
    existing.tick();
    setInterval(() => existing.tick(), 5000);
    setInterval(refresh, 5000);
    setInterval(() => sync.run(), 15000);
  } catch (e) {
    message(e.message, true);
    $('syncState').textContent = '组件未就绪';
    started = false;
  }
}
eagle.onPluginCreate(boot);
eagle.onPluginRun(() => {
  eagle.window.show();
  if (!started) boot();
  else {
    refresh();
    sync?.run();
  }
});
eagle.onLibraryChanged(() => {
  window.dispatchEvent(new Event('qingcun-library-changed'));
  sync?.run();
  existing?.tick();
});
setTimeout(() => boot(), 250);

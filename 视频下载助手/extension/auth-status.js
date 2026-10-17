'use strict';

/** A saved observation is history, never proof that a closed page is still signed in. */
globalThis.QingcunAuthStatus = {
  present(result, known, { live = true, now = Date.now() } = {}) {
    const labels = {
      signed_in: '已登录',
      signed_out: '未登录',
      permission_required: '需要网站权限',
      verification_required: '需要网页验证',
    };
    const fresh = live && result && now - Date.parse(result.checkedAt) < 5 * 60 * 1000;
    const history = ['signed_in', 'signed_out'].includes(result?.status) ? result : known;
    const time = history?.checkedAt ? new Date(history.checkedAt).toLocaleString() : '';
    if (live && ['permission_required', 'verification_required'].includes(result?.status))
      return { label: labels[result.status], detail: '请在浏览器处理，轻存会自动更新状态。' };
    if (fresh && ['signed_in', 'signed_out'].includes(result.status))
      return { label: labels[result.status], detail: '已自动检测；复用此浏览器的登录会话。' };
    if (history && ['signed_in', 'signed_out'].includes(history.status))
      return {
        label: '上次' + labels[history.status],
        detail: `${time}。${live ? '页面可读取时自动更新。' : '浏览器连接后自动更新。'}`,
      };
    return {
      label: !live ? '等待浏览器连接' : result?.status === 'not_open' ? '未打开' : '待自动确认',
      detail: '在此浏览器使用平台时自动检测，无需每次手动检查。',
    };
  },
};
if (typeof module !== 'undefined') module.exports = QingcunAuthStatus;

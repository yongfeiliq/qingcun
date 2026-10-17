import { spawn } from 'node:child_process';
import { homedir } from 'node:os';
import { join, dirname } from 'node:path';

const HOSTS = new Set([
  'youtube.com', 'www.youtube.com', 'm.youtube.com', 'youtu.be',
  'www.instagram.com', 'instagram.com', 'www.pinterest.com', 'pinterest.com',
  'www.behance.net', 'behance.net', 'vimeo.com', 'www.vimeo.com', 'player.vimeo.com',
  'www.bilibili.com', 'bilibili.com', 'm.bilibili.com',
  'www.douyin.com', 'douyin.com', 'm.douyin.com',
  'www.tiktok.com', 'tiktok.com', 'm.tiktok.com',
  'www.xinpianchang.com', 'xinpianchang.com',
  'x.com', 'www.x.com', 'twitter.com', 'www.twitter.com',
]);

export function installedHost() {
  if (process.platform === 'win32') {
    if (!process.env.LOCALAPPDATA) throw new Error('找不到 Windows 用户目录。');
    return join(process.env.LOCALAPPDATA, 'QingcunVideo', 'native', 'QingcunHost.exe');
  }
  if (process.platform === 'darwin') {
    return join(homedir(), 'Library', 'Application Support', 'QingcunVideo', 'native', 'QingcunHost');
  }
  throw new Error('轻存本地组件目前仅支持 Windows 和 macOS。');
}

export function validateUrls(values) {
  if (!Array.isArray(values) || values.length < 1 || values.length > 20) {
    throw new Error('每次请输入 1 至 20 条视频链接。');
  }
  return values.map((value) => {
    if (typeof value !== 'string' || value.length > 4000) throw new Error('视频链接无效。');
    let url;
    try { url = new URL(value.trim()); } catch { throw new Error('视频链接无效。'); }
    if (url.protocol !== 'https:' || url.username || url.password ||
        (url.port && url.port !== '443') || !HOSTS.has(url.hostname.toLowerCase())) {
      throw new Error('仅接受轻存支持平台的 HTTPS 视频链接。');
    }
    return url.href;
  });
}

export function rpc(request, { host = installedHost(), timeoutMs = 180000 } = {}) {
  const body = Buffer.from(JSON.stringify(request), 'utf8');
  if (body.length < 1 || body.length > 1048576) throw new Error('请求过大。');
  return new Promise((resolve, reject) => {
    const child = spawn(host, ['--eagle-rpc'], { cwd: dirname(host), windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    let chunks = [], size = 0, finished = false;
    const finish = (error, value) => {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      if (error) reject(error); else resolve(value);
    };
    const timer = setTimeout(() => {
      child.kill();
      finish(new Error('轻存本地组件响应超时。'));
    }, timeoutMs);
    child.on('error', () => finish(new Error('轻存本地组件未安装或无法启动。')));
    child.stdout.on('data', (chunk) => {
      size += chunk.length;
      if (size > 40 * 1024 * 1024) {
        child.kill();
        finish(new Error('轻存本地组件响应过大。'));
      } else chunks.push(chunk);
    });
    child.on('close', (code) => {
      if (finished) return;
      try {
        const bytes = Buffer.concat(chunks);
        if (code !== 0 || bytes.length < 4 || bytes.readUInt32LE(0) !== bytes.length - 4) {
          throw new Error('轻存本地组件退出或响应格式无效。');
        }
        const result = JSON.parse(bytes.subarray(4).toString('utf8'));
        if (!result?.ok) throw new Error(String(result?.error || '轻存操作失败。'));
        finish(null, result);
      } catch (error) { finish(error); }
    });
    child.stdin.on('error', () => {});
    const length = Buffer.alloc(4);
    length.writeUInt32LE(body.length, 0);
    child.stdin.end(Buffer.concat([length, body]));
  });
}

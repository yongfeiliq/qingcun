import { rpc, validateUrls } from './bridge.mjs';

const result = (data) => ({
  content: [{ type: 'text', text: JSON.stringify(data) }],
  details: data,
});

export function createQingcunPlugin(call = rpc) {
  return {
    id: 'qingcun',
    name: '轻存 · Eagle 参考片',
    description: '通过已安装的轻存本地组件下载并归档视频。',
    register(api) {
      api.registerTool({
        contextVersion: 2,
        create(context) {
          if (context.senderIsOwner !== true) return null;
          return {
            name: 'qingcun_status',
            label: '轻存状态',
            description: '检查本机轻存引擎是否可用，不读取用户素材。',
            parameters: { type: 'object', additionalProperties: false, properties: {} },
            async execute() {
              context.assertInvocationCurrent();
              const response = await call({ action: 'ping' });
              return result({ available: true, version: response.version || '', eagleStorage: Boolean(response.eagleStorage) });
            },
          };
        },
      }, { name: 'qingcun_status' });

      api.registerTool({
        contextVersion: 2,
        create(context) {
          if (context.senderIsOwner !== true || context.sandboxed === true) return null;
          return {
            name: 'qingcun_download',
            label: '轻存下载并入库',
            description: '将 1 至 20 条视频链接交给本机轻存；已有素材只返回复核信息，不自动覆盖。',
            parameters: {
              type: 'object', additionalProperties: false, required: ['urls'],
              properties: {
                urls: { type: 'array', minItems: 1, maxItems: 20, items: { type: 'string' } },
                quality: { type: 'string', enum: ['best', '1080', '720'] },
              },
            },
            async execute(_toolCallId, params) {
              const urls = validateUrls(params.urls);
              const quality = params.quality || 'best';
              const options = { urls, quality, brandOverride: '', subtitles: { mode: 'none', language: '', automatic: true, size: 24 } };
              const check = await call({ action: 'checkDownloads', ...options });
              if (check.requiresConfirmation) {
                return result({ queued: false, requiresReview: true, review: check.review, message: '已有下载或正在进行的任务，请在轻存界面人工复核；本工具不会覆盖素材。' });
              }
              context.assertInvocationCurrent();
              const queued = await call({ action: 'download', ...options, snapshot: check.review?.snapshot || '' });
              if (queued.review) return result({ queued: false, requiresReview: true, review: queued.review });
              return result({ queued: Boolean(queued.count), count: queued.count || 0, message: '任务已交给轻存。完成后由本机 Eagle 入库；平台访问权限仍受网站控制。' });
            },
          };
        },
      }, { name: 'qingcun_download', optional: true });
    },
  };
}

export default createQingcunPlugin();

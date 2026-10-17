import test from 'node:test';
import assert from 'node:assert/strict';
import { createQingcunPlugin } from '../index.mjs';
import { validateUrls } from '../bridge.mjs';

function toolsFor(call, context) {
  const registrations = [];
  createQingcunPlugin(call).register({ registerTool(factory, metadata) { registrations.push([factory, metadata]); } });
  return Object.fromEntries(registrations.map(([factory, meta]) => [meta.name, factory.create(context)]));
}

test('no local tool is exposed to other senders or sandboxed download calls', () => {
  const call = async () => { throw Error('should not run'); };
  const stranger = toolsFor(call, { senderIsOwner: false, assertInvocationCurrent() {} });
  assert.equal(stranger.qingcun_status, null);
  assert.equal(stranger.qingcun_download, null);
  const sandbox = toolsFor(call, { senderIsOwner: true, sandboxed: true, assertInvocationCurrent() {} });
  assert.equal(sandbox.qingcun_download, null);
});

test('duplicate review never queues or replaces a video', async () => {
  const calls = [];
  const tools = toolsFor(async (request) => {
    calls.push(request);
    return { ok: true, requiresConfirmation: true, review: { items: [{ known: true }], snapshot: 'test' } };
  }, { senderIsOwner: true, sandboxed: false, assertInvocationCurrent() {} });
  const response = await tools.qingcun_download.execute('id', { urls: ['https://vimeo.com/1101784155'] });
  assert.equal(response.details.requiresReview, true);
  assert.deepEqual(calls.map(x => x.action), ['checkDownloads']);
});

test('submission uses the checked snapshot and never supplies overwrite choices', async () => {
  const calls = [];
  const tools = toolsFor(async (request) => {
    calls.push(request);
    if (request.action === 'checkDownloads') return { ok: true, requiresConfirmation: false, review: { snapshot: 'same' } };
    return { ok: true, count: 2 };
  }, { senderIsOwner: true, sandboxed: false, assertInvocationCurrent() {} });
  const response = await tools.qingcun_download.execute('id', { urls: ['https://vimeo.com/1101784155', 'https://www.youtube.com/watch?v=4dY3Q8hrYRE'] });
  assert.equal(response.details.count, 2);
  assert.equal(calls[1].snapshot, 'same');
  assert.equal('choices' in calls[1], false);
});

test('rejects non-platform and non-HTTPS URLs before invoking local engine', async () => {
  for (const url of ['http://vimeo.com/123', 'https://localhost/video', 'file:///secret.mp4']) {
    assert.throws(() => validateUrls([url]));
  }
});

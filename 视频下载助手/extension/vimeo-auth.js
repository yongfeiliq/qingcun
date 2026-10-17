
/* The browser worker supplies platform cookies to the local downloader. */
globalThis.QingcunVimeo=(()=>{
 const actions=new Set(['download','subtitle','refreshDescription','refreshSource']);
 function handles(q){return actions.has(q?.action);}
 function request(q,sourceUrl=''){
  const ticket=crypto.randomUUID();
  return new Promise((resolve,reject)=>chrome.runtime.sendMessage({type:'qingcun.nativeTask',request:q,sourceUrl,ticket,waitPermission:false},r=>{
   if(chrome.runtime.lastError)return reject(Error('下载后台尚未就绪，请关闭弹窗再打开一次。'));
   if(!r?.ok)return reject(Error(r?.error||'下载请求未完成，请稍后重试。'));resolve(r);
  }));
 }
 return {handles,request};
})();

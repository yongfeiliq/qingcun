/* Shared URL validation. Public post links only; no profile/board bulk downloads. */
globalThis.QingcunPlatforms={
labelParserVersion:5,
normalize(raw){try{
const u=new URL(raw.trim());if(u.protocol!=='https:'||u.username||u.password||(u.port&&u.port!=='443')||raw.length>4000)return null;
const h=u.hostname.toLowerCase(),p=u.pathname;let m,id;
if(['youtube.com','www.youtube.com','m.youtube.com','music.youtube.com','www.youtube-nocookie.com','youtu.be'].includes(h)){
id=h==='youtu.be'?p.slice(1).replace(/\/$/,''):p==='/watch'?u.searchParams.get('v'):/^\/(?:shorts|live|embed)\/([\w-]{11})\/?$/.exec(p)?.[1];
return /^[\w-]{11}$/.test(id||'')?'https://www.youtube.com/watch?v='+id:null;}
if(/(^|\.)instagram\.com$/.test(h)&&(m=/^\/(?:[\w.]+\/)?(?:reels?|p|tv)\/([\w-]+)\/?$/.exec(p)))return 'https://www.instagram.com/p/'+m[1]+'/';
if(/^(?:[a-z]{2,3}\.)?pinterest\.(?:com|ca|co\.uk|com\.au|fr|de|jp|es|it|ch|co\.kr|com\.mx|tw)$/.test(h)&&(m=/^\/pin\/(?:[\w-]+--)?(\d+)\/?$/.exec(p)))return 'https://www.pinterest.com/pin/'+m[1]+'/';
if(['www.xinpianchang.com','xinpianchang.com'].includes(h)&&(m=/^\/a([1-9]\d{3,15})\/?$/.exec(p)))return 'https://www.xinpianchang.com/a'+m[1];
if(/(^|\.)behance\.net$/.test(h)&&(m=/^\/gallery\/(\d+)(\/[^/]+)?\/?$/.exec(p)))return 'https://www.behance.net/gallery/'+m[1]+(m[2]||'');
if(['x.com','www.x.com','twitter.com','www.twitter.com','mobile.twitter.com','m.twitter.com'].includes(h)&&(m=/^\/([A-Za-z0-9_]{1,30})\/status\/([1-9]\d{5,24})\/?$/.exec(p)))return 'https://x.com/'+m[1]+'/status/'+m[2];
if(['bilibili.com','www.bilibili.com','m.bilibili.com'].includes(h)&&(m=/^\/video\/((?:[bB][vV])[A-Za-z0-9]{10}|(?:[aA][vV])[1-9]\d*)\/?$/.exec(p))){const part=u.searchParams.get('p')||'1';if(!/^[1-9]\d{0,3}$/.test(part))return null;id=m[1].slice(0,2).toLowerCase()==='bv'?'BV'+m[1].slice(2):'av'+m[1].slice(2);return 'https://www.bilibili.com/video/'+id+'/'+(part!=='1'?'?p='+part:'');}
if(['douyin.com','www.douyin.com','m.douyin.com','www.iesdouyin.com','iesdouyin.com'].includes(h)){id=/^\/(?:video|share\/video)\/([1-9]\d{5,23})\/?$/.exec(p)?.[1]||u.searchParams.get('modal_id');if(/^[1-9]\d{5,23}$/.test(id||''))return 'https://www.douyin.com/video/'+id;}
if(['tiktok.com','www.tiktok.com','m.tiktok.com'].includes(h)){m=/^\/@([\w.-]{1,100})\/video\/([1-9]\d{5,23})\/?$/.exec(p);if(m)return 'https://www.tiktok.com/@'+m[1]+'/video/'+m[2];id=/^\/(?:share\/video|embed(?:\/v2)?)\/([1-9]\d{5,23})\/?$/.exec(p)?.[1];if(id)return 'https://www.tiktok.com/share/video/'+id;}
if(['vimeo.com','www.vimeo.com'].includes(h)&&(m=/^\/(?:channels\/[\w-]+\/|groups\/[\w-]+\/videos\/|(?:album|showcase)\/\d+\/video\/)(\d+)\/?$/.exec(p)))return 'https://vimeo.com/'+m[1];
if(['vimeo.com','www.vimeo.com','player.vimeo.com'].includes(h)&&(m=/^\/(?:video\/)?(\d+)(?:\/([a-f0-9]+))?\/?$/.exec(p))){const hash=m[2]||u.searchParams.get('h');return 'https://vimeo.com/'+m[1]+(/^[a-f0-9]+$/.test(hash||'')?'/'+hash:'');}
if(/(^|\.)(behance\.net|pinimg\.com|cdninstagram\.com|fbcdn\.net)$/.test(h)&&/\.(mp4|m3u8)$/i.test(p))return u.origin+p+u.search;
return null;}catch{return null;}},
site(raw){try{const h=new URL(raw).hostname.toLowerCase();if(['x.com','www.x.com','twitter.com','www.twitter.com','mobile.twitter.com','m.twitter.com'].includes(h))return 'X';if(['bilibili.com','www.bilibili.com','m.bilibili.com','b23.tv'].includes(h))return 'Bilibili（B站）';if(['douyin.com','www.douyin.com','m.douyin.com','v.douyin.com','www.iesdouyin.com','iesdouyin.com'].includes(h))return '抖音';if(['tiktok.com','www.tiktok.com','m.tiktok.com','vm.tiktok.com','vt.tiktok.com'].includes(h))return 'TikTok';if(['vimeo.com','www.vimeo.com','player.vimeo.com'].includes(h))return 'Vimeo';if(['www.xinpianchang.com','xinpianchang.com'].includes(h))return '新片场';return '';}catch{return '';}},
canRead(raw){try{return /(^|\.)(vimeo\.com|behance\.net|instagram\.com|pinterest\.[a-z.]+|douyin\.com|tiktok\.com|xinpianchang\.com)$/.test(new URL(raw).hostname);}catch{return false;}},
async within(promise,ms,message){
 let timer;try{return await Promise.race([promise,new Promise((_,reject)=>{timer=setTimeout(()=>{const error=Error(message);error.code='PAGE_READ_TIMEOUT';reject(error);},ms);})]);}finally{clearTimeout(timer);}
},
async read(chrome,tabId){
 const p=globalThis.QingcunPlatforms;
 const results=await p.within(chrome.scripting.executeScript({target:{tabId},injectImmediately:true,func:p.capture}),8000,'作品页读取超时，尚未取得页面资料。请确认作品已打开后重试。');
 const page=results.find(r=>r.frameId===0)?.result||results[0]?.result;
 if(!page||!/^https:\/\/www\.behance\.net\/gallery\//.test(p.normalize(page.pageUrl)||''))return page;
 // Nested wrappers can hide the player from the top document. Collect only
 // links from frames actually embedded by this work; metadata remains top-level.
 try{
  const rows=await p.within(chrome.scripting.executeScript({target:{tabId,allFrames:true},injectImmediately:true,func:p.frameLinks}),4000,'内嵌播放器读取超时。');
  const key=raw=>{try{const u=new URL(raw);return u.protocol==='https:'&&!u.username&&!u.password?(u.hash='',u.href):'';}catch{return '';}};
  const reachable=new Set((page.frameUrls||[]).map(key).filter(Boolean));
  const links=new Set(page.urls||[]),pending=rows.filter(r=>r.frameId!==0&&r.result);
  for(let depth=0;depth<8;depth++){
   let changed=false;
   for(let i=pending.length-1;i>=0;i--){const f=pending[i].result;
    if(!reachable.has(key(f.pageUrl)))continue;
    for(const u of [f.pageUrl,...(f.urls||[])]){const n=p.normalize(u);if(n&&(/^(?:https:\/\/vimeo\.com\/|https:\/\/www\.youtube\.com\/watch\?v=)/.test(n)||/^https:\/\/[^/]*behance\.net\/.*\.(mp4|m3u8)(\?|$)/i.test(n)))links.add(n);}
    for(const u of f.frameUrls||[]){const k=key(u);if(k)reachable.add(k);}
    pending.splice(i,1);changed=true;
   }
   if(!changed)break;
  }
  page.urls=[...links].slice(0,80);
 }catch(e){page.frameReadIncomplete=true;page.frameReadReason=e.code==='PAGE_READ_TIMEOUT'?'timeout':'unavailable';}
 // Always recheck after a timeout/permission error too. Late script results
 // are discarded; they cannot overwrite the result for a newly selected work.
 const current=await p.within(chrome.tabs.get(tabId),4000,'未能核对当前作品页，请重试。');
 if(p.normalize(current.url)!==p.normalize(page.pageUrl))throw Error('作品页面已跳转，请在目标作品页重新读取。');
 return page;
},
frameLinks:function(){
 const urls=[],frameUrls=[],add=(list,raw)=>{try{const u=new URL(raw,location.href);if(raw&&u.protocol==='https:'&&!u.username&&!u.password)list.push(u.href);}catch{}};
 for(const frame of document.querySelectorAll('iframe'))for(const raw of [frame.src,frame.dataset?.src,frame.getAttribute('data-lazy-src')])add(frameUrls,raw);
 for(const v of document.querySelectorAll('video'))for(const raw of [v.currentSrc,v.src,v.getAttribute('data-src')])add(urls,raw);
 for(const s of document.querySelectorAll('video source'))for(const raw of [s.src,s.getAttribute('data-src')])add(urls,raw);
 return {pageUrl:location.href,frameUrls:[...new Set(frameUrls)].slice(0,100),urls:[...new Set(urls)].slice(0,80)};
},
inputHint(raw){const site=QingcunPlatforms.site(raw);return site?'这是 '+site+' 的首页、个人页或短链接。请在浏览器打开单条视频，再点“下载当前视频”；B站请打开 BV / AV 视频页。':'请粘贴支持平台的完整视频链接。短链接请先在浏览器打开到视频详情。';},
pageMedia(raw,page){try{const u=new URL(raw),p=new URL(page);if(u.protocol!=='https:'||u.username||u.password||(u.port&&u.port!=='443')||raw.length>8000)return null;const h=u.hostname.toLowerCase(),isVideo=/\/(?:video|aweme)\//.test(u.pathname)||/\.(mp4|m3u8)$/i.test(u.pathname);if(!isVideo)return null;if(/^(www\.)?xinpianchang\.com$/.test(p.hostname)&&/(^|\.)(xpccdn\.com|xinpianchang\.com)$/.test(h)&&/\.(mp4|m3u8)$/i.test(u.pathname))return u.href;if(p.hostname==='www.douyin.com'&&/(^|\.)(douyinvod\.com|douyin\.com|douyinpic\.com|bytecdn\.cn)$/.test(h))return u.href;if(p.hostname==='www.tiktok.com'&&/(^|\.)(tiktok\.com|tiktokcdn\.com|tiktokcdn-us\.com|byteoversea\.com)$/.test(h))return u.href;return null;}catch{return null;}},
capture:function capturePageVideos(){
// Read JSON as data, never execute page scripts. Limit traversal to avoid huge feeds.
const jsonObjects=[];
for(const script of document.querySelectorAll('script[type="application/json"],script[type="application/ld+json"]')){
 const raw=script.textContent||'';if(raw.length>10000000)continue;
 try{const stack=[JSON.parse(raw)];let count=0;while(stack.length&&count++<50000){const v=stack.pop();if(!v||typeof v!=='object')continue;if(!Array.isArray(v))jsonObjects.push(v);for(const x of Object.values(v))if(x&&typeof x==='object')stack.push(x);}}catch{}
}
const labelValues=v=>{if(typeof v==='string'){const s=v.trim();if(s.startsWith('[')&&s.endsWith(']')){try{const parsed=JSON.parse(s);if(Array.isArray(parsed))return labelValues(parsed);}catch{}return s.slice(1,-1).split(',');}return v.split(',');}return Array.isArray(v)?v.flatMap(x=>typeof x==='string'?[x]:x&&typeof x.name==='string'?[x.name]:[]):[];};
const labelField=v=>typeof v==='string'||Array.isArray(v)&&v.every(x=>typeof x==='string'||x&&typeof x.name==='string');

if(/^(www\.)?vimeo\.com$/.test(location.hostname)){
 const match=/^\/(?:channels\/[^/]+\/|groups\/[^/]+\/videos\/|(?:album|showcase)\/\d+\/video\/)?(\d+)(?:\/([a-f0-9]+))?\/?$/.exec(location.pathname);
 if(!match)throw Error('请打开 Vimeo 单条视频页面。');
 const pageUrl='https://vimeo.com/'+match[1]+(match[2]?'/'+match[2]:''),clean=s=>String(s||'').replace(/^\s*#\s*/,'').replace(/\s+/g,' ').trim(),unique=a=>[...new Set(a.map(clean).filter(s=>s&&s.length<=240))];
 const declared=document.querySelector('meta[property="og:url"]')?.content;
 if(declared){let declaredId;try{declaredId=/^\/(\d+)(?:\/|$)/.exec(new URL(declared).pathname)?.[1];}catch{}if(declaredId!==match[1])throw Error('页面资料与视频编号不一致，请刷新作品页。');}
 const tags=[...document.querySelectorAll('meta[property="video.other:tag"],meta[property="video:tag"]')].map(n=>n.content),categories=[];
 let tagStatus='unavailable',categoryStatus='unavailable',labelOrigin='vimeo-dom';
 const videoId=raw=>{try{const u=new URL(raw,location.href);return /^(?:www\.|player\.)?vimeo\.com$/.test(u.hostname)?/^\/(?:video\/)?(\d+)(?:\/|$)/.exec(u.pathname)?.[1]:null;}catch{return null;}};
 for(const v of jsonObjects){
  const identified=[v.url,v['@id'],v.embedUrl].some(u=>typeof u==='string'&&videoId(u)===match[1])||String(v.id||v.video_id||'')===match[1]&&typeof (v.title||v.name)==='string';
  if(!identified)continue;
  for(const key of ['tags','keywords'])if(labelField(v[key])){tags.push(...labelValues(v[key]));tagStatus='read';labelOrigin='vimeo-structured';}
  if(labelField(v.categories)){categories.push(...labelValues(v.categories));categoryStatus='read';}
 }
 for(const node of document.querySelectorAll('a[href]')){
  if(node.closest('nav,header,footer,aside,[data-testid*="recommend"],[class*="recommend"],[class*="related"]')&&!node.closest('[data-testid="categories-nav"]'))continue;
  let u;try{u=new URL(node.href,location.href);}catch{continue;}
  if(!/^(www\.)?vimeo\.com$/.test(u.hostname))continue;
  const originalTag=/^\/tags?:(.+)$/.exec(u.pathname);
  if(originalTag){try{tags.push(decodeURIComponent(originalTag[1]));}catch{}continue;}
  if(/^\/(?:tag|tags)\//.test(u.pathname)||(/^\s*#/.test(node.textContent)&&/^\/search(?:\/|$)/.test(u.pathname)))tags.push(node.textContent);
  if(/^\/categories\/[^/]+/.test(u.pathname)){
   const originalCategories={'adsandcommercials':'Ads and Commercials','adsandcommercials/30secondspot':'30 Second Spot','animation':'Animation','animation/3d':'3D/CG','animation/vfx':'VFX','trailers':'Trailers'};
   const categoryKey=u.pathname.replace(/^\/categories\//,'').replace(/\/videos\/?$/,'').replace(/\/$/,'');
   categories.push(originalCategories[categoryKey]||node.textContent);
  }
 }
 const desc=document.querySelector('[data-testid="video-description"],[itemprop="description"]'),og=document.querySelector('meta[property="og:description"]');
 const description=(desc?.innerText||desc?.textContent||og?.content||'').trim();
 const cleanTags=unique(tags),cleanCategories=unique(categories);
 return {pageUrl,title:(document.querySelector('meta[property="og:title"]')?.content||document.title).slice(0,250),description:description.slice(0,12000),descriptionExcerpt:!desc,descriptionTruncated:description.length>12000,tags:cleanTags,categories:cleanCategories,labelReadStatus:cleanTags.length?'read':tagStatus,categoryReadStatus:cleanCategories.length?'read':categoryStatus,labelOrigin,urls:[pageUrl]};
}

if(/^(www\.)?instagram\.com$/.test(location.hostname)){
 const id=/^\/(?:[\w.]+\/)?(?:p|reels?|tv)\/([\w-]+)\/?$/.exec(location.pathname)?.[1];if(!id)throw Error('请打开 Instagram 单条帖子。');
 const pageUrl='https://www.instagram.com/p/'+id+'/',code=raw=>{try{return /^\/(?:[\w.]+\/)?(?:p|reels?|tv)\/([\w-]+)\/?$/.exec(new URL(raw,location.href).pathname)?.[1];}catch{return '';}};
 const declared=document.querySelector('meta[property="og:url"]')?.content;if(declared&&code(declared)!==id)throw Error('页面资料与 Instagram 帖子不一致，请刷新后重试。');
 let description='',read=false,labelOrigin='instagram-caption',name='',account='';
 for(const v of jsonObjects){if((v.shortcode||v.code)!==id)continue;
  const caption=typeof v.caption==='string'?v.caption:v.caption?.text??v.edge_media_to_caption?.edges?.[0]?.node?.text;
  if(typeof caption==='string'){description=caption;read=true;labelOrigin='instagram-structured';account=v.owner?.username||v.user?.username||'';name=v.owner?.full_name||v.user?.full_name||account;break;}
 }
 const og=document.querySelector('meta[property="og:description"]')?.content||'',ogTitle=document.querySelector('meta[property="og:title"]')?.content||document.title;
 // Modern Instagram renders captions as spans, not h1. Match the post's own
 // social preview to a complete DOM caption; never collect all article/comments text.
 const quoted=[og,ogTitle].map(s=>/[:：]\s*["“]([\s\S]*)["”]\.?\s*$/.exec(s)?.[1]||'').find(Boolean)||'';
 const normalized=s=>s.replace(/\s+/g,' ').trim();
 if(!read&&quoted){const seed=normalized(quoted).replace(/(?:…|\.{3})\s*$/,'');
  const candidates=[...document.querySelectorAll('h1,span')].filter(n=>!n.closest('aside,nav,header,[role="dialog"] [role="listbox"]')).map(n=>({node:n,text:(n.innerText||n.textContent||'').trim()})).filter(x=>normalized(x.text)===normalized(quoted)||seed.length>=30&&normalized(x.text).startsWith(seed));
  candidates.sort((a,b)=>a.text.length-b.text.length);
  const found=candidates.find(x=>!/(?:…|\.{3})\s*$/.test(x.text)&&!x.node.querySelector('button'));
  if(found){description=found.text;read=true;}
 }
 if(!read)description=quoted||og;
 const tags=read?[...new Set([...description.matchAll(/(?:^|[^\p{L}\p{N}_])#([\p{L}\p{M}\p{N}_]+)/gu)].map(m=>m[1]))]:[];
 const truncated=description.length>60000;
 return {pageUrl,title:ogTitle.slice(0,250),description:description.slice(0,60000),descriptionExcerpt:!read,descriptionTruncated:truncated,tags,categories:[],labelReadStatus:read&&!truncated?'read':'unavailable',categoryReadStatus:'unavailable',labelOrigin,name,account,profileUrl:account?'https://www.instagram.com/'+account+'/':'',urls:[pageUrl]};
}

if(/^(www\.)?xinpianchang\.com$/.test(location.hostname)){
 const id=/^\/a([1-9]\d+)\/?$/.exec(location.pathname)?.[1];if(!id)throw Error('请打开新片场单条作品页。');
 const pageUrl='https://www.xinpianchang.com/a'+id,clean=s=>String(s||'').replace(/\u0000/g,'').replace(/\s+/g,' ').trim(),unique=a=>[...new Set(a.map(clean).filter(s=>s&&s.length<=240))].slice(0,1000);
 const names=v=>Array.isArray(v)?v.flatMap(x=>typeof x==='string'?[x]:x&&typeof x==='object'?[x.name||x.category_name||x.categoryName||x.tag_name||x.tagName||x.label||'',...names(x.children||x.child||x.subcategories||x.sub||[])]:[]):typeof v==='string'?v.split(','):v&&typeof v==='object'?names([v]):[];
 let detail={};try{detail=JSON.parse(document.getElementById('__NEXT_DATA__')?.textContent||'{}').props?.pageProps?.detail||{};}catch{}
 if(detail.id&&String(detail.id).replace(/^a/,'')!==id)throw Error('页面资料与作品编号不一致，请刷新后再下载。');
 const video=detail.video||{},root=document.querySelector('main')||document;
 const plain=html=>{const doc=new DOMParser().parseFromString(String(html||'').replace(/<br\s*\/?>|<\/p>/gi,'\n'),'text/html');doc.querySelectorAll('script,style').forEach(n=>n.remove());return doc.body.textContent.trim();};
 const localTags=[...root.querySelectorAll('[class*="tag"] a,[class*="Tag"] a,a[href*="/tag/"]')].map(n=>n.textContent);
 const categories=[...root.querySelectorAll('[class*="categor"] a,[class*="Categor"] a,a[href*="categoryId="]')].map(n=>n.textContent);
 const descriptionNode=root.querySelector('[class*="description"],[class*="Description"],.desc');
 const description=plain(detail.description||detail.content||descriptionNode?.innerText||descriptionNode?.textContent||'').replace(/(?:展开全文|收起全文)\s*$/,'').trim();
 let tags=unique([...names(detail.tags||detail.keywords),...localTags]),cats=unique([...names(detail.categories||detail.category),...categories]);
 const userinfo=detail.author?.userinfo||{},authorId=String(userinfo.id||'');
 let name=/^[1-9]\d{0,19}$/.test(authorId)?clean(userinfo.username):'',account=name?authorId:'',profileUrl=name?'https://www.xinpianchang.com/u'+authorId:'';
 const author=root.querySelector('a[rel="author"][href], [class*="author"] a[href*="/u"],[class*="Author"] a[href*="/u"],[class*="creator"] a[href*="/u"]');
 if(!name&&author){try{const u=new URL(author.href);if(/^(www\.)?xinpianchang\.com$/.test(u.hostname)&&/^\/u\d+\/?$/.test(u.pathname)){profileUrl=u.origin+u.pathname;account=u.pathname.replace(/^\/u/,'').replace(/\/$/,'');name=clean(author.textContent);}}catch{}}
 const allowed=raw=>{try{const u=new URL(raw,location.href);return u.protocol==='https:'&&!u.username&&!u.password&&(!u.port||u.port==='443')&&/(^|\.)(xpccdn\.com|xinpianchang\.com)$/.test(u.hostname)&&/\.(mp4|m3u8)$/i.test(u.pathname)?u.href:'';}catch{return '';}};
 const players=[...document.querySelectorAll('video')].filter(v=>v.getBoundingClientRect().width>0);
 const mediaUrl=allowed(players[0]?.currentSrc||players[0]?.src||players[0]?.querySelector('source')?.src||'');
 return {pageUrl,title:(detail.title||root.querySelector('h1')?.textContent||document.querySelector('meta[property="og:title"]')?.content||document.title).slice(0,300),description:description.slice(0,60000),descriptionExcerpt:!detail.description&&!detail.content&&!!descriptionNode&&descriptionNode.scrollHeight>descriptionNode.clientHeight+8,descriptionTruncated:description.length>60000,tags,categories:cats,name,account,profileUrl,mediaUrl,urls:[pageUrl],xpc:{vid:String(video.vid||''),appKey:String(video.appKey||'')}};
}

const urls=new Set(),frameUrls=new Set(),add=raw=>{if(typeof raw!=='string'||!raw.trim())return;try{const u=new URL(raw,location.href);if(u.protocol==='https:'&&!u.username&&!u.password)urls.add(u.href);}catch{}};
const isBehance=/(^|\.)behance\.net$/.test(location.hostname);
const scope=isBehance?document:(document.querySelector('[role="dialog"] article,main article,article')||document);
for(const v of scope.querySelectorAll('video'))for(const raw of [v.currentSrc,v.src,v.getAttribute?.('data-src'),v.querySelector('source')?.src])add(raw);
if(isBehance){
 for(const frame of document.querySelectorAll('iframe'))for(const raw of [frame.src,frame.dataset?.src,frame.getAttribute?.('data-lazy-src')]){add(raw);try{const u=new URL(raw,location.href);if(raw&&u.protocol==='https:'&&!u.username&&!u.password)frameUrls.add(u.href);}catch{}}
 for(const source of document.querySelectorAll('video source')){add(source.src);add(source.getAttribute?.('data-src'));}
 // Lazy project modules may contain escaped embeds before an iframe is mounted.
 for(const script of document.querySelectorAll('script[type="application/json"],script[type="application/ld+json"]')){
  if(script.textContent.length>10000000)continue;
  const text=script.textContent.replace(/\\u002[fF]/g,'/').replace(/\\\//g,'/').replace(/&amp;/g,'&');
  for(const m of text.matchAll(/https:\/\/(?:player\.vimeo\.com\/video\/|(?:www\.)?youtube(?:-nocookie)?\.com\/embed\/)[^"'<>\s\\]+/g))add(m[0]);
  for(const m of text.matchAll(/https:\/\/[^"'<>\s\\]*\.behance\.net\/[^"'<>\s\\]*\.(?:mp4|m3u8)(?:\?[^"'<>\s\\]*)?/g))add(m[0]);
 }
}
for(const meta of scope.querySelectorAll('meta[property="og:video"],meta[property="og:video:secure_url"]'))add(meta.content);
const shortVideo=/^(?:www\.)?(?:douyin|tiktok)\.com$/.test(location.hostname);
let mediaUrl='',playerScope=null;
if(shortVideo){
 const candidates=[...document.querySelectorAll('video')].filter(v=>{const r=v.getBoundingClientRect();return r.width>0&&r.height>0&&r.bottom>0&&r.top<innerHeight&&r.right>0&&r.left<innerWidth;});
 candidates.sort((a,b)=>(Number(!b.paused)-Number(!a.paused))||(b.getBoundingClientRect().width*b.getBoundingClientRect().height-a.getBoundingClientRect().width*a.getBoundingClientRect().height));
 const v=candidates[0];if(v){mediaUrl=v.currentSrc||v.src||v.querySelector('source')?.src||'';playerScope=v.closest('article,[data-e2e="recommend-list-item-container"],[data-e2e="feed-active-video"]');}
 // Only the visible player is used; offscreen feed items are not collected.
 urls.clear();
}

let name='',account='',profileUrl='';
const author=(shortVideo?playerScope:document)?.querySelector('a[href^="/user/"],a[href*="douyin.com/user/"],a[href^="/@"],a[rel="author"],a[class*="Owner"][href],a[class*="owner"][href]');
if(author){try{const p=new URL(author.href);if(p.hostname===location.hostname&&/^\/(?:user\/)?[^/]+\/?$/.test(p.pathname)){profileUrl=p.href;name=author.textContent.trim().slice(0,120);account=p.pathname.split('/').filter(Boolean).pop().replace(/^@/,'');}}catch{}}
if(shortVideo&&!profileUrl&&location.hostname.endsWith('tiktok.com')){const m=/^\/@([\w.-]+)\/video\//.exec(location.pathname);if(m){account=m[1];profileUrl='https://www.tiktok.com/@'+account;}}
let description='',descriptionExcerpt=false,descriptionTruncated=false;
const visible=node=>{const s=getComputedStyle(node);return s.display!=='none'&&s.visibility!=='hidden';};
const textOf=node=>(node?.innerText||node?.textContent||'').replace(/\r\n?/g,'\n').trim();
let selectors=[];
if(/(^|\.)youtube\.com$/.test(location.hostname))selectors=['#description.ytd-watch-metadata #expanded yt-attributed-string','#description-inline-expander[is-expanded] #attributed-description'];
else if(/(^|\.)bilibili\.com$/.test(location.hostname))selectors=['.video-desc-container .basic-desc-info','.video-desc-container .desc-info-text'];
else if(/(^|\.)instagram\.com$/.test(location.hostname))selectors=['h1'];
else if(/(^|\.)pinterest\./.test(location.hostname))selectors=['[data-test-id="truncated-description"]','[data-test-id="pin-description"]'];
else if(/(^|\.)tiktok\.com$/.test(location.hostname))selectors=['[data-e2e="video-desc"]','[data-e2e="browse-video-desc"]'];
else if(/(^|\.)douyin\.com$/.test(location.hostname))selectors=['[data-e2e="video-desc"]','[data-e2e="video-description"]'];
else if(/(^|\.)vimeo\.com$/.test(location.hostname))selectors=['[data-testid="video-description"]','[itemprop="description"]'];
const descriptionScope=shortVideo?playerScope:scope;
for(const selector of selectors){const node=descriptionScope?.querySelector(selector);if(node&&visible(node)&&textOf(node)){description=textOf(node);descriptionExcerpt=/truncated/.test(selector);break;}}
if(isBehance){const nodes=[...document.querySelectorAll('.project-module-text,[data-module="text"]')].filter(n=>visible(n));description=nodes.filter(n=>!nodes.some(p=>p!==n&&p.contains(n))).map(textOf).filter(Boolean).join('\n\n');}
if(!description){
 // Social-preview descriptions may be abbreviated; never advertise them as complete captions.
 const og=document.querySelector('meta[property="og:description"],meta[name="description"]');
 const declared=document.querySelector('meta[property="og:url"],link[rel="canonical"]');
 let matches=!shortVideo;
 try{const u=new URL(declared?.content||declared?.href||'');matches=matches||u.pathname===location.pathname&&/\/video\/\d+/.test(u.pathname);}catch{}
 if(og&&matches){description=og.content||'';descriptionExcerpt=true;}
}
descriptionTruncated=description.length>12000;description=description.slice(0,12000);
const localLabelSelector=/(^|\.)bilibili\.com$/.test(location.hostname)?'.video-tag-container a,.tag-panel a':/(^|\.)youtube\.com$/.test(location.hostname)?'#super-title a[href*="/hashtag/"]':'';
const localLabels=localLabelSelector?[...document.querySelectorAll(localLabelSelector)].map(n=>n.textContent.replace(/^\s*#\s*/,'')):[];
const tags=[...new Set([...localLabels,...(document.querySelector('meta[name="keywords"]')?.content||'').split(','),...(isBehance?[...document.querySelectorAll('a[href*="/search/projects?"]')].map(n=>n.textContent.trim()):[])].map(s=>s.trim()).filter(s=>s&&s.length<=240))].slice(0,1000);
return {tags,description,descriptionExcerpt,descriptionTruncated,pageUrl:location.href,title:(document.querySelector('meta[property="og:title"]')?.content||document.title).slice(0,250),name,account,profileUrl,mediaUrl,frameUrls:[...frameUrls],urls:[...urls].slice(0,80)};
}};

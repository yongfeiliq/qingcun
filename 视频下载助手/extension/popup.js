(() => {
'use strict';
if (!globalThis.QingcunContext?.available) return;
'use strict';
const HOST = 'com.qingcun.video_downloader';
const $ = id => document.getElementById(id);
let currentPage = "", currentUrl = null, currentTabId=null, connected = false, refreshing = false, sending = false, pendingDownload = null;
const videoUrl=QingcunPlatforms.normalize;
let downloadContexts=[];
async function native(message) {
  if(QingcunVimeo.handles(message))return QingcunVimeo.request(message);
  return new Promise((resolve, reject) => chrome.runtime.sendNativeMessage(HOST, message, response => {
    const error = chrome.runtime.lastError;
    if (error) return reject(new Error('未连接到本地组件。请先运行轻存安装器，然后重新打开扩展。'));
    if (!response?.ok) return reject(new Error(response?.error || '下载组件没有返回有效结果。'));
    resolve(response);
  }));
}
function message(text, error = false) { $('message').textContent = text; $('message').classList.toggle('error', error); }
function buttons() {
  $('downloadCurrent').disabled = !connected || !currentUrl || sending || !!pendingDownload;
  $('downloadBatch').disabled = !connected || sending || !!pendingDownload;
  $('quality').disabled = sending || !!pendingDownload || $('subtitleMode').value==='only';
  for(const id of ['downloadBrand','subtitleMode','subtitleLanguage','subtitleAutomatic','subtitleSize'])$(id).disabled=sending||!!pendingDownload;
  $('downloadCurrent').textContent=$('subtitleMode').value==='only'?'↓ 下载当前视频的字幕':'↓ 下载当前视频';
  $('readPage').disabled = !connected || !currentUrl || !QingcunPlatforms.canRead(currentPage) || sending || !!pendingDownload;
  $('captureDescription').disabled = !connected || !currentUrl || sending || !!pendingDownload;
  $('openFolder').disabled = !connected;
  $('chooseFolder').disabled = !connected;
}
function renderJobs(jobs) {
  const target = $('jobs'); target.replaceChildren();
  if (!jobs.length) { const p = document.createElement('p'); p.className = 'empty'; p.textContent = '还没有下载任务，先保存一个喜欢的视频吧。'; target.append(p); return; }
  for (const job of jobs.slice(0, 6)) {
    const row = document.createElement('div'); row.className = 'job';
    const top = document.createElement('div'); top.className = 'job-top';
    const title = document.createElement('span'); title.className = 'job-title'; title.textContent = job.title || `${job.total} 个视频`; title.title = title.textContent;
    const state = document.createElement('span'); state.className = 'job-state';
    const labels = {queued:'等待开始',running:'下载中',completed:'已完成',partial:'部分失败',failed:'下载失败'};
    state.textContent = labels[job.status] || job.status;
    state.classList.toggle('failed', ['failed','partial'].includes(job.status)); top.append(title, state);
    const detail = document.createElement('div'); detail.className = 'job-detail'; detail.textContent = /连接当前 Vimeo 登录|连接或重新连接当前 Vimeo 登录/.test(job.detail||'')?'旧任务下载失败。当前版本已改为后台处理，可重新添加此视频。':job.detail || `${job.completed || 0} / ${job.total} 已保存`; detail.title = detail.textContent;
    row.append(top, detail);
    if (['queued','running'].includes(job.status)) {
      const track = document.createElement('div'); track.className = 'track'; const fill = document.createElement('div'); fill.className = 'fill';
      fill.style.width = `${Math.max(2, Math.min(100, Number(job.percent) || 0))}%`; track.append(fill); row.append(track);
    }
    target.append(row);
  }
}
async function refresh() {
  if (refreshing) return; refreshing = true;
  try {
    const result = await native({action:'list'}); connected = true;
    $('connection').className = 'connection ready'; $('connection').lastElementChild.textContent = result.storageMode==='eagle'?'本地组件已连接 · 视频入库 Eagle 后保留一份':'本地组件已连接 · 视频直接保存到电脑';
    document.querySelector('.folder .eyebrow').textContent=result.storageMode==='eagle'?'下载暂存位置 · 入库后清理':'保存位置';
    $('folderPath').textContent = result.folder; renderJobs(result.jobs || []);
  } catch (e) { connected = false; $('connection').className = 'connection error'; $('connection').lastElementChild.textContent = '请先安装本地下载组件'; }
  finally { refreshing = false; buttons(); }
}
function showDuplicateReview(result,urls,quality) {
  pendingDownload={urls,quality,review:result.review};
  const rows=result.review.items.filter(r=>r.known||r.active),box=$('duplicateList');box.replaceChildren();
  $('duplicateTitle').textContent=rows.length>1?'发现 '+rows.length+' 个重复视频':rows.length?'你已经下载过这个视频':'下载前请确认';
  const fresh=result.review.items.filter(r=>!r.known&&!r.active).length;
  $('duplicateHint').textContent=(result.changed?'下载记录已变化，请重新确认。':'请选择如何处理已有视频。')+(fresh?'另有 '+fresh+' 个新视频将正常下载。':'');
  for(const item of rows){
    const card=document.createElement('section');card.className='duplicate-card';card.dataset.url=item.url;
    const title=document.createElement('h3');title.textContent=item.title;card.append(title);
    const state=document.createElement('p');state.textContent=item.active?'已有任务正在下载，此次跳过。':item.collection?'此作品已有多个视频，可跳过或另存整组。':item.missing?'以前下载过，但原位置已找不到视频。':item.analyzing?'视频正在分析，可另存一份或稍后替换。':'素材库中已有 '+item.files.filter(f=>f.exists).length+' 个文件。';card.append(state);
    const label=document.createElement('label');label.textContent='本次操作';
    const select=document.createElement('select');select.className='duplicate-choice';select.setAttribute('aria-label',item.title+'的下载选择');
    function option(value,text){const o=document.createElement('option');o.value=value;o.textContent=text;select.append(o);}
    option('skip','跳过 · 保留现有文件');
    const targets=item.files.filter(f=>f.canReplace);
    if(!item.active){if(targets.length)option('replace','替换选中的旧文件');option('copy',item.missing?'重新下载':'另存一份 · 两份都保留');}
    select.disabled=item.active;label.append(select);card.append(label);
    const targetLabel=document.createElement('label');targetLabel.textContent='选择要替换的文件';targetLabel.hidden=true;
    const target=document.createElement('select');target.className='duplicate-target';target.setAttribute('aria-label','选择要替换的文件');
    for(const file of targets){const o=document.createElement('option');o.value=file.path;o.textContent=(file.height?file.height+'p · ':'')+file.path.split(/[\\/]/).pop();o.title=file.path;target.append(o);}
    targetLabel.append(target);card.append(targetLabel);
    const path=document.createElement('p');path.className='duplicate-path';path.textContent=(item.files.find(f=>f.exists)||{}).path||'';card.append(path);
    select.addEventListener('change',()=>{targetLabel.hidden=select.value!=='replace';if(select.value==='replace')path.textContent=target.value;});
    target.addEventListener('change',()=>{path.textContent=target.value;});
    box.append(card);
  }
  if(!$('duplicateDialog').open)$('duplicateDialog').showModal();
  $('cancelDownload').focus();buttons();message('等待你的选择；尚未创建下载任务。');
}
async function submitDownload(urls,quality,extra={}) {
  const captureTabId=urls.length===1&&urls[0]===currentUrl&&/^https:\/\/(?:vimeo\.com\/|www\.instagram\.com\/p\/)/.test(currentUrl)?currentTabId:null;
  const result=await native({action:$('subtitleMode').value==='only'?'subtitle':'download',urls,quality,subtitles:subtitleOptions(),brandOverride:$('downloadBrand').value.trim(),contexts:downloadContexts,captureTabId,...extra});
  if(result.review){showDuplicateReview(result,urls,quality);return;}
  pendingDownload=null;if(result.count>0)$('downloadBrand').value='';if($('duplicateDialog').open)$('duplicateDialog').close();
  message(result.count===0?'已跳过这些视频，没有创建新任务。':'已添加 '+result.count+' 个'+($('subtitleMode').value==='only'?'字幕任务':'视频')+(result.skipped?'，跳过 '+result.skipped+' 个':'')+'。关闭弹窗后仍会继续下载。');
  await refresh();
}
async function download(urls) {
  if(sending||pendingDownload)return;sending=true;buttons();message('正在检查是否下载过…');
  try{await submitDownload(urls,$('quality').value);}catch(e){message(e.message,true);}finally{sending=false;buttons();}
}
function cancelDownload(){if(sending)return;pendingDownload=null;$('duplicateDialog').close();message('已取消，没有创建下载任务。');buttons();}
$('cancelDownload').addEventListener('click',cancelDownload);
$('duplicateDialog').addEventListener('cancel',e=>{e.preventDefault();cancelDownload();});
$('confirmDownload').addEventListener('click',async()=>{
  if(sending||!pendingDownload)return;sending=true;buttons();$('confirmDownload').disabled=true;$('cancelDownload').disabled=true;
  const {urls,quality,review}=pendingDownload,choices=[...document.querySelectorAll('.duplicate-card')].map(card=>({url:card.dataset.url,decision:card.querySelector('.duplicate-choice').value,target:card.querySelector('.duplicate-target').value}));
  try{await submitDownload(urls,quality,{snapshot:review.snapshot,choices});}
  catch(e){$('duplicateHint').textContent=e.message;message(e.message,true);}
  finally{sending=false;$('confirmDownload').disabled=false;$('cancelDownload').disabled=false;buttons();}
});

async function currentDescription(){
 const [tab]=await chrome.tabs.query({active:true,currentWindow:true});
 if(!tab?.id||videoUrl(tab.url)!==currentUrl)throw Error('当前页面已变化，请重新打开轻存。');
 const page=await QingcunPlatforms.read(chrome,tab.id);if(!page||videoUrl(page.pageUrl)!==currentUrl)throw Error('页面在读取时发生了变化，请重新摘录。');return page;
}
$('downloadCurrent').addEventListener('click',async()=>{
 if(sending||pendingDownload||!currentUrl)return;downloadContexts=[];
 if(currentUrl.includes('behance.net'))return readPageVideos();
 if(currentUrl.includes('xinpianchang.com')){
  sending=true;buttons();try{const page=await currentDescription();downloadContexts=[{...page,url:currentUrl,pageUrl:currentUrl}];await native({action:'saveXpcPage',...page,pageUrl:currentUrl});}catch(e){message(e.message,true);sending=false;buttons();return;}finally{sending=false;buttons();}
  return download([currentUrl]);
 }
 // Keep Vimeo permission in the original click; native metadata captures its description.
 if(/^https:\/\/vimeo\.com\//.test(currentUrl))return download([currentUrl]);
 if($('subtitleMode').value!=='only'){sending=true;buttons();try{const page=await currentDescription();downloadContexts=[{url:currentUrl,pageUrl:currentUrl,labelReadStatus:page.labelReadStatus,categoryReadStatus:page.categoryReadStatus,labelOrigin:page.labelOrigin,categories:page.categories||[],tags:page.tags||[],title:page.title,description:page.description,descriptionExcerpt:!!page.descriptionExcerpt,descriptionTruncated:!!page.descriptionTruncated}];}catch{/* Downloader metadata still supplies descriptions when page reading is unavailable. */}finally{sending=false;buttons();}}
 download([currentUrl]);
});
$('captureDescription').addEventListener('click',async()=>{
 if(sending||pendingDownload)return;sending=true;buttons();message('正在摘录当前视频的简介…');
 try{const page=await currentDescription();if(page.xpc)await native({action:'saveXpcPage',...page,pageUrl:currentUrl});const r=await native({action:'savePageDescription',pageUrl:currentUrl,labelReadStatus:page.labelReadStatus,categoryReadStatus:page.categoryReadStatus,labelOrigin:page.labelOrigin,title:page.title,tags:page.tags||[],categories:page.categories||[],description:page.description||'',descriptionExcerpt:!!page.descriptionExcerpt,descriptionTruncated:!!page.descriptionTruncated});message(r.message+(r.errors?.length?' '+r.errors.join('；'):''),!!r.errors?.length);}
 catch(e){message(e.message||'简介读取失败，请展开原视频简介后重试。',true);}finally{sending=false;buttons();}
});
$('downloadBatch').addEventListener('click', () => {
  const text=$('urls').value.trim();const lines=(text.match(/https?:\/\/[^\s<>"']+/g)||text.split(/\s+/)).map(u=>u.replace(/[，。；、！）)\]>]+$/g,'')).filter(Boolean);
  if (!lines.length) return message('请先粘贴视频链接。', true);
  const urls = lines.map(videoUrl);
  if (urls.some(u => !u)) return message(QingcunPlatforms.inputHint(lines.find((u,i)=>!urls[i])||''), true);
  const unique = [...new Set(urls)]; if (unique.length > 20) return message('一次最多下载 20 个视频，请分批添加。', true);
  downloadContexts=[];download(unique);
});
$('refresh').addEventListener('click', refresh);
$('quality').addEventListener('change', () => chrome.storage.local.set({quality:$('quality').value}));
$('openFolder').addEventListener('click', async () => { try { await native({action:'openFolder'}); } catch(e) { message(e.message,true); } });
$('chooseFolder').addEventListener('click', () => chrome.tabs.create({url:chrome.runtime.getURL('settings.html')}));
function subtitleOptions(){return {mode:$('subtitleMode').value,language:$('subtitleLanguage').value,automatic:$('subtitleAutomatic').checked,size:Number($('subtitleSize').value)};}
function updateSubtitleOptions(){const mode=$('subtitleMode').value;$('subtitleSettings').hidden=mode==='none';$('subtitleSizeRow').hidden=mode!=='burn';$('subtitleHint').textContent=mode==='burn'?'另存带字幕的 MP4，保留原视频和 SRT。白字黑边，底部居中；烧录需要一些时间。':mode==='only'?'只保存 SRT。自动模式依次优先中文、英语、其他可用语言；不会翻译或识别语音。':mode==='files'?'SRT 与视频保存在同一目录。没有字幕时仍保留视频，并提示原因。':'字幕来自平台提供的字幕轨道。';buttons();}
for(const id of ['subtitleMode','subtitleLanguage','subtitleAutomatic','subtitleSize'])$(id).addEventListener('change',()=>{updateSubtitleOptions();chrome.storage.local.set({subtitles:subtitleOptions()});});
async function init() {
  const settings = await chrome.storage.local.get(['quality','subtitles']);
  const st=settings.subtitles||{};for(const [id,key] of [['subtitleMode','mode'],['subtitleLanguage','language'],['subtitleSize','size']])if([...$(id).options].some(o=>o.value===String(st[key])))$(id).value=st[key];
  if(typeof st.automatic==='boolean')$('subtitleAutomatic').checked=st.automatic;updateSubtitleOptions(); if (['best','1080','720'].includes(settings.quality)) $('quality').value = settings.quality;
  try { const [tab] = await chrome.tabs.query({active:true,currentWindow:true}); currentPage=tab?.url||'';currentTabId=tab?.id??null;currentUrl = videoUrl(currentPage); $('pageTitle').textContent = currentUrl ? (tab.title || '当前视频').replace(/ - YouTube$/, '') : (QingcunPlatforms.site(currentPage)?'先打开 '+QingcunPlatforms.site(currentPage)+' 的单条视频':'打开支持平台的视频详情'); $('pageHint').textContent = currentUrl ? '已识别视频，可直接下载；B站按当前分P保存。' : (QingcunPlatforms.site(currentPage)?QingcunPlatforms.inputHint(currentPage):'支持 YouTube · Instagram · Pinterest · Behance · Vimeo · B站 · 抖音 · TikTok · 新片场 · X'); } catch { $('pageTitle').textContent = '请在下方粘贴视频链接'; }
  await refresh(); setInterval(refresh, 2500);
}
init().catch(e => message(e.message,true));

$('readPage').addEventListener('click',readPageVideos);
let detectedPage=null;
async function readPageVideos(){
 if(sending||pendingDownload)return;
 // Keep permission acquisition in the click. The worker captures this exact
 // Vimeo tab before queuing the native download, including duplicate retries.
 if(/^https:\/\/vimeo\.com\//.test(currentUrl||'')){downloadContexts=[];return download([currentUrl]);}
 detectedPage=null;downloadContexts=[];$('pageVideoList').replaceChildren();
 sending=true;buttons();message('正在读取当前作品页中的视频…');
 try{
  const [tab]=await chrome.tabs.query({active:true,currentWindow:true});
  if(!videoUrl(tab?.url||'')||!QingcunPlatforms.canRead(tab.url))throw Error('请先打开 Behance 作品、Instagram / Pinterest、抖音、TikTok 或新片场 的单条视频详情。');
  const captured=await QingcunPlatforms.read(chrome,tab.id);
  if(!captured||typeof captured!=='object'||!Array.isArray(captured.urls))throw Error('页面读取没有返回有效数据。请刷新作品页，并重新加载轻存扩展后再试。');
  if(videoUrl(captured.pageUrl||'')!==videoUrl(tab.url))throw Error('页面在读取期间发生了跳转，请在目标作品页重新读取。');
  if(captured.xpc){detectedPage={...captured,urls:[videoUrl(captured.pageUrl)]};downloadContexts=[{...detectedPage,url:videoUrl(captured.pageUrl),pageUrl:videoUrl(captured.pageUrl)}];await native({action:'saveXpcPage',...detectedPage});sending=false;buttons();return download([videoUrl(captured.pageUrl)]);}
  const mediaUrl=QingcunPlatforms.pageMedia(captured.mediaUrl||'',videoUrl(captured.pageUrl));
  detectedPage={...captured,mediaUrl,urls:captured.urls.filter(u=>typeof u==='string')};
  const urls=mediaUrl?[videoUrl(detectedPage.pageUrl)]:[...new Set(detectedPage.urls.map(videoUrl).filter(Boolean))].filter(u=>u!==videoUrl(detectedPage.pageUrl)).slice(0,20);
  if(!urls.length&&String(captured.mediaUrl||'').startsWith('blob:'))throw Error('当前播放器使用临时视频地址，无法直接保存。请尝试“下载当前视频”；需要登录或加密的视频暂不支持。');
  if(!urls.length)throw Error(captured.frameReadIncomplete?'作品内嵌播放器读取未完成。请刷新作品页后重试；若浏览器提示网站访问权限，请允许轻存读取播放器。':'页面已读取，但尚未解析出可用的视频入口。请展开目标播放器后重试；这不代表作品一定没有视频。');
  detectedPage.urls=urls;const list=$('pageVideoList');list.replaceChildren();
  urls.forEach((url,i)=>{const label=document.createElement('label'),input=document.createElement('input'),text=document.createElement('span');input.type='checkbox';input.value=url;input.checked=i===0;text.textContent=(mediaUrl?'当前播放的视频':'视频 '+(i+1))+' · '+new URL(url).hostname;label.append(input,text);list.append(label);});
  $('pageVideoHint').textContent=mediaUrl?'保存当前播放的画质。原视频网址与发布账号会一起归档。':'找到 '+urls.length+' 个视频入口，选择要保存的视频。';$('pageVideos').showModal();message('');
 }catch(e){detectedPage=null;downloadContexts=[];const text=String(e?.message||'');message(/Cannot access|permission|Cannot read|No tab|frame.*removed|not been granted/i.test(text)?'无法读取此页面。请刷新作品页，重新打开轻存，并检查扩展是否已启用。':text||'页面读取失败，请刷新作品页后重试。',true);}
 finally{sending=false;buttons();}
}
$('cancelPageVideos').addEventListener('click',()=>$('pageVideos').close());
$('savePageVideos').addEventListener('click',()=>{
 if(!detectedPage||!Array.isArray(detectedPage.urls)){message('页面读取结果已失效，请重新读取当前作品页。',true);$('pageVideos').close();return;}
 const urls=[...$('pageVideoList').querySelectorAll('input:checked')].map(x=>x.value).filter(url=>detectedPage.urls.includes(url));
 if(!urls.length){$('pageVideoHint').textContent='请至少选择一个视频。';return;}
 downloadContexts=urls.map(url=>({url,pageUrl:videoUrl(detectedPage.pageUrl),tags:detectedPage.tags||[],categories:detectedPage.categories||[],title:detectedPage.title,name:detectedPage.name,account:detectedPage.account,profileUrl:detectedPage.profileUrl,description:detectedPage.description||'',descriptionExcerpt:!!detectedPage.descriptionExcerpt,descriptionTruncated:!!detectedPage.descriptionTruncated,mediaUrl:detectedPage.mediaUrl||''}));
 $('pageVideos').close();download(urls);
});


})();

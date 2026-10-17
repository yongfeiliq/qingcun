(() => {
'use strict';
if (!globalThis.QingcunContext?.available) return;
'use strict';
const $=id=>document.getElementById(id),HOST='com.qingcun.video_downloader';
let page=0,total=0,pageSize=12,selectedTags=new Set(),current=null,editingTag=null,requestSerial=0,debounce,notesDirty=false,tagBusy=false;
let currentAnalysisJob={};
const filterIds=['category','workflow','orientation','minHeight','maxDuration','person','analysis'];
async function api(data){if(QingcunVimeo.handles(data))return QingcunVimeo.request(data,current?.url||'');return new Promise((resolve,reject)=>chrome.runtime.sendNativeMessage(HOST,data,r=>{const err=chrome.runtime.lastError;if(err)return reject(Error('未连接到本地组件。请运行新版 Install.cmd，再重新打开素材库。'));if(!r?.ok)return reject(Error(r?.error||'操作没有完成。'));resolve(r);}));}
function el(tag,cls,text){const n=document.createElement(tag);if(cls)n.className=cls;if(text!==undefined)n.textContent=text;return n;}
function notice(text='',error=false,detail=false){const n=$(detail?'detailNotice':'notice');n.textContent=text;n.classList.toggle('error',error);}
function seconds(v){v=Number(v)||0;return v<60?Math.round(v*10)/10+' 秒':Math.floor(v/60)+' 分 '+Math.round(v%60)+' 秒';}
function options(select,values,all){const previous=select.value;select.replaceChildren();if(all!==undefined){const o=el('option','',all);o.value='';select.append(o);}for(const value of values){const o=el('option','',value);o.value=value;select.append(o);}if([...select.options].some(o=>o.value===previous))select.value=previous;}
function tagKey(t){return t.dimension+'|'+t.name.toLowerCase();}
function sourceLabel(t){if(t.status==='confirmed')return t.source==='manual'?'手动添加 · 已确认':'已手动确认';return t.source==='metadata'?'来源信息':'规则推断 · 待确认';}
async function load(){
 const serial=++requestSerial,q={action:'library',page,search:$('search').value,tags:[...selectedTags],favorite:$('favorite').checked,shotSearch:$('shotSearch').value};
 for(const id of filterIds)q[id]=['minHeight','maxDuration'].includes(id)?Number($(id).value)||0:$(id).value;
 try{const r=await api(q);if(serial!==requestSerial)return;total=r.total;pageSize=r.pageSize;if(page>0&&page*pageSize>=total){page=0;return load();}
 $('total').textContent=r.libraryTotal;$('analyzedTotal').textContent=r.analyzedTotal||0;$('directorTotal').textContent=r.directorTotal||0;options($('person'),r.people||[],'全部人员');$('resultCount').textContent='找到 '+r.total+' 条素材'+(selectedTags.size?' · 同时包含已选标签':'');
 options($('category'),r.categories,'全部分类');options($('workflow'),r.workflows,'全部状态');renderFacets(r.tagFacets);renderCards(r.items);$('empty').hidden=r.items.length>0;
 $('pageInfo').textContent=(page+1)+' / '+Math.max(1,Math.ceil(total/pageSize));$('prev').disabled=page===0;$('next').disabled=(page+1)*pageSize>=total;
 }catch(e){if(serial===requestSerial)notice(e.message,true);}
}
function renderFacets(facets){
 $('facets').replaceChildren();for(const f of facets.slice(0,12)){const b=el('button','chip'+(selectedTags.has(f.name)?' active':''),f.name+' '+f.count);b.setAttribute('aria-pressed',selectedTags.has(f.name));b.addEventListener('click',()=>toggleTag(f.name));$('facets').append(b);}
 $('selectedTags').replaceChildren();for(const tag of selectedTags){const b=el('button','chip active',tag+' ×');b.addEventListener('click',()=>toggleTag(tag));$('selectedTags').append(b);}
}
function toggleTag(tag){selectedTags.has(tag)?selectedTags.delete(tag):selectedTags.add(tag);page=0;load();}
function renderCards(items){
 const grid=$('grid');grid.replaceChildren();
 for(const a of items){
  const card=el('article','card');card.tabIndex=0;card.setAttribute('aria-label','查看 '+a.title+' 的标签和详情');
  card.addEventListener('click',()=>openDetail(a.id));card.addEventListener('keydown',e=>{if(e.target===card&&(e.key==='Enter'||e.key===' ')){e.preventDefault();openDetail(a.id);}});
  const cover=el('div','cover');if(a.thumbnail){const img=el('img');img.src=a.thumbnail;img.alt=a.title;img.loading='lazy';cover.append(img);}else cover.append(el('span','','▤'));
  cover.append(el('span','duration',seconds(a.media.duration)));
  const fav=el('button','fav',a.favorite?'★':'☆');fav.title=a.favorite?'取消收藏':'收藏';fav.setAttribute('aria-label',fav.title);fav.addEventListener('click',async e=>{e.stopPropagation();fav.disabled=true;try{await api({action:'editAsset',id:a.id,edit:'flags',favorite:!a.favorite});await load();}catch(e){notice(e.message,true);fav.disabled=false;}});cover.append(fav);
  const body=el('div','card-body'),title=el('h2','',a.title);title.title=a.title;
  const meta=el('div','card-meta');meta.append(el('span','',a.category),el('span','',(a.media.height||'—')+'p · '+(a.media.orientation||'未知画幅')));
  const tags=el('div','tags');for(const t of a.tags.slice(0,5)){const chip=el('button','chip'+(t.status==='suggested'?' suggested':''),t.name);chip.title=t.dimension+' · '+sourceLabel(t);chip.addEventListener('click',e=>{e.stopPropagation();toggleTag(t.name);});tags.append(chip);}if(a.tagCount>5)tags.append(el('span','chip','+'+(a.tagCount-5)));
  const foot=el('div','card-footer');foot.append(el('span','',a.workflow),el('span','',a.analysisSummary?.shotCount?a.analysisSummary.shotCount+' 个镜头 · '+(a.analysisSummary.status==='confirmed'?'已确认':'待确认'):'画面待分析'));
  body.append(title,meta,cardPeople(a),tags,foot);card.append(cover,body);grid.append(card);
 }
}
async function openDetail(id){
 try{const r=await api({action:'asset',id});adoptAsset(r.asset);notesDirty=false;options($('detailCategory'),r.categories);options($('detailWorkflow'),r.workflows);options($('tagDimension'),r.dimensions);
 resetTagForm();renderDetail();notice('',false,true);if(!$('detail').open)$('detail').showModal();setupDetail(r);
 }catch(e){notice(e.message,true);}
}
function renderDetail(){
 if(current){$('assetBrand').value=current.brand?.name||'';$('brandEvidence').textContent=(current.brand?.evidence||'未识别品牌，可手动指定。')+(current.brand?.candidates?.length>1?' 候选：'+current.brand.candidates.join('、'):'');}
 renderDescription();
 if(!current)return;
 $('detailTitle').textContent=current.title;const f=current.files[0]||{};
 $('detailMeta').textContent=[current.channel&&'频道：'+current.channel,(f.width||'—')+' × '+(f.height||'—'),f.fps&&f.fps+' 帧/秒',seconds(f.duration),current.uploadDate&&'发布日期：'+current.uploadDate.slice(0,4)+'-'+current.uploadDate.slice(4,6)+'-'+current.uploadDate.slice(6,8)].filter(Boolean).join(' · ');
 $('detailCategory').value=current.category;$('detailWorkflow').value=current.workflow;$('detailFavorite').textContent=current.favorite?'★ 已收藏':'☆ 收藏';$('source').hidden=!current.url;if(current.url)$('source').href=current.url;if(!notesDirty)$('notes').value=current.notes||'';
 const box=$('detailTags');box.replaceChildren();
 for(const t of current.tags){
  const row=el('div','tag-row');row.append(el('span','dim',t.dimension),el('span','tag-word',t.name));const source=el('span','provenance',sourceLabel(t));source.title=t.evidence||'';row.append(source);
  if(t.status!=='confirmed'){const confirm=el('button','','确认');confirm.addEventListener('click',()=>edit({edit:'confirmTag',key:tagKey(t)}));row.append(confirm);}
  const change=el('button','','修改');change.addEventListener('click',()=>{if(tagBusy)return;editingTag=tagKey(t);$('tagName').value=t.name;$('tagDimension').value=t.dimension;$('saveTag').textContent='保存修改';$('cancelTagEdit').hidden=false;$('tagName').focus();});
  const remove=el('button','danger','移除');remove.addEventListener('click',()=>edit({edit:'removeTag',key:tagKey(t)}));row.append(change,remove);for(const button of row.querySelectorAll("button"))button.disabled=tagBusy;box.append(row);
 }
 if(!current.tags.length)box.append(el('p','hint','暂无中文标签。可以在下方手动添加。'));
 $('files').replaceChildren();current.files.forEach((file,index)=>{const row=el('div','file'),info=el('div');info.append(el('p','',file.path),el('p','hint',file.width+' × '+file.height+' · '+Math.round(file.bytes/1048576*10)/10+' MB'));const b=el('button','','定位文件');b.addEventListener('click',async()=>{try{await api({action:'revealAsset',id:current.id,index});}catch(e){notice(e.message,true,true);}});row.append(info,b);$('files').append(row);});
 const sourceTagsRead=current.sourceLabelCapture?.status==='read'||current.vimeoMetadata?.tags==='read';
 const emptySourceTags=sourceTagsRead?'已核实，未设置':'尚未核实';
 const captionRead=(sourceTagsRead&&/instagram\.com/.test(current.url||''))||current.vimeoMetadata?.description==='read'||(!!current.description&&!current.descriptionExcerpt&&!current.descriptionTruncated);
 $('originalInfo').textContent=['原标题：'+current.title,'平台原分类：'+((current.sourceCategories||[]).join('、')||(current.vimeoMetadata?.categories==='read'?'已核实，未设置':'尚未返回')),'原始关键词：'+((current.originalTags||[]).join('、')||emptySourceTags),'原始话题词：'+((current.originalHashtags||[]).join('、')||(captionRead?'已核实，未设置':'尚未核实')),'制作信息：'+(current.sourceFacts||[]).map(f=>f.label+'：'+f.value).join('；'),'简介已在本页上方单独展示。'].join('\n\n');
}
async function edit(values){
 if(!current)return false;
 try{const r=await api({action:'editAsset',id:current.id,...values});adoptAsset(r.asset);if(Object.hasOwn(values,'notes'))notesDirty=false;if(['addTag','replaceTag'].includes(values.edit))resetTagForm();renderDetail();notice('已保存。',false,true);await load();return true;}
 catch(e){notice(e.message,true,true);return false;}
}
function setTagBusy(value){tagBusy=value;for(const id of ['tagName','tagDimension','saveTag','cancelTagEdit'])$(id).disabled=value;for(const b of $('detailTags').querySelectorAll('button'))b.disabled=value;}
function resetTagForm(){editingTag=null;$('tagName').value='';$('saveTag').textContent='添加标签';$('cancelTagEdit').hidden=true;}
$('tagForm').addEventListener('submit',async e=>{e.preventDefault();const name=$('tagName').value.trim();if(!['品牌','来源','平台关键词','平台分类','制作方式','软件','产品','系列'].includes($('tagDimension').value)&&!/[\u3400-\u9fff]/.test(name)){notice('镜头等分析标签使用中文；品牌、平台和平台关键词可以保留原名。',true,true);return;}
 const values={edit:editingTag?'replaceTag':'addTag',key:editingTag||'',name,dimension:$('tagDimension').value};setTagBusy(true);try{await edit(values);}finally{setTagBusy(false);}});
$('cancelTagEdit').addEventListener('click',resetTagForm);
$('detailCategory').addEventListener('change',()=>edit({edit:'category',category:$('detailCategory').value}));
$('detailWorkflow').addEventListener('change',()=>edit({edit:'flags',workflow:$('detailWorkflow').value}));
$('detailFavorite').addEventListener('click',()=>edit({edit:'flags',favorite:!current.favorite}));
$('notes').addEventListener('input',()=>{notesDirty=true;});
$('saveNotes').addEventListener('click',()=>edit({edit:'flags',notes:$('notes').value}));
$('closeDetail').addEventListener('click',()=>$('detail').close());
$('search').addEventListener('input',()=>{clearTimeout(debounce);debounce=setTimeout(()=>{page=0;load();},250);});
for(const id of [...filterIds,'favorite'])$(id).addEventListener('change',()=>{page=0;load();});
$('clear').addEventListener('click',()=>{for(const id of filterIds)$(id).value='';$('favorite').checked=false;$('search').value='';$('shotSearch').value='';selectedTags.clear();page=0;notice();load();});
$('prev').addEventListener('click',()=>{page=Math.max(0,page-1);load();});
$('next').addEventListener('click',()=>{page++;load();});
$('refresh').addEventListener('click',()=>{notice();load();});
$('import').addEventListener('click',async()=>{$('import').disabled=true;notice('请选择已有视频；将生成缩略图和标签，原视频保持在原位置。');
try{const r=await api({action:'importFiles'});notice('已导入 '+r.imported+' 个视频。'+(r.errors.length?'部分文件失败：'+r.errors.join('；'):''),r.errors.length>0);page=0;await load();}catch(e){notice(e.message,true);}finally{$('import').disabled=false;}});

let modelConfig=null,creditEditId='',analysisTimer=null,detailEpoch=0,previewEpoch=0,previewSource='',previewSession='',previewTimer=null,previewSeek=0,fileIndex=0,shotEditId='',visualDirty=false,thumbObserver=null;
const shotLabels={movement:'运镜',scale:'景别',color:'色调'};
const shotOptions={scale:['远景','全景','中景','近景','特写','大特写','变化景别','待确认'],movement:['固定','推近','拉远','横移','跟拍','环绕','水平摇镜','俯仰摇镜','升降','手持','变焦','滚转','甩镜','推拉变焦','复合运镜','待确认'],color:['暖调','冷调','中性','冷暖混合','待确认']};
function labelValue(s,k){return shotOptions[k].includes(s[k])?s[k]:'待确认';}
function adoptAsset(asset){const oldFiles=current?.files||[];for(const f of asset.files||[]){const old=oldFiles.find(x=>x.path===f.path);if(old?.key&&!f.key)f.key=old.key;}current=asset;}
function safeLink(raw){try{const u=new URL(raw);return u.protocol==='https:'&&!u.username&&!u.password?u.href:'';}catch{return '';}}
function profileLink(url,text){const a=el('a','profile-link',text);a.href=safeLink(url);a.target='_blank';a.rel='noopener noreferrer';a.addEventListener('click',e=>e.stopPropagation());return a;}
function filterPerson(person){$('person').value=person;if($('person').value!==person){const o=el('option','',person);o.value=person;$('person').append(o);$('person').value=person;}page=0;if($('detail').open)$('detail').close();load();}
function personButton(person){const name=person.name||person.account||'未署名';const b=el('button','person-link',name);b.type='button';b.addEventListener('click',e=>{e.stopPropagation();filterPerson(name);});return b;}
function cardPeople(a){const box=el('div','card-people'),directors=(a.credits||[]).filter(c=>['导演','联合导演'].includes(c.role));const row=el('div');row.append(el('span','','导演'));if(directors.length){const names=el('div');directors.forEach((p,i)=>{if(i)names.append(document.createTextNode(' / '));names.append(personButton(p));});row.append(names);}else row.append(el('small','','未找到署名'));box.append(row);const pub=a.publisher||{},creator=el('div');creator.append(el('span','','发布'));const text=pub.account||pub.name||'本地导入';creator.append(safeLink(pub.url)?profileLink(pub.url,text+' ↗'):el('small','',text));box.append(creator);return box;}
async function refreshModel(){try{modelConfig=await api({action:'modelSettings'});const ready=modelConfig.enabled&&modelConfig.configured;$('modelState').textContent=ready?'智能分析已开启':modelConfig.enabled?'模型暂未连接':'智能分析已关闭（可选）';$('modelHint').textContent=ready?(modelConfig.provider==='codex'?'本机 Codex 登录 · 抽帧分析':'Gemini · 视频分析'):modelConfig.enabled?'可在设置中检查连接':'下载、入库与手动标签正常使用';document.querySelector('.status-dot').classList.toggle('ready',ready);if(current)renderAnalysis();}catch(e){$('modelState').textContent='请更新本地组件';$('modelHint').textContent=e.message;}}
function switchTab(name){for(const tab of document.querySelectorAll('[data-tab]')){const active=tab.dataset.tab===name;tab.setAttribute('aria-selected',active);tab.tabIndex=active?0:-1;$('pane-'+tab.dataset.tab).hidden=!active;}}
for(const tab of document.querySelectorAll('[data-tab]')){tab.addEventListener('click',()=>switchTab(tab.dataset.tab));tab.addEventListener('keydown',e=>{if(!['ArrowLeft','ArrowRight','Home','End'].includes(e.key))return;e.preventDefault();const tabs=[...document.querySelectorAll('[data-tab]')],n=tabs.indexOf(tab),next=e.key==='Home'?0:e.key==='End'?tabs.length-1:(n+(e.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length;switchTab(tabs[next].dataset.tab);tabs[next].focus();});}
const narrowDetail=matchMedia('(max-width:680px)');
function placeFileDetails(){const panel=document.querySelector('.file-details');(narrowDetail.matches?$('pane-files'):document.querySelector('.detail-sidebar')).append(panel);}
narrowDetail.addEventListener('change',placeFileDetails);placeFileDetails();
function setupDetail(r){currentAnalysisJob=r.job||{};detailEpoch++;document.querySelector('.detail-content').scrollTop=0;fileIndex=Math.max(0,current.files.findIndex(f=>f.path===(current.analysisDraft||current.analysis||{}).filePath));const select=$('fileVariant');select.replaceChildren();current.files.forEach((f,i)=>{const o=el('option','',(i+1)+' · '+f.height+'p · '+seconds(f.duration)+(f.subtitleBurned?' · 已烧录字幕':''));o.value=i;select.append(o);});select.value=String(fileIndex);options($('creditRole'),r.roles||['导演','摄影指导','创作者']);creditEditId='';$('sourceEditor').open=false;$('creditForm').reset();$('creditEditor').open=false;visualDirty=false;switchTab('analysis');resetPreview();renderCredits();renderAnalysis();renderSubtitles();showJob(r.job||{});scheduleAnalysisPoll();}
function releasePreviewSession(){
 clearInterval(previewTimer);previewTimer=null;const session=previewSession;previewSession='';previewSource='';
 if(session)api({action:'closePlayback',session}).catch(()=>{});
}
function resetPreview(clearPosition=true){
 previewEpoch++;releasePreviewSession();const video=$('preview');video.onloadedmetadata=null;video.onerror=null;video.pause();video.removeAttribute('src');video.load();
 if(clearPosition)$('previewPosition').textContent='点击右侧镜头，定位原视频';
 $('previewPlaceholder').hidden=false;$('previewStatus').textContent='直接播放本地原文件，保留原画质与声音。';$('loadPreview').disabled=false;$('loadPreview').textContent='播放原视频';
}
async function playOriginalAt(at){
 previewSeek=Math.max(0,Number(at)||0);
 const video=$('preview');
 if(!previewSource)return loadPreview(previewSeek);
 if(video.readyState<1)return;
 video.currentTime=Math.min(previewSeek,Number.isFinite(video.duration)?Math.max(0,video.duration-.01):previewSeek);
 try{await video.play();}catch{if(previewSource)$('previewStatus').textContent='原视频已就绪，请点击播放器中的播放键。声音已保留。';}
}
async function loadPreview(at=0){
 resetPreview(false);previewSeek=Math.max(0,Number(at)||0);const epoch=previewEpoch,id=current.id,index=fileIndex;
 $('loadPreview').disabled=true;$('previewStatus').textContent='正在打开本地原视频…';
 try{
  const info=await api({action:'openPlayback',id,index});
  if(epoch!==previewEpoch){api({action:'closePlayback',session:info.session}).catch(()=>{});return;}
  const url=new URL(info.url);if(url.protocol!=='http:'||url.hostname!=='127.0.0.1'||!url.port||url.username||url.password)throw Error('本地播放地址无效，请更新下载组件。');
  previewSession=info.session;previewSource=url.href;
  previewTimer=setInterval(()=>{const session=previewSession;if(session)api({action:'keepPlayback',session}).catch(e=>{if(epoch===previewEpoch)$('previewStatus').textContent=e.message;});},30000);
  const video=$('preview');video.crossOrigin='anonymous';video.muted=false;
  video.onloadedmetadata=()=>{if(epoch!==previewEpoch)return;$('previewPlaceholder').hidden=true;$('previewStatus').textContent='原画质 · 原声音 · 点击镜头可直接跳转';playOriginalAt(previewSeek);};
  video.onerror=()=>{if(epoch!==previewEpoch)return;const code=video.error?.code;$('previewStatus').textContent=code===3||code===4?'浏览器无法解码这个原视频。可点击“用系统播放器打开”。':'原视频读取中断，请重新播放；文件移动后需重新打开详情。';releasePreviewSession();};
  video.src=previewSource;video.load();$('loadPreview').textContent='重新播放原视频';
 }catch(e){if(epoch===previewEpoch){releasePreviewSession();$('previewStatus').textContent=e.message;}}
 finally{if(epoch===previewEpoch)$('loadPreview').disabled=false;}
}
$('openOriginal').addEventListener('click',async()=>{try{await api({action:'openOriginal',id:current.id,index:fileIndex});}catch(e){$('previewStatus').textContent=e.message;}});
window.addEventListener('pagehide',()=>{previewEpoch++;releasePreviewSession();});

$('loadPreview').addEventListener('click',()=>loadPreview());
$('fileVariant').addEventListener('change',()=>{fileIndex=Number($('fileVariant').value);visualDirty=false;resetPreview();renderAnalysis();renderSubtitles();});
$('detail').addEventListener('close',()=>{detailEpoch++;clearTimeout(analysisTimer);resetPreview();thumbObserver?.disconnect();});

let subtitleBusy=false,subtitleTimer=null,subtitleJobId='',subtitleAssetId='';
function renderSubtitles(){
 if(!current)return;const box=$('assetSubtitleFiles');box.replaceChildren();
 const selected=current.files[fileIndex];
 for(const item of current.subtitleFiles||[]){const row=el('div','file'),info=el('div');info.append(el('p','',(item.label||item.language)+(item.automatic?' · 自动字幕':'')),el('p','hint',item.path));const button=el('button','','定位字幕');button.addEventListener('click',async()=>{try{await api({action:'revealSubtitle',id:current.id,path:item.path});}catch(e){$('subtitleStatus').textContent=e.message;}});row.append(info,button);box.append(row);}
 if(!box.childElementCount)box.append(el('p','hint','还没有保存字幕。'));
 $('assetDownloadSubtitles').disabled=subtitleBusy||!current.url;$('assetBurnSubtitles').disabled=subtitleBusy||!current.url||!!selected?.subtitleBurned;$('assetImportSubtitles').disabled=subtitleBusy||!!selected?.subtitleBurned;
 if(!subtitleBusy)$('subtitleStatus').textContent=selected?.subtitleBurned?'此文件已烧录字幕。再次烧录请先选原视频。':!current.url?'这是本地视频，可以导入自己的字幕进行烧录。':'';
 if(subtitleJobId&&subtitleAssetId===current.id)scheduleSubtitlePoll();
}
async function startSubtitle(mode,importLocal=false){
 if(subtitleBusy)return;subtitleBusy=true;renderSubtitles();
 const id=current.id,index=fileIndex;$('subtitleStatus').textContent=importLocal?'请在文件选择窗口中选择字幕…':'正在创建字幕任务…';
 try{const r=await api({action:'subtitle',id,index,importLocal,subtitles:{mode,language:$('assetSubtitleLanguage').value,automatic:$('assetSubtitleAutomatic').checked,size:Number($('assetSubtitleSize').value)}});
 if(r.cancelled){$('subtitleStatus').textContent='已取消选择。';return;}
 subtitleJobId=r.id;subtitleAssetId=id;$('subtitleStatus').textContent='任务已开始，关闭页面后仍会继续。';scheduleSubtitlePoll();
 }catch(e){$('subtitleStatus').textContent=e.message;}finally{subtitleBusy=false;const text=$('subtitleStatus').textContent;renderSubtitles();$('subtitleStatus').textContent=text;}
}
function scheduleSubtitlePoll(){clearTimeout(subtitleTimer);subtitleTimer=setTimeout(pollSubtitles,1500);}
async function pollSubtitles(){
 if(!subtitleJobId||!$('detail').open||current?.id!==subtitleAssetId)return;
 const jobId=subtitleJobId,assetId=subtitleAssetId;
 try{const r=await api({action:'list'});if(jobId!==subtitleJobId||current?.id!==assetId)return;const j=(r.jobs||[]).find(j=>j.id===jobId);if(!j){$('subtitleStatus').textContent='请在下载弹窗的最近任务中查看字幕处理结果。';return;}
 $('subtitleStatus').textContent=j.detail||'正在处理字幕…';
 if(['queued','running'].includes(j.status)){scheduleSubtitlePoll();return;}
 subtitleJobId='';const fresh=await api({action:'asset',id:assetId});if(current?.id!==assetId)return;
 const selectedPath=current.files[fileIndex]?.path;adoptAsset(fresh.asset);renderDetail();renderSubtitles();
 const select=$('fileVariant');select.replaceChildren();current.files.forEach((f,i)=>{const o=el('option','',(i+1)+' · '+f.height+'p · '+seconds(f.duration)+(f.subtitleBurned?' · 已烧录字幕':''));o.value=i;select.append(o);});fileIndex=Math.max(0,current.files.findIndex(f=>f.path===selectedPath));select.value=String(fileIndex);$('subtitleStatus').textContent=j.detail;load();
 }catch(e){$('subtitleStatus').textContent=e.message;scheduleSubtitlePoll();}
}
$('assetDownloadSubtitles').addEventListener('click',()=>startSubtitle('only'));
$('assetBurnSubtitles').addEventListener('click',()=>startSubtitle('burn'));
$('assetImportSubtitles').addEventListener('click',()=>startSubtitle('burn',true));


function descriptionSections(){
 const sections=[];if(current?.description)sections.push({text:current.description,label:'视频简介',excerpt:current.descriptionExcerpt,truncated:current.descriptionTruncated,url:current.descriptionUrl||current.url});
 for(const p of current?.sourcePages||[])if(p.description&&!sections.some(s=>s.text===p.description)&&!(current.description&&!current.descriptionExcerpt&&!current.descriptionTruncated&&(p.descriptionExcerpt||p.descriptionTruncated)&&p.pageUrl===(current.descriptionUrl||current.url)))sections.push({text:p.description,label:'作品页简介',excerpt:p.descriptionExcerpt,truncated:p.descriptionTruncated,url:p.pageUrl});
 return sections;
}
function descriptionBody(){const sections=descriptionSections();return sections.length===1?sections[0].text:sections.map(s=>s.label+'\n'+(s.url?'来源：'+s.url+'\n':'')+s.text).join('\n\n');}
function renderDescription(){
 if(!current)return;const sections=descriptionSections(),text=descriptionBody();
 $('descriptionText').textContent=text||'尚未读取到简介。可点击“更新简介”；也可打开原视频、展开简介，在轻存弹窗点击“摘录此页简介到素材库”。';
 $('descriptionHint').textContent=sections.some(s=>s.excerpt||s.truncated)?'部分内容来自页面摘要或经过截取，可能不完整。展开原页面简介后可重新摘录。':'保存平台原文中的换行、署名、话题标签和网址。';
 $('copyDescription').disabled=!text;$('exportDescription').disabled=!text;$('refreshDescription').disabled=!current.url;
 $('descriptionStatus').textContent='';
}
$('copyDescription').addEventListener('click',async()=>{
 const text=descriptionBody();if(!text)return;
 try{await navigator.clipboard.writeText(text);$('descriptionStatus').textContent='简介已复制。';}
 catch{const range=document.createRange();range.selectNodeContents($('descriptionText'));const selection=getSelection();selection.removeAllRanges();selection.addRange(range);$('descriptionStatus').textContent='浏览器未允许自动复制，已选中简介，请按 Ctrl+C。';}
});
for(const [button,action] of [['exportDescription','exportDescription'],['refreshDescription','refreshDescription']])$(button).addEventListener('click',async()=>{
 if(!current)return;const epoch=detailEpoch,id=current.id;$(button).disabled=true;$('descriptionStatus').textContent=action==='refreshDescription'?'正在读取平台简介，视频不会重新下载…':'正在保存简介文本…';
 try{const r=await api({action,id});if(epoch!==detailEpoch||current?.id!==id)return;adoptAsset(r.asset);renderDescription();renderCredits();$('descriptionStatus').textContent=r.message+(r.errors?.length?' '+r.errors.join('；'):'')+(r.paths?.length?'\n'+r.paths.join('\n'):'');}
 catch(e){if(epoch===detailEpoch)$('descriptionStatus').textContent=e.message;}finally{if(epoch===detailEpoch)$(button).disabled=button==='refreshDescription'?!current.url:!descriptionBody();}
});

function visibleAnalysis(){return current?.analysisDraft&&Object.keys(current.analysisDraft).length?current.analysisDraft:current?.analysis||{};}
function analysisMatches(v){const f=current?.files?.[fileIndex];return !!f&&v.filePath===f.path&&(!f.key||v.fileKey===f.key);}
function renderAnalysis(){
 if(!current)return;const v=visibleAnalysis(),has=Array.isArray(v.shots)&&v.shots.length>0,match=has&&analysisMatches(v);
 $('analysisConsent').textContent=modelConfig?.provider==='codex'?'点击分析会通过本机已登录的 Codex，分段发送带时间的抽帧画面给 OpenAI，使用你的 Codex 额度。每段最多 96 张；已完成段落保留，失败可继续。':'点击分析会将当前文件的压缩静音副本发送给 Google Gemini，费用由你的模型账户结算。原视频保留在本机。';
 const ready=modelConfig?.configured&&modelConfig?.enabled;$('analyze').textContent=ready?(has?'重新分析当前文件':'开始镜头分析'):'配置镜头分析';$('analyze').disabled=false;
 $('analysisEmpty').hidden=has;$('analysisResult').hidden=!has;showJob(currentAnalysisJob);
 if(!has){$('analysisEmpty').querySelector('p').textContent=ready?'模型已就绪。点击“开始镜头分析”，生成运镜、景别和冷暖色调标签。':'标注运镜、景别与冷暖色调。先完成模型配置，再开始第一条分析。';return;}
 $('shotCount').textContent=v.shots.length+' 个';$('analysisModel').textContent=v.status==='confirmed'?'已确认':'待复核';
 $('analysisWarning').textContent=!match?'这份分析对应另一个文件或较早的文件版本。请切换到对应文件，或重新分析当前文件。':v.status==='confirmed'?'已人工确认。仍可修正单个镜头；重新分析会生成独立的新结果。':'运镜与镜头边界为模型建议，可逐个修正后确认。';
 if(v.samplingNote)$('analysisWarning').textContent+=' '+v.samplingNote;
 if(v.schemaVersion!==2)$('analysisWarning').textContent+=' 这是旧版结果，请重新分析以获得简洁标签。';
 $('acceptAnalysis').disabled=!match;$('discardAnalysis').hidden=!current.analysisDraft;$('shotResultCount').textContent=v.status==='confirmed'?'已确认':'待复核';
 renderShots();
}
function renderShots(){
 thumbObserver?.disconnect();const v=visibleAnalysis(),box=$('shotList');box.replaceChildren();const terms=$('shotFilter').value.trim().toLowerCase().split(/\s+/).filter(Boolean),shots=v.shots||[],epoch=detailEpoch,id=current.id,index=fileIndex,match=analysisMatches(v);
 const matching=shots.map((s,i)=>({s,i})).filter(({s})=>terms.every(t=>Object.keys(shotLabels).map(k=>labelValue(s,k)).join(' ').toLowerCase().includes(t)));
 $('shotResultCount').textContent=matching.length+' / '+shots.length+' 个';
 const queue=[];let active=0;
 function pump(){if(detailEpoch!==epoch||fileIndex!==index){queue.length=0;return;}while(active<2&&queue.length){const task=queue.shift();active++;api({action:'shotThumbnail',id,index,time:(task.s.start+task.s.end)/2}).then(r=>{if(detailEpoch===epoch&&fileIndex===index&&task.button.isConnected&&r.image){const img=el('img');img.src=r.image;img.alt='第 '+(task.i+1)+' 镜头代表画面';task.button.prepend(img);}}).catch(()=>{}).finally(()=>{active--;pump();});}}
 thumbObserver=new IntersectionObserver(entries=>{for(const entry of entries)if(entry.isIntersecting){thumbObserver.unobserve(entry.target);queue.push(entry.target._shotTask);pump();}},{root:document.querySelector('.detail-content'),rootMargin:'100px'});
 for(const {s,i} of matching){
  const card=el('article','shot-card'),button=el('button','shot-cover');button.type='button';button.setAttribute('aria-label','定位第 '+(i+1)+' 镜头');button.append(el('span','','▶ '+clock(s.start)));button.disabled=!match;
  button.addEventListener('click',async()=>{$('previewPosition').textContent='第 '+String(i+1).padStart(2,'0')+' 镜头 · '+clock(s.start)+' — '+clock(s.end);document.querySelectorAll('.shot-card').forEach(c=>c.classList.remove('selected'));card.classList.add('selected');await playOriginalAt(s.start);});
  const body=el('div','shot-body'),top=el('div','shot-top');top.append(el('strong','','第 '+String(i+1).padStart(2,'0')+' 镜头'),el('small','',clock(s.start)+' — '+clock(s.end)+' · '+seconds(s.end-s.start)));
  const meta=el('div','shot-meta');for(const [key,label] of Object.entries(shotLabels)){const chip=el('span','chip',label+' · '+labelValue(s,key));chip.title=label;meta.append(chip);}
  const tools=el('div','shot-tools');for(const [label,op] of [['修改','edit'],['拆分','split'],['合并下一个','merge']]){const b=el('button','',label);b.type='button';b.disabled=!match||(op==='merge'&&i===shots.length-1);b.addEventListener('click',()=>op==='edit'?openShotEditor(s,i):updateAnalysis({op,shotId:s.id}));tools.append(b);}tools.append(el('small','hint',s.status==='confirmed'?'已确认':'待确认'));
  body.append(top,meta,tools);card.append(button,body);box.append(card);
  if(match){button._shotTask={s,i,button};thumbObserver.observe(button);}
 }
 if(!matching.length)box.append(el('p','hint','没有匹配的镜头，请调整搜索词。'));
}
function clock(t){const n=Math.max(0,Number(t)||0);return String(Math.floor(n/60)).padStart(2,'0')+':'+(n%60).toFixed(2).padStart(5,'0');}
$('shotFilter').addEventListener('input',renderShots);
function showJob(job){currentAnalysisJob=job;const running=['queued','running'].includes(job.state),failed=job.state==='failed';$('analysisProgress').hidden=!running&&!failed;$('analysisMessage').textContent=(failed?'分析未完成：':'')+(job.message||'');$('analysisPercent').textContent=running?Math.round(job.percent||0)+'%':'';$('analysisBar').value=job.percent||0;$('analysisBar').hidden=!running;$('analysisProgress').classList.toggle('error',failed);$('analysisEmpty').hidden=running||!!visibleAnalysis().shots?.length;if(running){$('analyze').disabled=true;$('analyze').textContent='正在分析…';}if(failed){notice('本次分析未完成。原视频和已有标签保留，可重新尝试。',true,true);$('analyze').textContent=job.chunksCompleted>0?'继续分析剩余片段':'重新尝试分析';}else if(job.state==='completed')notice('分析完成，结果已进入待确认区。',false,true);return running;}
function scheduleAnalysisPoll(){clearTimeout(analysisTimer);if(!$('detail').open)return;const epoch=detailEpoch,id=current.id;analysisTimer=setTimeout(async()=>{try{const r=await api({action:'analysisJob',id});if(detailEpoch!==epoch||!$('detail').open)return;const running=showJob(r.job||{});if(running)scheduleAnalysisPoll();else if(r.job?.state==='completed'&&current.analysisJobId===r.job.id){const updated=await api({action:'asset',id});if(detailEpoch!==epoch)return;adoptAsset(updated.asset);renderAnalysis();await load();}else $('analyze').disabled=false;}catch(e){if(detailEpoch===epoch)notice(e.message,true,true);}},1800);}
$('analyze').addEventListener('click',async()=>{if(!modelConfig?.configured||!modelConfig?.enabled){location.href='settings.html#analysis';return;}const id=current.id,epoch=detailEpoch;$('analyze').disabled=true;notice('正在提交分析任务…',false,true);try{const r=await api({action:'startAnalysis',id,index:fileIndex});if(detailEpoch!==epoch)return;current.analysisJobId=r.job.id;showJob(r.job);notice('分析将在后台继续。新结果进入待确认区，已有确认结果会保留。',false,true);scheduleAnalysisPoll();}catch(e){if(detailEpoch===epoch){notice(e.message,true,true);$('analyze').disabled=false;}}});
async function updateAnalysis(values){const epoch=detailEpoch;try{const r=await api({action:'editAnalysis',id:current.id,...values});if(detailEpoch!==epoch)return;adoptAsset(r.asset);visualDirty=false;renderAnalysis();notice('镜头档案已保存。',false,true);await load();return true;}catch(e){if(detailEpoch===epoch)notice(e.message,true,true);return false;}}
$('acceptAnalysis').addEventListener('click',()=>updateAnalysis({op:'accept'}));
$('discardAnalysis').addEventListener('click',()=>updateAnalysis({op:'discard'}));
function openShotEditor(s,i){shotEditId=s.id;$('shotEditorTitle').textContent='修正第 '+(i+1)+' 镜头';const fields=$('shotFields');fields.replaceChildren();for(const [key,label] of [['start','开始时间（秒）'],['end','结束时间（秒）'],...Object.entries(shotLabels)]){const l=el('label','',label),input=el(shotOptions[key]?'select':'input');input.id='shot-'+key;if(shotOptions[key]){options(input,shotOptions[key]);input.value=labelValue(s,key);}else input.value=s[key]??'';input.required=true;if(key==='start'||key==='end'){input.type='number';input.step='0.001';input.min='0';input.max=visibleAnalysis().duration;input.readOnly=key==='start'&&i===0||key==='end'&&i===visibleAnalysis().shots.length-1;}else input.maxLength=key==='description'?280:80;l.append(input);fields.append(l);}$('shotEditNotice').textContent='';$('shotEditor').showModal();}
$('closeShot').addEventListener('click',()=>$('shotEditor').close());
$('shotForm').addEventListener('submit',async e=>{e.preventDefault();const values={op:'shot',shotId:shotEditId,start:Number($('shot-start').value),end:Number($('shot-end').value)};for(const k of Object.keys(shotLabels))values[k]=$('shot-'+k).value;const button=e.submitter;button.disabled=true;try{if(await updateAnalysis(values))$('shotEditor').close();else $('shotEditNotice').textContent='保存失败，请检查时间范围和中文标签。';}finally{button.disabled=false;}});
function platformName(url){try{const h=new URL(url).hostname;return h==='youtu.be'||h==='youtube.com'||h.endsWith('.youtube.com')?'YouTube':h==='instagram.com'||h.endsWith('.instagram.com')?'Instagram':h==='behance.net'||h.endsWith('.behance.net')?'behance':h;}catch{return '未记录来源';}}
function sourceLinkRow(label,url){const row=el('div','source-link-row');row.append(el('span','',label));row.append(safeLink(url)?profileLink(url,url):el('small','','未提供，可手动补充'));return row;}
function renderSourceLinks(){
 if(!current)return;const p=current.publisher||{},pub=$('publisher');pub.replaceChildren();
 const head=el('div','source-heading'),title=el('div');title.append(el('small','','发布账号 · '+platformName(current.url)),el('br'),el('strong','',p.name||current.channel||'未记录发布者'));
 const refresh=el('button','','更新简介与人员');refresh.id='refreshSource';refresh.addEventListener('click',async()=>{const epoch=detailEpoch;refresh.disabled=true;notice('正在检查来源资料与账号主页…',false,true);try{const r=await api({action:'refreshSource',id:current.id});if(epoch!==detailEpoch)return;adoptAsset(r.asset);renderDetail();renderCredits();renderDescription();notice(r.message,false,true);await load();}catch(e){if(epoch===detailEpoch)notice(e.message,true,true);}finally{refresh.disabled=false;}});
 head.append(title,refresh);pub.append(head);
 if(p.account){const account=el('div','source-account');account.append(safeLink(p.url)?profileLink(p.url,p.account+' ↗'):el('span','',p.account));pub.append(account);}
 const rows=el('div','source-links');rows.append(sourceLinkRow('原视频地址',current.url),sourceLinkRow('账号主页',p.url));pub.append(rows,el('p','hint','发布账号与导演职务分别记录。'));
 const other=$('fileSourceLinks');other.replaceChildren();const links=el('div','source-links');links.append(sourceLinkRow('原视频地址',current.url),sourceLinkRow('发布者主页',p.url));for(const page of current.sourcePages||[]){if(page.pageUrl!==current.url){rows.append(sourceLinkRow('收藏作品页',page.pageUrl));links.append(sourceLinkRow('收藏作品页',page.pageUrl));}if(page.profileUrl&&page.profileUrl!==p.url){rows.append(sourceLinkRow('作品作者主页',page.profileUrl));links.append(sourceLinkRow('作品作者主页',page.profileUrl));}}other.append(links);
}
$('sourceEditor').addEventListener('toggle',()=>{if(!$('sourceEditor').open||!current)return;const p=current.publisher||{};$('sourceVideoUrl').value=current.url||'';$('sourcePublisherName').value=p.name||current.channel||'';$('sourceAccount').value=p.account||'';$('sourceProfileUrl').value=p.url||'';});
$('sourceForm').addEventListener('submit',async e=>{e.preventDefault();const epoch=detailEpoch;$('saveSource').disabled=true;try{const r=await api({action:'editSource',id:current.id,url:$('sourceVideoUrl').value,name:$('sourcePublisherName').value,account:$('sourceAccount').value,profileUrl:$('sourceProfileUrl').value});if(epoch!==detailEpoch)return;adoptAsset(r.asset);renderDetail();renderSourceLinks();$('sourceEditor').open=false;notice('平台链接已保存。',false,true);await load();}catch(e){if(epoch===detailEpoch)notice(e.message,true,true);}finally{$('saveSource').disabled=false;}});
$('cancelSource').addEventListener('click',()=>$('sourceEditor').open=false);

function renderCredits(){
 if(!current)return;renderSourceLinks();
 const box=$('credits');box.replaceChildren();
 const staff=current.vimeoMetadata||current.xpcMetadata;if(staff)box.append(el('p','hint',staff.message||''));
 for(const c of current.credits||[]){const card=el('article','credit-card'),top=el('div','credit-top'),actions=el('div','credit-actions');top.append(el('span','credit-role',c.role));
 const editButton=el('button','','修改'),remove=el('button','','移除');editButton.addEventListener('click',()=>{creditEditId=c.id;$('creditRole').value=c.role;$('creditName').value=c.name||'';$('creditAccount').value=c.account||'';$('creditUrl').value=c.url||'';$('creditEvidence').value=c.evidence||'';$('creditEditor').open=true;$('creditEditor').scrollIntoView({block:'nearest'});});remove.addEventListener('click',async()=>{remove.disabled=true;await saveCredit({creditId:c.id,remove:true});});actions.append(editButton,remove);top.append(actions);
 const name=el('h4');name.append(personButton(c));card.append(top,name);if(safeLink(c.url))card.append(profileLink(c.url,(c.account||'打开主页')+' ↗'));else card.append(el('span','hint',c.account||'暂未提供账号链接'));
 card.append(el('p','',c.status==='confirmed'?'人工确认':c.source==='vimeo-credits'?(c.roleOriginal?'Vimeo 原职务：'+c.roleOriginal:'Vimeo 制作人员 · 页面未注明职务'):c.accountStatus==='待确认'?'来源署名 · 姓名与账号关联待确认':'来源署名 · 职务请核对'));if(c.evidence)card.append(el('p','',c.evidence));box.append(card);}
 if(!current.credits?.length)box.append(el('p','hint','没有找到明确的导演或制作人员署名。可以根据原视频简介手动补充，不会把发布者自动当成导演。'));
}
async function saveCredit(values){const epoch=detailEpoch;try{const r=await api({action:'editCredit',id:current.id,...values});if(epoch!==detailEpoch)return;adoptAsset(r.asset);renderCredits();notice('人员信息已保存。',false,true);await load();return true;}catch(e){if(epoch===detailEpoch){notice(e.message,true,true);renderCredits();}return false;}}
$('creditForm').addEventListener('submit',async e=>{e.preventDefault();$('saveCredit').disabled=true;try{if(await saveCredit({creditId:creditEditId,role:$('creditRole').value,name:$('creditName').value,account:$('creditAccount').value,url:$('creditUrl').value,evidence:$('creditEvidence').value})){creditEditId='';$('creditForm').reset();$('creditEditor').open=false;}}finally{$('saveCredit').disabled=false;}});
$('cancelCredit').addEventListener('click',()=>{creditEditId='';$('creditForm').reset();$('creditEditor').open=false;});
refreshModel();

$('shotSearch').addEventListener('input',()=>{clearTimeout(debounce);debounce=setTimeout(()=>{page=0;load();},300);});
load();

async function organizeBrandFiles(single){
 const id=single?current?.id:null,epoch=detailEpoch,button=$(single?'saveBrand':'organizeBrands');
 if(single){$('preview')?.pause();}button.disabled=true;notice('正在整理品牌目录…',false,single);
 try{
  const q={action:'organizeBrands'};if(id){q.id=id;q.brand=$('assetBrand').value.trim();}
  const r=await api(q);
  let same=single&&epoch===detailEpoch;
  if(same){const fresh=await api({action:'asset',id});same=epoch===detailEpoch;if(same){adoptAsset(fresh.asset);renderDetail();setupDetail(fresh);switchTab('files');}}
  notice(r.message+(r.errors?.length?' '+r.errors.join('；'):''),!!r.errors?.length,same);
  await load();
 }catch(e){notice(e.message,true,single&&epoch===detailEpoch);}finally{button.disabled=false;}
}
$('brandForm').addEventListener('submit',e=>{e.preventDefault();organizeBrandFiles(true);});
$('organizeBrands').addEventListener('click',()=>organizeBrandFiles(false));

})();

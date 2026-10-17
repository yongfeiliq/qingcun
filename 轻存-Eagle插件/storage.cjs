'use strict';
module.exports=call=>({
 upgrade:()=>call({action:'upgradeCategories'}),
 configure:libraryPath=>call({action:'configureEagleStorage',libraryPath}),
 resolve:async id=>(await call({action:'asset',id})).asset,
 adopt:async q=>{const a=(await call({action:'asset',id:q.id})).asset,f=a.files.find(f=>f.path.toLowerCase()===q.sourcePath.toLowerCase());
  if(!f)throw Error('下载文件关联已变化，稍后自动重试。');
  return call({action:'adoptEagleCopy',...q,sourceKey:f.key});},
 cleanup:q=>call({action:'cleanupEagleCopy',...q})
});

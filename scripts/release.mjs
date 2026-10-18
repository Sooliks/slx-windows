import {createHash} from 'node:crypto';
import {readFile,readdir,stat} from 'node:fs/promises';
import {resolve} from 'node:path';
import {root,owner,repo,git,releaseMetadata,authorizationHeaders,githubApi} from './release-metadata.mjs';

const safe=['GITHUB_AUTH_REQUIRED','GITHUB_OWNER_MISMATCH','RELEASE_SCOPE','ASSET_FAILED','WORKTREE_NOT_CLEAN','RELEASE_VERSION','RELEASE_HASH_MISMATCH','RELEASE_BUILD_MISMATCH'];
try {
 const metadata=releaseMetadata();
 if(git(['status','--porcelain']))throw Error('WORKTREE_NOT_CLEAN');
 const revision=git(['rev-parse','HEAD']);
 if(!/^[0-9a-f]{40}$/.test(revision))throw Error('RELEASE_SCOPE');
 const names=(await readdir(metadata.directory)).sort();
 if(JSON.stringify(names)!==JSON.stringify([...metadata.assets].sort()))throw Error('RELEASE_SCOPE');
 const provenance=JSON.parse(await readFile(resolve(metadata.directory,'provenance.json'),'utf8'));
 if(provenance.version!==metadata.version||provenance.fileVersion!==metadata.fileVersion||provenance.commit!==revision
  ||provenance.build?.commit!==revision||provenance.build?.sourceClean!==true||provenance.build?.version!==metadata.version
  ||provenance.build?.candidate!==false||provenance.build?.publicationAllowed!==true)
  throw Error('RELEASE_BUILD_MISMATCH');
 const checksumLines=(await readFile(resolve(metadata.directory,'SHA256SUMS.txt'),'utf8')).trim().split(/\r?\n/);
 const checksums=new Map();
 for(const line of checksumLines){
  const match=/^([0-9a-f]{64}) {2}([A-Za-z0-9.+_-]+)$/.exec(line);
  if(!match||checksums.has(match[2]))throw Error('RELEASE_HASH_MISMATCH');
  checksums.set(match[2],match[1]);
 }
 if(checksums.size!==metadata.assets.length-1)throw Error('RELEASE_HASH_MISMATCH');
 const digests=new Map();
 for(const name of metadata.assets){
  const path=resolve(metadata.directory,name),info=await stat(path);
  if(!info.isFile()||info.size===0||info.size>300*1024*1024)throw Error('RELEASE_SCOPE');
  const digest=createHash('sha256').update(await readFile(path)).digest('hex');
  digests.set(name,digest);
  if(name==='SHA256SUMS.txt')continue;
  if(checksums.get(name)!==digest)throw Error('RELEASE_HASH_MISMATCH');
 }
 const setupName=metadata.prefix+'-win-x64-setup.exe';
 if(provenance.build.installer!==setupName||provenance.build.installerSha256!==checksums.get(setupName))
  throw Error('RELEASE_BUILD_MISMATCH');
 const headers=authorizationHeaders(),api=githubApi(headers);
 const me=await api('/user');if(me.status!==200||me.data.login!==owner)throw Error('GITHUB_OWNER_MISMATCH');
 const reference=await api('/repos/'+owner+'/'+repo+'/git/ref/tags/'+encodeURIComponent(metadata.tag));
 if(reference.status===200){
  let object=reference.data.object;
  for(let depth=0;object?.type==='tag'&&depth<5;depth++){
   const tag=await api('/repos/'+owner+'/'+repo+'/git/tags/'+object.sha);
   if(tag.status!==200)throw Error('RELEASE_SCOPE');
   object=tag.data.object;
  }
  if(object?.type!=='commit'||object.sha!==revision)throw Error('RELEASE_SCOPE');
 }else if(reference.status!==404)throw Error('RELEASE_SCOPE');
 let release=await api('/repos/'+owner+'/'+repo+'/releases/tags/'+metadata.tag);
 if(release.status===404)release=await api('/repos/'+owner+'/'+repo+'/releases','POST',{
  tag_name:metadata.tag,target_commitish:revision,name:'SLX VPN '+metadata.version+' — Windows 10 / 11',
  body:await readFile(resolve(root,'docs/RELEASE_NOTES.md'),'utf8'),draft:true,prerelease:true});
 if(![200,201].includes(release.status)||!release.data.draft||release.data.tag_name!==metadata.tag
  ||release.data.target_commitish!==revision)throw Error('RELEASE_SCOPE');
 const existing=new Map((release.data.assets??[]).map(asset=>[asset.name,asset]));
 if([...existing.keys()].some(name=>!metadata.assets.includes(name)))throw Error('RELEASE_SCOPE');
 for(const name of metadata.assets){
  if(existing.has(name)){
   const digest=existing.get(name).digest;
   if(digest!=='sha256:'+digests.get(name))throw Error('RELEASE_SCOPE');
   continue;
  }
  const path=resolve(metadata.directory,name),bytes=await readFile(path);
  const endpoint='https://uploads.github.com/repos/'+owner+'/'+repo+'/releases/'+release.data.id+'/assets?name='+encodeURIComponent(name);
  const upload=await fetch(endpoint,{method:'POST',headers:{...headers,'Content-Type':'application/octet-stream','Content-Length':String(bytes.length)},
   body:bytes,signal:AbortSignal.timeout(180000)});
  if(upload.status!==201)throw Error('ASSET_FAILED');
  console.log(JSON.stringify({uploaded:name,bytes:bytes.length}));
 }
 const published=await api('/repos/'+owner+'/'+repo+'/releases/'+release.data.id,'PATCH',{draft:false,prerelease:true});
 if(published.status!==200)throw Error('RELEASE_SCOPE');
 console.log(JSON.stringify({release:published.data.html_url,commit:revision,version:metadata.version}));
}catch(error){console.log(JSON.stringify({failure:safe.includes(error.message)?error.message:'RELEASE_FAILED'}));process.exitCode=1}

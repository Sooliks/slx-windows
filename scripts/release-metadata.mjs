import {spawnSync} from 'node:child_process';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

export const root=fileURLToPath(new URL('../',import.meta.url));
export const owner='Sooliks',repo='slx-windows';
export const safeEnvironment=Object.fromEntries(
 ['PATH','SystemRoot','TEMP','TMP','USERPROFILE','USERNAME','USERDOMAIN','HOMEDRIVE','HOMEPATH','APPDATA','LOCALAPPDATA','ProgramData','COMSPEC']
 .filter(key=>process.env[key]).map(key=>[key,process.env[key]]));
Object.assign(safeEnvironment,{GIT_TERMINAL_PROMPT:'0',GCM_INTERACTIVE:'never'});
export function git(arguments_,options={}) {
 const result=spawnSync('git',arguments_,{cwd:root,encoding:'utf8',windowsHide:true,timeout:15000,
  env:safeEnvironment,stdio:['pipe','pipe','ignore'],...options});
 if(result.status!==0)throw Error('GIT_FAILED');
 return result.stdout.trim();
}
export function releaseMetadata() {
 const xml=readFileSync(resolve(root,'Directory.Build.props'),'utf8');
 const property=name=>{
  const matches=[...xml.matchAll(new RegExp('<'+name+'>([^<]+)</'+name+'>','g'))];
  if(matches.length!==1)throw Error('RELEASE_VERSION');
  return matches[0][1].trim();
 };
 const version=property('Version');
 const match=/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$/.exec(version);
 if(!match||version.length>100)throw Error('RELEASE_VERSION');
 const fileVersion=property('FileVersion'),baseVersion=match.slice(1,4).join('.');
 if(!/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/.test(fileVersion)
  ||fileVersion.split('.').some(part=>Number(part)>65535)||!fileVersion.startsWith(baseVersion+'.'))throw Error('RELEASE_VERSION');
 const pre=/^(?:alpha|beta|rc)\.(\d+)$/.exec(match[4]??'');
 if(pre&&fileVersion!==baseVersion+'.'+pre[1])throw Error('RELEASE_VERSION');
 const prefix='SLX-VPN-'+version;
 return{version,fileVersion,tag:'v'+version,prefix,directory:resolve(root,'artifacts/release',version),
  assets:[prefix+'-win-x64-setup.exe',prefix+'-source-full.zip',prefix+'-source-changed.zip','DELETIONS.txt','provenance.json','SHA256SUMS.txt']};
}
export function authorizationHeaders() {
 const credential=git(['credential','fill'],{input:'protocol=https\nhost=github.com\n\n'});
 const token=credential.split(/\r?\n/).find(line=>line.startsWith('password='))?.slice(9);
 if(!token)throw Error('GITHUB_AUTH_REQUIRED');
 return{Authorization:'Bearer '+token,Accept:'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28','User-Agent':'SLX-Windows-Release'};
}
export function githubApi(headers) {
 return async(path,method='GET',body)=>{
  const response=await fetch('https://api.github.com'+path,{method,headers:{...headers,...(body?{'Content-Type':'application/json'}:{})},
   body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(30000)});
  return{status:response.status,data:await response.json()};
 };
}

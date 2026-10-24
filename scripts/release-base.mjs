import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';
import {git,releaseMetadata} from './release-metadata.mjs';

export function previousReleaseTag(version) {
 const match=/^((?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)-(?:alpha|beta|rc))\.([1-9]\d*)$/.exec(version);
 if(!match||BigInt(match[2])<=1n)throw Error('RELEASE_BASE_REQUIRED');
 return 'v'+match[1]+'.'+(BigInt(match[2])-1n);
}

export function previousReleaseBase(version,revision,gitCommand=git) {
 const tag=previousReleaseTag(version);
 let commit;
 try{commit=gitCommand(['rev-parse','--verify','--end-of-options',tag+'^{commit}']);}
 catch{throw Error('RELEASE_BASE_MISSING');}
 if(!/^[0-9a-f]{40}$/.test(commit)||commit===revision)throw Error('RELEASE_BASE_MISMATCH');
 try{gitCommand(['merge-base','--is-ancestor',commit,revision]);}
 catch{throw Error('RELEASE_BASE_MISMATCH');}
 const xml=gitCommand(['show',tag+':Directory.Build.props']);
 const versions=[...xml.matchAll(/<Version>([^<]+)<\/Version>/g)];
 if(versions.length!==1||'v'+versions[0][1].trim()!==tag)throw Error('RELEASE_BASE_MISMATCH');
 return{tag,commit};
}

if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 try{
  const metadata=releaseMetadata();
  console.log(previousReleaseBase(metadata.version,git(['rev-parse','HEAD'])).tag);
 }catch(error){
  const safe=['RELEASE_BASE_REQUIRED','RELEASE_BASE_MISSING','RELEASE_BASE_MISMATCH'];
  console.error(safe.includes(error.message)?error.message:'RELEASE_BASE_FAILED');process.exitCode=1;
 }
}

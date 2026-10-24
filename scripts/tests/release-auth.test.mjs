import test from 'node:test';
import assert from 'node:assert/strict';
import {actionsReleaseContext,releaseAuthorizationHeaders,verifyReleaseAuthority,assertMainHead} from '../release-auth.mjs';

const revision='1'.repeat(40);
const token='test-token-for-release-unit-checks';
function environment(changes={}) {
 return{GITHUB_ACTIONS:'true',GITHUB_REPOSITORY:'Sooliks/slx-windows',GITHUB_REPOSITORY_ID:'1423916513',
  GITHUB_REPOSITORY_OWNER:'Sooliks',GITHUB_REF:'refs/heads/main',GITHUB_SHA:revision,
  GITHUB_WORKFLOW_REF:'Sooliks/slx-windows/.github/workflows/release.yml@refs/heads/main',GITHUB_WORKFLOW_SHA:revision,
  GITHUB_SERVER_URL:'https://github.com',GITHUB_API_URL:'https://api.github.com',RUNNER_OS:'Windows',
  GITHUB_EVENT_NAME:'push',GITHUB_TOKEN:token,...changes};
}
const repository={id:1423916513,full_name:'Sooliks/slx-windows',private:false,default_branch:'main'};
function apiWith({repo=repository,head=revision,login='Sooliks'}={}) {
 const calls=[];
 const api=async path=>{
  calls.push(path);
  if(path==='/user')return{status:200,data:{login}};
  if(path==='/repos/Sooliks/slx-windows')return{status:200,data:repo};
  if(path==='/repos/Sooliks/slx-windows/git/ref/heads/main')
   return{status:200,data:{ref:'refs/heads/main',object:{type:'commit',sha:head}}};
  throw Error('Unexpected API path: '+path);
 };
 return{api,calls};
}

for(const event of ['push','workflow_dispatch'])test('Fixed main workflow accepts '+event+' without a /user identity',async()=>{
 const env=environment({GITHUB_EVENT_NAME:event});
 const context=actionsReleaseContext(env,revision);
 const {api,calls}=apiWith();
 assert.equal(releaseAuthorizationHeaders(context,env,()=>{throw Error('Local credentials used in Actions');}).Authorization,'Bearer '+token);
 await verifyReleaseAuthority(api,context);
 assert.deepEqual(calls,['/repos/Sooliks/slx-windows','/repos/Sooliks/slx-windows/git/ref/heads/main']);
});

for(const [key,value] of [
 ['GITHUB_REPOSITORY','other/slx-windows'],['GITHUB_REPOSITORY_ID','123'],['GITHUB_REPOSITORY_OWNER','other'],
 ['GITHUB_REF','refs/heads/feature'],['GITHUB_REF','refs/tags/v0.1.0-alpha.18'],['GITHUB_SHA','2'.repeat(40)],
 ['GITHUB_WORKFLOW_REF','Sooliks/slx-windows/.github/workflows/verify.yml@refs/heads/main'],
 ['GITHUB_WORKFLOW_SHA','2'.repeat(40)],['GITHUB_EVENT_NAME','pull_request'],['GITHUB_EVENT_NAME','pull_request_target'],
 ['GITHUB_EVENT_NAME','workflow_run'],['GITHUB_SERVER_URL','https://example.invalid'],
 ['GITHUB_API_URL','https://example.invalid'],['RUNNER_OS','Linux']
])test('Refuses CI scope mismatch '+key+'='+value,()=>{
 assert.throws(()=>actionsReleaseContext(environment({[key]:value}),revision),/GITHUB_CI_SCOPE/);
});

test('Actions context cannot fall back to local credentials when the job token is missing',()=>{
 const env=environment({GITHUB_TOKEN:undefined});
 const context=actionsReleaseContext(env,revision);
 assert.throws(()=>releaseAuthorizationHeaders(context,env,()=>{throw Error('Unexpected fallback');}),/GITHUB_AUTH_REQUIRED/);
});

test('Token in local shell retains credential helper and exact account owner authorization',async()=>{
 const env={GITHUB_TOKEN:token};
 const context=actionsReleaseContext(env,revision);
 assert.equal(context,null);
 let credentialCalls=0;
 assert.deepEqual(releaseAuthorizationHeaders(context,env,()=>{credentialCalls++;return{local:true};}),{local:true});
 assert.equal(credentialCalls,1);
 const {api,calls}=apiWith();
 await verifyReleaseAuthority(api,context);
 assert.deepEqual(calls,['/user']);
 await assert.rejects(()=>verifyReleaseAuthority(apiWith({login:'github-actions[bot]'}).api,context),/GITHUB_OWNER_MISMATCH/);
});

test('Wrong repository identity or moved main cannot authorize publication',async()=>{
 const context=actionsReleaseContext(environment(),revision);
 await assert.rejects(()=>verifyReleaseAuthority(apiWith({repo:{...repository,id:5}}).api,context),/GITHUB_CI_SCOPE/);
 await assert.rejects(()=>verifyReleaseAuthority(apiWith({repo:{...repository,private:true}}).api,context),/GITHUB_CI_SCOPE/);
 await assert.rejects(()=>verifyReleaseAuthority(apiWith({head:'2'.repeat(40)}).api,context),/RELEASE_HEAD_MOVED/);
 // The second guard used immediately before publishing is independently effective.
 await assert.rejects(()=>assertMainHead(apiWith({head:'3'.repeat(40)}).api,context),/RELEASE_HEAD_MOVED/);
});

test('Missing remote main or a noncommit ref is refused',async()=>{
 const context=actionsReleaseContext(environment(),revision);
 await assert.rejects(()=>assertMainHead(async()=>({status:404,data:{}}),context),/RELEASE_HEAD_MOVED/);
 await assert.rejects(()=>assertMainHead(async()=>({status:200,data:{ref:'refs/heads/main',object:{type:'tag',sha:revision}}}),context),/RELEASE_HEAD_MOVED/);
});

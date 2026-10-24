import test from 'node:test';
import assert from 'node:assert/strict';
import {previousReleaseTag,previousReleaseBase} from '../release-base.mjs';

const revision='1'.repeat(40),base='2'.repeat(40),tag='v0.1.0-alpha.17';
function gitFixture({missing=false,unrelated=false,version='0.1.0-alpha.17',baseCommit=base}={}) {
 const calls=[];
 const git=args=>{
  calls.push(args);
  if(args[0]==='rev-parse'){
   assert.deepEqual(args,['rev-parse','--verify','--end-of-options',tag+'^{commit}']);
   if(missing)throw Error('Tag does not exist');
   return baseCommit;
  }
  if(args[0]==='merge-base'){
   assert.deepEqual(args,['merge-base','--is-ancestor',baseCommit,revision]);
   if(unrelated)throw Error('Not an ancestor');
   return '';
  }
  assert.deepEqual(args,['show',tag+':Directory.Build.props']);
  return '<Project><PropertyGroup><Version>'+version+'</Version></PropertyGroup></Project>';
 };
 return{git,calls};
}

test('Alpha18 requires alpha17 exactly rather than a mutable latest release',()=>{
 assert.equal(previousReleaseTag('0.1.0-alpha.18'),tag);
 assert.equal(previousReleaseTag('0.1.0-alpha.19'),'v0.1.0-alpha.18');
 const {git,calls}=gitFixture();
 assert.deepEqual(previousReleaseBase('0.1.0-alpha.18',revision,git),{tag,commit:base});
 assert.equal(calls.length,3);
});

test('A missing predecessor fails without falling back to an older tag',()=>{
 const {git,calls}=gitFixture({missing:true});
 assert.throws(()=>previousReleaseBase('0.1.0-alpha.18',revision,git),/RELEASE_BASE_MISSING/);
 assert.equal(calls.length,1);
});

test('A tag must identify the expected earlier version on the current source ancestry',()=>{
 for(const fixture of [{unrelated:true},{version:'0.1.0-alpha.16'},{baseCommit:revision},{baseCommit:'invalid'}])
  assert.throws(()=>previousReleaseBase('0.1.0-alpha.18',revision,gitFixture(fixture).git),/RELEASE_BASE_MISMATCH/);
});

test('Release-line transitions need a reviewed base instead of a guessed predecessor',()=>{
 for(const version of ['0.1.0','0.1.0-alpha.1','0.1.0-alpha.01','0.1.0-beta.1','invalid'])
  assert.throws(()=>previousReleaseTag(version),/RELEASE_BASE_REQUIRED/);
});

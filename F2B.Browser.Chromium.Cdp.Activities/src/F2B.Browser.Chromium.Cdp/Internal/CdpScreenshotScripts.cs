namespace F2B.Browser.Chromium.Cdp.Internal
{
    /// <summary>
    /// Page scripts for long-screenshot capture: expand overflow ancestors, then restore.
    /// Mirrors the approach that succeeds on nested overflow:scroll pages (expand chain + clip).
    /// </summary>
    internal static class CdpScreenshotScripts
    {
        internal const string AttrName = "data-f2b-ss-bak";

        /// <summary>
        /// Expand <c>this</c> and ancestor chain; return page-coordinate clip as "x y w h", or "" if no expand needed.
        /// </summary>
        internal const string ExpandElementAndAncestors =
            @"function(){
var ATTR='" + AttrName + @"';
function needsExpand(el){
  if(!el||!el.getBoundingClientRect)return false;
  if(el.scrollHeight>el.clientHeight+10)return true;
  var r=el.getBoundingClientRect();
  if(r.height>window.innerHeight+10)return true;
  var n=el.parentElement;
  while(n){
    var st=window.getComputedStyle(n);
    var oy=st.overflowY||st.overflow;
    if((oy==='auto'||oy==='scroll'||oy==='overlay')&&n.scrollHeight>n.clientHeight+10)return true;
    n=n.parentElement;
  }
  return false;
}
function expand(el){
  var backups=[];
  var idx=0;
  var fullH=Math.max(el.scrollHeight||0,el.clientHeight||0);
  var node=el;
  while(node&&node.nodeType===1){
    var key=String(idx++);
    backups.push({k:key,css:node.style.cssText});
    node.setAttribute(ATTR,key);
    node.style.setProperty('overflow','visible','important');
    node.style.setProperty('overflow-x','visible','important');
    node.style.setProperty('overflow-y','visible','important');
    node.style.setProperty('max-height','none','important');
    if(node===document.documentElement)break;
    node=node.parentElement;
  }
  el.style.setProperty('height',fullH+'px','important');
  try{el.scrollTop=0;el.scrollLeft=0;}catch(e){}
  window.__f2bSsBackups=backups;
  window.__f2bSsAttr=ATTR;
  void el.offsetHeight;
  var r=el.getBoundingClientRect();
  var sx=window.scrollX||window.pageXOffset||0;
  var sy=window.scrollY||window.pageYOffset||0;
  var w=Math.max(1,Math.ceil(r.width));
  var h=Math.max(1,Math.ceil(Math.max(r.height,fullH)));
  return Math.round(r.left+sx)+' '+Math.round(r.top+sy)+' '+w+' '+h;
}
if(!needsExpand(this))return '';
return expand(this);
}";

        /// <summary>
        /// Find the main overflow scroll container, expand it and ancestors; return clip "x y w h" or "".
        /// </summary>
        internal const string FindMainScrollableExpandAndMeasure =
            @"function(){
var ATTR='" + AttrName + @"';
function findMain(){
  var best=null,bestScore=0;
  var all=document.querySelectorAll('body *');
  for(var i=0;i<all.length;i++){
    var el=all[i];
    var st=window.getComputedStyle(el);
    var oy=st.overflowY||'';
    if(oy!=='auto'&&oy!=='scroll'&&oy!=='overlay')continue;
    var sh=el.scrollHeight||0,ch=el.clientHeight||0;
    if(sh<=ch+50)continue;
    var r=el.getBoundingClientRect();
    if(r.width<120||r.height<80)continue;
    var score=(sh-ch)*r.width;
    if(score>bestScore){bestScore=score;best=el;}
  }
  return best;
}
function expand(el){
  var backups=[];
  var idx=0;
  var fullH=Math.max(el.scrollHeight||0,el.clientHeight||0);
  var node=el;
  while(node&&node.nodeType===1){
    var key=String(idx++);
    backups.push({k:key,css:node.style.cssText});
    node.setAttribute(ATTR,key);
    node.style.setProperty('overflow','visible','important');
    node.style.setProperty('overflow-x','visible','important');
    node.style.setProperty('overflow-y','visible','important');
    node.style.setProperty('max-height','none','important');
    if(node===document.documentElement)break;
    node=node.parentElement;
  }
  el.style.setProperty('height',fullH+'px','important');
  try{el.scrollTop=0;el.scrollLeft=0;}catch(e){}
  window.__f2bSsBackups=backups;
  window.__f2bSsAttr=ATTR;
  void el.offsetHeight;
  var r=el.getBoundingClientRect();
  var sx=window.scrollX||window.pageXOffset||0;
  var sy=window.scrollY||window.pageYOffset||0;
  var w=Math.max(1,Math.ceil(r.width));
  var h=Math.max(1,Math.ceil(Math.max(r.height,fullH)));
  return Math.round(r.left+sx)+' '+Math.round(r.top+sy)+' '+w+' '+h;
}
var target=findMain();
if(!target)return '';
return expand(target);
}";

        internal const string RestoreExpandedStyles =
            @"function(){
var backups=window.__f2bSsBackups;
var ATTR=window.__f2bSsAttr||'" + AttrName + @"';
if(!backups||!backups.length){
  var marked=document.querySelectorAll('['+ATTR+']');
  for(var i=0;i<marked.length;i++){marked[i].removeAttribute(ATTR);}
  return true;
}
for(var j=0;j<backups.length;j++){
  var b=backups[j];
  var n=document.querySelector('['+ATTR+'=""'+b.k+'""]');
  if(n){
    n.style.cssText=b.css||'';
    n.removeAttribute(ATTR);
  }
}
try{delete window.__f2bSsBackups;delete window.__f2bSsAttr;}catch(e){
  window.__f2bSsBackups=null;window.__f2bSsAttr=null;
}
return true;
}";
    }
}

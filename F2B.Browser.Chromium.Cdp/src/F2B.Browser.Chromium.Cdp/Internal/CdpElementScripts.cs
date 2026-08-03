namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpElementScripts
    {
        internal const string InnerHtml = "return this.innerHTML;";

        internal const string RawText = "return this.innerText;";

        internal const string InnerText =
            "return Array.from(this.childNodes).filter(function(n){return n.nodeType===Node.TEXT_NODE;})" +
            ".map(function(n){return n.textContent;}).join('');";

        internal const string Value = "return this.value;";

        internal const string BaseUri = "return this.baseURI;";

        internal const string PropertyTemplate = "return this.{0};";

        internal const string StyleTemplate =
            "return window.getComputedStyle(this{0}).getPropertyValue(\"{1}\");";

        internal const string ScrollPosition =
            "return (this.scrollLeft||0).toString() + ' ' + (this.scrollTop||0).toString();";

        internal const string ScrollIntoView = "this.scrollIntoViewIfNeeded({0});";

        internal const string ScrollToCenter =
            @"this.scrollIntoViewIfNeeded(false);
function getWindowScrollTop(){var scroll_top=0;
if(document.documentElement&&document.documentElement.scrollTop){scroll_top=document.documentElement.scrollTop;}
else if(document.body){scroll_top=document.body.scrollTop;}
return scroll_top;}
var rect=this.getBoundingClientRect();
var elCenter=rect.top+rect.height/2;
var center=window.innerHeight/2;
window.scrollTo({top:getWindowScrollTop()-(center-elCenter),behavior:'instant'});";

        internal const string ElementPathXpath =
            @"function(){
function build(el){
if(!(el instanceof Element))return '';
var path='';
while(el.nodeType===Node.ELEMENT_NODE){
var tag=el.nodeName.toLowerCase();
var sib=el,nth=0;
while(sib){if(sib.nodeType===Node.ELEMENT_NODE&&sib.nodeName.toLowerCase()===tag){nth+=1;}sib=sib.previousSibling;}
path='/'+tag+'['+nth+']'+path;
el=el.parentNode;
}
return path;
}
return build(this);}";

        internal const string ElementPathCss =
            @"function(){
function build(el){
if(!(el instanceof Element))return '';
var path='';
while(el.nodeType===Node.ELEMENT_NODE){
var id=el.getAttribute('id');
if(id){path='>'+el.tagName.toLowerCase()+'#'+id+path;el=el.parentNode;continue;}
var sib=el,nth=0;
while(sib){if(sib.nodeType===Node.ELEMENT_NODE){nth+=1;}sib=sib.previousSibling;}
path='>'+el.tagName.toLowerCase()+':nth-child('+nth+')'+path;
el=el.parentNode;
}
return path.length>0?path.substr(1):path;
}
return build(this);}";

        internal const string FormattedText =
            @"function(){
var nowrapList=['br','sub','sup','em','strong','a','font','b','span','s','i','del','ins','img','td',
'th','abbr','bdi','bdo','cite','code','data','dfn','kbd','mark','q','rp','rt','ruby',
'samp','small','time','u','var','wbr','button','slot','content'];
var wrapAfterList=['p','div','h1','h2','h3','h4','h5','h6','ol','li','blockquote','header',
'footer','address','article','aside','main','nav','section','figcaption','summary'];
var noTextList=['script','style','video','audio','iframe','embed','noscript','canvas','template'];
var tabList=['td','th'];
function decodeHtml(s){if(!s)return s;var t=document.createElement('textarea');t.innerHTML=s;return t.value;}
function getNodeTxt(ele,pre){
var tag=ele.tagName?ele.tagName.toLowerCase():'';
if(tag==='br')return [true];
if(!pre&&tag==='pre')pre=true;
var strList=[];
if(noTextList.indexOf(tag)>=0&&!pre)return strList;
var nodes=ele.childNodes;
var prevEle='';
for(var i=0;i<nodes.length;i++){
var el=nodes[i];
if(el.nodeType===Node.TEXT_NODE){
if(pre){strList.push(el.textContent);}
else{
var txt=el.textContent.replace(/[\n\t\r]/g,'');
if(txt.replace(/ /g,'')!==''){
txt=el.textContent.replace(/\r\n/g,' ').replace(/\n/g,' ');
txt=txt.replace(/ {2,}/g,' ');
strList.push(txt);
}
}
}else if(el.nodeType===Node.ELEMENT_NODE){
var elTag=el.tagName.toLowerCase();
if(nowrapList.indexOf(elTag)<0&&strList.length>0&&strList[strList.length-1]!=='\n'){strList.push('\n');}
if(tabList.indexOf(elTag)>=0&&tabList.indexOf(prevEle)>=0){strList.push('\t');}
var sub=getNodeTxt(el,pre);
for(var j=0;j<sub.length;j++){strList.push(sub[j]);}
prevEle=elTag;
}
}
if(wrapAfterList.indexOf(tag)>=0&&strList.length>0&&strList[strList.length-1]!=='\n'&&strList[strList.length-1]!==true){strList.push('\n');}
return strList;
}
var tag=this.tagName?this.tagName.toLowerCase():'';
if(noTextList.indexOf(tag)>=0)return this.innerText||'';
var reStr=getNodeTxt(this,false);
if(reStr.length>0&&reStr[reStr.length-1]==='\n'){reStr.pop();}
var l=reStr.length;
if(l===0)return '';
if(l===1)return reStr[0]===true?'\n':decodeHtml(String(reStr[0]).trim());
var r=[];
for(var i=0;i<l-1;i++){
var i1=reStr[i],i2=reStr[i+1];
if(i1===true){r.push('\n');continue;}
if(i2===true){r.push(i1);continue;}
if(String(i1).slice(-1)===' '&&String(i2).charAt(0)===' '){i1=String(i1).slice(0,-1);}
r.push(i1);
}
r.push(reStr[l-1]===true?'\n':reStr[l-1]);
return decodeHtml(r.join('').trim());}";

        internal const string LocationInViewportTemplate =
            @"function(){var x={0};var y={1};
var scrollLeft=document.documentElement.scrollLeft;
var scrollTop=document.documentElement.scrollTop;
var vWidth=document.documentElement.clientWidth;
var vHeight=document.documentElement.clientHeight;
if(x<scrollLeft||y<scrollTop||x>vWidth+scrollLeft||y>vHeight+scrollTop){return false;}
return true;}";

        internal const string IsDisplayed =
            "return !(window.getComputedStyle(this).visibility==='hidden'||" +
            "window.getComputedStyle(this).display==='none'||this.hidden);";

        internal const string IsEnabled = "return !this.disabled;";

        internal const string IsChecked = "return !!this.checked;";

        internal const string IsSelected = "return !!this.selected;";

        internal const string ClickJs = "this.click();";

        internal const string ClickForNewTabJs =
            "if(this&&this.href){window.open(this.href,this.target||'_blank');}else{this.click();}";

        internal const string ClearValueJs =
            "this.value='';this.dispatchEvent(new Event('change',{bubbles:true}));";

        internal const string SetInnerHtmlTemplate = "this.innerHTML={0};";

        internal const string SetStyleTemplate = "this.style.setProperty({0},{1});";

        internal const string SetAttrTemplate = "this.setAttribute({0},{1});";

        internal const string RemoveAttrTemplate = "this.removeAttribute({0});";

        internal const string SetPropertyTemplate = "this.{0}={1};";

        internal const string FocusJs = "this.focus();";

        internal static string BuildSetValueJs(string serializedValue)
        {
            return "this.value=" + serializedValue + ";this.dispatchEvent(new Event('change',{bubbles:true}));";
        }

        internal static string BuildCheckJs(bool uncheck)
        {
            return "this.checked=" + (uncheck ? "false" : "true") +
                   ";this.dispatchEvent(new Event('change',{bubbles:true}));";
        }
    }
}

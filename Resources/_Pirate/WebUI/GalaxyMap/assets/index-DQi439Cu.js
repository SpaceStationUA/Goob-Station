var Ta=Object.defineProperty;var Ma=(e,n,t)=>n in e?Ta(e,n,{enumerable:!0,configurable:!0,writable:!0,value:t}):e[n]=t;var lt=(e,n,t)=>Ma(e,typeof n!="symbol"?n+"":n,t);(function(){const n=document.createElement("link").relList;if(n&&n.supports&&n.supports("modulepreload"))return;for(const o of document.querySelectorAll('link[rel="modulepreload"]'))a(o);new MutationObserver(o=>{for(const s of o)if(s.type==="childList")for(const i of s.addedNodes)i.tagName==="LINK"&&i.rel==="modulepreload"&&a(i)}).observe(document,{childList:!0,subtree:!0});function t(o){const s={};return o.integrity&&(s.integrity=o.integrity),o.referrerPolicy&&(s.referrerPolicy=o.referrerPolicy),o.crossOrigin==="use-credentials"?s.credentials="include":o.crossOrigin==="anonymous"?s.credentials="omit":s.credentials="same-origin",s}function a(o){if(o.ep)return;o.ep=!0;const s=t(o);fetch(o.href,s)}})();const Ea=!1,La=(e,n)=>e===n,en=Symbol("solid-proxy"),Ca=typeof Proxy=="function",Pa=Symbol("solid-track"),zt={equals:La};let Wn=Kn;const rt=1,Dt=2,Vn={owned:null,cleanups:null,context:null,owner:null};var Ae=null;let Qt=null,Ra=null,ke=null,Pe=null,Xe=null,Kt=0;function It(e,n){const t=ke,a=Ae,o=e.length===0,s=n===void 0?a:n,i=o?Vn:{owned:null,cleanups:null,context:s?s.context:null,owner:s},r=o?e:()=>e(()=>Ze(()=>_t(i)));Ae=i,ke=null;try{return At(r,!0)}finally{ke=t,Ae=a}}function me(e,n){n=n?Object.assign({},zt,n):zt;const t={value:e,observers:null,observerSlots:null,comparator:n.equals||void 0},a=o=>(typeof o=="function"&&(o=o(t.value)),Yn(t,o));return[jn.bind(t),a]}function U(e,n,t){const a=dn(e,n,!1,rt);$t(a)}function it(e,n,t){Wn=Na;const a=dn(e,n,!1,rt);a.user=!0,Xe?Xe.push(a):$t(a)}function pe(e,n,t){t=t?Object.assign({},zt,t):zt;const a=dn(e,n,!0,0);return a.observers=null,a.observerSlots=null,a.comparator=t.equals||void 0,$t(a),jn.bind(a)}function Ze(e){if(ke===null)return e();const n=ke;ke=null;try{return e()}finally{ke=n}}function St(e){it(()=>Ze(e))}function ze(e){return Ae===null||(Ae.cleanups===null?Ae.cleanups=[e]:Ae.cleanups.push(e)),e}function jn(){if(this.sources&&this.state)if(this.state===rt)$t(this);else{const e=Pe;Pe=null,At(()=>Ut(this),!1),Pe=e}if(ke){const e=this.observers;if(!e||e[e.length-1]!==ke){const n=e?e.length:0;ke.sources?(ke.sources.push(this),ke.sourceSlots.push(n)):(ke.sources=[this],ke.sourceSlots=[n]),e?(e.push(ke),this.observerSlots.push(ke.sources.length-1)):(this.observers=[ke],this.observerSlots=[ke.sources.length-1])}}return this.value}function Yn(e,n,t){let a=e.value;return(!e.comparator||!e.comparator(a,n))&&(e.value=n,e.observers&&e.observers.length&&At(()=>{for(let o=0;o<e.observers.length;o+=1){const s=e.observers[o],i=Qt&&Qt.running;i&&Qt.disposed.has(s),(i?!s.tState:!s.state)&&(s.pure?Pe.push(s):Xe.push(s),s.observers&&Xn(s)),i||(s.state=rt)}if(Pe.length>1e6)throw Pe=[],new Error},!1)),n}function $t(e){if(!e.fn)return;_t(e);const n=Kt;Ia(e,e.value,n)}function Ia(e,n,t){let a;const o=Ae,s=ke;ke=Ae=e;try{a=e.fn(n)}catch(i){return e.pure&&(e.state=rt,e.owned&&e.owned.forEach(_t),e.owned=null),e.updatedAt=t+1,Zn(i)}finally{ke=s,Ae=o}(!e.updatedAt||e.updatedAt<=t)&&(e.updatedAt!=null&&"observers"in e?Yn(e,a):e.value=a,e.updatedAt=t)}function dn(e,n,t,a=rt,o){const s={fn:e,state:a,updatedAt:null,owned:null,sources:null,sourceSlots:null,cleanups:null,value:n,owner:Ae,context:Ae?Ae.context:null,pure:t};return Ae===null||Ae!==Vn&&(Ae.owned?Ae.owned.push(s):Ae.owned=[s]),s}function Ft(e){if(e.state===0)return;if(e.state===Dt)return Ut(e);if(e.suspense&&Ze(e.suspense.inFallback))return e.suspense.effects.push(e);const n=[e];for(;(e=e.owner)&&(!e.updatedAt||e.updatedAt<Kt);)e.state&&n.push(e);for(let t=n.length-1;t>=0;t--)if(e=n[t],e.state===rt)$t(e);else if(e.state===Dt){const a=Pe;Pe=null,At(()=>Ut(e,n[0]),!1),Pe=a}}function At(e,n){if(Pe)return e();let t=!1;n||(Pe=[]),Xe?t=!0:Xe=[],Kt++;try{const a=e();return Oa(t),a}catch(a){t||(Xe=null),Pe=null,Zn(a)}}function Oa(e){if(Pe&&(Kn(Pe),Pe=null),e)return;const n=Xe;Xe=null,n.length&&At(()=>Wn(n),!1)}function Kn(e){for(let n=0;n<e.length;n++)Ft(e[n])}function Na(e){let n,t=0;for(n=0;n<e.length;n++){const a=e[n];a.user?e[t++]=a:Ft(a)}for(n=0;n<t;n++)Ft(e[n])}function Ut(e,n){e.state=0;for(let t=0;t<e.sources.length;t+=1){const a=e.sources[t];if(a.sources){const o=a.state;o===rt?a!==n&&(!a.updatedAt||a.updatedAt<Kt)&&Ft(a):o===Dt&&Ut(a,n)}}}function Xn(e){for(let n=0;n<e.observers.length;n+=1){const t=e.observers[n];t.state||(t.state=Dt,t.pure?Pe.push(t):Xe.push(t),t.observers&&Xn(t))}}function _t(e){let n;if(e.sources)for(;e.sources.length;){const t=e.sources.pop(),a=e.sourceSlots.pop(),o=t.observers;if(o&&o.length){const s=o.pop(),i=t.observerSlots.pop();a<o.length&&(s.sourceSlots[i]=a,o[a]=s,t.observerSlots[a]=i)}}if(e.tOwned){for(n=e.tOwned.length-1;n>=0;n--)_t(e.tOwned[n]);delete e.tOwned}if(e.owned){for(n=e.owned.length-1;n>=0;n--)_t(e.owned[n]);e.owned=null}if(e.cleanups){for(n=e.cleanups.length-1;n>=0;n--)e.cleanups[n]();e.cleanups=null}e.state=0}function za(e){return e instanceof Error?e:new Error(typeof e=="string"?e:"Unknown error",{cause:e})}function Zn(e,n=Ae){throw za(e)}const Da=Symbol("fallback");function pn(e){for(let n=0;n<e.length;n++)e[n]()}function Fa(e,n,t={}){let a=[],o=[],s=[],i=0,r=n.length>1?[]:null;return ze(()=>pn(s)),()=>{let l=e()||[],u=l.length,d,c;return l[Pa],Ze(()=>{let b,w,m,k,M,S,A,I,$;if(u===0)i!==0&&(pn(s),s=[],a=[],o=[],i=0,r&&(r=[])),t.fallback&&(a=[Da],o[0]=It(Y=>(s[0]=Y,t.fallback())),i=1);else if(i===0){for(o=new Array(u),c=0;c<u;c++)a[c]=l[c],o[c]=It(f);i=u}else{for(m=new Array(u),k=new Array(u),r&&(M=new Array(u)),S=0,A=Math.min(i,u);S<A&&a[S]===l[S];S++);for(A=i-1,I=u-1;A>=S&&I>=S&&a[A]===l[I];A--,I--)m[I]=o[A],k[I]=s[A],r&&(M[I]=r[A]);for(b=new Map,w=new Array(I+1),c=I;c>=S;c--)$=l[c],d=b.get($),w[c]=d===void 0?-1:d,b.set($,c);for(d=S;d<=A;d++)$=a[d],c=b.get($),c!==void 0&&c!==-1?(m[c]=o[d],k[c]=s[d],r&&(M[c]=r[d]),c=w[c],b.set($,c)):s[d]();for(c=S;c<u;c++)c in m?(o[c]=m[c],s[c]=k[c],r&&(r[c]=M[c],r[c](c))):o[c]=It(f);o=o.slice(0,i=u),a=l.slice(0)}return o});function f(b){if(s[c]=b,r){const[w,m]=me(c);return r[c]=m,n(l[c],w)}return n(l[c])}}}function N(e,n){return Ze(()=>e(n||{}))}function Mt(){return!0}const Ua={get(e,n,t){return n===en?t:e.get(n)},has(e,n){return n===en?!0:e.has(n)},set:Mt,deleteProperty:Mt,getOwnPropertyDescriptor(e,n){return{configurable:!0,enumerable:!0,get(){return e.get(n)},set:Mt,deleteProperty:Mt}},ownKeys(e){return e.keys()}};function Jt(e){return(e=typeof e=="function"?e():e)?e:{}}function qa(){for(let e=0,n=this.length;e<n;++e){const t=this[e]();if(t!==void 0)return t}}function bn(...e){let n=!1;for(let i=0;i<e.length;i++){const r=e[i];n=n||!!r&&en in r,e[i]=typeof r=="function"?(n=!0,pe(r)):r}if(Ca&&n)return new Proxy({get(i){for(let r=e.length-1;r>=0;r--){const l=Jt(e[r])[i];if(l!==void 0)return l}},has(i){for(let r=e.length-1;r>=0;r--)if(i in Jt(e[r]))return!0;return!1},keys(){const i=[];for(let r=0;r<e.length;r++)i.push(...Object.keys(Jt(e[r])));return[...new Set(i)]}},Ua);const t={},a=Object.create(null);for(let i=e.length-1;i>=0;i--){const r=e[i];if(!r)continue;const l=Object.getOwnPropertyNames(r);for(let u=l.length-1;u>=0;u--){const d=l[u];if(d==="__proto__"||d==="constructor")continue;const c=Object.getOwnPropertyDescriptor(r,d);if(!a[d])a[d]=c.get?{enumerable:!0,configurable:!0,get:qa.bind(t[d]=[c.get.bind(r)])}:c.value!==void 0?c:void 0;else{const f=t[d];f&&(c.get?f.push(c.get.bind(r)):c.value!==void 0&&f.push(()=>c.value))}}}const o={},s=Object.keys(a);for(let i=s.length-1;i>=0;i--){const r=s[i],l=a[r];l&&l.get?Object.defineProperty(o,r,l):o[r]=l?l.value:void 0}return o}const Ba=e=>`Stale read from <${e}>.`;function $e(e){const n="fallback"in e&&{fallback:()=>e.fallback};return pe(Fa(()=>e.each,e.children,n||void 0))}function te(e){const n=e.keyed,t=pe(()=>e.when,void 0,void 0),a=n?t:pe(t,void 0,{equals:(o,s)=>!o==!s});return pe(()=>{const o=a();if(o){const s=e.children;return typeof s=="function"&&s.length>0?Ze(()=>s(n?o:()=>{if(!Ze(a))throw Ba("Show");return t()})):s}return e.fallback},void 0,void 0)}const Ga=new Set(["innerHTML","textContent","innerText","children"]),Ha=Object.assign(Object.create(null),{className:"class",htmlFor:"for"}),Wa=new Set(["beforeinput","click","dblclick","contextmenu","focusin","focusout","input","keydown","keyup","mousedown","mousemove","mouseout","mouseover","mouseup","pointerdown","pointermove","pointerout","pointerover","pointerup","touchend","touchmove","touchstart"]),Va={xlink:"http://www.w3.org/1999/xlink",xml:"http://www.w3.org/XML/1998/namespace"},Ye=e=>pe(()=>e());function ja(e,n,t){let a=t.length,o=n.length,s=a,i=0,r=0,l=n[o-1].nextSibling,u=null;for(;i<o||r<s;){if(n[i]===t[r]){i++,r++;continue}for(;n[o-1]===t[s-1];)o--,s--;if(o===i){const d=s<a?r?t[r-1].nextSibling:t[s-r]:l;for(;r<s;)e.insertBefore(t[r++],d)}else if(s===r)for(;i<o;)(!u||!u.has(n[i]))&&n[i].remove(),i++;else if(n[i]===t[s-1]&&t[r]===n[o-1]){const d=n[--o].nextSibling;e.insertBefore(t[r++],n[i++].nextSibling),e.insertBefore(t[--s],d),n[o]=t[s]}else{if(!u){u=new Map;let c=r;for(;c<s;)u.set(t[c],c++)}const d=u.get(n[i]);if(d!=null)if(r<d&&d<s){let c=i,f=1,b;for(;++c<o&&c<s&&!((b=u.get(n[c]))==null||b!==d+f);)f++;if(f>d-r){const w=n[i];for(;r<d;)e.insertBefore(t[r++],w)}else e.replaceChild(t[r++],n[i++])}else i++;else n[i++].remove()}}}const xn="_$DX_DELEGATE";function Ya(e,n,t,a={}){let o;return It(s=>{o=s,n===document?e():R(n,e(),n.firstChild?null:void 0,t)},a.owner),()=>{o(),n.textContent=""}}function L(e,n,t,a){let o;const s=()=>{const r=a?document.createElementNS("http://www.w3.org/1998/Math/MathML","template"):document.createElement("template");return r.innerHTML=e,t?r.content.firstChild.firstChild:a?r.firstChild:r.content.firstChild},i=n?()=>Ze(()=>document.importNode(o||(o=s()),!0)):()=>(o||(o=s())).cloneNode(!0);return i.cloneNode=i,i}function un(e,n=window.document){const t=n[xn]||(n[xn]=new Set);for(let a=0,o=e.length;a<o;a++){const s=e[a];t.has(s)||(t.add(s),n.addEventListener(s,no))}}function h(e,n,t){t==null?e.removeAttribute(n):e.setAttribute(n,t)}function Ka(e,n,t,a){a==null?e.removeAttributeNS(n,t):e.setAttributeNS(n,t,a)}function Xa(e,n,t){t?e.setAttribute(n,""):e.removeAttribute(n)}function Za(e,n){n==null?e.removeAttribute("class"):e.className=n}function Qa(e,n,t,a){if(a)Array.isArray(t)?(e[`$$${n}`]=t[0],e[`$$${n}Data`]=t[1]):e[`$$${n}`]=t;else if(Array.isArray(t)){const o=t[0];e.addEventListener(n,t[0]=s=>o.call(e,t[1],s))}else e.addEventListener(n,t,typeof t!="function"&&t)}function Ja(e,n,t={}){const a=Object.keys(n||{}),o=Object.keys(t);let s,i;for(s=0,i=o.length;s<i;s++){const r=o[s];!r||r==="undefined"||n[r]||(_n(e,r,!1),delete t[r])}for(s=0,i=a.length;s<i;s++){const r=a[s],l=!!n[r];!r||r==="undefined"||t[r]===l||!l||(_n(e,r,!0),t[r]=l)}return t}function Qn(e,n,t){if(!n)return t?h(e,"style"):n;const a=e.style;if(typeof n=="string")return a.cssText=n;typeof t=="string"&&(a.cssText=t=void 0),t||(t={}),n||(n={});let o,s;for(s in t)n[s]==null&&a.removeProperty(s),delete t[s];for(s in n)o=n[s],o!==t[s]&&(a.setProperty(s,o),t[s]=o);return t}function fe(e,n,t){t!=null?e.style.setProperty(n,t):e.style.removeProperty(n)}function wn(e,n={},t,a){const o={};return U(()=>o.children=kt(e,n.children,o.children)),U(()=>typeof n.ref=="function"&&Qe(n.ref,e)),U(()=>eo(e,n,t,!0,o,!0)),o}function Qe(e,n,t){return Ze(()=>e(n,t))}function R(e,n,t,a){if(t!==void 0&&!a&&(a=[]),typeof n!="function")return kt(e,n,a,t);U(o=>kt(e,n(),o,t),a)}function eo(e,n,t,a,o={},s=!1){n||(n={});for(const i in o)if(!(i in n)){if(i==="children")continue;o[i]=kn(e,i,null,o[i],t,s,n)}for(const i in n){if(i==="children")continue;const r=n[i];o[i]=kn(e,i,r,o[i],t,s,n)}}function to(e){return e.toLowerCase().replace(/-([a-z])/g,(n,t)=>t.toUpperCase())}function _n(e,n,t){const a=n.trim().split(/\s+/);for(let o=0,s=a.length;o<s;o++)e.classList.toggle(a[o],t)}function kn(e,n,t,a,o,s,i){let r,l,u,d;if(n==="style")return Qn(e,t,a);if(n==="classList")return Ja(e,t,a);if(t===a)return a;if(n==="ref")s||t(e);else if(n.slice(0,3)==="on:"){const c=n.slice(3);a&&e.removeEventListener(c,a,typeof a!="function"&&a),t&&e.addEventListener(c,t,typeof t!="function"&&t)}else if(n.slice(0,10)==="oncapture:"){const c=n.slice(10);a&&e.removeEventListener(c,a,!0),t&&e.addEventListener(c,t,!0)}else if(n.slice(0,2)==="on"){const c=n.slice(2).toLowerCase(),f=Wa.has(c);if(!f&&a){const b=Array.isArray(a)?a[0]:a;e.removeEventListener(c,b)}(f||t)&&(Qa(e,c,t,f),f&&un([c]))}else if(n.slice(0,5)==="attr:")h(e,n.slice(5),t);else if(n.slice(0,5)==="bool:")Xa(e,n.slice(5),t);else if((d=n.slice(0,5)==="prop:")||(u=Ga.has(n))||(r=e.nodeName.includes("-")||"is"in i))d&&(n=n.slice(5),l=!0),n==="class"||n==="className"?Za(e,t):r&&!l&&!u?e[to(n)]=t:(n==="value"||n==="defaultValue")&&(e.nodeName==="INPUT"||e.nodeName==="TEXTAREA")?e[n]=t??"":e[n]=t;else{const c=n.indexOf(":")>-1&&Va[n.split(":")[0]];c?Ka(e,c,n,t):h(e,Ha[n]||n,t)}return t}function no(e){let n=e.target;const t=`$$${e.type}`,a=e.target,o=e.currentTarget,s=l=>Object.defineProperty(e,"target",{configurable:!0,value:l}),i=()=>{const l=n[t];if(l&&!n.disabled){const u=n[`${t}Data`];if(u!==void 0?l.call(n,u,e):l.call(n,e),e.cancelBubble)return}return n.host&&typeof n.host!="string"&&!n.host._$host&&n.contains(e.target)&&s(n.host),!0},r=()=>{for(;i()&&(n=n._$host||n.parentNode||n.host););};if(Object.defineProperty(e,"currentTarget",{configurable:!0,get(){return n||document}}),e.composedPath){const l=e.composedPath();s(l[0]);for(let u=0;u<l.length-2&&(n=l[u],!!i());u++){if(n._$host){n=n._$host,r();break}if(n.parentNode===o)break}}else r();s(a)}function kt(e,n,t,a,o){for(;typeof t=="function";)t=t();if(n===t)return t;const s=typeof n,i=a!==void 0;if(e=i&&t[0]&&t[0].parentNode||e,s==="string"||s==="number"){if(s==="number"&&(n=n.toString(),n===t))return t;if(i){let r=t[0];r&&r.nodeType===3?r.data!==n&&(r.data=n):r=document.createTextNode(n),t=vt(e,t,a,r)}else t!==""&&typeof t=="string"?t=e.firstChild.data=n:t=e.textContent=n}else if(n==null||s==="boolean")t=vt(e,t,a);else{if(s==="function")return U(()=>{let r=n();for(;typeof r=="function";)r=r();t=kt(e,r,t,a)}),()=>t;if(Array.isArray(n)){const r=[],l=t&&Array.isArray(t);if(tn(r,n,t,o))return U(()=>t=kt(e,r,t,a,!0)),()=>t;if(r.length===0){if(t=vt(e,t,a),i)return t}else l?t.length===0?Sn(e,r,a):ja(e,t,r):(t&&vt(e),Sn(e,r));t=r}else if(n.nodeType){if(Array.isArray(t)){if(i)return t=vt(e,t,a,n);vt(e,t,null,n)}else t==null||t===""||!e.firstChild?e.appendChild(n):e.replaceChild(n,e.firstChild);t=n}}return t}function tn(e,n,t,a){let o=!1;for(let s=0,i=n.length;s<i;s++){let r=n[s],l=t&&t[e.length],u;if(!(r==null||r===!0||r===!1))if((u=typeof r)=="object"&&r.nodeType)e.push(r);else if(Array.isArray(r))o=tn(e,r,l)||o;else if(u==="function")if(a){for(;typeof r=="function";)r=r();o=tn(e,Array.isArray(r)?r:[r],Array.isArray(l)?l:[l])||o}else e.push(r),o=!0;else{const d=String(r);l&&l.nodeType===3&&l.data===d?e.push(l):e.push(document.createTextNode(d))}}return o}function Sn(e,n,t=null){for(let a=0,o=n.length;a<o;a++)e.insertBefore(n[a],t)}function vt(e,n,t,a){if(t===void 0)return e.textContent="";const o=a||document.createTextNode("");if(n.length){let s=!1;for(let i=n.length-1;i>=0;i--){const r=n[i];if(o!==r){const l=r.parentNode===e;!s&&!i?l?e.replaceChild(o,r):e.insertBefore(o,t):l&&r.remove()}else s=!0}}else e.insertBefore(o,t);return[o]}function ut(e,n){return`${e},${n}`}const ao=[{q:1,r:0},{q:1,r:-1},{q:0,r:-1},{q:-1,r:0},{q:-1,r:1},{q:0,r:1}],oo=[0,5,4,3,2,1];function io(e,n){const t=ao[oo[n]];return{q:e.q+t.q,r:e.r+t.r}}function at(e,n){return{x:n*Math.sqrt(3)*(e.q+e.r/2),y:n*1.5*e.r}}function wt(e,n){const t=[];for(let a=0;a<6;a++){const o=Math.PI/180*(60*a-30);t.push({x:e.x+n*Math.cos(o),y:e.y+n*Math.sin(o)})}return t}function Jn(e,n,t){let a=Math.round(e),o=Math.round(n),s=Math.round(t);const i=Math.abs(a-e),r=Math.abs(o-n),l=Math.abs(s-t);return i>r&&i>l?a=-o-s:r>l?o=-a-s:s=-a-o,{q:a,r:s}}function Et(e,n){const t=(Math.sqrt(3)/3*e.x-.3333333333333333*e.y)/n,a=2/3*e.y/n;return Jn(t,-t-a,a)}function ro(e,n){const t=e.q-n.q,a=e.r-n.r;return(Math.abs(t)+Math.abs(t+a)+Math.abs(a))/2}function so(e,n){const t=ro(e,n);if(t===0)return[e];const a=e.q,o=e.r,s=-a-o,i=n.q,r=n.r,l=-i-r,u=[];for(let d=0;d<=t;d++){const c=d/t;u.push(Jn(a+(i-a)*c+1e-6,s+(l-s)*c+2e-6,o+(r-o)*c-3e-6))}return u}function hn(e,n,t){const a=[],o=Math.floor(-(n/2)/(1.5*t))-1,s=Math.ceil(n/2/(1.5*t))+1;for(let i=o;i<=s;i++){const r=t*Math.sqrt(3)*i/2,l=Math.floor((-e/2-r)/(t*Math.sqrt(3)))-1,u=Math.ceil((e/2-r)/(t*Math.sqrt(3)))+1;for(let d=l;d<=u;d++){const c=at({q:d,r:i},t);c.x>=-e/2&&c.x<=e/2&&c.y>=-n/2&&c.y<=n/2&&a.push({q:d,r:i})}}return a}function lo(e,n){let t=!1;for(let a=0,o=n.length-1;a<n.length;o=a++){const s=n[a],i=n[o];s.y>e.y!=i.y>e.y&&e.x<(i.x-s.x)*(e.y-s.y)/(i.y-s.y)+s.x&&(t=!t)}return t}function co(e,n){let t=1/0;for(let a=0,o=n.length-1;a<n.length;o=a++){const s=n[a],i=n[o],r=i.x-s.x,l=i.y-s.y,u=r*r+l*l;let d=u>0?((e.x-s.x)*r+(e.y-s.y)*l)/u:0;d=Math.max(0,Math.min(1,d)),t=Math.min(t,Math.hypot(s.x+r*d-e.x,s.y+l*d-e.y))}return t}function uo(e,n,t,a=""){const o=new Map,s=[];let i=0;for(const r of n){const l=at(r,t),u=ut(r.q,r.r),d=[];for(const b of e)lo(l,b.polygon)&&d.push(b);if(d.length===0){o.set(u,a),i++;continue}if(d.length===1){o.set(u,d[0].id);continue}s.push({cell:r,claimants:d.map(b=>b.id)});let c=d[0],f=-1;for(const b of d){const w=co(l,b.polygon);w>f&&(f=w,c=b)}o.set(u,c.id)}return{ownership:o,contested:s,unclaimedCells:i}}const $n=1e4,nt=e=>`${Math.round(e.x*$n)},${Math.round(e.y*$n)}`;function ho(e,n){const t=new Map,a=(i,r)=>{const l=nt(i),u=t.get(l);u?u.push(r):t.set(l,[r])};for(const i of e)a(i.a,i),a(i.b,i);const o=new Set,s=[];for(const i of e){if(o.has(i))continue;const r=i.a,l=[r],u=[];let d=r,c=i;for(let f=0;f<=e.length&&(o.add(c),u.push(c),d=nt(c.a)===nt(d)?c.b:c.a,l.push(d),nt(d)!==nt(r));f++){const b=(t.get(nt(d))??[]).filter(m=>!o.has(m));if(b.length===0)break;let w;w=b[0],c=w}l.length>=2&&s.push({loop:l,edges:u,closed:nt(l[l.length-1])===nt(r)})}return s}const fo={roundLy:.34,arcSamples:4,wobbleLy:.3,wobbleStepLy:.5,wobbleWavelengthLy:9,seed:1337};function vo(e,n,t=fo){return ho(mo(e,n)).map(a=>{const o=t.roundLy>0?go(a.loop,t.roundLy,t.arcSamples):a.loop;return t.wobbleLy>0?yo(o,t):o})}function go(e,n,t){const a=e.length;if(a<3)return e.slice();const o=[];for(let s=0;s<a;s++){const i=e[(s-1+a)%a],r=e[s],l=e[(s+1)%a],u=Math.hypot(r.x-i.x,r.y-i.y),d=Math.hypot(l.x-r.x,l.y-r.y),c=Math.min(n,u/2,d/2);if(c<=1e-4){o.push(r);continue}const f={x:r.x+(i.x-r.x)/u*c,y:r.y+(i.y-r.y)/u*c},b={x:r.x+(l.x-r.x)/d*c,y:r.y+(l.y-r.y)/d*c};o.push(f);for(let w=1;w<t;w++){const m=w/t,k=1-m;o.push({x:k*k*f.x+2*k*m*r.x+m*m*b.x,y:k*k*f.y+2*k*m*r.y+m*m*b.y})}o.push(b)}return o}function mo(e,n){const t=[];for(const a of e){const[o,s]=a.split(","),i={q:+o,r:+s},r=wt(at(i,n),n);for(let l=0;l<6;l++){const u=io(i,l);e.has(ut(u.q,u.r))||t.push({a:r[l],b:r[(l+1)%6]})}}return t}function An(e,n){let t=e*374761393+n*668265263|0;return t=Math.imul(t^t>>>13,1274126177),t^=t>>>16,(t>>>0)/4294967295*2-1}function Tn(e,n){const t=Math.floor(e),a=e-t,o=a*a*(3-2*a);return An(t,n)*(1-o)+An(t+1,n)*o}function yo(e,n){const t=[],a=e.length,o=Math.max(.001,n.wobbleWavelengthLy);let s=0;for(let i=0;i<a;i++){const r=e[i],l=e[(i+1)%a];t.push(r);const u=l.x-r.x,d=l.y-r.y,c=Math.hypot(u,d),f=Math.max(1,Math.round(c/n.wobbleStepLy));if(f===1)continue;const b=-d/c,w=u/c;for(let m=1;m<f;m++){const k=m/f,M=s+c*k,S=(Tn(M/o,n.seed)+.4*Tn(M/(o/3.5),n.seed+91))*n.wobbleLy;t.push({x:r.x+u*k+b*S,y:r.y+d*k+w*S})}s+=c}return t}const Lt=e=>Math.abs(e)<1e-4?"0":e.toFixed(3);function po(e,n=!0){if(e.length<2)return"";const t=`M${Lt(e[0].x)},${Lt(e[0].y)}`,a=e.slice(1).map(o=>`L${Lt(o.x)},${Lt(o.y)}`).join("");return t+a+(n&&e.length>2?"Z":"")}function qt(e){const n=new Map;for(const[t,a]of e){const o=n.get(a);o?o.push(t):n.set(a,[t])}return n}function Ke(e,n,t,a){const o=(e%t+t)%t,s=(n%t+t)%t;let i=Math.imul(o,668265261)^Math.imul(s,374761393)^Math.imul(a|0,2654435769);return i=Math.imul(i^i>>>15,2246822507),i^=i>>>13,i=Math.imul(i,3266489909),i^=i>>>16,(i>>>0)/4294967296}function bo(e,n,t,a){const o=Math.floor(e),s=Math.floor(n),i=e-o,r=n-s,l=i*i*(3-2*i),u=r*r*(3-2*r),d=Ke(o,s,t,a),c=Ke(o+1,s,t,a),f=Ke(o,s+1,t,a),b=Ke(o+1,s+1,t,a);return d+(c-d)*l+(f-d)*u+(d-c-f+b)*l*u}function Ce(e,n,t,a,o){let s=0,i=.5,r=t,l=e,u=n;for(let d=0;d<a;d++)s+=bo(l,u,r,o+d*1013)*i,l*=2,u*=2,r*=2,i*=.5;return s}function Mn(e,n,t,a){const o=e*t,s=n*t,i=Math.floor(o),r=Math.floor(s);let l=1/0;for(let u=-1;u<=1;u++)for(let d=-1;d<=1;d++){const c=i+u,f=r+d,b=c+Ke(c,f,t,a),w=f+Ke(c,f,t,a+7919),m=b-o,k=w-s,M=m*m+k*k;M<l&&(l=M)}return Math.sqrt(l)}function Bt(e,n,t,a){const o=Math.floor(n),s=e+o*.31,i=s-Math.floor(s),r=n-o,l=Ke(Math.floor(s),o,t,a),u=Math.hypot(i-.25-l*.5,r-.25-l*.5),d=l*.25;return d<=0?1:dt(u*.75/d)}function En(e,n,t,a){const o=Bt(e,n,t,a),s=Bt(e+11.37,n+11.37,t,a);return 1-o*s}function We(e){const n=e.replace("#",""),t=parseInt(n.length===3?n.replace(/./g,a=>a+a):n,16);return[t>>16&255,t>>8&255,t&255]}function Me(e,n,t){return[e[0]+(n[0]-e[0])*t,e[1]+(n[1]-e[1])*t,e[2]+(n[2]-e[2])*t]}function Ln(e,n,t){const a=A=>{const I=A[0]/255,$=A[1]/255,Y=A[2]/255,B=Math.max(I,$,Y),ne=Math.min(I,$,Y),ye=(B+ne)/2;if(B===ne)return[0,0,ye];const C=B-ne,P=ye>.5?C/(2-B-ne):C/(B+ne);let y;return B===I?y=(($-Y)/C+($<Y?6:0))/6:B===$?y=((Y-I)/C+2)/6:y=((I-$)/C+4)/6,[y,P,ye]},[o,s,i]=a(e),[r,l,u]=a(n);let d=r-o;d>.5&&(d-=1),d<-.5&&(d+=1);const c=(o+d*t+1)%1*360,f=s+(l-s)*t,b=i+(u-i)*t,w=(1-Math.abs(2*b-1))*f,m=w*(1-Math.abs(c/60%2-1)),k=b-w/2,M=Math.floor(c/60)%6,S=[[w,m,0][M]??0,[m,w,0][(M+4)%6]??0,[0,m,w][(M+2)%6]??0];return[(S[0]+k)*255,(S[1]+k)*255,(S[2]+k)*255]}const nn=Math.PI*2,xo=[0,8,2,10,12,4,14,6,3,11,1,9,15,7,13,5].map(e=>e/16-.5);function dt(e){const n=Math.max(0,Math.min(1,e));return n*n*(3-2*n)}function yt(e){return e-Math.floor(e)}const wo={star:{kind:"star",cutoff:0,emissive:!1,atmo:"#ffc46a",sea:["#c2470d","#e8801f","#ffc463","#fff3d4"],land:null,cloud:0},terran:{kind:"terrain",cutoff:.5,emissive:!1,atmo:"#7cc0ee",sea:["#2f6796","#22507c","#173a5e","#102845"],land:["#7ba055","#63903f","#527a35","#44652c"],water:["#2a6f8e","#3f9ec4"],cloud:.56},river:{kind:"terrain",cutoff:.3,emissive:!1,atmo:"#8fd0d8",sea:["#1d4a52","#16383f","#102a30","#0a1c21"],land:["#63ab3f","#3b7d4f","#2f5753","#283540"],water:["#4fa4b8","#404973"],riverCutoff:.3,riverOctaves:6,cloud:.5},ocean:{kind:"terrain",cutoff:.62,emissive:!1,atmo:"#7cc0ee",sea:["#2a6a9c","#1c4d7c","#10335a","#0a2340"],land:["#86ab6a","#6b9154","#557841","#44612f"],water:["#1f7fa0","#38b4d6"],cloud:.58},desert:{kind:"terrain",cutoff:.04,emissive:!1,atmo:"#e8b878",sea:["#c9a066","#a87c46","#7d5a31","#553c21"],land:["#e8cf9a","#dcbd80","#d0ab68","#c29952"],cloud:.72},ice:{kind:"terrain",cutoff:.34,emissive:!1,atmo:"#bfe4ff",sea:["#7fa8cd","#5c86ad","#3f6288","#2c4562"],land:["#e8f4fc","#d6e9f6","#c4dcee","#b2cfe4"],cloud:.56,lakes:!0},gas:{kind:"lat",cutoff:.5,emissive:!1,atmo:"#f0d8a8",sea:["#f8eecd","#e0b87c","#bd8a4e","#8a5c33"],land:null,cloud:0},lava:{kind:"terrain",cutoff:.58,emissive:!0,atmo:"#ff7a2a",sea:["#6e2a12","#4a1c0e","#2a1109","#180905"],land:["#ffb457","#e8681f","#8a3312","#4a1a0c"],cloud:0,craters:!0},barren:{kind:"terrain",cutoff:.08,emissive:!1,atmo:null,sea:["#9a9aa6","#6e6e7a","#4a4a54","#30303a"],land:["#c6c6d0","#adadb9","#9494a2","#7b7b8b"],cloud:0,craters:!0},asteroid:{kind:"solid",cutoff:.5,emissive:!1,atmo:null,sea:["#7b7166","#574f46","#3a342d","#241f1b"],land:["#8d8376","#786f63","#635b50","#4e473e"],cloud:0,craters:!0}},Cn=["terran","ocean","river","desert","ice","gas","lava","barren"],_o=12;function ko(e,n,t){return Ce(e*7+3.1,n*2.2+7.7,64,3,t+131)*1.6-.62}function Pn(e,n,t,a){const o=ko(e,n,t);return e>o?0:e<=o-.05?1:a>.5?.45:0}function ea(e,n,t){const a=wo[e.type],o=a.kind==="star",s=o?1.5:a.kind==="solid"?1.3:1,i=e.tint?We(e.tint):null,r=e.tintAmount??0,l=n<28,u=e.light??-2.2,d=.21,c=.5+Math.cos(u)*d,f=.5+Math.sin(u)*d;return{d:n,rPx:n/2/s,glow:s,isStar:o,kind:a.kind,cutoff:a.cutoff,emissive:a.emissive,sea:a.sea.map(b=>{const w=We(b);return i?Ln(w,i,r):w}),land:a.land?a.land.map(b=>{const w=We(b);return i?Ln(w,i,r):w}):null,riverCutoff:a.riverCutoff,riverOctaves:a.riverOctaves,water:a.water?[We(a.water[0]),We(a.water[1])]:null,craters:a.craters===!0&&!e.suppressCraters,lakes:a.lakes===!0&&!e.suppressLakes,craterFreq:Math.max(3,Math.min(7,Math.round(n/30))),atmo:a.atmo?We(a.atmo):null,nightFloor:l?.42:.1,night:l?[40,52,80]:[8,11,22],cloud:o?0:t??a.cloud,dither:e.dither!==!1,period:3,octaves:Math.max(2,Math.min(6,Math.floor(Math.log2(n*2/3))+1)),seed:(e.seed|0)^40503,rot:Ke(e.seed|0,7,64,13)*Math.PI*2,lx:c,ly:f}}function ta(e,n,t,a,o,s){const{d:i,rPx:r,glow:l,sea:u,land:d,water:c,atmo:f,night:b,nightFloor:w,period:m,octaves:k,seed:M,kind:S}=e,A=e.riverCutoff,I=e.riverOctaves??k,$=n.data,Y=.09;for(let B=0;B<i;B++)for(let ne=0;ne<i;ne++){const ye=((B+o)*t+(ne+a))*4,C=(ne+.5-i/2)/r,P=(B+.5-i/2)/r,y=C*C+P*P;if(y>l*l)continue;if(y>1){if(!e.isStar)continue;const T=1-(y-1)/(l*l-1);$[ye]=f?f[0]:255,$[ye+1]=f?f[1]:210,$[ye+2]=f?f[2]:140;const x=Pn(Math.sqrt(y),Math.atan2(P,C)*.4,M,1);$[ye+3]=Math.round(Math.min(1,T*T*T*.85*(1+x*.55))*255);continue}const G=C,Q=P,re=1/(Math.sqrt(1-y)+1),se=G*re*.5+.5,oe=Q*re*.5+.5,ae=yt(se+s+e.rot),de=e.dither?xo[(B&3)*4+(ne&3)]+.5:.5,be=1-dt((Math.abs(Q)-.7)/.3);let H,ue,q=1,Le=1,we=!1,v=1;if(e.isStar){const T=1-y;T>.7?H=Me(u[0],[255,255,255],(T-.7)/.3):T>.4?H=Me(u[1],u[0],(T-.4)/.3):T>.14?H=Me(u[2],u[1],(T-.14)/.26):H=Me(u[3],u[2],T/.14);const x=Mn(ae,oe,14,M+31),p=Mn(ae,oe,30,M+57);let g=Math.min(1,x*p*2.6);g=g<.3?g*(.375/.3):g<.62?.375+(g-.3)*(.25/.32):.625+(g-.62)*(.375/.38),g+=(p-.45)*.12,de>.5&&(g+=.1),H=u[Math.max(0,Math.min(3,Math.floor(g*4)))];const _=1-y;if(_>.88?H=Me(H,u[3],.5):_>.8&&de>.5?H=Me(H,u[3],.25):H=Me(u[0],H,.45),ue=H,y>.2){const O=Pn(Math.sqrt(y),Math.atan2(Q,G)*.4,M,de);O>0&&(ue=Me(ue,[255,233,186],O*.45))}}else{const T=.5+G*.5,x=.5+Q*.5;q=Math.hypot(T-e.lx,x-e.ly)+(Ce(ae*m,oe*m,m,Math.max(2,k-2),M+404)-.5)*.55*be;const g=q*q*.62,_=.055;H=u[0],g>.085&&(H=u[1]),g>.085&&g<.085+_&&de>.5&&(H=u[0]),g>.2&&(H=u[2]),g>.2&&g<.2+_&&de>.5&&(H=u[1]),g>.4&&(H=u[3]),g>.4&&g<.4+_&&de>.5&&(H=u[2]);const O=H,D=d?.5+(Ce(ae*m,oe*m,m,k,M)-.5)*be:0;if(d&&D>=e.cutoff){we=!0;const z=e.lx-.5,j=e.ly-.5,J=D*be,_e=Ce(ae*m-z*J,oe*m-j*J,m,k,M+101),Fe=Ce(ae*m-z*J*1.5,oe*m-j*J*1.5,m,k,M+211),Te=Ce(ae*m-z*J*2.2,oe*m-j*J*2.2,m,k,M+307);if(H=d[3],Te+g<D&&(H=d[2]),Fe+g<D&&(H=d[1]),_e+g<D&&(H=d[0]),c){const Ue=Ce(ae*m+D*6,oe*m+D*6,m,I,M+503),qe=A,Ve=qe??D*.5,Je=qe!==void 0?qe*1.12:D*.56;Ue<Ve?H=c[0]:Ue<Je&&(H=c[1])}e.emissive&&_e+g<D*.7&&(H=d[0])}if(e.lakes&&we){const z=.5+(Ce(ae*m*2.4,oe*m*2.4,m,Math.max(2,k-1),M+733)-.5)*be;(z<.44||z<.4&&de>.5)&&(we=!1,H=O)}if(S==="lat"){const z=yt(se+s),j=oe*1.6+dt(Math.abs(se-.4)/1.3)*.3;let J=0;for(let V=0;V<9;V++)J+=Bt(z*m*.5+V+11,j*m*.5+V+11,m,M+5);J/=9;const _e=.5+(Ce(z*m+J*3,j*m+J*3,m,k,M+313)-.5)*be,Fe=Ce(0,oe*9,m,3,M+401),Te=9,Ue=[0,1,2,1,0,1,2,1,0],qe=oe*Te+(_e-.5)*.8*(.15+Fe*1.2),Ve=Math.floor(qe),Je=qe-Ve,pt=(Ve%Te+Te)%Te;let E=Ue[pt];Je<.09&&de>.5?E=Ue[(pt+Te-1)%Te]:Je>.91&&de<.5&&(E=Ue[(pt+1)%Te]);const F=Ce(z*m*.9,j*m*.5,m,Math.max(2,k-2),M+907)-.5;F>.17?E=Math.min(3,E+1):F<-.17&&(E=Math.max(0,E-1)),H=u[E]}else if(S==="solid"){const z=T+s+e.rot,j=.5+(Ce(z*2.2,x*2.2,3,2,M+907)-.5)*.6,J=.02+Math.sqrt(y)*.5;v=j>J||j>J-.05&&de>.5?1:0;const _e=Ce(z*5,x*5,5,3,M+907),Te=Ce(z*5+(e.lx-.5)*1.1,x*5+(e.ly-.5)*1.1,5,3,M+907)-_e;H=u[1],Te<-.025?H=u[0]:Te<-.008?H=de>.5?u[0]:u[1]:Te>.008&&(H=u[2]),Le=g<.085?1:g<.2?.82:g<.4?.6:.4,ue=Me(u[3],H,.36+.64*Le)}if(e.craters){const z=e.craterFreq,j=En(ae*z,oe*z,z,M+611),J=En(ae*z+(e.lx-.5)*1.7,oe*z+(e.ly-.5)*1.7,z,M+611),_e=d?d[3]:u[3],Fe=d?d[0]:u[0];j>.6||j>.53&&de>.5?H=Me(H,_e,.85):(j>.42||j>.35&&de>.5)&&(J>j-(.5-q)*.55?H=Me(H,Fe,.55):H=Me(H,_e,.38))}if(Le=g<.085?1:g<.2?.8:g<.4?.58:.36,ue=we||S==="lat"?Me(b,H,w+(1-w)*Le):H,f&&y>.82&&q<.3){const z=y>.93?.5:.22;ue=Me(ue,f,z)}}if(e.cloud>0){const T=s+Y*Math.sin(s*nn),x=yt(se+T),p=oe*1.4+dt(Math.abs(se-.4)/1.3)*.25,g=5;let _=0;for(let z=0;z<6;z++)_+=Bt(x*g+z*1.7+11,p*g*.6+z*2.3+11,g,M+5);_/=6;const O=.5+(Ce(x*m+_*.9,p*m+_*.9,m,Math.max(2,k-1),M+313)-.5)*be;if((dt((O-e.cloud)/.05)>de?1:0)>0){const z=Math.min(1,Math.max(0,(O-e.cloud)/.18)),j=Me([206,216,228],[255,255,255],z);ue=Me(ue,Me(b,j,Le),.9)}}$[ye]=ue[0],$[ye+1]=ue[1],$[ye+2]=ue[2],$[ye+3]=v*255}}const Gt=new Map;function na(e,n,t){const a=Object.keys(e).sort().map(o=>`${o}=${String(e[o])}`).join(",");return[_o,t,n.toFixed(4),a].join("|")}function aa(e,n){const t=e.dpr??1,a=Math.max(3,Math.round(e.px*t)),o=document.createElement("canvas");o.width=a*n,o.height=a;const s=o.getContext("2d");if(!s)return"";const i=s.createImageData(a*n,a),r=ea(e,a,e.cloudThreshold);for(let l=0;l<n;l++)ta(r,i,a*n,l*a,0,n===1?e.spin??0:l/n);return s.putImageData(i,0,0),o.toDataURL("image/png")}function So(e,n,t,a){const o=Math.max(1,(a==null?void 0:a.batch)??6);let s=!1,i=!1;return setTimeout(()=>{if(i)return;i=!0;const l=e.dpr,u=Math.max(3,Math.round(e.px*l)),d=document.createElement("canvas");d.width=u*n,d.height=u;const c=d.getContext("2d");if(!c){t("");return}const f=c.createImageData(u*n,u),b=ea(e,u,e.cloudThreshold);let w=0;const m=()=>{var M;if(s)return;const k=Math.min(n,w+o);for(;w<k;w++)ta(b,f,u*n,w*u,0,w/n),(M=a==null?void 0:a.onFrame)==null||M.call(a,w+1);w<n?setTimeout(m,0):(c.putImageData(f,0,0),t(d.toDataURL("image/png")))};setTimeout(m,0)},0),{cancel:()=>s=!0}}function oa(e){const n=na(e,e.spin??0,1),t=Gt.get(n);if(t)return t;const a=aa(e,1);return Gt.set(n,a),a}function $o(e,n){const t=Math.max(1,Math.floor(n)),a=na(e,1/t,t);let o=Gt.get(a);return o===void 0&&(o=aa(e,t),Gt.set(a,o)),{uri:o,frames:t,framePx:Math.max(3,Math.round(e.px*e.dpr))}}function ia(e,n){return e==="star"?"star":e==="station"||e==="outpost"?"asteroid":Cn[Math.abs(ot(n))%Cn.length]}function an(e,n,t){return n!=="gas"?!1:t!==void 0?t:Ke(e|0,5,1<<20,91)<.34}function ot(e){let n=2166136261;for(let t=0;t<e.length;t++)n^=e.charCodeAt(t),n=Math.imul(n,16777619);return n|0}var Ao=L("<svg aria-hidden=true>"),To=L("<svg><path></svg>",!1,!0,!1);function on(e,n,t=0,a=1){const o=e.rx-e.band*t,s=e.ry-e.band*.34*t,i=Math.max(.5,e.rx-e.band*a),r=Math.max(.5,e.ry-e.band*.34*a),l=Math.PI*2,u=n?Math.PI:0,d=(S,A)=>{if(A-S<.001)return"";const I=($,Y,B)=>`${(Math.cos(B+u)*$).toFixed(2)} ${(Math.sin(B+u)*Y).toFixed(2)}`;return`M ${I(o,s,S)} A ${o.toFixed(2)} ${s.toFixed(2)} 0 0 1 ${I(o,s,A)} L ${I(i,r,A)} A ${i.toFixed(2)} ${r.toFixed(2)} 0 0 0 ${I(i,r,S)} Z`},c=e.gapHalf??0;if(e.gapAngle===void 0||c<=0)return d(0,Math.PI);const f=S=>(S%l+l)%l,b=f(e.gapAngle-c-u),w=f(e.gapAngle+c-u),m=w>b?w-b:w+l-b,k=Math.max(0,Math.min(Math.PI,b)),M=Math.max(0,Math.min(Math.PI,b+m));return M<=k+.001?d(0,Math.PI):k>=Math.PI-M?d(0,k):d(M,Math.PI)}function rn(e,n,t){const a=e*(e>=60?.64:.78);return{rx:a,ry:a*(t??(e>=60?.24:.34)),band:a*(e>=60?.26:.34),gapAngle:[-2.5,-.9,.9,2.5][(n>>>0)%4]+((n>>>3)%100/100-.5)*.7,gapHalf:e>=60?.1:.16}}function Rn(e){const n=()=>rn(e.px,e.seed??1,e.tilt),t=()=>n().rx*2+2,a=()=>({width:`${t()}px`,height:`${t()}px`,left:`${(e.px-t())/2}px`,top:`${(e.px-t())/2}px`}),o=(s,i,r)=>{const l=r.color??"#f2ead9",u=r.dark??"#4a4034",d=c=>{const f=We(l),b=We(u);return`rgb(${Math.round(f[0]+(b[0]-f[0])*c)},${Math.round(f[1]+(b[1]-f[1])*c)},${Math.round(f[2]+(b[2]-f[2])*c)})`};return s==="front"?[d(.3),d(.92),d(0)][i]:[d(.55),d(1),d(.3)][i]};return N(te,{get when(){return e.front===!0?"front":"back"},children:s=>(()=>{var i=Ao();return R(i,N($e,{each:[0,1,2],children:r=>(()=>{var l=To();return U(u=>{var d=on(n(),s()==="back",r/3,(r+1)/3),c=o(s(),r,e);return d!==u.e&&h(l,"d",u.e=d),c!==u.t&&h(l,"fill",u.t=c),u},{e:void 0,t:void 0}),l})()})),U(r=>{var l=`world-ring world-ring-${s()}`,u=t(),d=t(),c=`${-t()/2} ${-t()/2} ${t()} ${t()}`,f=a();return l!==r.e&&h(i,"class",r.e=l),u!==r.t&&h(i,"width",r.t=u),d!==r.a&&h(i,"height",r.a=d),c!==r.o&&h(i,"viewBox",r.o=c),r.i=Qn(i,f,r.i),r},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0}),i})()})}var In=L("<svg><g></svg>",!1,!0,!1),Mo=L("<svg><circle cx=0 cy=0 fill=#2f5a91 opacity=0.45></svg>",!1,!0,!1),Eo=L("<svg><circle cx=0 cy=0 fill=#9dc4f2 opacity=0.95></svg>",!1,!0,!1),Lo=L("<svg><circle cx=0 cy=0 fill=#ffffff></svg>",!1,!0,!1),Co=L("<svg><circle cx=0 cy=0 fill=none stroke=#080d16></svg>",!1,!0,!1),Po=L("<svg><ellipse class=quasar-disc cx=0 cy=0 fill=none stroke=#c9a24a></svg>",!1,!0,!1),Ro=L("<svg><ellipse cx=0 cy=0 fill=#0a0f18></svg>",!1,!0,!1),Io=L("<svg><defs><linearGradient x1=0 y1=1 x2=0 y2=0><animate attributeName=y1 values=1;0.1;-0.9 dur=1.35s repeatCount=indefinite></animate><animate attributeName=y2 values=2;1.1;0.1 dur=1.35s repeatCount=indefinite></animate><stop offset=0% stop-color=#ffffff stop-opacity=1></stop><stop offset=30% stop-color=#9fc6ff stop-opacity=0.45></stop><stop offset=100% stop-color=#9fc6ff stop-opacity=0></svg>",!1,!0,!1),Oo=L("<svg><g><path></path><path></svg>",!1,!0,!1),No=L("<svg><path fill=#dceaff opacity=0.92></svg>",!1,!0,!1);function zo(e){const n=()=>e.size/2,t=()=>e.kind==="pulsar";return(()=>{var a=In();return R(a,N(te,{get when(){return t()},get fallback(){return[(()=>{var o=Po();return U(s=>{var i=n()*.78,r=n()*.22,l=Math.max(1.4,n()*.1);return i!==s.e&&h(o,"rx",s.e=i),r!==s.t&&h(o,"ry",s.t=r),l!==s.a&&h(o,"stroke-width",s.a=l),s},{e:void 0,t:void 0,a:void 0}),o})(),(()=>{var o=Ro();return U(s=>{var i=n()*.5,r=n()*.13;return i!==s.e&&h(o,"rx",s.e=i),r!==s.t&&h(o,"ry",s.t=r),s},{e:void 0,t:void 0}),o})(),(()=>{var o=Io(),s=o.firstChild;return U(()=>h(s,"id",`mj-${e.seed}`)),o})(),(()=>{var o=Oo(),s=o.firstChild,i=s.nextSibling;return U(r=>{var l=`rotate(${e.seed%90})`,u=`M 0 ${-n()*.2} L ${-n()*.1} ${-n()*1.5} L ${n()*.1} ${-n()*1.5} Z`,d=`url(#mj-${e.seed})`,c=`M 0 ${n()*.2} L ${-n()*.1} ${n()*1.5} L ${n()*.1} ${n()*1.5} Z`,f=`url(#mj-${e.seed})`;return l!==r.e&&h(o,"transform",r.e=l),u!==r.t&&h(s,"d",r.t=u),d!==r.a&&h(s,"fill",r.a=d),c!==r.o&&h(i,"d",r.o=c),f!==r.i&&h(i,"fill",r.i=f),r},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0}),o})()]},get children(){return[(()=>{var o=In();return R(o,N($e,{each:[0,180],children:s=>(()=>{var i=No();return U(()=>h(i,"d",`M 0 ${s===0?-n()*.34:n()*.34} L ${-n()*.24} ${s===0?-n()*1.7:n()*1.7} L ${n()*.24} ${s===0?-n()*1.7:n()*1.7} Z`)),i})()})),U(()=>h(o,"transform",`rotate(${e.seed%180})`)),o})(),(()=>{var o=Mo();return U(()=>h(o,"r",n()*.62)),o})(),(()=>{var o=Eo();return U(()=>h(o,"r",n()*.34)),o})(),(()=>{var o=Lo();return U(()=>h(o,"r",n()*.2)),o})(),(()=>{var o=Co();return U(s=>{var i=n()*.38,r=Math.max(.8,n()*.07);return i!==s.e&&h(o,"r",s.e=i),r!==s.t&&h(o,"stroke-width",s.t=r),s},{e:void 0,t:void 0}),o})()]}})),U(o=>{var s=`remnant-mark remnant-${e.kind}`,i=e.kind,r=`translate(${e.x} ${e.y})`;return s!==o.e&&h(a,"class",o.e=s),i!==o.t&&h(a,"data-remnant",o.t=i),r!==o.a&&h(a,"transform",o.a=r),o},{e:void 0,t:void 0,a:void 0}),a})()}const fn=["#ffffeb","#fff540","#ffb84a","#ed7b39","#bd4035"],Ot=["#272737","#ffffeb","#ed7b39"],sn=3,ra=14,sa=.065,la=6.598,ca=3,Do=.766,Fo=.2,Nt=300,Uo=100,da=.247,ua=.028,ha=.15,Ht=fn.length,qo=8;function Bo(e,n){const t=n*(da/sn);return{d:n,hr:t,hlw:n*(ua/sn),hole:Ot.map(We),disc:fn.map(We),diskWidth:sa,discScale:1,tilt:(yt(e.seed*.6180339887)<.5?-1:1)*(.22+yt(e.seed*.2718281)*.45),size:la,seed:(e.seed|0)^20973}}function gt(e,n,t){if(e>=t)return 0;const a=n-e;if(a<=1e-6)return 0;const o=Math.max(0,Math.min(1,(t-e)/a));return o*o*(3-2*o)}function Go(e,n,t,a,o,s){const{d:i,hr:r,hlw:l,hole:u,disc:d,tilt:c,size:f,seed:b}=e,w=n.data,m=(S,A,I)=>{const $=Math.cos(I),Y=Math.sin(I),B=S-.5,ne=A-.5;return[B*$-ne*Y+.5,B*Y+ne*$+.5]},k=s*nn,M=Math.sin(s*nn*.4)*.01;for(let S=0;S<i;S++)for(let A=0;A<i;A++){const I=((S+o)*t+(A+a))*4,$=(A+.5)/i,Y=(S+.5)/i;let B=null;const ne=Math.hypot($-.5,Y-.5)*i;ne<=r&&(B=u[0],ne>r-l&&(B=u[1]),ne>r-l*.5&&(B=u[2]));{const ye=Math.floor(Y*Nt)/Nt,C=yt($+ye)<=1/Nt;let[P,y]=m($,Y,c);const G=P,Q=y;P=(P-.5)*1.3+.5,[P,y]=m(P,y,M);let le=.5,re=.5,se=e.diskWidth;if(y<.5){const we=Math.hypot(P-.5,y-.5);y+=gt(we,.5,.2),se+=gt(we,.5,.3),re-=gt(we,.5,.2)}else if(y>.53){const we=Math.hypot(P-.5,y-.5);y-=gt(we,.4,.17),se+=gt(we,.5,.2),re+=gt(we,.5,.2)}const oe=ra,ae=Math.hypot(G-le,(Q-re)*oe)*.3,de=(P-.5)*e.discScale;let be=(y-.5)*oe*e.discScale;const H=Math.hypot(de,be);let ue=dt((H-(.1-se*2))/(.5-se-(.1-se*2)));ue*=dt((.4-(H-se))/se);let q=de;[q,be]=m(q,be+.5,k);const Le=Ce(q*f,be*f,64,ca,b+17);if(ue*=Math.pow(Math.max(0,Le),.5),C&&(ue*=1.2),ue>=ha){const we=Math.max(0,Math.min(Ht-1,Math.floor((ue+ae)*(Ht-1))));B=d[we]}}B&&(w[I]=B[0],w[I+1]=B[1],w[I+2]=B[2],w[I+3]=255)}}const Wt=new Map;function fa(e,n,t){const a=Object.keys(e).sort().map(o=>`${o}=${String(e[o])}`).join(",");return[qo,t,n.toFixed(4),a].join("|")}function va(e,n){const t=Math.max(3,Math.round(e.px*(e.dpr??1))),a=document.createElement("canvas");a.width=t*n,a.height=t;const o=a.getContext("2d");if(!o)return"";const s=o.createImageData(t*n,t),i=Bo(e,t);for(let r=0;r<n;r++)Go(i,s,t*n,r*t,0,r/n);return o.putImageData(s,0,0),a.toDataURL("image/png")}function ga(e){const n=fa(e,0,1),t=Wt.get(n);if(t)return t;const a=va(e,1);return Wt.set(n,a),a}function Ho(e,n){const t=fa(e,0,n),a=Wt.get(t);if(a)return{uri:a,frames:n,px:e.px};const o=va(e,n);return Wt.set(t,o),{uri:o,frames:n,px:e.px}}const Wo=`
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  v_uv = a_pos * 0.5 + 0.5;
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`,Vo=`
precision highp float;
varying vec2 v_uv;

uniform vec2  u_res;
uniform float u_time;          // seconds, already multiplied by time_speed
uniform float u_seed;
uniform float u_rotation;
uniform float u_timeSpeed;
uniform float u_diskWidth;
uniform float u_perspective;
uniform float u_size;
uniform float u_pixels;        // disc UV quantisation
uniform float u_holePixels;
uniform float u_holeRadius;
uniform float u_holeLightWidth;
uniform float u_doppler;       // asymmetric brightening, 0 disables
uniform float u_photonRing;    // 0 disables
uniform float u_spiral;        // spiral density wave, 0 disables
uniform float u_holeRatio;
uniform vec3  u_hole0;         // the void
uniform vec3  u_hole1;         // white ring
uniform vec3  u_hole2;         // orange outer ring
uniform vec3  u_d0;            // cream
uniform vec3  u_d1;            // yellow
uniform vec3  u_d2;            // orange
uniform vec3  u_d3;            // deep orange
uniform vec3  u_d4;            // red-brown

/**
 * smoothstep(distance_to_centre, outer, inner), transcribed.
 *
 * The reference calls smoothstep with its first argument where an edge belongs, so
 * this is a call with edge0 > edge1 -- undefined per the GLSL spec, and in practice
 * clamp((x - edge0) / (edge1 - edge0)) with x = 0.2. That is
 * clamp((inner - d) / (outer - d)): it peaks at inner/outer at the centre and is
 * already ZERO by inner. It is not a plateau.
 *
 * The full smoothstep CURVE is applied, not the linear ratio. Reading it as linear
 * was one of three transcription errors that had this rendering as a rounded cigar
 * rather than a swept band.
 */
float bump(float d, float outer, float inner) {
  if (d >= inner) return 0.0;
  float den = outer - d;
  if (den <= 1e-6) return 0.0;
  float t = clamp((inner - d) / den, 0.0, 1.0);
  return t * t * (3.0 - 2.0 * t);
}

/**
 * The 4x4 ordered dither, for the photon ring.
 *
 * The ring was originally dithered with 'dith' -- the disc's own ordered pattern, reused
 * because carrying a second copy of a matrix felt redundant. It is a 2-pixel pattern, so
 * the ring came out visibly BEADED: at chart scale that reads as sparkle and is fine, and
 * at the overlay's 190px it is a dotted circle. The reference dithers this against
 * bayer4, which is a 4x4 ordered matrix and is finer and more even.
 *
 * Transcribed as a branch tree rather than a 16-entry lookup, because the branch form is
 * what gl-star.ts already carries for the same matrix and two copies of the same function
 * in two files is one more thing to keep in step.
 */
float bayer4(vec2 position) {
  vec2 cell = mod(floor(position), 4.0);
  if (cell.y < 1.0) {
    if (cell.x < 1.0) return 0.03125;
    if (cell.x < 2.0) return 0.53125;
    if (cell.x < 3.0) return 0.15625;
    return 0.65625;
  }
  if (cell.y < 2.0) {
    if (cell.x < 1.0) return 0.8125;
    if (cell.x < 2.0) return 0.3125;
    if (cell.x < 3.0) return 0.9375;
    return 0.4375;
  }
  if (cell.y < 3.0) {
    if (cell.x < 1.0) return 0.21875;
    if (cell.x < 2.0) return 0.71875;
    if (cell.x < 3.0) return 0.09375;
    return 0.59375;
  }
  if (cell.x < 1.0) return 0.578125;
  if (cell.x < 2.0) return 0.078125;
  if (cell.x < 3.0) return 0.878125;
  return 0.378125;
}

/** The reference's hash: sin-based, and tiling at 2*size by size. */
float rnd(vec2 coord) {
  vec2 m = vec2(2.0, 1.0) * floor(u_size + 0.5);
  coord = mod(coord, m);
  return fract(sin(dot(coord, vec2(12.9898, 78.233))) * 15.5453 * u_seed);
}

float vnoise(vec2 coord) {
  vec2 i = floor(coord);
  vec2 f = fract(coord);
  float a = rnd(i);
  float b = rnd(i + vec2(1.0, 0.0));
  float c = rnd(i + vec2(0.0, 1.0));
  float d = rnd(i + vec2(1.0, 1.0));
  vec2 cubic = f * f * (3.0 - 2.0 * f);
  return mix(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;
}

/** Three octaves, because the scene says three. */
float fbm(vec2 coord) {
  float value = 0.0;
  float scale = 0.5;
  for (int i = 0; i < ${ca}; i++) {
    value += vnoise(coord) * scale;
    coord *= 2.0;
    scale *= 0.5;
  }
  return value;
}

vec2 rotate(vec2 coord, float angle) {
  coord -= 0.5;
  coord *= mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
  return coord + 0.5;
}

void main() {
  vec3 col = vec3(0.0);
  float alpha = 0.0;

  // Hoisted out of their blocks so the photon ring can use them. 'd' is the horizon's
  // CIRCULAR screen-space distance in the horizon sprite's quantised UV, and 'dith' is
  // the ordered dither the disc already computes. Declaring them here rather than
  // reaching into the blocks is the only reason the ring is a separate pass at all.
  float d = 1.0;
  float dith = 0.0;

  // ---- the horizon, underneath -------------------------------------------
  // BlackHole.gdshader. Its sprite is 100x100 and the disc's is 200x200, concentric,
  // so this sprite's UV is the canvas UV scaled by two about the middle.
  {
    vec2 huv = (v_uv - 0.5) * u_holeRatio + 0.5;
    vec2 uv = floor(huv * u_holePixels) / u_holePixels;
    d = distance(uv, vec2(0.5));
    vec3 hc = u_hole0;
    if (d > u_holeRadius - u_holeLightWidth) hc = u_hole1;
    if (d > u_holeRadius - u_holeLightWidth * 0.5) hc = u_hole2;
    if (u_holeRadius >= d) { col = hc; alpha = 1.0; }
  }

  // ---- the disc, over the horizon ----------------------------------------
  // BlackHoleRing.gdshader, in its order, with the scene's constants.
  {
    vec2 uv = floor(v_uv * u_pixels) / u_pixels;

    // dither(UV, uv): the RAW uv as the first argument, the quantised one as the
    // second. Passing the quantised value for both is a different pattern.
    dith = mod(v_uv.x + uv.y, 2.0 / u_pixels) <= 1.0 / u_pixels ? 1.0 : 0.0;

    uv = rotate(uv, u_rotation);
    vec2 uv2 = uv;

    // Compress x, or the disc looks stretched out.
    uv.x -= 0.5;
    uv.x *= 1.3;
    uv.x += 0.5;

    uv = rotate(uv, sin(u_time * u_timeSpeed * 2.0) * 0.01);

    vec2 l_origin = vec2(0.5);
    float d_width = u_diskWidth;

    // The warp. The distance is taken from the CURRENT uv -- after the rotation and
    // the x compression, before the y displacement. Mixing the two frames is a real
    // bug and was one.
    if (uv.y < 0.5) {
      float dd = distance(vec2(0.5), uv);
      uv.y += bump(dd, 0.5, 0.2);
      d_width += bump(dd, 0.5, 0.3);
      l_origin.y -= bump(dd, 0.5, 0.2);
    } else if (uv.y > 0.53) {
      float dd = distance(vec2(0.5), uv);
      uv.y -= bump(dd, 0.4, 0.17);
      d_width += bump(dd, 0.5, 0.2);
      l_origin.y += bump(dd, 0.5, 0.2);
    }

    float light_d =
      distance(uv2 * vec2(1.0, u_perspective), l_origin * vec2(1.0, u_perspective)) * 0.3;

    vec2 uv_center = uv - vec2(0.0, 0.5);
    uv_center *= vec2(1.0, u_perspective);
    float center_d = distance(uv_center, vec2(0.5, 0.0));

    float doppler = 0.0;
    if (u_doppler > 0.0) {
      doppler = (uv_center.x - 0.5) / max(center_d, 1e-3) * u_doppler;
    }

    // Two circles of different sizes; only the intersection. This describes a FILLED
    // ellipse, not a ring -- the thin band is what survives the alpha cut below, and
    // the fbm decides where that boundary falls.
    float disk = smoothstep(0.1 - d_width * 2.0, 0.5 - d_width, center_d);
    disk *= smoothstep(center_d - d_width, center_d, 0.4);

    uv_center = rotate(uv_center + vec2(0.0, 0.5), u_time * u_timeSpeed * 3.0);
    disk *= pow(fbm(uv_center * u_size), 0.5);

    // The SPIRAL DENSITY WAVE, from the same reference:
    //
    //   float bands = sin(radial * 54.0 - angle * 3.0 + BodySeed * 0.17) * 0.5 + 0.5;
    //   coverage = edge * DiskDensity * (0.42 + bands * 0.28 + noise * 0.45);
    //
    // A coherent two-armed wave in addition to the fbm, which is a field with no
    // preferred direction at all. The fbm alone gives a disc that looks like static;
    // what makes a disc read as a disc is that it is SHEARED, and shear is exactly what
    // a term in 'radial * k - angle * m' is. The two are complementary and both are
    // wanted: the wave for structure, the noise for texture.
    //
    // 54 and 3 are the reference's, so the wave has the same pitch in radial and
    // azimuthal terms that theirs does. 'center_d' is the radial coordinate.
    if (u_spiral > 0.0) {
      float ang = atan(uv.y - 0.5, uv.x - 0.5);
      float wave = sin(center_d * 54.0 - ang * 3.0 + u_seed * 0.17) * 0.5 + 0.5;
      disk *= mix(1.0, 0.72 + wave * 0.56, u_spiral);
    }
    if (dith > 0.5) disk *= 1.2;

    // DOPPLER BEAMING, ported from Cosmoglyph's black_hole.glsl.
    //
    // There, heat gains 'dot(tangent, normalize(eye - point)) * 0.16' -- the disc
    // material's velocity dotted with the line of sight -- before the palette index is
    // taken. One side of an accretion disc is approaching and the other receding, and
    // the approaching side is brighter and bluer. It is the single most recognisable
    // thing about a real accreting black hole and ours had none.
    //
    // In the reference the tangent is 'normalize(vec3(-point.z, 0, point.x))', i.e. the
    // direction of rotation in the disc's plane, and the eye direction is out of that
    // plane. So the dot product reduces to the TANGENTIAL COMPONENT ALONG THE LINE OF
    // SIGHT, which for a disc squashed vertically by 'u_perspective' is proportional to
    // the horizontal offset -- and it must be NORMALISED BY RADIUS.
    //
    // The normalisation is the part that matters: the boost is strongest where the
    // material's velocity is most transverse to our line of sight, which is at the
    // limb. Dividing by centre_d rather than using the raw offset is what puts the
    // brightening on the outer disc and leaves the inner disc nearly symmetric, which is
    // what the reference's tangent-length normalisation does for free in 3D.

    float posterized = floor((disk + light_d + doppler) * ${Ht-1}.0);
    posterized = min(posterized, ${Ht-1}.0);

    // The alpha is a STEP, not a ramp: opaque or not, with the palette chosen
    // independently. Treating it as a ramp is what turns the band into a smear.
    if (disk >= ${ha}) { // the scene's step(ALPHA_CUT, disk)
      vec3 dc = u_d0;
      if (posterized >= 3.5) dc = u_d4;
      else if (posterized >= 2.5) dc = u_d3;
      else if (posterized >= 1.5) dc = u_d2;
      else if (posterized >= 0.5) dc = u_d1;
      col = dc;
      alpha = 1.0;
    }
  }

  // ---- the photon ring ------------------------------------------------------
  // Ported from Cosmoglyph's black_hole.glsl, which draws it from the ray's IMPACT
  // PARAMETER: '1.0 - smoothstep(0.015, 0.11, abs(impact - 1.08))', dithered against
  // bayer4 and painted with the brightest palette entry. The impact parameter is the
  // closest approach of the sightline to the centre, which in a 2D panel is just the
  // screen-space distance from the centre -- so the two agree without any 3D.
  //
  // 1.08 is the number that makes it a photon ring rather than a bright limb: light
  // that passes just outside the horizon is bent so hard it orbits and escapes along a
  // narrow annulus, which is why it sits slightly OUTSIDE the horizon and not on it.
  // The inner and outer edges are in units of the horizon radius, as in the reference.
  //
  // It is drawn AFTER the disc, so where they overlap the ring wins -- the ring is in
  // front of the near limb of the disc from any viewpoint outside it.
  if (u_photonRing > 0.0) {
    // The horizon block already computed a CIRCULAR screen-space distance in the
    // horizon sprite's own quantised UV, which is the right frame for this: the disc's
    // 'center_d' is measured in a frame with y stretched by u_perspective and would put
    // the ring in an ellipse.
    float pr = abs(d - u_holeRadius * 1.08);
    float ring = 1.0 - smoothstep(u_holeRadius * 0.015, u_holeRadius * 0.11, pr);
    ring *= min(1.0, u_photonRing);
    // Dithered against bayer4, as the reference does. Against 'dith' instead -- the
    // disc's own 2-pixel pattern -- the ring was visibly BEADED at overlay size, which is
    // the one thing about it that did not look right. At chart scale the beading reads
    // as sparkle and is fine; at 190px it is a dotted circle.
    //
    // The matrix is scaled rather than used raw, because the ring's profile is a smooth
    // falloff across roughly three pixels and an ordered pattern only reads as ordered
    // dithering when its full range is used. Dividing by four lays the matrix's 0..1
    // across the ring's narrow band, and the ring's own value carries most of the
    // decision so the falloff still reads as a falloff.
    if (ring > bayer4(gl_FragCoord.xy) * 0.25 + ring * 0.75) {
      col = u_d0;
      alpha = 1.0;
    }
  }

  if (alpha < 0.5) discard;
  gl_FragColor = vec4(col, 1.0);
}`,jo=.24,Yo=.9,Ko=.85;function Ct(e){const n=parseInt(e.slice(1),16);return[(n>>16&255)/255,(n>>8&255)/255,(n&255)/255]}function Xo(e){if(typeof document>"u")return null;const n=document.createElement("canvas");let t=null;const a={preserveDrawingBuffer:!0,alpha:!0,antialias:!1};try{t=n.getContext("webgl2",a)??n.getContext("webgl",a)??n.getContext("experimental-webgl",a)}catch{return null}if(!t)return null;const o=(A,I)=>{const $=t.createShader(A);return $?(t.shaderSource($,I),t.compileShader($),t.getShaderParameter($,t.COMPILE_STATUS)?$:(console.warn("[GalaxyMap] shader failed to compile",t.getShaderInfoLog($)),t.deleteShader($),null)):null},s=o(t.VERTEX_SHADER,Wo),i=o(t.FRAGMENT_SHADER,Vo);if(!s||!i)return null;const r=t.createProgram();if(!r)return null;if(t.attachShader(r,s),t.attachShader(r,i),t.linkProgram(r),!t.getProgramParameter(r,t.LINK_STATUS))return console.warn("[GalaxyMap] program link failed",t.getProgramInfoLog(r)),null;t.useProgram(r);const l=t.createBuffer();t.bindBuffer(t.ARRAY_BUFFER,l),t.bufferData(t.ARRAY_BUFFER,new Float32Array([-1,-1,3,-1,-1,3]),t.STATIC_DRAW);const u=t.getAttribLocation(r,"a_pos");t.enableVertexAttribArray(u),t.vertexAttribPointer(u,2,t.FLOAT,!1,0,0);const d=A=>t.getUniformLocation(r,A),c=A=>A-Math.floor(A);t.uniform1f(d("u_seed"),1+c(e.seed*.6180339887)*9),t.uniform1f(d("u_rotation"),Do+(c(e.seed*.2718281)-.5)*.5),t.uniform1f(d("u_timeSpeed"),Fo),t.uniform1f(d("u_diskWidth"),sa),t.uniform1f(d("u_perspective"),ra),t.uniform1f(d("u_doppler"),e.doppler??jo),t.uniform1f(d("u_photonRing"),e.photonRing??Yo),t.uniform1f(d("u_spiral"),e.spiral??Ko),t.uniform1f(d("u_size"),la),t.uniform1f(d("u_pixels"),Nt),t.uniform1f(d("u_holePixels"),Uo),t.uniform1f(d("u_holeRatio"),sn),t.uniform1f(d("u_holeRadius"),da),t.uniform1f(d("u_holeLightWidth"),ua),t.uniform3fv(d("u_hole0"),Ct(Ot[0])),t.uniform3fv(d("u_hole1"),Ct(Ot[1])),t.uniform3fv(d("u_hole2"),Ct(Ot[2])),fn.forEach((A,I)=>t.uniform3fv(d(`u_d${I}`),Ct(A)));const f=()=>window.devicePixelRatio||1;let b=0,w=!1;const m=()=>{const A=Math.max(1,Math.round(e.px*f()));n.width!==A&&(n.width=A,n.height=A),n.style.width=`${e.px}px`,n.style.height=`${e.px}px`,t.viewport(0,0,A,A),t.uniform2f(d("u_res"),A,A)};m();const k=performance.now(),M=d("u_time"),S=A=>{w||(m(),t.uniform1f(M,e.time??(A-k)/1e3),t.drawArrays(t.TRIANGLES,0,3),e.animate!==!1&&(b=requestAnimationFrame(S)))};return S(k),{canvas:n,dispose(){w=!0,b&&cancelAnimationFrame(b),t=null}}}var Zo=L('<div class="world blackhole"><img class=world-still alt><div class=world-gl>'),Qo=L("<div class=world-turn>");function Vt(){var e;return typeof window<"u"&&((e=window.matchMedia)==null?void 0:e.call(window,"(prefers-reduced-motion: reduce)").matches)===!0}function ma(e){const n=()=>Math.max(1,Math.floor(e.frames??0)),t=()=>typeof window>"u"?1:window.devicePixelRatio||1,a=()=>e.seed,o=()=>e.px,s=()=>Vt()?1:n(),i=()=>ga({seed:a(),px:o(),dpr:t()}),[r,l]=me(),[u,d]=me(null),[c,f]=me(!1);let b;return it(()=>{const w=a(),m=o(),k=Vt(),M=Xo({seed:w,px:m,period:e.period??6,doppler:e.doppler,photonRing:e.photonRing,spiral:e.spiral,animate:!k});if(!M)return;f(!1),b==null||b.appendChild(M.canvas),d(M);const S=requestAnimationFrame(()=>f(!0));ze(()=>{cancelAnimationFrame(S),d(null),M.dispose(),M.canvas.remove()})}),it(()=>{const w=s(),m=a(),k=o();if(w<=1){l(void 0);return}l(void 0);const M=window.setTimeout(()=>{l(Ho({seed:m,px:k,dpr:1},w).uri)},0);ze(()=>window.clearTimeout(M))}),(()=>{var w=Zo(),m=w.firstChild,k=m.nextSibling;R(w,N(te,{get when(){return Ye(()=>!!r())()&&!u()},keyed:!0,children:S=>(()=>{var A=Qo();return fe(A,"background-image",`url(${S})`),U(I=>{var $=`${o()*s()}px ${o()}px`,Y=`${e.period??6}s`,B=`steps(${s()})`,ne=`-${o()*s()}px`;return $!==I.e&&fe(A,"background-size",I.e=$),Y!==I.t&&fe(A,"animation-duration",I.t=Y),B!==I.a&&fe(A,"animation-timing-function",I.a=B),ne!==I.o&&fe(A,"--world-end",I.o=ne),I},{e:void 0,t:void 0,a:void 0,o:void 0}),A})()}),k);var M=b;return typeof M=="function"?Qe(M,k):b=k,U(S=>{var A=r()!==void 0,I=`${o()}px`,$=`${o()}px`,Y=e.title?"img":void 0,B=e.title,ne=i(),ye=c(),C=!u();return A!==S.e&&w.classList.toggle("turning",S.e=A),I!==S.t&&fe(w,"width",S.t=I),$!==S.a&&fe(w,"height",S.a=$),Y!==S.o&&h(w,"role",S.o=Y),B!==S.i&&h(w,"aria-label",S.i=B),ne!==S.n&&h(m,"src",S.n=ne),ye!==S.s&&(m.hidden=S.s=ye),C!==S.h&&(k.hidden=S.h=C),S},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0,s:void 0,h:void 0}),w})()}function Jo(e,n,t,a=0){const o=Math.min((n-a*2)/e.w,(t-a*2)/e.h),s=n/2,i=t/2;return{scale:o,ox:s,oy:i,width:n,height:t,toPx:r=>({x:r.x*o+s,y:r.y*o+i}),toLy:r=>({x:(r.x-s)/o,y:(r.y-i)/o})}}function ei(e,n){return e.map(t=>po(t.map(n.toPx),!0)).filter(Boolean).join(" ")}const ya=[{id:"en-US",label:"EN"},{id:"uk-UA",label:"УКР"}],pa={grid:{en:"GRID",uk:"СІТКА"},planets:{en:"PLANETS",uk:"ПЛАНЕТИ"},discFx:{en:"Disc",uk:"Диск"},fxDoppler:{en:"Beaming",uk:"Променювання"},fxRing:{en:"Ring",uk:"Кільце"},fxSpiral:{en:"Spiral",uk:"Спіраль"},sky:{en:"Space behind the chart. Click to change it.",uk:"Космос за картою. Натисніть, щоб змінити."},kindPulsar:{en:"PULSAR",uk:"ПУЛЬСАР"},kindQuasar:{en:"QUASAR",uk:"КВАЗАР"},kindDwarf:{en:"WHITE DWARF",uk:"БІЛИЙ КАРЛИК"},kindRemnant:{en:"SUPERNOVA REMNANT",uk:"ЗАЛИШОК СНАПЛАВУ"},paint:{en:"PAINT",uk:"ФАРБА"},undo:{en:"UNDO",uk:"СКАСУВАТИ"},close:{en:"CLOSE",uk:"ЗАКРИТИ"},showNamesIn:{en:"Show names in {$locale}",uk:"Показати назви мовою {$locale}"},brushPainting:{en:"PAINTING {$name}",uk:"ФАРБУЄМО: {$name}"},brushUnclaimed:{en:"PAINTING UNCLAIMED SPACE",uk:"ФАРБУЄМО: НЕЗАЙНЯТІ СХОДИ"},brushContesting:{en:"MARKING CONTESTED",uk:"ПОЗНАЧАЄМО СПІРНУ ДІЛЯНКУ"},brushClearingContest:{en:"CLEARING CONTESTED",uk:"ЗНИМАЄМО ПОЗНАЧКУ СПОРУ"},brushHint:{en:"— click cells, ESC to stop",uk:"— клацніть клітинки, ESC для виходу"},tipUnclaim:{en:"Return the cell to unclaimed space",uk:"Повернути клітинку до незайнятих сходу"},tipContest:{en:"Mark the cell as contested — two claims, one owner",uk:"Позначити клітинку як спірну — дві претензії, один власник"},tipUncontest:{en:"Settle the dispute — the owner is not in question",uk:"Зняти позначку спору — власник не змінюється"},sovereignTerritory:{en:"SOVEREIGN TERRITORY",uk:"СУВЕРЕННА ТЕРИТОРІЯ"},unclaimedSpace:{en:"UNCLAIMED SPACE",uk:"НЕЗАЙНЯТІ СХОДИ"},labelCells:{en:"CELLS",uk:"КЛІТИНКИ"},labelSystems:{en:"SYSTEMS",uk:"СИСТЕМИ"},labelCapitals:{en:"CAPITALS",uk:"СТОЛИЦІ"},labelContested:{en:"CONTESTED",uk:"СПІРНІ"},kindStar:{en:"STAR SYSTEM",uk:"ЗОРЯНА СИСТЕМА"},kindBlackHole:{en:"COLLAPSED SYSTEM",uk:"ЗІРКА, ЩО ЗІРКОЛАЛАСЬЯ"},kindPlanet:{en:"PLANETARY SYSTEM",uk:"ПЛАНЕТАРНА СИСТЕМА"},kindStation:{en:"STATION",uk:"СТАНЦІЯ"},kindGate:{en:"GATE",uk:"БРАМА"},kindOutpost:{en:"OUTPOST",uk:"АВПОСТ"},labelSovereign:{en:"SOVEREIGN",uk:"СУВЕРЕН"},labelPosition:{en:"POSITION",uk:"КОORDINАТИ"},capitalOf:{en:"CAPITAL OF {$name}",uk:"СТОЛИЦЯ {$name}"},unclaimedOwner:{en:"none — unclaimed space",uk:"немає — незайняті сходи"},tipClickSystem:{en:"Click a star to read about its system",uk:"Клацніть зорю, щоб дізнатися про її систему"},orionSpur:{en:"ORION SPUR",uk:"РУКАВ ОРІОНА"},extent:{en:"{$w} × {$h} LY · {$cells} cells @ {$size} LY",uk:"{$w} × {$h} св.р. · {$cells} клітинок по {$size} св.р."},legendContested:{one:{en:"{$n} contested",uk:"{$n} спірна клітинка"},few:{en:"{$n} contested",uk:"{$n} спірні клітинки"},many:{en:"{$n} contested",uk:"{$n} спірних клітинок"}},legendEdits:{one:{en:"{$n} local edit",uk:"{$n} локальна зміна"},few:{en:"{$n} local edits",uk:"{$n} локальні зміни"},many:{en:"{$n} local edits",uk:"{$n} локальних змін"}},pluralCell:{one:{en:"{$n} cell",uk:"{$n} клітинка"},few:{en:"{$n} cells",uk:"{$n} клітинки"},many:{en:"{$n} cells",uk:"{$n} клітинок"}},pluralContested:{one:{en:"{$n} contested",uk:"{$n} спірна"},few:{en:"{$n} contested",uk:"{$n} спірні"},many:{en:"{$n} contested",uk:"{$n} спірних"}},pluralEdit:{one:{en:"{$n} local edit",uk:"{$n} локальна зміна"},few:{en:"{$n} local edits",uk:"{$n} локальні зміни"},many:{en:"{$n} local edits",uk:"{$n} локальних змін"}},pluralSystem:{one:{en:"{$n} system",uk:"{$n} система"},few:{en:"{$n} systems",uk:"{$n} системи"},many:{en:"{$n} systems",uk:"{$n} систем"}}};function ti(e,n){if(!ba(n))return e===1?"one":"many";const t=e%10,a=e%100;return t===1&&a!==11?"one":t>=2&&t<=4&&(a<12||a>14)?"few":"many"}function Oe(e,n){return e?ba(n)&&e.uk?e.uk:e.en??"":""}function ba(e){return e.toLowerCase().startsWith("uk")}function xa(e,n){return e.replace(/\{\$(\w+)\}/g,(t,a)=>a in n?String(n[a]):t)}let vn=pa,wa=ya;function ni(){return wa}function ce(e,n,t){const a=vn[e],o=Oe(a,n);return t?xa(o,t):o}function On(e,n,t){const a=vn[e],o=ti(n,t),s=(a==null?void 0:a[o])??(a==null?void 0:a.many)??(a==null?void 0:a.one);return xa(Oe(s,t),{n})}function ai(e){vn={...pa,...e}}function oi(e){wa=e.length>0?e:ya}var ii=L('<svg><path d="M-1,1 l2,-2 M0,9 l9,-9 M8,10 l2,-2"stroke-width=1.1 opacity=0.5></svg>',!1,!0,!1),ri=L('<svg><path d="M-1,1 l2,-2 M0,9 l9,-9 M8,10 l2,-2"stroke-width=1 opacity=0.4></svg>',!1,!0,!1),si=L('<svg><path d="M1,-1 l2,2 M9,0 l-9,9 M10,8 l-2,2"stroke-width=1 opacity=0.4></svg>',!1,!0,!1),li=L("<svg><circle cx=4.5 cy=4.5 r=1.4 opacity=0.5></svg>",!1,!0,!1),ci=L('<svg><path d="M9,0 L9,9 M0,9 L9,9"stroke-width=1 opacity=0.45></svg>',!1,!0,!1),di=L('<svg><path d="M0,2.5 L9,2.5 M0,6.5 L9,6.5"stroke-width=1.1 opacity=0.45></svg>',!1,!0,!1),ui=L('<svg><path d="M2.5,0 L2.5,9 M6.5,0 L6.5,9"stroke-width=1.1 opacity=0.45></svg>',!1,!0,!1),hi=L("<svg><rect x=0 y=0 width=4.5 height=4.5 opacity=0.4></svg>",!1,!0,!1),fi=L("<svg><rect x=4.5 y=4.5 width=4.5 height=4.5 opacity=0.4></svg>",!1,!0,!1),vi=L("<svg><g class=graticule clip-path=url(#frame-clip)></svg>",!1,!0,!1),gi=L("<svg><g class=stroke-pending><path fill-rule=evenodd opacity=0.5></path><path fill=none stroke-width=1.4 opacity=0.9></svg>",!1,!0,!1),mi=L('<div class=chart-host><svg class=chart><defs><radialGradient id=vignette cx=50% cy=42% r=78%><stop offset=0% stop-color=#0d1526 stop-opacity=0></stop><stop offset=58% stop-color=#070c17 stop-opacity=0></stop><stop offset=100% stop-color=#03050a stop-opacity=0.62></stop></radialGradient><clipPath id=frame-clip><rect></rect></clipPath><pattern id=pat-contested width=8 height=8 patternUnits=userSpaceOnUse><rect width=8 height=8 fill=#ffb454 fill-opacity=0.1></rect><path d="M0,8 L8,0"stroke=#ffb454 stroke-width=1.6 stroke-opacity=0.85></path><path d="M-2,2 L2,-2 M6,10 L10,6"stroke=#ffb454 stroke-width=1.6 stroke-opacity=0.85></path></pattern></defs><rect x=0 y=0 fill=url(#vignette)></rect><g></g><g class=contested-layer clip-path=url(#frame-clip)><path fill=url(#pat-contested) fill-rule=evenodd></path><path fill=none stroke=#ffb454 stroke-width=1.1 opacity=0.6></path></g><g></g><g></g><polygon class=cell-hover></polygon><rect>'),yi=L("<svg><clipPath><rect></svg>",!1,!0,!1),pi=L("<svg><pattern width=9 height=9 patternUnits=userSpaceOnUse></svg>",!1,!0,!1),bi=L("<svg><circle fill=#dfe9f5></svg>",!1,!0,!1),xi=L("<svg><path></svg>",!1,!0,!1),Nn=L("<svg><path fill-rule=evenodd></svg>",!1,!0,!1),zn=L("<svg><path fill=none stroke-linejoin=round></svg>",!1,!0,!1),wi=L("<svg><path fill=none stroke=#eaf3fb stroke-width=2.4 opacity=0.85 stroke-linejoin=round></svg>",!1,!0,!1),_i=L("<svg><path fill=none stroke=#7fd4c8 stroke-width=1.3 opacity=0.6></svg>",!1,!0,!1),ki=L("<svg><text class=terr-name></svg>",!1,!0,!1),Si=L("<svg><circle class=sys-hit fill=transparent></svg>",!1,!0,!1),$i=L("<svg><circle fill=none stroke-width=1.6 opacity=0.9></svg>",!1,!0,!1),Ai=L("<svg><rect width=7 height=7 fill=#cbd8e6 opacity=0.9></svg>",!1,!0,!1),Ti=L("<svg><text class=system-label></svg>",!1,!0,!1),Mi=L("<svg><image class=blackhole-mark></svg>",!1,!0,!1),Ei=L("<svg><circle stroke=#0a0f18 stroke-width=1></svg>",!1,!0,!1),Li=L('<svg><g><image class="planet-mark turning"><animate attributeName=x calcMode=discrete dur=26s repeatCount=indefinite></svg>',!1,!0,!1),Ci=L("<svg><path class=saturn-far></svg>",!1,!0,!1),Pi=L("<svg><image class=planet-mark></svg>",!1,!0,!1),Ri=L("<svg><path class=saturn-near></svg>",!1,!0,!1);function Ii(e){const n=e.color;switch(e.kind){case"hatch":return(()=>{var t=ii();return h(t,"stroke",n),t})();case"crosshatch":return[(()=>{var t=ri();return h(t,"stroke",n),t})(),(()=>{var t=si();return h(t,"stroke",n),t})()];case"dots":return(()=>{var t=li();return h(t,"fill",n),t})();case"grid":return(()=>{var t=ci();return h(t,"stroke",n),t})();case"horizontal":return(()=>{var t=di();return h(t,"stroke",n),t})();case"vertical":return(()=>{var t=ui();return h(t,"stroke",n),t})();case"checker":return[(()=>{var t=hi();return h(t,"fill",n),t})(),(()=>{var t=fi();return h(t,"fill",n),t})()];default:return null}}function Oi(e){return()=>{e|=0,e=e+1831565813|0;let n=Math.imul(e^e>>>15,1|e);return n=n+Math.imul(n^n>>>7,61|n)^n,((n^n>>>14)>>>0)/4294967296}}function Ni(e,n,t,a){const o=Oi(a),s=[];for(let i=0;i<t;i++){const r=o();s.push({x:o()*e,y:o()*n,r:.4+r*r*1.7,o:.14+r*.66})}return s}function jt(e,n=.74){const t=/^#?([0-9a-f]{6})$/i.exec(e.trim());if(!t)return e;const a=parseInt(t[1],16),o=(a>>16&255)/255,s=(a>>8&255)/255,i=(a&255)/255,r=Math.max(o,s,i),l=Math.min(o,s,i),u=(r+l)/2,d=r-l,c=d===0?0:d/(1-Math.abs(2*u-1));let f=0;d!==0&&(r===o?f=(s-i)/d%6:r===s?f=(i-o)/d+2:f=(o-s)/d+4,f*=60,f<0&&(f+=360));const b=Math.max(u,n),w=u>=n?c:c*Math.max(.45,1-(b-u)*1.4),m=(1-Math.abs(2*b-1))*w,k=m*(1-Math.abs(f/60%2-1)),M=b-m/2;let S;return f<60?S=[m,k,0]:f<120?S=[k,m,0]:f<180?S=[0,m,k]:f<240?S=[0,k,m]:f<300?S=[k,0,m]:S=[m,0,k],"#"+S.map(A=>Math.round((A+M)*255).toString(16).padStart(2,"0")).join("")}function zi(e){const[n,t]=me({w:1200,h:700});let a,o;it(()=>{const C=a;if(!C)return;const P=new ResizeObserver(y=>{const G=y[0].contentRect;G.width>0&&G.height>0&&t({w:G.width,h:G.height})});P.observe(C),ze(()=>P.disconnect())});const s=56,i=()=>Jo(e.model.extentLy,n().w,n().h,s),r=pe(()=>new Map(e.model.territories.map(C=>[C.id,C]))),l=12,u=()=>!Vt(),d=pe(()=>{if(!e.planets)return new Map;const C=window.devicePixelRatio||1,P=new Map;for(const y of e.model.systems){if(y.kind!=="star"&&y.kind!=="planet")continue;const G=y.importance>=3?40:y.importance>=2?30:y.importance>=1?21:16,Q=y.planetType??ia(y.kind,y.id),le=ot(y.id);P.set(y.id,{strip:u()&&$o({seed:le,type:Q,px:G,dpr:1},l).uri||null,href:oa({seed:le,type:Q,px:G,dpr:C,tint:void 0,tintAmount:0}),size:G,ring:G/2+7,saturn:an(le,Q,y.rings)})}return P}),c=(C,P)=>({px:P>=2?30:20,seed:ot(C),dpr:typeof window>"u"?1:window.devicePixelRatio||1}),f=pe(()=>e.model.systems.map(C=>{var P;return{id:C.id,system:C,colour:((P=r().get(C.territory))==null?void 0:P.color)??"#94a3b8",P:i().toPx({x:C.xLy,y:C.yLy}),hit:Math.max(12,(e.planets&&(C.kind==="star"||C.kind==="planet"||C.kind==="blackhole")?C.importance>=3?20:C.importance>=2?15:C.importance>=1?10.5:8:C.kind==="station"||C.kind==="outpost"?3.5:C.importance>=2?5:3.2)+7)}})),b=pe(()=>{const C=[];for(const P of f()){const y=d().get(P.id);if(!(y!=null&&y.strip))continue;const G=[],Q=P.P.x-y.size/2;for(let le=0;le<l;le++)G.push((Q-le*y.size).toFixed(2));C.push({id:P.id,x:P.P.x-y.size/2,y:P.P.y-y.size/2,size:y.size,offsets:G.join(";")})}return C});function w(C){let P,y=1/0;for(const G of f()){const Q=Math.hypot(G.P.x-C.x,G.P.y-C.y);Q<=G.hit&&Q<y&&(y=Q,P=G.id)}return P}const m=pe(()=>{const C=new Map(e.model.systems.map(y=>[y.id,y])),P=[];for(const y of e.model.routes){const G=C.get(y.from),Q=C.get(y.to);if(!G||!Q)continue;const le=i().toPx({x:G.xLy,y:G.yLy}),re=i().toPx({x:Q.xLy,y:Q.yLy}),se=(le.x+re.x)/2+(re.y-le.y)*.16,oe=(le.y+re.y)/2-(re.x-le.x)*.16;P.push({id:`${y.from}>${y.to}`,d:`M${le.x},${le.y} Q${se},${oe} ${re.x},${re.y}`,kind:y.kind})}return P}),k=pe(()=>{const C=e.model.hexSizeLy,P=[];for(const y of e.model.contested){const[G,Q]=y.split(","),le=wt(at({q:+G,r:+Q},C),C).map(i().toPx);P.push("M"+le.map(re=>`${re.x.toFixed(1)},${re.y.toFixed(1)}`).join("L")+"Z")}return P.join(" ")}),M=pe(()=>{const C=e.pendingCells;if(!C||C.size===0)return"";const P=e.model.hexSizeLy,y=[];for(const G of C){const[Q,le]=G.split(","),re=wt(at({q:+Q,r:+le},P),P).map(i().toPx);y.push("M"+re.map(se=>`${se.x.toFixed(1)},${se.y.toFixed(1)}`).join("L")+"Z")}return y.join(" ")}),S=pe(()=>{const C=e.hoverCell;if(!C)return"";const P=e.model.hexSizeLy;return wt(at(C,P),P).map(i().toPx).map(y=>`${y.x.toFixed(1)},${y.y.toFixed(1)}`).join(" ")}),A=()=>{const C=qt(e.model.ownership),P=new Map;for(const y of e.model.territories){const G=new Set(C.get(y.id)??[]);if(!G.size)continue;const Q=ei(vo(G,e.model.hexSizeLy),i());Q&&P.set(y.id,Q)}return P},I=()=>hn(e.model.extentLy.w,e.model.extentLy.h,e.model.hexSizeLy).map(C=>"M"+wt(at(C,e.model.hexSizeLy),e.model.hexSizeLy).map(i().toPx).map(y=>`${y.x.toFixed(1)},${y.y.toFixed(1)}`).join("L")+"Z"),$=()=>Ni(n().w,n().h,460,90210),Y=()=>{const C=qt(e.model.ownership),P=e.model.systems.map(G=>i().toPx({x:G.xLy,y:G.yLy})),y=[];for(const G of e.model.territories){const Q=C.get(G.id);if(!Q||Q.length<8)continue;let le=0,re=0;for(const de of Q){const[be,H]=de.split(","),ue=at({q:+be,r:+H},e.model.hexSizeLy);le+=ue.x,re+=ue.y}const se=i().toPx({x:le/Q.length,y:re/Q.length});if(G.unclaimed){y.push({id:G.id,name:Oe(G.name,e.locale),p:se,size:12,faint:!0,color:jt(G.color)});continue}const oe=Math.max(11,Math.min(22,Math.sqrt(Q.length)*.95)),ae=Oe(G.name,e.locale);y.push({id:G.id,name:ae,p:B(se,ae,oe,P),size:oe,faint:!1,color:jt(G.color)})}return y};function B(C,P,y,G){const Q=P.length*y*.65/2,le=y*.7,re=[C];for(let ae=1;ae<=4;ae++)for(let de=0;de<12;de++){const be=de/12*Math.PI*2+ae*.3;re.push({x:C.x+Math.cos(be)*ae*17,y:C.y+Math.sin(be)*ae*12})}let se=C,oe=-1/0;for(const ae of re){let de=1/0;for(const H of G){const ue=Math.max(0,Math.abs(H.x-ae.x)-Q),q=Math.max(0,Math.abs(H.y-ae.y)-le);de=Math.min(de,Math.hypot(ue,q))}const be=Math.min(de,34)-Math.hypot(ae.x-C.x,ae.y-C.y)*.3;be>oe&&(oe=be,se=ae)}return se}function ne(C){const P=o;if(!P)return null;const y=P.getBoundingClientRect();return y.width===0||y.height===0?null:{x:C.clientX-y.left,y:C.clientY-y.top}}const ye=()=>({x:i().ox-e.model.extentLy.w/2*i().scale,y:i().oy-e.model.extentLy.h/2*i().scale,width:e.model.extentLy.w*i().scale,height:e.model.extentLy.h*i().scale});return(()=>{var C=mi(),P=C.firstChild,y=P.firstChild,G=y.firstChild,Q=G.nextSibling,le=Q.firstChild,re=y.nextSibling,se=re.nextSibling,oe=se.nextSibling,ae=oe.firstChild,de=ae.nextSibling,be=oe.nextSibling,H=be.nextSibling,ue=H.nextSibling,q=ue.nextSibling,Le=a;typeof Le=="function"?Qe(Le,C):a=C,P.addEventListener("mouseleave",()=>{e.onLeave(),e.stroking&&e.onStrokeEnd()}),P.$$click=v=>{if(e.stroking)return;const T=ne(v);if(T){if(!e.brushArmed&&e.onSystemClick){const x=w(T);if(x){e.onSystemClick(x);return}}e.onClick(i().toLy(T))}},P.$$mouseup=v=>{e.stroking&&(v.preventDefault(),e.onStrokeEnd())},P.$$mousedown=v=>{if(v.button!==0||!e.canPaint||!e.brushArmed)return;const T=ne(v);T&&e.onStrokeStart(i().toLy(T))},P.$$mousemove=v=>{const T=ne(v);if(!T)return;const x=i().toLy(T);e.stroking&&e.onStrokeMove(x),e.onHover(x)};var we=o;return typeof we=="function"?Qe(we,P):o=P,R(y,N($e,{get each(){return b()},children:v=>(()=>{var T=yi(),x=T.firstChild;return U(p=>{var g=`turn-${v.id}`,_=v.x,O=v.y,D=v.size,z=v.size;return g!==p.e&&h(T,"id",p.e=g),_!==p.t&&h(x,"x",p.t=_),O!==p.a&&h(x,"y",p.a=O),D!==p.o&&h(x,"width",p.o=D),z!==p.i&&h(x,"height",p.i=z),p},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0}),T})()}),G),R(y,N($e,{get each(){return e.model.territories},children:v=>(()=>{var T=pi();return R(T,N(Ii,{get kind(){return v.pattern},get color(){return v.color}})),U(()=>h(T,"id",`pat-${v.id}`)),T})()}),Q),wn(le,bn(ye),!0),R(P,N($e,{get each(){return $()},children:v=>(()=>{var T=bi();return U(x=>{var p=v.x,g=v.y,_=v.r,O=v.o;return p!==x.e&&h(T,"cx",x.e=p),g!==x.t&&h(T,"cy",x.t=g),_!==x.a&&h(T,"r",x.a=_),O!==x.o&&h(T,"opacity",x.o=O),x},{e:void 0,t:void 0,a:void 0,o:void 0}),T})()}),se),R(P,N(te,{get when(){return e.showCells},get children(){var v=vi();return R(v,N($e,{get each(){return I()},children:T=>(()=>{var x=xi();return h(x,"d",T),x})()})),v}}),se),R(P,N($e,{get each(){return e.model.territories},children:v=>N(te,{get when(){return A().get(v.id)},get children(){return[(()=>{var T=Nn();return U(x=>{var p=A().get(v.id),g=v.color,_=v.unclaimed?.14:.26;return p!==x.e&&h(T,"d",x.e=p),g!==x.t&&h(T,"fill",x.t=g),_!==x.a&&h(T,"opacity",x.a=_),x},{e:void 0,t:void 0,a:void 0}),T})(),(()=>{var T=Nn();return U(x=>{var p=A().get(v.id),g=`url(#pat-${v.id})`,_=v.unclaimed?.22:.8;return p!==x.e&&h(T,"d",x.e=p),g!==x.t&&h(T,"fill",x.t=g),_!==x.a&&h(T,"opacity",x.a=_),x},{e:void 0,t:void 0,a:void 0}),T})()]}})}),se),R(P,N($e,{get each(){return e.model.territories},children:v=>N(te,{get when(){return A().get(v.id)},get children(){return[(()=>{var T=zn();return U(x=>{var p=A().get(v.id),g=v.color,_=v.unclaimed?2:9,O=v.unclaimed?.16:.28;return p!==x.e&&h(T,"d",x.e=p),g!==x.t&&h(T,"stroke",x.t=g),_!==x.a&&h(T,"stroke-width",x.a=_),O!==x.o&&h(T,"opacity",x.o=O),x},{e:void 0,t:void 0,a:void 0,o:void 0}),T})(),(()=>{var T=zn();return U(x=>{var p=A().get(v.id),g=v.color,_=v.unclaimed?1.2:2.4,O=v.unclaimed?.4:.95;return p!==x.e&&h(T,"d",x.e=p),g!==x.t&&h(T,"stroke",x.t=g),_!==x.a&&h(T,"stroke-width",x.a=_),O!==x.o&&h(T,"opacity",x.o=O),x},{e:void 0,t:void 0,a:void 0,o:void 0}),T})()]}})}),se),R(se,N($e,{get each(){return e.model.territories},children:v=>N(te,{get when(){return Ye(()=>!v.unclaimed)()&&A().get(v.id)},get children(){var T=wi();return U(()=>h(T,"d",A().get(v.id))),T}})})),R(P,N(te,{get when(){return e.stroking},get children(){var v=gi(),T=v.firstChild,x=T.nextSibling;return U(p=>{var g=M(),_=e.pendingColour??"#ffd479",O=M(),D=e.pendingColour??"#ffd479";return g!==p.e&&h(T,"d",p.e=g),_!==p.t&&h(T,"fill",p.t=_),O!==p.a&&h(x,"d",p.a=O),D!==p.o&&h(x,"stroke",p.o=D),p},{e:void 0,t:void 0,a:void 0,o:void 0}),v}}),oe),R(be,N($e,{get each(){return m()},children:v=>(()=>{var T=_i();return U(x=>{var p=v.d,g=v.kind==="gate"?"7 5":"3 5";return p!==x.e&&h(T,"d",x.e=p),g!==x.t&&h(T,"stroke-dasharray",x.t=g),x},{e:void 0,t:void 0}),T})()})),R(P,N($e,{get each(){return Y()},children:v=>(()=>{var T=ki();return R(T,()=>v.name),U(x=>{var p=v.p.x,g=v.p.y,_=`${v.size}px`,O=v.faint?"1px":`${v.size*.3}px`,D=e.selected===v.id,z=!!(e.selected&&e.selected!==v.id),j=!!v.faint,J=v.color;return p!==x.e&&h(T,"x",x.e=p),g!==x.t&&h(T,"y",x.t=g),_!==x.a&&h(T,"font-size",x.a=_),O!==x.o&&h(T,"letter-spacing",x.o=O),D!==x.i&&T.classList.toggle("hot",x.i=D),z!==x.n&&T.classList.toggle("dim",x.n=z),j!==x.s&&T.classList.toggle("faint",x.s=j),J!==x.h&&h(T,"fill",x.h=J),x},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0,s:void 0,h:void 0}),T})()}),H),R(H,N($e,{get each(){return f()},children:v=>{const T=()=>d().get(v.system.id),x=()=>e.hoverSystem===v.system.id;return[N(te,{get when(){return Ye(()=>!!e.onSystemClick)()&&!e.brushArmed},get children(){var p=Si();return p.addEventListener("mouseleave",()=>{var g;return(g=e.onSystemHover)==null?void 0:g.call(e,void 0)}),p.addEventListener("mouseenter",()=>{var g;return(g=e.onSystemHover)==null?void 0:g.call(e,v.system.id)}),U(g=>{var _=!!x(),O=v.system.id,D=v.P.x,z=v.P.y,j=v.hit;return _!==g.e&&p.classList.toggle("hot",g.e=_),O!==g.t&&h(p,"data-sys",g.t=O),D!==g.a&&h(p,"cx",g.a=D),z!==g.o&&h(p,"cy",g.o=z),j!==g.i&&h(p,"r",g.i=j),g},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0}),p}}),N(te,{get when(){return v.system.importance===3},get children(){var p=$i();return U(g=>{var j;var _=v.P.x,O=v.P.y,D=((j=T())==null?void 0:j.ring)??10,z=v.colour;return _!==g.e&&h(p,"cx",g.e=_),O!==g.t&&h(p,"cy",g.t=O),D!==g.a&&h(p,"r",g.a=D),z!==g.o&&h(p,"stroke",g.o=z),g},{e:void 0,t:void 0,a:void 0,o:void 0}),p}}),N(te,{get when(){return v.system.kind==="station"||v.system.kind==="outpost"},get children(){var p=Ai();return U(g=>{var _=v.P.x-3.5,O=v.P.y-3.5;return _!==g.e&&h(p,"x",g.e=_),O!==g.t&&h(p,"y",g.t=O),g},{e:void 0,t:void 0}),p}}),N(te,{get when(){return v.system.kind==="blackhole"},get children(){return N($e,{get each(){return[c(v.system.id,v.system.importance)]},children:p=>(()=>{var g=Mi();return U(_=>{var O=v.system.id,D=ga({seed:p.seed,px:p.px,dpr:p.dpr}),z=v.P.x-p.px/2,j=v.P.y-p.px/2,J=p.px,_e=p.px;return O!==_.e&&h(g,"data-bh",_.e=O),D!==_.t&&h(g,"href",_.t=D),z!==_.a&&h(g,"x",_.a=z),j!==_.o&&h(g,"y",_.o=j),J!==_.i&&h(g,"width",_.i=J),_e!==_.n&&h(g,"height",_.n=_e),_},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0}),g})()})}}),N(te,{get when(){return v.system.kind==="pulsar"||v.system.kind==="quasar"},get children(){return N(zo,{get x(){return v.P.x},get y(){return v.P.y},get size(){return c(v.system.id,v.system.importance).px},get kind(){return v.system.kind},get seed(){return ot(v.system.id)}})}}),N(te,{get when(){return v.system.kind==="star"||v.system.kind==="planet"},get children(){return N(te,{get when(){return T()},get fallback(){return(()=>{var p=Ei();return U(g=>{var _=v.P.x,O=v.P.y,D=v.system.importance>=2?5:3.2,z=v.colour;return _!==g.e&&h(p,"cx",g.e=_),O!==g.t&&h(p,"cy",g.t=O),D!==g.a&&h(p,"r",g.a=D),z!==g.o&&h(p,"fill",g.o=z),g},{e:void 0,t:void 0,a:void 0,o:void 0}),p})()},children:p=>[N(te,{get when(){return p().saturn},get children(){return N($e,{each:[0,1,2],children:g=>(()=>{var _=Ci();return U(O=>{var D=v.system.id,z=on(rn(p().size,ot(v.system.id)),!0,g/3,(g+1)/3),j=`translate(${v.P.x} ${v.P.y})`,J=["#6b5f4c","#3d3428","#8f8064"][g];return D!==O.e&&h(_,"data-saturn",O.e=D),z!==O.t&&h(_,"d",O.t=z),j!==O.a&&h(_,"transform",O.a=j),J!==O.o&&h(_,"fill",O.o=J),O},{e:void 0,t:void 0,a:void 0,o:void 0}),_})()})}}),N(te,{get when(){return p().strip},get fallback(){return(()=>{var g=Pi();return U(_=>{var O=p().href,D=v.P.x-p().size/2,z=v.P.y-p().size/2,j=p().size,J=p().size;return O!==_.e&&h(g,"href",_.e=O),D!==_.t&&h(g,"x",_.t=D),z!==_.a&&h(g,"y",_.a=z),j!==_.o&&h(g,"width",_.o=j),J!==_.i&&h(g,"height",_.i=J),_},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0}),g})()},get children(){var g=Li(),_=g.firstChild,O=_.firstChild;return U(D=>{var Ve;var z=`url(#turn-${v.system.id})`,j=v.system.id,J=p().strip,_e=v.P.x-p().size/2,Fe=v.P.y-p().size/2,Te=p().size*l,Ue=p().size,qe=((Ve=b().find(Je=>Je.id===v.system.id))==null?void 0:Ve.offsets)??"";return z!==D.e&&h(g,"clip-path",D.e=z),j!==D.t&&h(_,"data-turning",D.t=j),J!==D.a&&h(_,"href",D.a=J),_e!==D.o&&h(_,"x",D.o=_e),Fe!==D.i&&h(_,"y",D.i=Fe),Te!==D.n&&h(_,"width",D.n=Te),Ue!==D.s&&h(_,"height",D.s=Ue),qe!==D.h&&h(O,"values",D.h=qe),D},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0,s:void 0,h:void 0}),g}}),N(te,{get when(){return p().saturn},get children(){return N($e,{each:[0,1,2],children:g=>(()=>{var _=Ri();return U(O=>{var D=v.system.id,z=on(rn(p().size,ot(v.system.id)),!1,g/3,(g+1)/3),j=`translate(${v.P.x} ${v.P.y})`,J=["#cdc2a8","#7b6f56","#f6efdd"][g];return D!==O.e&&h(_,"data-saturn",O.e=D),z!==O.t&&h(_,"d",O.t=z),j!==O.a&&h(_,"transform",O.a=j),J!==O.o&&h(_,"fill",O.o=J),O},{e:void 0,t:void 0,a:void 0,o:void 0}),_})()})}})]})}}),(()=>{var p=Ti();return R(p,()=>Oe(v.system.name,e.locale)),U(g=>{var _=v.P.x+(v.system.kind==="blackhole"?c(v.system.id,v.system.importance).px/2+5:v.system.importance===3?15:9),O=v.P.y-5,D=v.system.id,z=v.system.importance===3;return _!==g.e&&h(p,"x",g.e=_),O!==g.t&&h(p,"y",g.t=O),D!==g.a&&h(p,"data-sys",g.a=D),z!==g.o&&p.classList.toggle("capital",g.o=z),g},{e:void 0,t:void 0,a:void 0,o:void 0}),p})()]}})),wn(q,bn(ye,{class:"frame-line"}),!0),U(v=>{var T=!!e.painting,x=!!e.stroking,p=n().w,g=n().h,_=`0 0 ${n().w} ${n().h}`,O=n().w,D=n().h,z=k(),j=k(),J=!e.hoverCell,_e=S();return T!==v.e&&P.classList.toggle("painting",v.e=T),x!==v.t&&P.classList.toggle("stroking",v.t=x),p!==v.a&&h(P,"width",v.a=p),g!==v.o&&h(P,"height",v.o=g),_!==v.i&&h(P,"viewBox",v.i=_),O!==v.n&&h(re,"width",v.n=O),D!==v.s&&h(re,"height",v.s=D),z!==v.h&&h(ae,"d",v.h=z),j!==v.r&&h(de,"d",v.r=j),J!==v.d&&ue.classList.toggle("off",v.d=J),_e!==v.l&&h(ue,"points",v.l=_e),v},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0,s:void 0,h:void 0,r:void 0,d:void 0,l:void 0}),C})()}un(["mousemove","mousedown","mouseup","click"]);function gn(e){return`
uniform float u_size;
uniform float u_seed;
uniform float u_pixels;
uniform vec3  u_palette[8];
uniform vec4  u_background;

/**
 * The aspect correction, and it is here because BOTH layers use it.
 *
 * Declaring it per-shader put it only in Nebulae, which compiled and drew normally
 * while StarStuff failed on an undeclared identifier and drew nothing at all.
 *
 * Nothing said so. The layer helper logs the compile error and returns; the second
 * layer then covers the ground the first would have drawn; and the result is
 * indistinguishable from a layer that is merely subtle. It was found by a NEGATIVE
 * CONTROL -- suppressing StarStuff and getting byte-identical numbers -- which is
 * the only reason it was found at all. Hence also Nebula.layers, so a check can
 * assert that both programs built. (No backticks in these comments: they terminate
 * the template literal the shader lives in. Fourth time.)
 */
uniform vec2  u_uvCorrect;

/**
 * The reference's hash. Note the seed is ADDED to the multiplier here, where the
 * planet shaders MULTIPLY by it:
 *
 *     planet shaders:  fract(sin(dot(c, k)) * 15.5453 * seed)
 *     background:      fract(sin(dot(c, k)) * (15.5453 + seed))
 *
 * Both are faithful to their own source, and they are not the same function. Copying
 * one into the other would change every background without changing a line of the
 * original, which is the sort of thing that is invisible until two images that should
 * match do not.
 */
float rnd(vec2 coord, float tilesize) {
  return fract(sin(dot(coord, vec2(12.9898, 78.233))) * (15.5453 + u_seed));
}

float vnoise(vec2 coord, float tilesize) {
  vec2 i = floor(coord);
  vec2 f = fract(coord);
  float a = rnd(i, tilesize);
  float b = rnd(i + vec2(1.0, 0.0), tilesize);
  float c = rnd(i + vec2(0.0, 1.0), tilesize);
  float d = rnd(i + vec2(1.0, 1.0), tilesize);
  vec2 cubic = f * f * (3.0 - 2.0 * f);
  return mix(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;
}

float fbm(vec2 coord, float tilesize) {
  float value = 0.0;
  float scale = 0.5;
  for (int i = 0; i < ${e}; i++) {
    value += vnoise(coord, tilesize) * scale;
    coord *= 2.0;
    scale *= 0.5;
  }
  return value;
}

/** Ordered dither. Transcribed with its arguments the reference passes them. */
bool dither(vec2 uv1, vec2 uv2) {
  return mod(uv1.y + uv2.x, 2.0 / u_pixels) <= 1.0 / u_pixels;
}

/** By Leukbaars, from https://www.shadertoy.com/view/4tK3zR */
float circleNoise(vec2 uv, float tilesize) {
  float uv_y = floor(uv.y);
  uv.x += uv_y * 0.31;
  vec2 f = fract(uv);
  float h = rnd(vec2(floor(uv.x), floor(uv_y)), tilesize);
  float m = length(f - 0.25 - (h * 0.5));
  float r = h * 0.25;
  return smoothstep(0.0, r, m * 0.75);
}

/** Two iterations of circleNoise summed into the fbm's domain. */
float cloud_alpha(vec2 uv, float tilesize) {
  float c_noise = 0.0;
  for (int i = 0; i < 2; i++) {
    c_noise += circleNoise(uv * 0.5 + (float(i + 1)) + vec2(-0.3, 0.0), ceil(tilesize * 0.5));
  }
  return fbm(uv + c_noise, tilesize);
}

/**
 * The colourscheme, as a lerp across its eight evenly spaced stops.
 *
 * The reference samples a 1D gradient texture at \`vec2(col_value, 0)\`, and its
 * Colorscheme.tres puts the stops at 0, 1/7, ... 1, so this is exact.
 */
vec3 ramp(float t) {
  float x = clamp(t, 0.0, 1.0) * 7.0;

  // UNROLLED, and it has to be.
  //
  // The obvious version is a loop with u_palette[i], and it will not compile:
  // GLSL ES 1.00 permits only CONSTANT index expressions on a uniform array, so
  // "the index is an int" is a compile error rather than a runtime one. Every index
  // below is a literal, which is the only reason this works at all.
  vec3 a = u_palette[0];
  vec3 b = u_palette[1];
  if (x >= 6.0)      { a = u_palette[6]; b = u_palette[7]; }
  else if (x >= 5.0) { a = u_palette[5]; b = u_palette[6]; }
  else if (x >= 4.0) { a = u_palette[4]; b = u_palette[5]; }
  else if (x >= 3.0) { a = u_palette[3]; b = u_palette[4]; }
  else if (x >= 2.0) { a = u_palette[2]; b = u_palette[3]; }
  else if (x >= 1.0) { a = u_palette[1]; b = u_palette[2]; }
  return mix(a, b, fract(x));
}
`}const Yt=[{label:"ABYSS",note:"cool and deep, so the territories keep their colours",stops:["#070a12","#0c1120","#111a30","#172440","#1d2f52","#243a64","#2c4676","#345287"],background:"#070a12"},{label:"EMBER",note:"the reference's own ramp, warm end to end",stops:["#1e2118","#6b4420","#a2543a","#ca5a2e","#ff7831","#f39949","#ebc275","#dfd785"],background:"#171711"},{label:"DUST",note:"neutral and nearly flat, for when the gas should not be noticed",stops:["#08080a","#101013","#18171b","#201e23","#28252a","#302c31","#383338","#403a3f"],background:"#08080a"}],ln=0,_a={size:5,octaves:3,seed:4.507,pixels:500},ka={size:10,octaves:8,seed:6.521,pixels:500},Di=`
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y flipped, for the same reason the ring's is: Godot's UV has (0,0) top-left and
  // WebGL's has it bottom-left, and the background's dither and centre-distance both
  // care about which way up it is.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`,Fi=`
precision highp float;
varying vec2 v_uv;
${gn(_a.octaves)}
uniform bool u_reduce;
uniform bool u_tile;

void main() {
  vec2 uv = floor(v_uv * u_pixels) / u_pixels;

  // Distance from the centre, for the radial falloff. The tiling branch drops this.
  float d = distance(uv, vec2(0.5)) * 0.4;

  uv *= u_uvCorrect;
  bool dith = dither(uv, v_uv);

  float n = cloud_alpha(uv * u_size, u_size);
  float n2 = fbm(uv * u_size + vec2(1.0, 1.0), u_size);
  float n_lerp = n2 * n;
  // The reference computes n_dust from the SAME expression as n. Transcribed, not
  // tidied: see the header.
  float n_dust = cloud_alpha(uv * u_size, u_size);
  float n_dust_lerp = n_dust * n_lerp;

  if (dith) {
    n_dust_lerp *= 0.95;
    n_lerp *= 0.95;
    d *= 0.98;
  }

  // Two thresholds a hair apart, which is what makes the thin bright band around the
  // nebula's edge. step(edge, x) is x >= edge, so this reads "0.1 + d has reached
  // n2" -- i.e. the nebula is where the noise is LOW.
  //
  // (No backticks in these comments: they terminate the template literal the shader
  // lives in. Third time.)
  float a = step(n2, 0.1 + d);
  float a2 = step(n2, 0.115 + d);
  if (u_tile) {
    a = step(n2, 0.3);
    a2 = step(n2, 0.315);
  }

  if (u_reduce) {
    n_dust_lerp = pow(n_dust_lerp, 1.2) * 0.7;
  }

  float col_value = 0.0;
  if (a2 > a) {
    col_value = floor(n_dust_lerp * 35.0) / 7.0;
  } else {
    col_value = floor(n_dust_lerp * 14.0) / 7.0;
  }

  vec3 col = ramp(col_value);
  if (col_value < 0.1) {
    col = u_background.rgb;
  }

  if (a2 < 0.5) discard;
  gl_FragColor = vec4(col, 1.0);
}`,Ui=`
precision highp float;
varying vec2 v_uv;
${gn(ka.octaves)}
uniform bool u_reduce;

void main() {
  vec2 uv = floor(v_uv * u_pixels) / u_pixels * u_uvCorrect;
  bool dith = dither(uv, v_uv);

  float n_alpha = fbm(uv * ceil(u_size * 0.5) + vec2(2.0, 2.0), ceil(u_size * 0.5));
  float n_dust = cloud_alpha(uv * u_size, u_size);
  float n_dust2 = fbm(uv * ceil(u_size * 0.2) - vec2(2.0, 2.0), ceil(u_size * 0.2));
  float n_dust_lerp = n_dust2 * n_dust;

  if (dith) {
    n_dust_lerp *= 0.95;
  }

  float a_dust = step(n_alpha, n_dust_lerp * 1.8);
  n_dust_lerp = pow(n_dust_lerp, 3.2) * 56.0;
  if (dith) {
    n_dust_lerp *= 1.1;
  }
  if (u_reduce) {
    n_dust_lerp = pow(n_dust_lerp, 0.8) * 0.7;
  }

  float col_value = floor(n_dust_lerp) / 7.0;
  vec3 col = ramp(col_value);

  if (a_dust < 0.5) discard;
  gl_FragColor = vec4(col, 1.0);
}`;function Dn(e){const n=parseInt(e.slice(1),16);return[(n>>16&255)/255,(n>>8&255)/255,(n&255)/255]}function qi(){if(typeof document>"u")return!1;try{const e=document.createElement("canvas");return!!(e.getContext("webgl2")||e.getContext("webgl")||e.getContext("experimental-webgl"))}catch{return!1}}function Bi(e){if(!qi())return null;const n=Yt[e.palette??ln]??Yt[ln],t=document.createElement("canvas"),a=t.getContext("webgl2",{preserveDrawingBuffer:!0,alpha:!0,antialias:!1})??t.getContext("webgl",{preserveDrawingBuffer:!0,alpha:!0,antialias:!1});if(!a)return null;const o=(m,k)=>{const M=a.createShader(m);return M?(a.shaderSource(M,k),a.compileShader(M),a.getShaderParameter(M,a.COMPILE_STATUS)?M:(console.warn("[GalaxyMap] background shader failed",a.getShaderInfoLog(M)),a.deleteShader(M),null)):null},s=o(a.VERTEX_SHADER,Di);if(!s)return null;const i=a.createBuffer();a.bindBuffer(a.ARRAY_BUFFER,i),a.bufferData(a.ARRAY_BUFFER,new Float32Array([-1,-1,3,-1,-1,3]),a.STATIC_DRAW);const r=()=>window.devicePixelRatio||1;let l=0,u=0,d=!1,c=0;const f=(m,k)=>{const M=o(a.FRAGMENT_SHADER,m);if(!M)return;const S=a.createProgram();if(!S)return;if(a.attachShader(S,s),a.attachShader(S,M),a.linkProgram(S),!a.getProgramParameter(S,a.LINK_STATUS)){console.warn("[GalaxyMap] background link failed",a.getProgramInfoLog(S));return}a.useProgram(S);const A=a.getAttribLocation(S,"a_pos");a.enableVertexAttribArray(A),a.vertexAttribPointer(A,2,a.FLOAT,!1,0,0);const I=Y=>a.getUniformLocation(S,Y),$=(e.seed??1)+k.seed*1e-4;a.uniform1f(I("u_size"),k.size),a.uniform1f(I("u_seed"),$),a.uniform1f(I("u_pixels"),k.pixels),a.uniform1i(I("u_reduce"),e.reduce?1:0),a.uniform1i(I("u_tile"),1),a.uniform2f(I("u_uvCorrect"),u>l?l/u:1,l>u?u/l:1),a.uniform4fv(I("u_background"),[...Dn(n.background),1]),a.uniform3fv(I("u_palette"),n.stops.flatMap(Y=>Dn(Y))),a.enable(a.BLEND),a.blendFunc(a.SRC_ALPHA,a.ONE_MINUS_SRC_ALPHA),a.viewport(0,0,l,u),a.drawArrays(a.TRIANGLES,0,3),c++,a.deleteProgram(S),a.deleteShader(M)},b=()=>{const m=Math.max(1,Math.round(e.px*r())),k=Math.max(1,Math.round(e.px*r()*.58));return t.width===m&&t.height===k&&l===m?!1:(t.width=m,t.height=k,l=m,u=k,!0)};return(()=>{d||b()&&(t.style.width=`${e.px}px`,t.style.height=`${Math.round(e.px*.58)}px`,a.viewport(0,0,l,u),a.clearColor(0,0,0,0),a.clear(a.COLOR_BUFFER_BIT),f(Ui,ka),f(Fi,_a))})(),c!==2&&console.warn(`[GalaxyMap] background drew ${c} of 2 layers; see the compile errors above`),{canvas:t,palette:n.label,layers:c,dispose(){var m;d=!0,(m=a.getExtension("WEBGL_lose_context"))==null||m.loseContext()}}}var Gi=L("<div class=nebula-backdrop aria-hidden=true>");function Hi(e){let n;const t=()=>{const a=n.getBoundingClientRect();if(a.width<8)return;const o=Bi({px:Math.round(a.width),reduce:e.reduce,seed:e.seed,palette:e.palette});o&&(o.canvas.classList.add("nebula-canvas"),n.dataset.layers=String(o.layers),n.dataset.palette=o.palette,n.replaceChildren(o.canvas),ze(()=>o.dispose()))};return St(()=>{t();let a;it(()=>{e.palette,e.reduce,t()});const o=new ResizeObserver(()=>{a&&clearTimeout(a),a=setTimeout(t,180)});o.observe(n),ze(()=>{a&&clearTimeout(a),o.disconnect()})}),(()=>{var a=Gi(),o=n;return typeof o=="function"?Qe(o,a):n=a,a})()}const Wi=4,Ee={reach:.46,base:.026,tip:.019,tilt:-.314,period:1.6},Vi=`
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y is flipped, for the same reason the ring's is: Godot's UV has (0,0) at the top
  // left and WebGL's at the bottom left, so a straight port puts every half-space test
  // on the wrong half. Once, here, and everything downstream is in Godot's convention.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`,ji=`precision highp float;
`+gn(Wi)+`
uniform float u_reach;
uniform float u_base;
uniform float u_tip;
uniform float u_tilt;
uniform float u_period;
uniform float u_phase;
uniform float u_time;

varying vec2 v_uv;

void main() {
  vec2 uv = v_uv;

  // The tilt, applied to the sample point rather than to the geometry, so there is no
  // transform to keep in sync with anything else.
  float c = cos(u_tilt);
  float s = sin(u_tilt);
  vec2 p = vec2(uv.x - 0.5, uv.y - 0.5);
  p = vec2(p.x * c - p.y * s, p.x * s + p.y * c);

  float t = p.y;
  float x = p.x;
  float at = abs(t);

  // The axis wanders. Low frequency, so the beam bends over its length instead of
  // buzzing. A straight beam is a ruler.
  float axis = (fbm(vec2(0.0, at * 3.0) + u_phase, 1.0) - 0.5) * 0.03;
  float across = x - axis;

  // The envelope along the beam: bright at the pole, gone by the tip. Declared before
  // the width because the width depends on it.
  //
  // The SVG had to CUT the polygon at the tip, and a cut is a cut at every resolution.
  // Here the tip goes to zero, and -- the part that matters -- the beam also NARROWS as
  // it fades, so it tapers to a point instead of ending in a rounded bulb. Fading only
  // the brightness of a constant-width beam gives exactly that bulb, because the
  // gaussian profile is widest at the tip and a dim wide end reads as a lozenge.
  float along = smoothstep(u_reach, u_reach * 0.18, at);
  along *= smoothstep(0.0, 0.05, at);

  // Near-parallel, and narrowing toward the tip with the envelope rather than widening
  // to it.
  float w = u_base + (u_tip - u_base) * pow(at, 0.75);
  w *= mix(0.22, 1.0, along);

  // The knot, as a fraction of the way out. Wraps, so it leaves at the tip and a new
  // one leaves the pole.
  float k = fract(at / u_reach - u_time / u_period);

  // Two gaussians per cycle: the shock itself, and a second one behind it that is
  // wider and dimmer. One travelling feature reads as a dot going round a circle; two
  // with different widths read as a wake.
  float shock = exp(-pow((k - 0.14) / 0.085, 2.0));
  float wake = 0.45 * exp(-pow((k - 0.02) / 0.20, 2.0));

  // The knot SWELLS the beam. That is what makes it a bulge and not a bright dot, and
  // it is the thing a moving gradient could not do at all.
  w *= 1.0 + 1.1 * shock + 0.4 * wake;

  // Across the beam: a bell with a flat core, not a linear ramp. This is the mask the
  // SVG version needed two luminance masks for, and here it is one expression.
  float u = abs(across) / max(w, 1e-4);
  float profile = exp(-u * u * 2.2);
  // The cutoff starts at 0.35 rather than at the edge, so the flank has somewhere to
  // fade to zero over several pixels. Cutting at the silhouette instead gives a step,
  // and a step on a thin bright shape is the single most obvious way to tell it is a
  // polygon.
  profile *= smoothstep(1.0, 0.35, u);

  // The interior. Anisotropic on purpose: 12.0 across the beam and 1.4 along it, so
  // the texture is drawn out into filaments the way plasma accelerating out of a pole
  // actually is. Sampled in the beam's OWN frame, so the structure rides the bend
  // rather than sliding across it.
  // 240 across the beam and 5 along it, so about four noise cells fit ACROSS a beam
  // that is 0.02 wide and several fit along its length. The first attempt used 12.0
  // across, which over a beam 0.02 wide samples a range of 0.24 -- less than one
  // noise cell, so the whole beam got a single flat value and the interior texture
  // that is the entire reason for using a shader was simply not there.
  float fil = fbm(vec2(across * 240.0, at * 5.0) + u_phase, 1.0);
  // Amplitude matters more than frequency here. At 0.75 the filaments were there but
  // the beam still read as milk; the contrast between a filament and the gap beside it
  // is what makes it look like plasma rather than a painted shape.
  float turb = 0.28 + 1.15 * fil * fil;
  // The base is smooth and the far end is turbulent, which is right: the shear grows
  // with distance from the source.
  turb = mix(1.0, turb, smoothstep(0.0, 0.55, at));

  float a = profile * along * turb;
  // The shock is additive on top of the beam's own brightness, so it reads as brighter
  // rather than as a different colour laid over the top.
  a += profile * along * (shock * 0.9 + wake * 0.25);
  // Optically thin. A jet you cannot see through is a painted ribbon, and this one is
  // drawn OVER the accretion disc on purpose -- so any opacity here is opacity the disc
  // loses.
  a *= 0.78;
  a = clamp(a, 0.0, 1.0);

  if (a < 0.004) discard;

  // Colour from cold blue-white at the pole through to a dim blue at the tip. The knot
  // is pushed towards white, because a shock front is the hottest thing in the beam.
  float heat = clamp(1.0 - at / u_reach, 0.0, 1.0);
  vec3 cold = vec3(0.42, 0.58, 0.86);
  vec3 mid = vec3(0.78, 0.86, 1.0);
  vec3 col = mix(cold, mid, heat);
  col = mix(col, vec3(1.0), clamp(shock * 0.8 + wake * 0.3, 0.0, 1.0));

  gl_FragColor = vec4(col, a);
}`;function Yi(e){if(typeof document===void 0)return null;const n=document.createElement("canvas");let t=null;const a={preserveDrawingBuffer:!0,alpha:!0,antialias:!1};try{t=n.getContext("webgl2",a)??n.getContext("webgl",a)??n.getContext("experimental-webgl",a)}catch{return null}if(!t)return null;const o=(A,I)=>{const $=t.createShader(A);return $?(t.shaderSource($,I),t.compileShader($),t.getShaderParameter($,t.COMPILE_STATUS)?$:(console.warn("[GalaxyMap] jet shader failed",t.getShaderInfoLog($)),t.deleteShader($),null)):null},s=o(t.VERTEX_SHADER,Vi),i=o(t.FRAGMENT_SHADER,ji);if(!s||!i)return null;const r=t.createProgram();if(!r)return null;if(t.attachShader(r,s),t.attachShader(r,i),t.linkProgram(r),!t.getProgramParameter(r,t.LINK_STATUS))return console.warn("[GalaxyMap] jet link failed",t.getProgramInfoLog(r)),null;t.useProgram(r);const l=t.createBuffer();t.bindBuffer(t.ARRAY_BUFFER,l),t.bufferData(t.ARRAY_BUFFER,new Float32Array([-1,-1,3,-1,-1,3]),t.STATIC_DRAW);const u=t.getAttribLocation(r,"a_pos");t.enableVertexAttribArray(u),t.vertexAttribPointer(u,2,t.FLOAT,!1,0,0);const d=A=>t.getUniformLocation(r,A),c=A=>A-Math.floor(A);t.uniform1f(d("u_reach"),Ee.reach),t.uniform1f(d("u_base"),Ee.base),t.uniform1f(d("u_tip"),Ee.tip),t.uniform1f(d("u_tilt"),Ee.tilt),t.uniform1f(d("u_period"),Ee.period),t.uniform1f(d("u_phase"),e.phase??0),t.uniform1f(d("u_seed"),1+c(e.seed*.6180339887)*9),t.uniform1f(d("u_size"),1),t.uniform1f(d("u_pixels"),e.canvasPx),t.uniform1f(d("u_time"),0);const f=()=>window.devicePixelRatio||1;let b=0,w=!1;const m=()=>{const A=Math.max(1,Math.round(e.canvasPx*f()));n.width!==A&&(n.width=A,n.height=A),n.style.width=`${e.canvasPx}px`,n.style.height=`${e.canvasPx}px`,t.viewport(0,0,A,A)};m();const k=performance.now(),M=d("u_time"),S=A=>{w||(m(),t.uniform1f(M,(A-k)/1e3),t.drawArrays(t.TRIANGLES,0,3),e.animate!==!1&&(b=requestAnimationFrame(S)))};return S(k),{canvas:n,dispose(){w=!0,b&&cancelAnimationFrame(b),t=null}}}var Ki=L("<canvas class=pulsar aria-hidden=true>"),Xi=L("<svg class=quasar-jets aria-hidden=true><defs></defs><g transform=rotate(-18)><path></path><path></path><path></path><path>"),Zi=L('<div class="world-gl quasar-jets-gl">'),Qi=L("<div class=quasar>"),Ji=L("<svg><linearGradient gradientUnits=userSpaceOnUse y1=0 y2=0><stop offset=0% stop-color=#000000></stop><stop offset=28% stop-color=#555555></stop><stop offset=50% stop-color=#ffffff></stop><stop offset=72% stop-color=#555555></stop><stop offset=100% stop-color=#000000></svg>",!1,!0,!1),er=L("<svg><stop offset=0% stop-color=#eaf4ff stop-opacity=0></svg>",!1,!0,!1),tr=L("<svg><stop offset=42% stop-color=#ffffff stop-opacity=0.95></svg>",!1,!0,!1),nr=L("<svg><stop offset=58% stop-color=#eaf4ff stop-opacity=0.55></svg>",!1,!0,!1),ar=L("<svg><stop offset=100% stop-color=#eaf4ff stop-opacity=0></svg>",!1,!0,!1),or=L("<svg><stop offset=0% stop-color=#ffffff stop-opacity=0.95></svg>",!1,!0,!1),ir=L("<svg><stop offset=14% stop-color=#eaf4ff stop-opacity=0.7></svg>",!1,!0,!1),rr=L('<svg><stop offset=52% stop-color="rgba(120, 170, 255, 0.28)"stop-opacity=0.3></svg>',!1,!0,!1),sr=L('<svg><stop offset=100% stop-color="rgba(120, 170, 255, 0.28)"stop-opacity=0.04></svg>',!1,!0,!1),lr=L("<svg><linearGradient x1=0 y1=1 x2=0 y2=0></svg>",!1,!0,!1),cr=L("<svg><linearGradient x1=0 y1=0 x2=0 y2=1></svg>",!1,!0,!1),dr=L("<svg><linearGradient x1=0 y1=1 x2=0 y2=0><animate attributeName=y1 values=1;0.02;-1 dur=1.6s repeatCount=indefinite></animate><animate attributeName=y2 values=2;1.02;0 dur=1.6s repeatCount=indefinite></svg>",!1,!0,!1),ur=L("<svg><linearGradient x1=0 y1=1 x2=0 y2=0><animate attributeName=y1 values=0;0.98;1.98 dur=1.6s begin=-0.8s repeatCount=indefinite></animate><animate attributeName=y2 values=1;1.98;2.98 dur=1.6s begin=-0.8s repeatCount=indefinite></svg>",!1,!0,!1),Fn=L("<svg><mask maskUnits=userSpaceOnUse><rect></svg>",!1,!0,!1);const hr="#ffffff",fr="#cfe2ff",vr="rgba(150, 196, 255, 0.5)";function gr(e){let n;return St(()=>{const t=()=>window.devicePixelRatio||1,a=e.animate===!1||typeof matchMedia<"u"&&matchMedia("(prefers-reduced-motion: reduce)").matches,o=9e3,s=performance.now();let i=0,r=!1;const l=u=>{if(r)return;const d=Math.max(1,Math.round(e.px*t()));n.width!==d&&(n.width=d,n.height=d);const c=n.getContext("2d");if(!c)return;c.setTransform(1,0,0,1,0,0),c.clearRect(0,0,d,d);const f=d/2,b=d/2,w=d*.62,m=Math.max(1.2,d*.018),k=a?.6:(u-s)/o*Math.PI*2;c.globalCompositeOperation="lighter";for(let S=0;S<2;S++){const A=k+S*Math.PI;c.save(),c.translate(f,b),c.rotate(A);const I=c.createRadialGradient(0,0,m,0,0,w);I.addColorStop(0,vr),I.addColorStop(.35,"rgba(150, 196, 255, 0.22)"),I.addColorStop(1,"rgba(150, 196, 255, 0)"),c.fillStyle=I,c.beginPath(),c.moveTo(0,-m*.6),c.quadraticCurveTo(-w*.2,-w*.5,0,-w),c.quadraticCurveTo(w*.2,-w*.5,0,-m*.6),c.closePath(),c.fill(),c.restore()}const M=c.createRadialGradient(f,b,0,f,b,d*.2);M.addColorStop(0,"rgba(207, 226, 255, 0.55)"),M.addColorStop(.4,"rgba(160, 200, 255, 0.16)"),M.addColorStop(1,"rgba(160, 200, 255, 0)"),c.fillStyle=M,c.beginPath(),c.arc(f,b,d*.2,0,Math.PI*2),c.fill(),c.globalCompositeOperation="source-over",c.fillStyle=fr,c.beginPath(),c.arc(f,b,m*1.9,0,Math.PI*2),c.fill(),c.fillStyle=hr,c.beginPath(),c.arc(f,b,m,0,Math.PI*2),c.fill()};if(l(s),!a){const u=d=>{l(d),r||(i=requestAnimationFrame(u))};i=requestAnimationFrame(u)}ze(()=>{r=!0,i&&cancelAnimationFrame(i)})}),(()=>{var t=Ki(),a=n;return typeof a=="function"?Qe(a,t):n=t,U(o=>{var s=`${e.px}px`,i=`${e.px}px`;return s!==o.e&&fe(t,"width",o.e=s),i!==o.t&&fe(t,"height",o.t=i),o},{e:void 0,t:void 0}),t})()}function mr(e){const[n,t]=me(!1);let a;return it(()=>{const o=Yi({canvasPx:e.px,seed:e.seed,phase:e.seed*.37,animate:!e.reducedMotion});if(!o)return;t(!1),a==null||a.appendChild(o.canvas);const s=requestAnimationFrame(()=>t(!0));ze(()=>{cancelAnimationFrame(s),o.dispose(),o.canvas.remove()})}),[(()=>{var o=Xi(),s=o.firstChild,i=s.nextSibling,r=i.firstChild,l=r.nextSibling,u=l.nextSibling,d=u.nextSibling;return R(s,()=>xr(e.seed,e.px)),U(c=>{var f=e.px,b=e.px,w=`${-e.px/2} ${-e.px/2} ${e.px} ${e.px}`,m=n()?"hidden":"visible",k=Pt(1,e.px,1),M=`url(#plume-up-${e.seed})`,S=`url(#beam-${e.seed})`,A=Pt(1,e.px,ct),I=`url(#knot-up-${e.seed})`,$=`url(#flare-${e.seed})`,Y=Pt(-1,e.px,1),B=`url(#plume-down-${e.seed})`,ne=`url(#beam-${e.seed})`,ye=Pt(-1,e.px,ct),C=`url(#knot-down-${e.seed})`,P=`url(#flare-${e.seed})`;return f!==c.e&&h(o,"width",c.e=f),b!==c.t&&h(o,"height",c.t=b),w!==c.a&&h(o,"viewBox",c.a=w),m!==c.o&&fe(o,"visibility",c.o=m),k!==c.i&&h(r,"d",c.i=k),M!==c.n&&h(r,"fill",c.n=M),S!==c.s&&h(r,"mask",c.s=S),A!==c.h&&h(l,"d",c.h=A),I!==c.r&&h(l,"fill",c.r=I),$!==c.d&&h(l,"mask",c.d=$),Y!==c.l&&h(u,"d",c.l=Y),B!==c.u&&h(u,"fill",c.u=B),ne!==c.c&&h(u,"mask",c.c=ne),ye!==c.w&&h(d,"d",c.w=ye),C!==c.m&&h(d,"fill",c.m=C),P!==c.f&&h(d,"mask",c.f=P),c},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0,s:void 0,h:void 0,r:void 0,d:void 0,l:void 0,u:void 0,c:void 0,w:void 0,m:void 0,f:void 0}),o})(),(()=>{var o=Zi();return Qe(s=>a=s,o),U(()=>o.hidden=!n()),o})()]}function yr(e){return(()=>{var n=Qi();return R(n,N(ma,{get px(){return e.px},get seed(){return e.seed},frames:0,period:2.4}),null),R(n,N(mr,{get px(){return e.px},get seed(){return e.seed},get reducedMotion(){return e.reducedMotion??!1}}),null),U(t=>{var a=`${e.px}px`,o=`${e.px}px`;return a!==t.e&&fe(n,"width",t.e=a),o!==t.t&&fe(n,"height",t.t=o),t},{e:void 0,t:void 0}),n})()}const pr=Ee.base,br=Ee.tip,ct=1.9;function Pt(e,n,t){const a=n*.46,o=e*n*.08,s=e*a,i=r=>(r===0?n*pr:a*br)*t;return`M ${-i(0)} ${o} L ${-i(1)} ${s} L ${i(1)} ${s} L ${i(0)} ${o} Z`}const xr=(e,n)=>{const t=n*.46,a=(i,r)=>(()=>{var l=Ji();return h(l,"id",`edge-grad-${i}`),h(l,"x1",-r),h(l,"x2",r),l})(),o=i=>[er(),tr(),nr(),ar()],s=[or(),ir(),rr(),sr()];return[(()=>{var i=lr();return h(i,"id",`plume-up-${e}`),R(i,s),i})(),(()=>{var i=cr();return h(i,"id",`plume-down-${e}`),R(i,s),i})(),(()=>{var i=dr(),r=i.firstChild;return r.nextSibling,h(i,"id",`knot-up-${e}`),R(i,()=>o(),null),i})(),(()=>{var i=ur(),r=i.firstChild;return r.nextSibling,h(i,"id",`knot-down-${e}`),R(i,()=>o(),null),i})(),Ye(()=>a(`beam-${e}`,t*Ee.tip)),Ye(()=>a(`flare-${e}`,t*Ee.tip*ct)),(()=>{var i=Fn(),r=i.firstChild;return h(i,"id",`beam-${e}`),h(i,"y",-n),h(i,"height",n*2),h(r,"y",-n),h(r,"height",n*2),h(r,"fill",`url(#edge-grad-beam-${e})`),U(l=>{var u=-t*Ee.tip*1.3,d=t*Ee.tip*2.6,c=-t*Ee.tip*1.3,f=t*Ee.tip*2.6;return u!==l.e&&h(i,"x",l.e=u),d!==l.t&&h(i,"width",l.t=d),c!==l.a&&h(r,"x",l.a=c),f!==l.o&&h(r,"width",l.o=f),l},{e:void 0,t:void 0,a:void 0,o:void 0}),i})(),(()=>{var i=Fn(),r=i.firstChild;return h(i,"id",`flare-${e}`),h(i,"y",-n),h(i,"height",n*2),h(r,"y",-n),h(r,"height",n*2),h(r,"fill",`url(#edge-grad-flare-${e})`),U(l=>{var u=-t*Ee.tip*ct*1.3,d=t*Ee.tip*ct*2.6,c=-t*Ee.tip*ct*1.3,f=t*Ee.tip*ct*2.6;return u!==l.e&&h(i,"x",l.e=u),d!==l.t&&h(i,"width",l.t=d),c!==l.a&&h(r,"x",l.a=c),f!==l.o&&h(r,"width",l.o=f),l},{e:void 0,t:void 0,a:void 0,o:void 0}),i})()]};function wr(e){return N(te,{get when(){return e.kind==="quasar"},get fallback(){return N(gr,{get px(){return e.px},get seed(){return e.seed}})},get children(){return N(yr,{get px(){return e.px},get seed(){return e.seed}})}})}const _r=6,Sa=.127,kr=.2,Sr=15,$r=4,Ar=300,Un=[-.1,.3],Tr=.28,qn=["#eec39a","#b37a50","#8f563b"],Mr=["#7a4a4e","#4e3b57","#3b3950"],Er=2.1,Lr=(.4+Sa)/.5;function Cr(e,n){return e/n}const Pr=`
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y IS FLIPPED, and the whole shader depends on it.
  //
  // Godot's UV has (0,0) at the TOP left. WebGL's has (0,0) at the BOTTOM left. So
  // a straight port puts the reference's \`if (uv.y < 0.5)\` on the wrong half, and
  // that line is the planet's hole: the reference uses it to cut the planet out of
  // the ring's FAR arm. Flipped, it cut the far arm out of the NEAR side instead,
  // which deleted the near arm exactly where it should cross in front of the planet
  // and left the far arm drawn across the planet's face.
  //
  // It looked almost right, which is why it survived a screenshot. The planet sprite
  // paints over the ring anyway, so the far arm's mistake was hidden — and what was
  // actually visible was the near arm's absence: the ring stopped dead at the
  // planet's edge on both sides instead of passing round it.
  //
  // Flipping once, here, puts everything downstream into Godot's convention: the
  // hole test, \`light_origin\` at y=0.3, and the sense of \`rotation\`, which is
  // clockwise on screen in a y-down space and counter-clockwise in a y-up one.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`,Rr=`
precision highp float;
varying vec2 v_uv;

uniform float u_pixels;
uniform float u_rotation;
uniform vec2  u_lightOrigin;
uniform float u_ringWidth;
uniform float u_perspective;
uniform float u_holeScale;
uniform float u_size;
uniform float u_seed;
uniform float u_time;
uniform float u_timeSpeed;
uniform vec3  u_c0;
uniform vec3  u_c1;
uniform vec3  u_c2;
uniform vec3  u_d0;
uniform vec3  u_d1;
uniform vec3  u_d2;
uniform int   u_nColors;

/** The reference's hash: sin-based, and tiling at 2*size by size. */
float rnd(vec2 coord) {
  vec2 m = vec2(2.0, 1.0) * floor(u_size + 0.5);
  coord = mod(coord, m);
  return fract(sin(dot(coord, vec2(12.9898, 78.233))) * 15.5453 * u_seed);
}

float vnoise(vec2 coord) {
  vec2 i = floor(coord);
  vec2 f = fract(coord);
  float a = rnd(i);
  float b = rnd(i + vec2(1.0, 0.0));
  float c = rnd(i + vec2(0.0, 1.0));
  float d = rnd(i + vec2(1.0, 1.0));
  vec2 cubic = f * f * (3.0 - 2.0 * f);
  return mix(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;
}

float fbm(vec2 coord) {
  float value = 0.0;
  float scale = 0.5;
  for (int i = 0; i < ${$r}; i++) {
    value += vnoise(coord) * scale;
    coord *= 2.0;
    scale *= 0.5;
  }
  return value;
}

vec2 rotate(vec2 coord, float angle) {
  coord -= 0.5;
  coord *= mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
  return coord + 0.5;
}

void main() {
  // Pixelise, exactly as the reference does, and BEFORE the light distance is
  // taken — light_d is computed from the quantised uv. v_uv arrives y-flipped from
  // the vertex shader, so everything below is in Godot's y-down space.
  vec2 uv = floor(v_uv * u_pixels) / u_pixels;

  float light_d = distance(uv, u_lightOrigin);
  uv = rotate(uv, u_rotation);

  vec2 uv_center = uv - vec2(0.0, 0.5);

  // Tilt. This is the ring's foreshortening and it is what stops it reading as a
  // hoop laid on the disc.
  uv_center *= vec2(1.0, u_perspective);
  float center_d = distance(uv_center, vec2(0.5, 0.0));

  // Two circles of different sizes; only the intersection is kept. So this is a
  // FILLED annulus, and the noise below is what turns it into bands.
  float ring = smoothstep(0.5 - u_ringWidth * 2.0, 0.5 - u_ringWidth, center_d);
  // Reversed edges, as in the reference: clamp((0.4 + w - center_d) / w).
  ring *= smoothstep(center_d - u_ringWidth, center_d, 0.4);

  // The planet's hole, cut by the ring rather than by the sprite behind it.
  if (uv.y < 0.5) {
    ring *= step(1.0 / u_holeScale, distance(uv, vec2(0.5)));
  }

  // The material turns independently of the ring's own shape.
  uv_center = rotate(uv_center + vec2(0.0, 0.5), u_time * u_timeSpeed);
  ring *= fbm(uv_center * u_size);

  // Six tones in two ramps, not one ramp: the reference switches to its
  // dark_colors outright once posterized passes 1.0, rather than carrying on
  // through colors.
  //
  // (Backticks cannot appear in this comment: they terminate the template literal
  // the shader lives in.)
  float posterized = floor((ring + pow(light_d, 2.0) * 2.0) * 4.0) / 4.0;
  posterized = min(posterized, 2.0);
  vec3 col;
  if (posterized <= 1.0) {
    float f = posterized * float(u_nColors - 1);
    col = f < 0.5 ? u_c0 : (f < 1.5 ? u_c1 : u_c2);
  } else {
    float f = (posterized - 1.0) * float(u_nColors - 1);
    col = f < 0.5 ? u_d0 : (f < 1.5 ? u_d1 : u_d2);
  }

  // A STEP, and the fbm decides where it lands — which is what puts divisions in
  // the ring instead of a clean edge.
  if (ring < ${Tr}) discard;
  gl_FragColor = vec4(col, 1.0);
}`;function Ir(){if(typeof document>"u")return!1;try{const e=document.createElement("canvas");return!!(e.getContext("webgl2")||e.getContext("webgl")||e.getContext("experimental-webgl"))}catch{return!1}}function $a(e){return .7+((t=>t-Math.floor(t))(e*.2718281)-.5)*.6}function Bn(e){const n=parseInt(e.slice(1),16);return[(n>>16&255)/255,(n>>8&255)/255,(n&255)/255]}function Or(e,n){const t=e/2,a=n/2;return{planetR:t,rx:Lr*a,holeR:t}}function Nr(e){if(typeof document>"u")return null;const n=e.canvasPx??e.planetPx*3,t=e.planetPx/2,a=document.createElement("canvas");let o=null;const s={preserveDrawingBuffer:!0,alpha:!0,antialias:!1};try{o=a.getContext("webgl2",s)??a.getContext("webgl",s)??a.getContext("experimental-webgl",s)}catch{return null}if(!o)return null;const i=($,Y)=>{const B=o.createShader($);return B?(o.shaderSource(B,Y),o.compileShader(B),o.getShaderParameter(B,o.COMPILE_STATUS)?B:(console.warn("[GalaxyMap] ring shader failed",o.getShaderInfoLog(B)),o.deleteShader(B),null)):null},r=i(o.VERTEX_SHADER,Pr),l=i(o.FRAGMENT_SHADER,Rr);if(!r||!l)return null;const u=o.createProgram();if(!u)return null;if(o.attachShader(u,r),o.attachShader(u,l),o.linkProgram(u),!o.getProgramParameter(u,o.LINK_STATUS))return console.warn("[GalaxyMap] ring link failed",o.getProgramInfoLog(u)),null;o.useProgram(u);const d=o.createBuffer();o.bindBuffer(o.ARRAY_BUFFER,d),o.bufferData(o.ARRAY_BUFFER,new Float32Array([-1,-1,3,-1,-1,3]),o.STATIC_DRAW);const c=o.getAttribLocation(u,"a_pos");o.enableVertexAttribArray(c),o.vertexAttribPointer(c,2,o.FLOAT,!1,0,0);const f=$=>o.getUniformLocation(u,$),b=$=>$-Math.floor($);o.uniform1f(f("u_pixels"),Ar),o.uniform1f(f("u_rotation"),$a(e.seed)),o.uniform2f(f("u_lightOrigin"),Un[0],Un[1]),o.uniform1f(f("u_ringWidth"),Sa),o.uniform1f(f("u_perspective"),_r),o.uniform1f(f("u_holeScale"),Cr(n,t)),o.uniform1f(f("u_size"),Sr),o.uniform1f(f("u_seed"),1+b(e.seed*.6180339887)*9),o.uniform1f(f("u_timeSpeed"),kr),o.uniform1i(f("u_nColors"),qn.length),qn.forEach(($,Y)=>o.uniform3fv(f(`u_c${Y}`),Bn($))),Mr.forEach(($,Y)=>o.uniform3fv(f(`u_d${Y}`),Bn($)));const w=()=>window.devicePixelRatio||1;let m=0,k=!1;const M=()=>{const $=Math.max(1,Math.round(n*w()));a.width!==$&&(a.width=$,a.height=$),a.style.width=`${n}px`,a.style.height=`${n}px`,o.viewport(0,0,$,$)};M();const S=performance.now(),A=f("u_time"),I=$=>{k||(M(),o.uniform1f(A,($-S)/1e3),o.drawArrays(o.TRIANGLES,0,3),e.animate!==!1&&(m=requestAnimationFrame(I)))};return I(S),{canvas:a,dispose(){k=!0,m&&cancelAnimationFrame(m),o=null}}}var zr=L("<div class=world-ring-gl-host aria-hidden=true>"),Dr=L("<div class=world-ring-fallback aria-hidden=true>");function Fr(e){let n;const t=()=>e.planetPx*Er,a=()=>Or(e.planetPx,t()),[o,s]=me(!1);return St(()=>{const i=Ir()?Nr({seed:e.seed,planetPx:e.planetPx,canvasPx:t(),animate:e.animate}):null;if(!i){s(!0);return}i.canvas.classList.add("world-ring-gl"),n.appendChild(i.canvas),ze(()=>i.dispose())}),N(te,{get when(){return!o()},get fallback(){return(()=>{var i=Dr();return R(i,N(Rn,{get px(){return e.planetPx},get seed(){return e.seed}}),null),R(i,N(Rn,{get px(){return e.planetPx},get seed(){return e.seed},front:!0}),null),i})()},get children(){var i=zr(),r=n;return typeof r=="function"?Qe(r,i):n=i,U(l=>{var u=`${t()}px`,d=`${t()}px`,c=`${(e.planetPx-t())/2}px`,f=`${(e.planetPx-t())/2}px`,b=Math.round(a().rx),w=Math.round(a().holeR),m=Math.round(t()),k=$a(e.seed);return u!==l.e&&fe(i,"width",l.e=u),d!==l.t&&fe(i,"height",l.t=d),c!==l.a&&fe(i,"left",l.a=c),f!==l.o&&fe(i,"top",l.o=f),b!==l.i&&h(i,"data-ring-outer",l.i=b),w!==l.n&&h(i,"data-ring-hole",l.n=w),m!==l.s&&h(i,"data-ring-canvas",l.s=m),k!==l.h&&h(i,"data-ring-rotation",l.h=k),l},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0,s:void 0,h:void 0}),i}})}var Ur=L("<div class=world>"),qr=L("<img class=world-still>"),Br=L("<div class=world-turn>");function Gr(e){let n=!1;St(()=>{n=!0}),ze(()=>{n=!1});const t=()=>Math.max(1,Math.floor(e.frames??0)),a=()=>typeof window>"u"?1:window.devicePixelRatio||1,o=()=>1,[s,i]=me();return it(()=>{const r=Vt()?1:t(),l=e.seed,u=e.px,d=e.type,c=e.cloudThreshold;if(r<=1){i(void 0);return}i(void 0);const f=So({seed:l,type:d,px:u,dpr:o(),cloudThreshold:c},r,b=>{n&&i(b)},{batch:4});ze(()=>f.cancel())}),(()=>{var r=Ur();return R(r,N(te,{get when(){return e.ring===!0},get children(){return N(Fr,{get planetPx(){return e.px},get seed(){return e.seed}})}}),null),R(r,N(te,{get when(){return Hr(e,a())},keyed:!0,children:l=>(()=>{var u=qr();return h(u,"src",l),U(()=>h(u,"alt",e.title??"")),u})()}),null),R(r,N(te,{get when(){return s()},keyed:!0,children:l=>(()=>{var u=Br();return fe(u,"background-image",`url(${l})`),U(d=>{var c=e.title?"img":void 0,f=e.title,b=`${e.px*t()}px ${e.px}px`,w=`${e.period??15}s`,m=`steps(${t()})`,k=`-${e.px*t()}px`;return c!==d.e&&h(u,"role",d.e=c),f!==d.t&&h(u,"aria-label",d.t=f),b!==d.a&&fe(u,"background-size",d.a=b),w!==d.o&&fe(u,"animation-duration",d.o=w),m!==d.i&&fe(u,"animation-timing-function",d.i=m),k!==d.n&&fe(u,"--world-end",d.n=k),d},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0}),u})()}),null),U(l=>{var u=s()!==void 0,d=e.ring===!0,c=`${e.px}px`,f=`${e.px}px`;return u!==l.e&&r.classList.toggle("turning",l.e=u),d!==l.t&&r.classList.toggle("ringed",l.t=d),c!==l.a&&fe(r,"width",l.a=c),f!==l.o&&fe(r,"height",l.o=f),l},{e:void 0,t:void 0,a:void 0,o:void 0}),r})()}function Hr(e,n){return oa({seed:e.seed,type:e.type,px:e.px,dpr:n,cloudThreshold:e.cloudThreshold})}const Gn=4,Rt=["#fffdf2","#ffe89a","#ffab4a","#e0561f"],Hn=["#ffd9a0","#a8664a"],Wr=`
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
  // Y flipped, as everywhere else here: Godot's UV has (0,0) at the top left and
  // WebGL's at the bottom left.
  v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`,Vr=`
precision highp float;

uniform vec2  u_resolution;
uniform float u_time;
uniform float u_seed;
uniform float u_phase;

uniform float u_bodyRadius;
uniform float u_colors;
uniform float u_coronaColors;
uniform float u_activity;
uniform float u_granulation;
uniform float u_spots;
uniform float u_corona;
uniform float u_flares;

uniform vec3 u_surface0;
uniform vec3 u_surface1;
uniform vec3 u_surface2;
uniform vec3 u_surface3;
uniform vec3 u_corona0;
uniform vec3 u_corona1;

varying vec2 v_uv;

const float PI  = 3.14159265359;
const float TAU = 6.28318530718;

// Cosmoglyph's hash, verbatim. Not glsl.ts's -- see the file header.
float hash(float value) {
  return fract(sin(value * 12.9898) * 43758.5453);
}

float hash2(vec2 p) {
  return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

// The 4x4 ordered dither, transcribed branch for branch. Verbatim rather than the
// usual 16-entry matrix lookup, because this is a transcription and the branch form is
// what the reference has.
float bayer4(vec2 position) {
  vec2 cell = mod(floor(position), 4.0);
  if (cell.y < 1.0) {
    if (cell.x < 1.0) return 0.03125;
    if (cell.x < 2.0) return 0.53125;
    if (cell.x < 3.0) return 0.15625;
    return 0.65625;
  }
  if (cell.y < 2.0) {
    if (cell.x < 1.0) return 0.8125;
    if (cell.x < 2.0) return 0.3125;
    if (cell.x < 3.0) return 0.9375;
    return 0.4375;
  }
  if (cell.y < 3.0) {
    if (cell.x < 1.0) return 0.21875;
    if (cell.x < 2.0) return 0.71875;
    if (cell.x < 3.0) return 0.09375;
    return 0.59375;
  }
  if (cell.x < 1.0) return 0.578125;
  if (cell.x < 2.0) return 0.078125;
  if (cell.x < 3.0) return 0.878125;
  return 0.378125;
}

float vnoise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  float a = hash2(i);
  float b = hash2(i + vec2(1.0, 0.0));
  float c = hash2(i + vec2(0.0, 1.0));
  float d = hash2(i + vec2(1.0, 1.0));
  return mix(a, b, f.x) + (c - a) * f.y * (1.0 - f.x) + (d - b) * f.x * f.y;
}

float fbm(vec2 p) {
  float v = 0.0;
  float s = 0.5;
  for (int i = 0; i < ${Gn}; i++) {
    v += vnoise(p) * s;
    p *= 2.0;
    s *= 0.5;
  }
  return v;
}

/**
 * 3D value noise, and fbm on top of it.
 *
 * This exists because a 2D equirectangular texture CANNOT texture a sphere seen face
 * on. Every longitude meets at the centre of the visible disc, so any lat/long mapping
 * has a pole there and the cells converge into a visible pinch -- which is exactly what
 * appeared once the projection was fixed and stopped being one-dimensional.
 *
 * Sampling the noise on the SURFACE POINT is seamless by construction: there is no
 * seam to wrap and no pole to converge at, because the star's own geometry supplies
 * the parameterisation. The reference avoids this the same way, by evaluating a
 * pre-rendered surface MAP -- but a map is a lat/long image too, so it has the pole as
 * well; it simply happens to sit at the back of the sphere there rather than in the
 * middle of the visible face.
 *
 * Same hash as everything else in this file, so the star breaks down into the same
 * grain as its own corona.
 */
float hash3(vec3 p) {
  return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

float vnoise3(vec3 p) {
  vec3 i = floor(p);
  vec3 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  float n000 = hash3(i);
  float n100 = hash3(i + vec3(1.0, 0.0, 0.0));
  float n010 = hash3(i + vec3(0.0, 1.0, 0.0));
  float n110 = hash3(i + vec3(1.0, 1.0, 0.0));
  float n001 = hash3(i + vec3(0.0, 0.0, 1.0));
  float n101 = hash3(i + vec3(1.0, 0.0, 1.0));
  float n011 = hash3(i + vec3(0.0, 1.0, 1.0));
  float n111 = hash3(i + vec3(1.0, 1.0, 1.0));
  float x00 = mix(n000, n100, f.x);
  float x10 = mix(n010, n110, f.x);
  float x01 = mix(n001, n101, f.x);
  float x11 = mix(n011, n111, f.x);
  return mix(mix(x00, x10, f.y), mix(x01, x11, f.y), f.z);
}

float fbm3(vec3 p) {
  float v = 0.0;
  float s = 0.5;
  for (int i = 0; i < ${Gn}; i++) {
    v += vnoise3(p) * s;
    p *= 2.0;
    s *= 0.5;
  }
  return v;
}

/**
 * Three bands, unrolled.
 *
 * GLSL ES 1.00 permits only CONSTANT index expressions into a uniform array, so
 * 'palette[band]' where band is a float is a COMPILE error rather than a runtime one.
 * 'ramp()' in gl-ring.ts hit this and is unrolled for the same reason.
 */
vec3 surfaceBand(float band) {
  float x = clamp(band, 0.0, 0.999) * 3.0;
  if (x >= 2.0)      return mix(u_surface2, u_surface3, x - 2.0);
  else if (x >= 1.0) return mix(u_surface1, u_surface2, x - 1.0);
  return mix(u_surface0, u_surface1, x);
}

vec3 coronaBand(float band) {
  float x = clamp(band, 0.0, 0.999);
  return mix(u_corona0, u_corona1, x);
}

void main() {
  vec2 uv = v_uv;
  vec2 p = (uv - 0.5) * 2.0;
  float r = length(p);

  float bodyR = u_bodyRadius;
  vec3 surface = vec3(0.0);
  float onBody = step(r, bodyR);

  if (onBody > 0.5) {
    // Analytic sphere normal. The reference gets this from a per-pixel sphere
    // intersection; the difference is recorded in the file header.
    float z = sqrt(max(0.0, 1.0 - (r * r) / (bodyR * bodyR)));
    vec3 n = vec3(p / bodyR, z);

    // Equirectangular, and SCROLLED. The scroll rate is the activity: a star with
    // more activity turns faster, and it is the one parameter that separates a still
    // disc from something alive.
    // THE PROJECTION, and the first one was degenerate.
    //
    // It read atan(n.z, n.x) -- the viewer-axis component against x. For a sphere
    // FACING the camera that viewer component is large and nearly constant across the
    // whole disc, so the longitude was pinned near one value and the granulation was
    // effectively ONE-DIMENSIONAL. A one-dimensional texture sampled at high frequency
    // and then cut into four hard bands is exactly salt-and-pepper, which is what the
    // surface looked like no matter what I did to the frequency or the ramp width.
    //
    // For a front-facing sphere the longitude comes from the SCREEN-SPACE direction and
    // the latitude from the viewer axis, which is the standard mapping:
    //
    //     longitude = atan2(screen up, screen right)
    //     latitude  = asin(viewer axis)
    // The granulation scrolls by ROTATING THE SAMPLE POINT about the star's axis,
    // rather than by offsetting a texture coordinate. A rotated 3D point on a sphere
    // stays on the sphere, so the cells turn with the surface and there is still no
    // seam and no pole. Offsetting a uv would slide the pattern across the sphere
    // instead, which shears it at the wrap.
    float spin = u_phase * (1.0 + floor(u_activity * 2.999)) * 0.35;
    float cs = cos(spin);
    float sn = sin(spin);
    vec3 sp = vec3(n.x * cs - n.y * sn, n.x * sn + n.y * cs, n.z);

    // Three channels from one fbm at different scales, which is what gives granulation
    // its cells AND gives the spots somewhere to live without a second texture.
    // FREQUENCY, and the first attempt was a planet rather than a star.
    //
    // 26 cells around the sphere is CONTINENTS. Granulation is convective cells and
    // there are hundreds of them; at 26 this produced brown continents on a cream
    // ball with a purple rim, which is a rocky planet and nothing else. 150 across is
    // the smallest count that still reads as cells rather than as flat tone.
    // 96, not 150, and the reason is ALIASING rather than taste.
    //
    // Granulation cells want to be eight to fifteen pixels across on screen. At 150
    // cells around the equator of a 460px star each cell is about six pixels, which is
    // the same scale as the 4x4 dither -- so the ordered dither and the cells beat
    // against each other and the surface reads as speckle instead of as convection.
    // 34 cells across the DIAMETER. The number is the cell count on the visible face,
    // not around an equator, because there is no equator here -- the frequency is on
    // the unit sphere and the visible face is half of it.
    // SINGLE OCTAVE, and this is the actual cause of the speckle.
    //
    // Four things were blamed for it and three were real but incidental: the noise
    // frequency, the width of the smoothstep, the projection, and the surface dither.
    // The cause is that this was an fbm.
    //
    // fbm3 at four octaves multiplies the frequency by two each octave, so a base of
    // 34 has a top octave at 272. On a 420px disc that is roughly one cycle every one
    // and a half pixels -- which is not granulation, it is per-pixel noise, and cutting
    // it into four hard bands turns it into salt and pepper. Every "fix" applied on top
    // was treating the SYMPTOM, and lowering the base frequency just moved the
    // speckle to a different octave.
    //
    // Granulation is convective cells and cells are BAND-LIMITED: one characteristic
    // size, not a fractal with eight times the detail on top. The reference's surface
    // map is drawn as flat regions, which is the same statement. One octave of value
    // noise, and the second channel likewise.
    float gran = vnoise3(sp * 30.0 + u_seed);
    float fine = vnoise3(sp * 62.0 + u_seed + 31.0);
    float spotN = vnoise3(sp * 4.0 + u_seed + 77.0);

    float g = 0.5;
    if (u_granulation > 0.001) {
      g = 0.5 + (gran - 0.5) / u_granulation;
    }
    // Transcribed: the reference reads these channels off a pre-rendered surface map.
    // Here they are three fbm evaluations, which is the same information at the same
    // cost rather than a texture fetch, and the banding below is unchanged.
    vec3 map = vec3(gran, spotN, fine);

    float energy = mix(0.5, clamp(g, 0.0, 1.0), 0.35 + u_granulation * 0.65);
    // The transition is much WIDER than the reference's 0.24..0.76, and that is the
    // single change that stops the speckle. A steep smoothstep across four hard bands
    // means most pixels sit near a band EDGE, so a small noise excursion flips them
    // into the neighbouring colour and you get salt and pepper instead of convection.
    // Spreading the ramp out gives each band a region rather than a boundary.
    float identity = smoothstep(0.10, 0.94, energy + map.b * 0.05);

    // SPOTS, and the transcription made every pixel a spot.
    //
    // map.g is an fbm centred on 0.5, so smoothstep(0.08, 0.55, map.g) is about 0.93
    // for almost the whole surface -- the subtraction was a near-constant offset, it
    // compressed the entire index distribution into the two END bands, and the two
    // middle colours were never drawn at all. Hence two-tone banding.
    //
    // The fix is the distribution, not the threshold: the reference's map.g is a
    // SPOTNESS channel that is near zero almost everywhere and high in a few places,
    // because it was rendered as one. So the noise is squared and biased down, which
    // is what makes it sparse, and the subtraction is then gated on it being sparse.
    float spotness = pow(clamp(map.g, 0.0, 1.0), 3.0);
    identity -= smoothstep(0.18, 0.62, spotness) * 0.6 * u_spots;
    identity = clamp(identity, 0.0, 0.999);

    // Limb darkening, applied to the INDEX rather than to a colour, because the
    // reference gets it for free: energy falls toward the edge, so the band index
    // falls with it. Written explicitly because with four bands and a hard quantizer
    // it is otherwise too subtle to see, and a star with a uniformly hot face reads as
    // a disc rather than as a sphere.
    // 0.42 was far too aggressive an exponent for a 4-band quantizer: it drove the
    // outermost band hard enough that the limb read as a dark outline rather than as a
    // cooler edge, which is the opposite of what limb darkening is for.
    float limb = pow(clamp(z, 0.0, 1.0), 0.22);
    identity *= 0.72 + 0.28 * limb;

    // One assignment, and the index arithmetic is the reference's: pick a band INDEX
    // out of 'colors', then spread those indices across the four surface bands. With
    // the default 4 colours the two coincide and this is the identity.
    float index = floor(clamp(identity, 0.0, 0.999) * u_colors);
    surface = surfaceBand(index * (3.0 / max(1.0, u_colors - 1.0)));

    // NO DITHER ON THE SURFACE, and removing it is what finally stopped the speckle.
    //
    // Three separate things were blamed for the salt-and-pepper and two of them were
    // real but minor: the granulation frequency, and the width of the smoothstep. The
    // cause was the dither itself.
    //
    // Dithering works by trading a hard edge for a pattern -- which only reads as
    // smoother when the two colours either side of the edge are CLOSE. Here they are
    // not: the bands are white against orange, which is most of the palette's range.
    // So the dither was not softening the band boundary, it was filling it with
    // high-contrast noise, and no amount of lowering its amplitude helped because even
    // a small perturbation of two far-apart flat colours is visible as two flat colours
    // alternating.
    //
    // The reference only ever dithers the CORONA, and its corona's two bands are
    // adjacent in the ramp. Its surfaces are FLAT regions with hard edges -- which is
    // what the screenshots show, and what the cell-shaded look actually is. I had added
    // surface dithering as a "deviation" to stop four bands reading as four rings, and
    // it was the deviation causing the artefact.
    gl_FragColor = vec4(surface, 1.0);
    return;
  }

  // ---------------------------------------------------------------- corona and flares
  float radial = r / bodyR;
  float animation = u_phase * TAU * (1.0 + floor(u_activity * 2.999));

  // The harmonics are functions of a DIRECTION, and that matters.
  //
  // The reference computes the corona's warp from a normal: it takes the centred
  // screen position, normalises it into the plane, and rotates it by the camera. So
  // the two components are the direction's, bounded in -1..1, and they vary smoothly
  // all the way out to the corner of the frame.
  //
  // The first attempt here passed 'radial' into those harmonics instead. radial grows
  // to about 3 at the corners of the canvas, so the corona's edge term was being
  // driven far outside its intended range and the corona flooded the entire frame --
  // which looked like a threshold problem and was a substitution of one quantity for
  // another. The direction is what belongs here.
  vec2 dir = p / max(r, 1e-4);

  // The corona's warp, transcribed: four harmonics of the screen-space normal, so the
  // edge is ragged rather than a clean falloff. Four is not decoration -- one harmonic
  // is a circle.
  // Transcribed term for term from coronaColor, with normal.x -> dir.x and normal.y ->
  // dir.y. Four harmonics, and the count is not decoration: one is a circle.
  float warp = sin(dir.x * 3.0 - dir.y * 5.0 + animation * 0.7) * 0.2
             + sin(dir.x * 7.0 + dir.y * 2.0 - animation * 1.3) * 0.1;
  float harmonics = sin(dir.x * 2.0 + dir.y * 2.0 + warp + animation)
                  + sin(dir.x * 4.0 - dir.y * 3.0 + warp * 2.0 - animation * 2.0)
                  + sin(dir.x * 7.0 + dir.y * 6.0 - warp * 3.0 + animation * 3.0)
                  + sin(dir.x * 11.0 + dir.y * 9.0 - warp * 3.0 + animation * 4.0);
  // The BASE is down and the HARMONIC amplitude is up, which is what makes the edge
  // ragged rather than merely soft. At 0.2 + harmonics*0.03 the constant term was half
  // the total, so the corona read as a smooth glow with a wobble in it.
  float width = u_corona * (0.11 + harmonics * 0.05) * 0.9;
  float density = clamp(1.0 - (radial - 1.0) / max(0.002, width), 0.0, 1.0);
  float coronaCoverage = pow(density, 0.7) * min(1.0, u_corona * 1.7);

  // ------------------------------------------------------------------- prominence loops
  //
  // Transcribed from flareLoop. A loop is anchored on the sphere by a HASH rather than
  // placed: angle, depth, and therefore radius all come from hash(seed + order), so 32
  // of them cost three hashes each and no state. Then it is drawn in a local frame
  // built from the anchor's OUTWARD direction and the tangent to it, as an ellipse arc
  // with two independent wobbles and a hashed gap.
  //
  // The two wobbles are what stop it reading as an ellipse. One bends the arc along its
  // length; the other displaces it perpendicular, and they run at different rates and
  // different phases, so the loop wobbles rather than undulates.
  float flare = 0.0;
  float strength = smoothstep(0.15, 0.35, u_flares);
  for (int index = 0; index < 32; index++) {
    float order = float(index);
    // Threshold: higher orders need more flares to appear at all, so raising the
    // parameter adds loops at the rim rather than making all 32 bigger.
    // Higher orders need a higher setting, so raising 'flares' adds loops rather than
    // inflating the existing ones. Divided by 40 rather than 20 because at 20 the first
    // order's threshold already exceeded the default 'flares' and it was being skipped
    // -- the largest loop, the best-anchored one, gone.
    float threshold = order / 40.0;
    if (strength <= smoothstep(threshold, threshold + 0.2, u_flares)) continue;

    float seed = u_seed + order;
    float anchorAngle = hash(seed) * TAU + animation * 0.2;
    float anchorDepth = hash(seed + 1.0) * 2.0 - 1.0;
    float anchorRadius = sqrt(max(0.0, 1.0 - anchorDepth * anchorDepth));

    vec2 anchor = vec2(cos(anchorAngle) * anchorRadius, sin(anchorAngle) * anchorRadius) * bodyR;
    // Front hemisphere only. The reference gets this from the anchor's z facing the
    // camera; projecting to 2D drops z, so this substitutes a facing test on the
    // anchor's own direction, which is the same condition.
    float facing = smoothstep(-0.15, 0.35, anchorDepth * -1.0 + 0.35);
    if (facing <= 0.001) continue;

    vec2 outward = normalize(anchor + vec2(1e-5, 0.0));
    vec2 tangent = vec2(-outward.y, outward.x);
    vec2 local = vec2(dot(p, outward), dot(p, tangent));

    float height = (0.05 + u_flares * 0.2) * bodyR;
    float widthL = height * 0.7;
    float loopCenter = 0.9 * bodyR;

    float arcPosition = local.y / widthL;
    float firstWobble = sin(arcPosition * 3.0 + animation * 2.0 + seed);
    local.x += firstWobble * height * 0.2;
    float secondWobble = sin((local.x - loopCenter) / height * 5.0 - animation * 3.0 + seed);
    local.y += secondWobble * widthL * 0.15;

    float ellipse = length(vec2((local.x - loopCenter) / height, local.y / widthL));
    // Thickness. The first version multiplied by bodyRadius AND used a width already
    // scaled by it, which put the tube at 0.004 -- about a pixel at 460px -- so all 32
    // loops rendered and none of them were visible. A prominence tube is a few per
    // cent of the star's radius, which is what 0.05 of 'height' is.
    // CLAMPED, and unclamped this flooded the entire canvas.
    //
    // (0.4 + firstWobble - secondWobble) is a sum of two sines in [-1,1] around 0.4,
    // so it goes NEGATIVE for part of every loop's life. The reference then adds an
    // ABSOLUTE 0.08 to it for the second smoothstep edge, so a slightly negative
    // thickness still leaves edge0 < edge1 and smoothstep behaves.
    //
    // Scaling the second edge by the thickness instead -- which is what tightening the
    // falloff tempted -- makes both edges negative together, and smoothstep with
    // edge0 > edge1 is UNDEFINED in GLSL. It returned 1, so line was 1 across the whole
    // frame, so the corona's coverage was ~0.5 everywhere and the dither drew a
    // half-tone field over the entire canvas.
    //
    // The symptom pointed at the corona's harmonics and at the radial/direction
    // substitution above, and both of those were wrong for other reasons and worth
    // fixing anyway. This one was arithmetic in the dark.
    float thickness = max(0.004, (0.4 + (firstWobble - secondWobble)) * height * 0.055);
    float line = 1.0 - smoothstep(thickness, thickness + thickness * 2.2, abs(ellipse - 1.0));

    // The gap. Without it this is an ellipse, which is a hoop on the sky rather than
    // a prominence anchored at one end.
    float centre = hash(seed + 2.0) * TAU;
    float arcWidth = (hash(seed + 3.0) - hash(seed + 4.0)) * TAU;
    float gap = abs(atan(sin(atan(local.y / widthL, (local.x - loopCenter) / height) - centre),
                         cos(atan(local.y / widthL, (local.x - loopCenter) / height) - centre)));
    float arcCoverage = 1.0 - smoothstep(abs(arcWidth), abs(arcWidth) + 0.4, gap);

    flare = max(flare, line * facing * strength * arcCoverage);
  }

  float coverage = clamp(max(coronaCoverage, flare), 0.0, 1.0);
  if (coverage <= 0.0) discard;
  if (bayer4(gl_FragCoord.xy) >= coverage) discard;

  // The band index is INVERTED against brightness, and that was a real fault rather
  // than a taste call: brightness peaks at the corona's inner edge, so indexing by it
  // put the dimmest colour where the corona is thickest. The inner edge should be the
  // hot one and the falloff should walk outward down the palette.
  // THE LOOPS GET THEIR OWN COLOUR, and this is what makes them visible.
  //
  // They were being drawn, and then banded into the CORONA palette alongside the
  // corona itself -- and since the corona is brightest at exactly the radius the loops
  // stand at, a loop and the corona behind it landed on the same index and there was
  // nothing to see. All 32 were rendering the entire time.
  //
  // A prominence is at or above photosphere temperature: it is denser and hotter than
  // the corona it stands in, which is the whole reason it is visible against the sky at
  // all. So a loop takes the star's hottest band and the corona never competes.
  if (flare > 0.02) {
    gl_FragColor = vec4(mix(u_surface1, u_surface0, clamp(flare * 1.6, 0.0, 1.0)), 1.0);
    return;
  }

  float brightness = max(density, flare);
  float band = min(u_coronaColors - 1.0, floor(clamp(1.0 - brightness, 0.0, 0.999) * u_coronaColors));
  gl_FragColor = vec4(coronaBand(band), 1.0);
}`;function jr(e){const n=parseInt(e.slice(1),16);return[(n>>16&255)/255,(n>>8&255)/255,(n&255)/255]}const cn={colors:4,coronaColors:2,activity:.4,granulation:.5,spots:.3,corona:.5,flares:.5};function Yr(e){if(typeof document===void 0)return null;const n=document.createElement("canvas");let t=null;const a={preserveDrawingBuffer:!0,alpha:!0,antialias:!1};try{t=n.getContext("webgl2",a)??n.getContext("webgl",a)??n.getContext("experimental-webgl",a)}catch{return null}if(!t)return null;const o=($,Y)=>{const B=t.createShader($);return B?(t.shaderSource(B,Y),t.compileShader(B),t.getShaderParameter(B,t.COMPILE_STATUS)?B:(console.warn("[GalaxyMap] star shader failed",t.getShaderInfoLog(B)),t.deleteShader(B),null)):null},s=o(t.VERTEX_SHADER,Wr),i=o(t.FRAGMENT_SHADER,Vr);if(!s||!i)return null;const r=t.createProgram();if(!r)return null;if(t.attachShader(r,s),t.attachShader(r,i),t.linkProgram(r),!t.getProgramParameter(r,t.LINK_STATUS))return console.warn("[GalaxyMap] star link failed",t.getProgramInfoLog(r)),null;t.useProgram(r);const l=t.createBuffer();t.bindBuffer(t.ARRAY_BUFFER,l),t.bufferData(t.ARRAY_BUFFER,new Float32Array([-1,-1,3,-1,-1,3]),t.STATIC_DRAW);const u=t.getAttribLocation(r,"a_pos");t.enableVertexAttribArray(u),t.vertexAttribPointer(u,2,t.FLOAT,!1,0,0);const d=$=>t.getUniformLocation(r,$),c=cn;t.uniform1f(d("u_seed"),1+e.seed*.6180339887%1*9),t.uniform1f(d("u_colors"),e.colors??c.colors),t.uniform1f(d("u_coronaColors"),e.coronaColors??c.coronaColors),t.uniform1f(d("u_activity"),e.activity??c.activity),t.uniform1f(d("u_granulation"),e.granulation??c.granulation),t.uniform1f(d("u_spots"),e.spots??c.spots),t.uniform1f(d("u_corona"),e.corona??c.corona),t.uniform1f(d("u_flares"),e.flares??c.flares),t.uniform1f(d("u_bodyRadius"),.46);const f=($,Y)=>t.uniform3f(d($),...jr(Y));f("u_surface0",Rt[0]),f("u_surface1",Rt[1]),f("u_surface2",Rt[2]),f("u_surface3",Rt[3]),f("u_corona0",Hn[0]),f("u_corona1",Hn[1]);const b=()=>window.devicePixelRatio||1;let w=0,m=!1;const k=()=>{const $=Math.max(1,Math.round(e.canvasPx*b()));n.width!==$&&(n.width=$,n.height=$),n.style.width=`${e.canvasPx}px`,n.style.height=`${e.canvasPx}px`,t.viewport(0,0,$,$),t.uniform2f(d("u_resolution"),$,$)};k();const M=performance.now(),S=d("u_time"),A=d("u_phase"),I=$=>{if(m)return;k();const Y=($-M)/1e3;t.uniform1f(S,Y),t.uniform1f(A,Y),t.drawArrays(t.TRIANGLES,0,3),e.animate!==!1&&(w=requestAnimationFrame(I))};return I(M),{canvas:n,dispose(){m=!0,w&&cancelAnimationFrame(w),t=null}}}var Kr=L("<div class=world-fallback>"),Xr=L('<div class="world star"data-testid=star><div class=world-gl>');function Zr(e,n){const t=o=>e*.6180339887*o,a=o=>o-Math.floor(o);return{colors:n.colors??cn.colors,activity:n.activity??.25+a(t(1))*.6,granulation:n.granulation??cn.granulation,spots:n.spots??a(t(2))*.55,corona:n.corona??.3+a(t(3))*.45,flares:n.flares??.45+a(t(4))*.4}}function Qr(e){const[n,t]=me(!1),[a,o]=me(null);let s;return it(()=>{const i=e.seed,r=e.px,l=e.reducedMotion,u=Yr({canvasPx:r,seed:i,...Zr(i,e),animate:!l});if(!u)return;t(!1),s==null||s.appendChild(u.canvas),o(u);const d=requestAnimationFrame(()=>t(!0));ze(()=>{cancelAnimationFrame(d),o(null),u.dispose(),u.canvas.remove()})}),(()=>{var i=Xr(),r=i.firstChild;return Qe(l=>s=l,r),R(i,N(te,{get when(){return!a()},get children(){return Kr()}}),null),U(l=>{var u=`${e.px}px`,d=`${e.px}px`,c=e.title?"img":void 0,f=e.title,b=!n();return u!==l.e&&fe(i,"width",l.e=u),d!==l.t&&fe(i,"height",l.t=d),c!==l.a&&h(i,"role",l.a=c),f!==l.o&&h(i,"aria-label",l.o=f),b!==l.i&&(r.hidden=l.i=b),l},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0}),i})()}const Jr={extentLy:{w:132,h:74},hexSizeLy:2,unclaimedId:"unclaimed"};function es(e,n){return uo(n,hn(e.extentLy.w,e.extentLy.h,e.hexSizeLy),e.hexSizeLy,e.unclaimedId)}function ts(e,n,t,a=[],o=[]){const{ownership:s,contested:i}=es(e,t);if(i.length>0){const r=i[0];console.info(`[galaxy] ${i.length} contested cell(s) resolved by depth, e.g. ${r.cell.q},${r.cell.r}: ${r.claimants.join(" vs ")}`)}return{extentLy:e.extentLy,hexSizeLy:e.hexSizeLy,territories:n,systems:a,routes:o,ownership:s,contested:new Set(i.map(r=>ut(r.cell.q,r.cell.r))),revision:0}}class ns{constructor(n,t,a,o=[],s=[]){lt(this,"listeners",[]);lt(this,"overlay",new Map);lt(this,"contestedOverlay",new Map);lt(this,"undoStack",[]);lt(this,"permissions",{paint:!0});lt(this,"current",null);this.spec=n,this.territories=t,this.claims=a,this.systems=o,this.routes=s}snapshot(){const n=ts(this.spec,this.territories,this.claims,this.systems,this.routes);if(this.overlay.size===0&&this.contestedOverlay.size===0)return n;const t=new Map(n.ownership);for(const[o,s]of this.overlay)t.set(o,s);const a=new Set(n.contested);for(const[o,s]of this.contestedOverlay)s?a.add(o):a.delete(o);return{...n,ownership:t,contested:a,revision:n.revision+1}}async load(){return this.current||(this.current=this.snapshot()),this.current}onChange(n){this.listeners.push(n)}emit(){this.current=this.snapshot();for(const n of this.listeners)n(this.current)}get edits(){return this.undoStack.length}async paint(n,t,a){return this.stroke([{q:n,r:t}],a)}async setContested(n,t,a){return this.contestStroke([{q:n,r:t}],a)}async stroke(n,t){const a=await this.load(),o=[];for(const s of n){const i=ut(s.q,s.r);a.ownership.has(i)&&(this.overlay.get(i)??a.ownership.get(i))!==t&&o.push({cell:i,owner:t})}if(o.length===0)return!1;for(const s of o)this.overlay.set(s.cell,s.owner);return this.undoStack.push({deltas:o}),this.emit(),!0}async contestStroke(n,t){const a=await this.load(),o=[];for(const s of n){const i=ut(s.q,s.r);a.ownership.has(i)&&(this.contestedOverlay.get(i)??a.contested.has(i))!==t&&o.push({cell:i,contested:t})}if(o.length===0)return!1;for(const s of o)this.contestedOverlay.set(s.cell,s.contested);return this.undoStack.push({deltas:o}),this.emit(),!0}async undo(){const n=this.undoStack.pop();if(n){for(const t of n.deltas)t.owner!==void 0&&this.overlay.delete(t.cell),t.contested!==void 0&&this.contestedOverlay.delete(t.cell);this.emit()}}}const K=(e,n)=>({x:e,y:n}),Z=(e,n)=>({en:e,uk:n}),as=[{id:"bieselite",name:Z("BISELITE REPUBLIC","РЕСПУБЛІКА БІЗЕЛІТ"),color:"#3f6fd8",pattern:"grid",blurb:Z("Tau Ceti and its annexations. Formon, now nominally sovereign.","Тау Цеті та її приєднання. Формон — нибито суверенний.")},{id:"nralakk",name:Z("NRALAKK FEDERATION","ФЕДЕРАЦІЯ НРАЛЛАК"),color:"#2f9e5c",pattern:"crosshatch",blurb:Z("The Skrell homeworld and the Traverse beyond it.","Скрелльська ріджина та Траєрвер за нею.")},{id:"solarian",name:Z("SOLARIAN ALLIANCE","СОЛАРІАНСЬКИЙ АЛЬЯНС"),color:"#b9c2cc",pattern:"horizontal",blurb:Z("A shrinking core. Earth, Mars, the Jewel Worlds.","Скорочуває ядро. Земля, Марс, Перлинові Світи.")},{id:"eridanian",name:Z("ERIDANIAN FEDERATION","ФЕДЕРАЦІЯ ЕРІДАНУ"),color:"#31b0c4",pattern:"hatch",blurb:Z("Corporate holdings strung along the Tradeband.","Корпоративні володіння вздовж Трейдбанду.")},{id:"goldendeep",name:Z("GOLDEN DEEP","ЗОЛОТА ГЛИБИНА"),color:"#8a6ec4",pattern:"grid",blurb:Z("The chart's south-west, where nothing else reaches.","Південний захід карти, куди не дістається ніхто інший.")},{id:"adhomai",name:Z("PEOPLE'S REPUBLIC OF ADHOMAI","НАРОДНА РЕСПУБЛІКА АДХОМАЙ"),color:"#c2419a",pattern:"dots",blurb:Z("Frontier world turned regional power.","Прикордонний світ, що став регіональною силою.")},{id:"izweski",name:Z("IZWESKI HEGEMONY","ІЗВЕСКІ-ГЕГЕМОНІЯ"),color:"#d8a72b",pattern:"vertical",blurb:Z("Old empire, and it would like its borders back.","Стара імперія, і вона хоче свої кордони назад.")},{id:"unclaimed",name:Z("UNCLAIMED SPACE","НЕЗАЙНЯТІ СХОДИ"),color:"#4a5568",pattern:"solid",unclaimed:!0,blurb:Z("Wild space. Nobody's problem, yet.","Дикече поле. Поки що не чиїсь проблема.")}],os=[{id:"solarian",polygon:[K(-11,9),K(-5.5,18),K(4,19.5),K(11,12.5),K(9.5,4),K(2,-.5),K(-9,2)]},{id:"bieselite",polygon:[K(10.2,5.2),K(15,4),K(24,.5),K(28,-9),K(20.5,-15.5),K(11,-14),K(3.2,.8)]},{id:"nralakk",polygon:[K(-58,3.5),K(-42.5,22),K(-25,24.5),K(-17,11),K(-22.5,-5.5),K(-37,-17),K(-53.5,-10)]},{id:"eridanian",polygon:[K(20,26.5),K(39,30),K(55,18.5),K(53,4.5),K(39,-1),K(26,6),K(18.5,16)]},{id:"adhomai",polygon:[K(-17,-32.5),K(2,-28),K(13.4,-20),K(11.4,-12.6),K(-2,-15.5),K(-16,-21.5)]},{id:"izweski",polygon:[K(25.4,-10),K(43,-16),K(52,-26),K(43,-34),K(28,-31),K(19.4,-14.4)]},{id:"goldendeep",polygon:[K(-53,-12),K(-37,-14),K(-20,-9),K(-17,-21),K(-26,-33),K(-43,-36),K(-60,-28),K(-64,-16)]}],ge=(e,n,t,a,o,s,i,r,l)=>({id:e,name:n,xLy:t,yLy:a,kind:o,importance:s,territory:i,planetType:r,rings:l}),is=[ge("kettlebel",Z("Kettlebel","Кетлбел"),-41,-22,"star",2,"goldendeep"),ge("ashgrave",Z("Ashgrave","Ешгрейв"),-27,-27,"planet",1,"goldendeep","barren"),ge("tau-ceti",Z("Tau Ceti","Тау Цеті"),17,-5,"star",3,"bieselite"),ge("mictlan",Z("Mictlan","Міктлан"),22,-11,"planet",2,"bieselite","terran"),ge("pulsar-1",Z("Cygnus X-1","Лебеди X-1"),-5,7,"pulsar",2,"solarian"),ge("quasar-1",Z("P Cygni","P Лебіді"),-1,4,"quasar",3,"solarian"),ge("port-antilla",Z("Port Antilla","Порт Антілья"),12,-10,"station",1,"bieselite"),ge("qerrbalak",Z("Qerrbalak","Керрбалак"),-30,9,"star",3,"nralakk"),ge("xanu",Z("Xanu","Ксану"),-41,13,"planet",2,"nralakk","desert"),ge("himeo",Z("Himeo","Гімео"),-50,3,"planet",1,"nralakk","barren"),ge("vysoka",Z("Vysoka","Висока"),-26,-1,"planet",1,"nralakk","ice"),ge("rzeka",Z("Rzeka","Річка"),-38,-6,"planet",2,"nralakk","river"),ge("tattuqig",Z("Tattuqig","Таттуквіг"),-46,-9,"outpost",0,"nralakk"),ge("persepolis",Z("Persepolis","Персеполіс"),39,19,"star",3,"eridanian"),ge("gadpathur",Z("Gadpathur","Гадпатур"),29,21,"planet",1,"eridanian","lava"),ge("burzsia",Z("Burzsia","Бурзія"),50,15,"planet",2,"eridanian","gas",!0),ge("meropis",Z("Meropis","Меропіс"),52,7,"planet",0,"eridanian","ocean"),ge("sol",Z("Sol","Соль"),-2,11,"star",3,"solarian"),ge("earth",Z("Earth","Земля"),3,5,"planet",2,"solarian","terran"),ge("mars",Z("Mars","Марс"),-6,12,"planet",1,"solarian","barren"),ge("epsilon-eridani",Z("Epsilon Eridani","Епсилон Ерідани"),7,8,"star",2,"solarian"),ge("adhomai",Z("Adhomai","Адхомай"),-1,-24,"star",3,"adhomai"),ge("hrozamal",Z("Hro'zamal","Хро'замаль"),8,-25,"planet",1,"adhomai","gas"),ge("moghes",Z("Moghes","Моггес"),-45,-27,"star",2,""),ge("the-crow",Z("The Crow","Ворона"),-38,-33,"blackhole",2,""),ge("sunreach",Z("Sunreach","Санріч"),-33,-24,"planet",0,"","ice"),ge("assunzione",Z("Assunzione","Ассунціоне"),37,-21,"star",2,"izweski"),ge("valley-hale",Z("Valley Hale","Валлі Гейл"),28,-19,"outpost",0,"izweski"),ge("harradon",Z("Harradon","Гаррадон"),32,-29,"planet",0,"izweski","ocean")],rs=[{from:"tau-ceti",to:"persepolis",kind:"gate",allowed:["bieselite","eridanian"]},{from:"qerrbalak",to:"epsilon-eridani",kind:"gate",allowed:["nralakk","solarian"]},{from:"sol",to:"tau-ceti",kind:"gate",allowed:[]},{from:"adhomai",to:"moghes",kind:"hyperlane",allowed:[]},{from:"persepolis",to:"burzsia",kind:"hyperlane",allowed:[]}];var ss=L("<div class=chart-shell>"),ls=L('<div class=paintpick><span class=paintlabel></span><div class="swatch swatch-unclaim"></div><div class="swatch swatch-contest"></div><div class="swatch swatch-uncontest">'),cs=L("<span class=armed-note>"),ds=L("<button> "),us=L("<span class=legend-contested>"),hs=L("<div class=chart-shell><div class=toolbar><button></button><button></button><div class=fx-pick></div><button class=sky-pick></button><div class=locpick></div></div><div class=legend><b>"),fs=L("<button class=fx-toggle>"),vs=L("<span class=sky-swatch>"),gs=L("<button>"),ms=L("<div class=swatch>"),ys=L("<div class=capital-tag>"),ps=L("<button class=link>"),bs=L('<div class="panel overlay"><button class="panel-close overlay-close"></button><div class=overlay-art></div><h2></h2><div class=sub></div><div class=stat><span></span><span></span></div><div class=stat><span></span><span class=mono> / <!> LY'),xs=L("<em>"),ws=L("<div class=blurb>"),_s=L("<div class=panel><h2></h2><div class=sub></div><div class=stat><span></span><span></span></div><div class=stat><span></span><span></span></div><div class=stat><span></span><span></span></div><div class=stat><span></span><span></span></div><button class=panel-close>");const mt=e=>e===void 0?"":e.kind==="owner"?`t:${e.territory}`:`${e.kind}${e.kind==="contest"?e.on?"":":off":""}`,ks={star:"kindStar",planet:"kindPlanet",blackhole:"kindBlackHole",pulsar:"kindPulsar",quasar:"kindQuasar",dwarf:"kindDwarf",remnant:"kindRemnant",station:"kindStation",gate:"kindGate",outpost:"kindOutpost"};function Ss(){const[e,n]=me(),[t,a]=me(),[o,s]=me(),[i,r]=me(),[l,u]=me(),[d,c]=me(!1),[f,b]=me(),[w,m]=me("en-US"),[k,M]=me(0),[S,A]=me(0),[I,$]=me(!0),[Y,B]=me(!1),[ne,ye]=me(7),[C,P]=me(ln);let y;const[G,Q]=me(!1),[le,re]=me(),[se,oe]=me(),ae=pe(()=>{var E;return I()&&((E=y==null?void 0:y.permissions)==null?void 0:E.paint)!==!1}),de=pe(()=>{var F,V;const E=f();if(E)return E.kind==="contest"?{colour:E.on?"#ffb454":"#8a7a52"}:E.kind==="unclaim"?{colour:((F=Le().get(we()))==null?void 0:F.color)??"#8a96a8"}:{colour:((V=Le().get(E.territory))==null?void 0:V.color)??"#ffd479"}}),[be]=me(ni());function H(E,F){ai(E),F&&oi(F),A(V=>V+1)}window.__galaxyAdoptStrings=H,window.__galaxySetPermission=E=>$(E),St(async()=>{y=new ns(Jr,as,os,is,rs),y.onChange(E=>{n(E),M((y==null?void 0:y.edits)??0)}),n(await y.load())});const ue=E=>{E.key==="Escape"&&(G()&&qe(),b(void 0))};window.addEventListener("keydown",ue),ze(()=>window.removeEventListener("keydown",ue));const q=pe(()=>(S(),w())),Le=pe(()=>{var F;const E=new Map;for(const V of((F=e())==null?void 0:F.territories)??[])E.set(V.id,V);return E}),we=pe(()=>{var E,F;return((F=(((E=e())==null?void 0:E.territories)??[]).find(V=>V.unclaimed))==null?void 0:F.id)??""}),v=pe(()=>{var F,V;const E=t();return E?((V=qt(((F=e())==null?void 0:F.ownership)??new Map).get(E))==null?void 0:V.length)??0:0}),T=E=>{var F;return(((F=e())==null?void 0:F.systems)??[]).filter(V=>V.territory===E)},x=pe(()=>{var E;return new Map((((E=e())==null?void 0:E.systems)??[]).map(F=>[F.id,F]))}),p=E=>E.planetType??ia(E.kind,E.id);function g(E){a(void 0),s(F=>F===E?void 0:E)}const _=pe(()=>{var E;return(((E=e())==null?void 0:E.territories)??[]).filter(F=>!F.unclaimed)}),O=pe(()=>{var E;return((E=e())==null?void 0:E.contested.size)??0}),D=E=>{var xe,Ne;const F=new Set(qt(((xe=e())==null?void 0:xe.ownership)??new Map).get(E)??[]);let V=0;for(const He of((Ne=e())==null?void 0:Ne.contested)??[])F.has(He)&&V++;return V},z=pe(()=>{const E=f();if(!E)return"";if(E.kind==="contest")return E.on?ce("brushContesting",q()):ce("brushClearingContest",q());if(E.kind==="unclaim")return ce("brushUnclaimed",q());const F=Le().get(E.territory);return ce("brushPainting",q(),{name:F?Oe(F.name,q()):E.territory})});function j(E){b(F=>mt(F)===mt(E)?void 0:E)}function J(E){const F=e();F&&u(Et(E,F.hexSizeLy))}function _e(E,F){const V=new Set(le()??[]);for(const xe of E?so(E,F):[F])V.add(ut(xe.q,xe.r));return V}function Fe(E){const F=e();if(!F||!ae()||!f())return;const V=Et(E,F.hexSizeLy);Q(!0),oe(V),re(_e(void 0,V))}function Te(E){const F=e();if(!F||!G())return;const V=Et(E,F.hexSizeLy),xe=se();xe&&xe.q===V.q&&xe.r===V.r||(oe(V),re(_e(xe,V)))}async function Ue(){var xe,Ne;const E=le(),F=f();if(!G()||(Q(!1),oe(void 0),re(void 0),!E||E.size===0||!F||!y))return;const V=[...E].map(He=>{const[et,Xt]=He.split(",");return{q:+et,r:+Xt}});F.kind==="contest"?await((xe=y.contestStroke)==null?void 0:xe.call(y,V,F.on)):await((Ne=y.stroke)==null?void 0:Ne.call(y,V,F.kind==="unclaim"?we():F.territory))}function qe(){Q(!1),oe(void 0),re(void 0)}async function Ve(E){var He;const F=e();if(!F)return;const V=Et(E,F.hexSizeLy),xe=f();if(xe&&y){xe.kind==="contest"?await((He=y.setContested)==null?void 0:He.call(y,V.q,V.r,xe.on)):await y.paint(V.q,V.r,xe.kind==="unclaim"?we():xe.territory);return}const Ne=F.ownership.get(ut(V.q,V.r));Ne!==void 0&&(s(void 0),a(et=>et===Ne?void 0:Ne))}async function Je(){await(y==null?void 0:y.undo())}const pt=pe(()=>{const E=e();return E?hn(E.extentLy.w,E.extentLy.h,E.hexSizeLy).length:0});return N(te,{get when(){return e()},get fallback(){return ss()},children:E=>(()=>{var F=hs(),V=F.firstChild,xe=V.firstChild,Ne=xe.nextSibling,He=Ne.nextSibling,et=He.nextSibling,Xt=et.nextSibling,ht=V.nextSibling,Aa=ht.firstChild;return R(F,N(Hi,{reduce:!0,seed:7,get palette(){return C()}}),V),R(F,N(zi,{get model(){return E()},get locale(){return q()},get showCells(){return d()},get selected(){return t()},get hoverCell(){return l()},get painting(){return!!f()},get canPaint(){return ae()},get brushArmed(){return!!f()},get stroking(){return G()},get pendingCells(){return le()},get pendingColour(){var W;return(W=de())==null?void 0:W.colour},get planets(){return Y()},onHover:J,onClick:Ve,onSystemClick:g,get hoverSystem(){return i()},onSystemHover:r,onStrokeStart:Fe,onStrokeMove:Te,onStrokeEnd:Ue,onLeave:()=>u(void 0)}),V),xe.$$click=()=>c(W=>!W),R(xe,()=>ce("grid",q())),Ne.$$click=()=>B(W=>!W),R(Ne,()=>ce("planets",q())),R(He,N($e,{each:[["fxDoppler",1],["fxRing",2],["fxSpiral",4]],children:([W,ve])=>(()=>{var X=fs();return X.$$click=()=>ye(ee=>ee&ve?ee&~ve:ee|ve),U(ee=>{var Se=(ne()&ve)!==0,ie=ce(W,q());return Se!==ee.e&&X.classList.toggle("on",ee.e=Se),ie!==ee.t&&h(X,"title",ee.t=ie),ee},{e:void 0,t:void 0}),X})()})),et.$$click=()=>P(W=>(W+1)%Yt.length),R(et,N($e,{each:Yt,children:(W,ve)=>(()=>{var X=vs();return U(ee=>{var Se=C()===ve(),ie=`linear-gradient(90deg, ${W.stops[1]}, ${W.stops[3]}, ${W.stops[5]}, ${W.stops[7]})`;return Se!==ee.e&&X.classList.toggle("on",ee.e=Se),ie!==ee.t&&fe(X,"background",ee.t=ie),ee},{e:void 0,t:void 0}),X})()})),R(Xt,N($e,{get each(){return be()},children:W=>(()=>{var ve=gs();return ve.$$click=()=>m(W.id),R(ve,()=>W.label),U(X=>{var ee=w()===W.id,Se=ce("showNamesIn",q(),{locale:W.label});return ee!==X.e&&ve.classList.toggle("on",X.e=ee),Se!==X.t&&h(ve,"title",X.t=Se),X},{e:void 0,t:void 0}),ve})()})),R(V,N(te,{get when(){return ae()},get children(){var W=ls(),ve=W.firstChild,X=ve.nextSibling,ee=X.nextSibling,Se=ee.nextSibling;return R(ve,()=>ce("paint",q())),R(W,N($e,{get each(){return _()},children:ie=>(()=>{var Re=ms();return Re.$$click=()=>j({kind:"owner",territory:ie.id}),U(Ie=>{var De=mt(f())===`t:${ie.id}`,Be=ie.color,Ge=Oe(ie.name,q());return De!==Ie.e&&Re.classList.toggle("on",Ie.e=De),Be!==Ie.t&&fe(Re,"background",Ie.t=Be),Ge!==Ie.a&&h(Re,"title",Ie.a=Ge),Ie},{e:void 0,t:void 0,a:void 0}),Re})()}),X),X.$$click=()=>j({kind:"unclaim"}),ee.$$click=()=>j({kind:"contest",on:!0}),Se.$$click=()=>j({kind:"contest",on:!1}),U(ie=>{var Re=!!f(),Ie=mt(f())==="unclaim",De=ce("tipUnclaim",q()),Be=mt(f())==="contest",Ge=ce("tipContest",q()),tt=mt(f())==="contest:off",st=ce("tipUncontest",q());return Re!==ie.e&&W.classList.toggle("armed",ie.e=Re),Ie!==ie.t&&X.classList.toggle("on",ie.t=Ie),De!==ie.a&&h(X,"title",ie.a=De),Be!==ie.o&&ee.classList.toggle("on",ie.o=Be),Ge!==ie.i&&h(ee,"title",ie.i=Ge),tt!==ie.n&&Se.classList.toggle("on",ie.n=tt),st!==ie.s&&h(Se,"title",ie.s=st),ie},{e:void 0,t:void 0,a:void 0,o:void 0,i:void 0,n:void 0,s:void 0}),W}}),null),R(V,N(te,{get when(){return f()},get children(){var W=cs();return R(W,z,null),R(W,()=>ce("brushHint",q()),null),W}}),null),R(V,N(te,{get when(){return Ye(()=>!!ae())()&&k()>0},get children(){var W=ds(),ve=W.firstChild;return W.$$click=Je,R(W,()=>ce("undo",q()),ve),R(W,k,null),W}}),null),R(F,N(te,{get when(){return o()},children:W=>{const ve=x().get(W());return N(te,{when:ve,children:X=>{const ee=()=>Le().get(X().territory),Se=()=>ks[X().kind];return(()=>{var ie=bs(),Re=ie.firstChild,Ie=Re.nextSibling,De=Ie.nextSibling,Be=De.nextSibling,Ge=Be.nextSibling,tt=Ge.firstChild,st=tt.nextSibling,Zt=Ge.nextSibling,bt=Zt.firstChild,ft=bt.nextSibling,Tt=ft.firstChild,xt=Tt.nextSibling;return xt.nextSibling,Re.$$click=()=>s(void 0),R(Re,()=>ce("close",q())),R(Ie,()=>{const he=X().kind,je=ot(X().id);return he==="pulsar"||he==="quasar"?N(wr,{kind:he,px:190,seed:je}):he==="blackhole"?N(ma,{px:190,seed:je,frames:48,period:6,get doppler(){return ne()&1?void 0:0},get photonRing(){return ne()&2?void 0:0},get spiral(){return ne()&4?void 0:0},get title(){return Oe(X().name,q())}}):he==="star"?N(Qr,{px:200,seed:je,get title(){return Oe(X().name,q())}}):N(Gr,{seed:je,get type(){return p(X())},px:200,frames:96,period:12,get ring(){return an(je,p(X()),X().rings)},get title(){return Oe(X().name,q())}})}),R(De,()=>Oe(X().name,q())),R(Be,()=>ce(Se(),q())),R(ie,N(te,{get when(){return X().importance===3},get children(){var he=ys();return R(he,()=>ce("capitalOf",q(),{name:ee()?Oe(ee().name,q()):ce("unclaimedOwner",q())})),he}}),Ge),R(tt,()=>ce("labelSovereign",q())),R(st,N(te,{get when(){return Ye(()=>!!ee())()&&!ee().unclaimed},get fallback(){return(()=>{var he=xs();return R(he,()=>ce("unclaimedOwner",q())),he})()},get children(){var he=ps();return he.$$click=()=>a(X().territory),R(he,()=>Oe(ee().name,q())),U(je=>fe(he,"color",jt(ee().color))),he}})),R(bt,()=>ce("labelPosition",q())),R(ft,()=>X().xLy.toFixed(0),Tt),R(ft,()=>X().yLy.toFixed(0),xt),U(he=>{var yn;var je=!!(X().kind!=="blackhole"&&an(ot(X().id),p(X()),X().rings)),mn=jt(((yn=ee())==null?void 0:yn.color)??"#94a3b8");return je!==he.e&&ie.classList.toggle("ringed",he.e=je),mn!==he.t&&fe(De,"color",he.t=mn),he},{e:void 0,t:void 0}),ie})()}})}}),ht),R(F,N(te,{get when(){return t()},children:W=>{const ve=Le().get(W());return N(te,{when:ve,get children(){var X=_s(),ee=X.firstChild,Se=ee.nextSibling,ie=Se.nextSibling,Re=ie.firstChild,Ie=Re.nextSibling,De=ie.nextSibling,Be=De.firstChild,Ge=Be.nextSibling,tt=De.nextSibling,st=tt.firstChild,Zt=st.nextSibling,bt=tt.nextSibling,ft=bt.firstChild,Tt=ft.nextSibling,xt=bt.nextSibling;return R(ee,()=>Oe(ve.name,q())),R(Se,(()=>{var he=Ye(()=>!!ve.unclaimed);return()=>he()?ce("unclaimedSpace",q()):ce("sovereignTerritory",q())})()),R(X,N(te,{get when(){return ve.blurb},get children(){var he=ws();return R(he,()=>Oe(ve.blurb,q())),he}}),ie),R(Re,()=>ce("labelCells",q())),R(Ie,v),R(Be,()=>ce("labelSystems",q())),R(Ge,()=>T(W()).length),R(st,()=>ce("labelCapitals",q())),R(Zt,()=>T(W()).filter(he=>he.importance===3).length),R(ft,()=>ce("labelContested",q())),R(Tt,()=>D(W())),xt.$$click=()=>a(void 0),R(xt,()=>ce("close",q())),U(he=>fe(ee,"color",ve.color)),X}})}}),ht),R(Aa,()=>ce("orionSpur",q())),R(ht,()=>ce("extent",q(),{w:E().extentLy.w,h:E().extentLy.h,cells:pt(),size:E().hexSizeLy}),null),R(ht,N(te,{get when(){return O()>0},get children(){return[" · ",(()=>{var W=us();return R(W,()=>On("legendContested",O(),q())),W})()]}}),null),R(ht,N(te,{get when(){return k()>0},get children(){return[" · ",Ye(()=>On("legendEdits",k(),q()))]}}),null),U(W=>{var ve=!!d(),X=!!Y(),ee=ce("discFx",q()),Se=ce("sky",q());return ve!==W.e&&xe.classList.toggle("on",W.e=ve),X!==W.t&&Ne.classList.toggle("on",W.t=X),ee!==W.a&&h(He,"title",W.a=ee),Se!==W.o&&h(et,"title",W.o=Se),W},{e:void 0,t:void 0,a:void 0,o:void 0}),F})()})}un(["click"]);Ya(()=>N(Ss,{}),document.getElementById("root"));

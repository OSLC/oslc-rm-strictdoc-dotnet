const affectedBy = "http://open-services.net/ns/rm#affectedBy";
const trackedBy = "http://open-services.net/ns/rm#trackedBy";

const urlA = new URL(affectedBy);
const urlB = new URL(trackedBy);
const urlA2 = new URL(affectedBy);

console.log("JavaScript WHATWG URL");
console.log(`type=${urlA.constructor.name}`);
console.log(`a=${urlA.href} hash=${urlA.hash}`);
console.log(`b=${urlB.href} hash=${urlB.hash}`);
console.log(`a === b=${urlA === urlB}`);
console.log(`a.href === b.href=${urlA.href === urlB.href}`);
console.log(`a === a2=${urlA === urlA2}`);
console.log(`a.href === a2.href=${urlA.href === urlA2.href}`);
console.log(`Set<URL>.size=${new Set([urlA, urlB]).size}`);
console.log(`Set<URL> same lexical value size=${new Set([urlA, urlA2]).size}`);
console.log(`Set<string href>.size=${new Set([urlA.href, urlB.href]).size}`);


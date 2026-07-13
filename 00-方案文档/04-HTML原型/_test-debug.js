console.log('TEST START');
window.addEventListener('error', e => console.log('ERROR:', e.message, '@', e.filename + ':' + e.lineno));

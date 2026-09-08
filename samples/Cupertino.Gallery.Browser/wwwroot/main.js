const loading = document.getElementById('loading');
const message = document.getElementById('loading-message');
const retry = document.getElementById('reload-gallery');
const gallery = document.getElementById('out');

retry.addEventListener('click', () => {
  // Fetch a fresh entry page and its build-specific import map on retry.
  const url = new URL(globalThis.location.href);
  url.searchParams.set('reload', Date.now().toString());
  globalThis.location.replace(url);
});

const slowLoad = setTimeout(() => {
  message.textContent = 'This is taking longer than expected. You can reload and try again.';
  retry.hidden = false;
}, 30000);

try {
  // A dynamic import also lets us report a missing runtime instead of leaving
  // the loading screen up indefinitely. The build supplies its fingerprinted URL.
  const { dotnet } = await import('./_framework/dotnet.js');
  const runtime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

  message.textContent = 'Starting the gallery…';
  const config = runtime.getConfig();
  await runtime.runMain(config.mainAssemblyName, [globalThis.location.href]);
  loading.remove();
} catch (error) {
  console.error('Gallery startup failed:', error);
  loading.setAttribute('role', 'alert');
  document.getElementById('loading-title').textContent = 'Unable to load the gallery';
  message.textContent = 'Check your connection and reload to try again.';
  retry.hidden = false;
} finally {
  clearTimeout(slowLoad);
  gallery.setAttribute('aria-busy', 'false');
}

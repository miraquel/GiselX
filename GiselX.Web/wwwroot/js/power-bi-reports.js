document.addEventListener('DOMContentLoaded', () => {
  const frame = document.getElementById('power-bi-frame');
  const loading = document.getElementById('power-bi-loading');
  const container = document.getElementById('power-bi-container');

  frame?.addEventListener('load', () => loading?.classList.add('d-none'));
  document.getElementById('power-bi-fullscreen')?.addEventListener('click', async () => {
    if (container?.requestFullscreen) await container.requestFullscreen();
  });
});

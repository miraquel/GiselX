document.addEventListener('DOMContentLoaded', () => {
  const frame = document.getElementById('power-bi-frame');
  const loading = document.getElementById('power-bi-loading');
  const container = document.getElementById('power-bi-container');
  const fullscreenButton = document.getElementById('power-bi-fullscreen');
  const fullscreenLabel = fullscreenButton?.querySelector('.power-bi-fullscreen-label');
  const fullscreenStatus = document.getElementById('power-bi-fullscreen-status');
  const supportsFullscreen = Boolean(container?.requestFullscreen);

  frame?.addEventListener('load', () => loading?.classList.add('d-none'));

  const showFullscreenError = () => {
    if (!fullscreenStatus) return;
    fullscreenStatus.textContent = 'Fullscreen is unavailable. You can still open the report in a new tab.';
    fullscreenStatus.classList.remove('visually-hidden');
  };

  const updateFullscreenState = () => {
    const isFullscreen = document.fullscreenElement === container;
    fullscreenButton?.setAttribute('aria-pressed', String(isFullscreen));
    if (fullscreenLabel) fullscreenLabel.textContent = isFullscreen ? 'Exit full screen' : 'Open full screen';
  };

  if (!supportsFullscreen) {
    fullscreenButton?.classList.add('d-none');
    return;
  }

  document.addEventListener('fullscreenchange', updateFullscreenState);
  document.addEventListener('fullscreenerror', showFullscreenError);
  fullscreenButton?.addEventListener('click', async () => {
    try {
      if (document.fullscreenElement === container) {
        if (!document.exitFullscreen) {
          showFullscreenError();
          return;
        }
        await document.exitFullscreen();
      } else {
        await container.requestFullscreen();
      }
    } catch {
      showFullscreenError();
    }
  });
});

document.addEventListener('DOMContentLoaded', () => {
  const frame = document.getElementById('power-bi-frame');
  const loading = document.getElementById('power-bi-loading');
  const loadingLabel = document.getElementById('power-bi-loading-label');
  const loadStatus = document.getElementById('power-bi-load-status');
  const viewer = document.getElementById('power-bi-viewer');
  const retryButton = document.getElementById('power-bi-retry');
  const fullscreenButton = document.getElementById('power-bi-fullscreen');
  const fullscreenLabel = fullscreenButton?.querySelector('.power-bi-fullscreen-label');
  const fullscreenStatus = document.getElementById('power-bi-fullscreen-status');
  const reportUrl = frame?.dataset.reportUrl;
  const supportsFullscreen = Boolean(viewer?.requestFullscreen);
  let focusBeforeFullscreen = null;

  frame?.addEventListener('load', () => {
    loading?.classList.add('d-none');
    if (loadingLabel) loadingLabel.textContent = 'Loading report';
    if (loadStatus) loadStatus.textContent = 'Report loaded.';
  });

  retryButton?.addEventListener('click', () => {
    if (!frame || !reportUrl) return;
    loading?.classList.remove('d-none');
    if (loadingLabel) loadingLabel.textContent = 'Reloading report';
    if (loadStatus) loadStatus.textContent = 'Reloading report.';
    frame.src = reportUrl;
  });

  const showFullscreenError = () => {
    if (!fullscreenStatus) return;
    fullscreenStatus.textContent = 'Fullscreen is unavailable. You can still open the report in a new tab.';
    fullscreenStatus.classList.remove('visually-hidden');
  };

  const updateFullscreenState = () => {
    const isFullscreen = document.fullscreenElement === viewer;
    fullscreenButton?.setAttribute('aria-pressed', String(isFullscreen));
    if (fullscreenLabel) fullscreenLabel.textContent = isFullscreen ? 'Exit full screen' : 'Open full screen';
    if (isFullscreen) {
      fullscreenButton?.focus();
    } else {
      focusBeforeFullscreen?.focus();
      focusBeforeFullscreen = null;
    }
  };

  if (!supportsFullscreen) {
    fullscreenButton?.classList.add('d-none');
    return;
  }

  document.addEventListener('fullscreenchange', updateFullscreenState);
  document.addEventListener('fullscreenerror', showFullscreenError);
  fullscreenButton?.addEventListener('click', async () => {
    try {
      if (document.fullscreenElement === viewer) {
        if (!document.exitFullscreen) {
          showFullscreenError();
          return;
        }
        await document.exitFullscreen();
      } else {
        focusBeforeFullscreen = document.activeElement;
        await viewer.requestFullscreen();
      }
    } catch {
      showFullscreenError();
    }
  });
});

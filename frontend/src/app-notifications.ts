type NoticeDetail = { message: string; tone?: 'success' | 'error' | 'info' };

function showNotice({ message, tone = 'info' }: NoticeDetail) {
  document.querySelectorAll('.app-notice').forEach(node => node.remove());
  const notice = document.createElement('div');
  notice.className = `app-notice ${tone}`;
  notice.setAttribute('role', 'status');
  notice.innerHTML = `<span class="app-notice-mark">${tone === 'error' ? '!' : tone === 'success' ? '✓' : 'i'}</span><span>${message}</span><button aria-label="Dismiss notification">×</button>`;
  notice.querySelector('button')?.addEventListener('click', () => notice.remove());
  document.body.appendChild(notice);
  window.setTimeout(() => notice.remove(), 3000);
}

window.addEventListener('amcotts:notice', event => {
  const detail = (event as CustomEvent<NoticeDetail>).detail;
  if (detail?.message) showNotice(detail);
});

window.addEventListener('amcotts:workflow-error', event => {
  const detail = (event as CustomEvent<{ message?: string }>).detail;
  showNotice({ message: detail?.message || 'The workflow action could not be completed.', tone: 'error' });
});

// React notification banners are transient; the bell list remains available
// for anything that still needs attention after the banner disappears.
function dismissTransientBanners() {
  document.querySelectorAll<HTMLElement>('.toast-banner:not([data-timer-bound])').forEach(banner => {
    banner.dataset.timerBound = 'true';
    window.setTimeout(() => {
      // Keep the React-owned node mounted; removing it behind React's back can
      // cause the next state update to throw and blank the workspace.
      banner.dataset.autoDismissed = 'true';
      banner.style.opacity = '0';
      banner.style.pointerEvents = 'none';
    }, 3000);
  });
}

new MutationObserver(dismissTransientBanners).observe(document.documentElement, { childList: true, subtree: true });
dismissTransientBanners();

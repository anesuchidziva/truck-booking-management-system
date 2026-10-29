const apiBase = import.meta.env.VITE_API_BASE_URL as string | undefined;
let latestItems: Array<{ id?: string; status?: string; priority?: string }> = [];
let latestTotal = 0;

let recoveryShown = false;
function showWorkspaceRecovery(reason = '') {
  if (recoveryShown) return;
  recoveryShown = true;
  const root = document.getElementById('root');
  if (!root) return;
  root.innerHTML = `<main class="workspace-recovery"><section role="alert"><span>WORKSPACE RECOVERY</span><h1>This page needs to be reloaded</h1><p>Your last action was saved safely where possible, but the screen could not be redrawn. Reload the workspace and continue from the live booking record.</p><small data-recovery-error></small><button type="button">Reload workspace</button></section></main>`;
  const error = root.querySelector<HTMLElement>('[data-recovery-error]');
  if (error && reason) error.textContent = `Technical detail: ${reason}`;
  root.querySelector('button')?.addEventListener('click', () => window.location.reload());
}

window.addEventListener('error', event => {
  if (event.error instanceof Error || event.message) showWorkspaceRecovery(event.error?.message || event.message);
});
window.addEventListener('unhandledrejection', event => {
  if (event.reason instanceof Error) showWorkspaceRecovery(event.reason.message);
});

function setText(node: Element, value: string) {
  if (node.textContent !== value) node.textContent = value;
}

function renderLiveNotifications() {
  const trigger = document.querySelector('.notification-trigger');
  if (!trigger) return;
  const attention = latestItems.filter(x => x.status === 'UnderInspection');
  trigger.classList.toggle('has-unread', attention.length > 0);
  trigger.setAttribute('aria-label', attention.length ? `${attention.length} unread workshop notification${attention.length === 1 ? '' : 's'}` : 'Open system notifications');
  const existingCount = trigger.querySelector('[data-live-notification-count]');
  document.querySelectorAll('.toast-banner[data-live-notification]').forEach(node => {
    if (!attention.length) node.remove();
  });
  if (!attention.length) {
    existingCount?.remove();
    return;
  }

  if (existingCount && existingCount.textContent !== String(attention.length)) existingCount.textContent = String(attention.length);
  else {
    const count = document.createElement('span');
    count.dataset.liveNotificationCount = 'true';
    count.textContent = String(attention.length);
    trigger.appendChild(count);
  }

  const topbar = document.querySelector('.bp-topbar');
  if (!topbar) return;
  if (topbar.querySelector('.toast-banner[data-live-notification]')) return;
  const toast = document.createElement('div');
  toast.className = 'toast-banner amber';
  toast.dataset.liveNotification = 'true';
  toast.dataset.autoDismiss = 'true';
  toast.innerHTML = `<span>!</span><div><b>Diagnostic review required</b><small>${attention[0].id || 'A live booking'} has inspection work awaiting mechanic review.</small></div><button aria-label="Dismiss live notification">×</button>`;
  toast.querySelector('button')?.addEventListener('click', () => toast.remove());
  topbar.appendChild(toast);
}

function applyDatabaseState() {
  const empty = latestTotal === 0;
  document.body.classList.toggle('sql-empty', empty);
  document.querySelectorAll('.toast-banner:not([data-live-notification]), .notification-trigger span:not([data-live-notification-count])').forEach((node) => node.remove());

  const active = latestItems.filter(x => x.status !== 'Completed' && x.status !== 'Cancelled').length;
  const waiting = latestItems.filter(x => x.status === 'Waiting').length;
  const completed = latestItems.filter(x => x.status === 'Completed').length;
  const urgent = latestItems.filter(x => x.priority === 'URGENT' || x.priority === 'HIGH').length;
  const dashboardValues = [active, waiting, completed, urgent];
  document.querySelectorAll('.dash-top .bp-stat strong').forEach((node, index) => setText(node, String(dashboardValues[index] ?? 0)));
  document.querySelectorAll('.dash-top .bp-stat em').forEach((node) => setText(node, empty ? 'No stored records' : 'Live database records'));
  document.querySelectorAll('.dash-top .throughput b').forEach((node) => setText(node, latestTotal + ' SQL Server records'));

  const bookingPanel = document.querySelector('.booking-panel');
  if (bookingPanel) {
    const bookingStats = bookingPanel.parentElement?.querySelectorAll('.stats.four .bp-stat strong') ?? [];
    [latestTotal, latestItems.filter(x => x.status === 'InRepair').length, completed, waiting].forEach((value, index) => { if (bookingStats[index]) setText(bookingStats[index], String(value)); });
    const pagination = bookingPanel.querySelector('.pagination');
    const paginationText = 'Showing ' + latestTotal + ' live bookings ';
    if (pagination?.firstChild && pagination.firstChild.textContent !== paginationText) pagination.firstChild.textContent = paginationText;
  }

  renderLiveNotifications();

  const kanban = document.querySelector('.kanban');
  if (kanban) {
    const queueStats = kanban.parentElement?.querySelectorAll('.stats.four .bp-stat strong') ?? [];
    [active, '—', '—', completed].forEach((value, index) => { if (queueStats[index]) setText(queueStats[index], String(value)); });
    document.querySelectorAll('.kan-head span').forEach((node) => { if (empty) setText(node, '0'); });
    if (empty) {
      document.querySelectorAll('.kan-card').forEach((node) => node.remove());
      if (!kanban.parentElement?.querySelector('.sql-empty-queue')) {
        const emptyPanel = document.createElement('div');
        emptyPanel.className = 'bp-panel empty-live sql-empty-queue';
        emptyPanel.innerHTML = '<div><h2>No live jobs in the queue</h2><p>Bookings created through New Booking will appear here.</p></div>';
        kanban.parentElement?.appendChild(emptyPanel);
      }
    }
  }
}

async function refreshDatabaseState() {
  if (!apiBase || import.meta.env.VITE_DEMO_MODE === 'true') return;
  try {
    const response = await fetch(apiBase + '/api/bookings?page=1&pageSize=100');
    if (!response.ok) return;
    const data = await response.json() as { total?: number; items?: Array<{ status?: string; priority?: string }> };
    latestItems = data.items ?? [];
    latestTotal = data.total ?? 0;
    applyDatabaseState();
  } catch { /* the application reports API failures separately */ }
}

// React owns the live dashboard DOM. Do not use a global MutationObserver to
// rewrite React-rendered nodes; that can create render/mutation feedback loops.

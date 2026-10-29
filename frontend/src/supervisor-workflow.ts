const apiBase = import.meta.env.VITE_API_BASE_URL as string | undefined;
const boundActions = new WeakSet<HTMLButtonElement>();
let inspectionGuardShown = false;

function notice(message: string, tone: 'success' | 'error' = 'success') {
  const host = document.querySelector('.bp-content') || document.body;
  host.querySelector('[data-supervisor-workflow-notice]')?.remove();
  const node = document.createElement('div');
  node.dataset.supervisorWorkflowNotice = 'true';
  node.className = `form-message ${tone}`;
  node.textContent = message;
  host.prepend(node);
  window.setTimeout(() => node.remove(), 3000);
}

function bookingIdFromModal() {
  const text = document.querySelector('.detail-modal')?.textContent || '';
  const match = text.match(/BK-(\d{1,8})/i);
  return match ? Number(match[1]) : null;
}

async function hydrateDetailTimeline() {
  const modal = document.querySelector<HTMLElement>('.detail-modal');
  const panel = modal?.querySelector<HTMLElement>('.timeline-panel');
  if (!modal || !panel || !apiBase || modal.dataset.timelineHydrating === 'true') return;
  if (!panel.textContent?.includes('No status events returned')) return;
  const id = bookingIdFromModal();
  if (!id) return;
  modal.dataset.timelineHydrating = 'true';
  try {
    const response = await fetch(`${apiBase}/api/bookings/${id}`);
    if (!response.ok) return;
    const booking = await response.json();
    const events = (Array.isArray(booking.statusEvents) ? booking.statusEvents : []).filter((event: { reason?: string }) => event.reason !== 'Inspection started and completed before allocation review');
    if (!events.length || !panel.isConnected) return;
    const heading = panel.querySelector('h2');
    panel.replaceChildren();
    if (heading) panel.appendChild(heading);
    events.forEach((event: { newStatus: string; occurredUtc: string; reason?: string }, index: number) => {
      const item = document.createElement('div');
      item.className = `timeline ${index === events.length - 1 ? 'current' : ''}`;
      const marker = document.createElement('span');
      marker.textContent = '✓';
      const content = document.createElement('div');
      const title = document.createElement('b');
      title.textContent = event.newStatus.replace(/([A-Z])/g, ' $1').trim().toUpperCase();
      const date = document.createElement('small');
      date.textContent = new Date(event.occurredUtc).toLocaleString();
      const reason = document.createElement('p');
      reason.textContent = event.reason || 'Workflow status updated.';
      content.append(title, date, reason);
      item.append(marker, content);
      panel.appendChild(item);
    });
  } finally {
    modal.dataset.timelineHydrating = 'complete';
  }
}

function normalizeProgressForAssignedWorkflow() {
  const modal = document.querySelector<HTMLElement>('.detail-modal');
  const progress = modal?.querySelector<HTMLElement>('.detail-progress');
  const header = modal?.querySelector('.detail-head h2')?.textContent || '';
  if (!progress || !/Assigned|In Progress|Completed|Inspection|Ready for allocation/.test(header)) return;
  const rows = Array.from(progress.querySelectorAll<HTMLElement>(':scope > p'));
  if (rows.length !== 6 || progress.dataset.workflowOrder === 'normalized') return;
  const status = /Completed/.test(header) ? 'Completed' : /In Progress/.test(header) ? 'InRepair' : /Inspection/.test(header) ? 'UnderInspection' : /Ready for allocation/.test(header) ? 'ReadyForAllocation' : /Assigned/.test(header) ? 'Assigned' : '';
  if (!status) return;
  const labels = ['Booking created', 'Ready for allocation', 'Assigned', 'Inspection', 'Repair in progress', 'Completed'];
  const current = { Waiting: 0, ReadyForAllocation: 1, Assigned: 2, UnderInspection: 3, InRepair: 4, Completed: 5 }[status as 'Waiting' | 'ReadyForAllocation' | 'Assigned' | 'UnderInspection' | 'InRepair' | 'Completed'];
  rows.forEach((row, index) => {
    const icon = row.querySelector('svg')?.cloneNode(true);
    const state = row.querySelector('small');
    row.replaceChildren(...(icon ? [icon] : []), document.createTextNode(labels[index]), state || document.createElement('small'));
    const small = row.querySelector('small');
    if (small) small.textContent = index < current ? 'COMPLETED' : index === current ? status.replace(/([A-Z])/g, ' $1').trim().toUpperCase() : 'PENDING';
  });
  progress.dataset.workflowOrder = 'normalized';
}

function bindApprovalAction(button: HTMLButtonElement) {
  if (boundActions.has(button)) return;
  boundActions.add(button);
  button.textContent = 'Approve & Send to Allocation';
  button.title = 'Approve this booking for mechanic allocation';
  button.addEventListener('click', async event => {
    event.preventDefault();
    event.stopImmediatePropagation();
    const id = bookingIdFromModal();
    if (!apiBase || !id) return notice('Open a live booking before approving it.', 'error');
    button.disabled = true;
    try {
      const response = await fetch(`${apiBase}/api/bookings/${id}/status`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newStatus: 'ReadyForAllocation', actorRole: 'Supervisor/Manager', reason: 'Supervisor approved booking for mechanic allocation' })
      });
      const data = await response.json().catch(() => null);
      if (!response.ok) throw new Error(data?.message || 'The booking could not be approved for allocation.');
      window.dispatchEvent(new Event('amcotts:data-changed'));
      document.querySelector<HTMLButtonElement>('.detail-head > button:first-child')?.click();
      notice('Booking approved and sent to mechanic allocation.');
    } catch (error) {
      button.disabled = false;
      notice(error instanceof Error ? error.message : 'The booking could not be approved for allocation.', 'error');
    }
  }, true);
}

function guardSupervisorInspection() {
  if (document.body.dataset.amcottsRole !== 'Supervisor/Manager') return;
  const inspection = document.querySelector('.inspection-card');
  if (!inspection) return;
  if (!inspectionGuardShown) {
    inspectionGuardShown = true;
    notice('Inspection is completed by the assigned mechanic. Approve and allocate this booking first.', 'error');
  }
  document.querySelectorAll<HTMLElement>('.inspect-item').forEach(item => {
    item.setAttribute('aria-disabled', 'true');
    item.title = 'Inspection is completed by the assigned mechanic.';
    item.style.pointerEvents = 'none';
    item.style.opacity = '0.62';
  });
  document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>('.findings input, .findings textarea, .findings select').forEach(field => { field.disabled = true; });
  document.querySelectorAll<HTMLButtonElement>('button').forEach(button => {
    const text = button.textContent?.trim() || '';
    if (text === 'Save Progress' || text === 'Complete & Send') {
      button.disabled = true;
      button.title = 'This is a mechanic task.';
    }
  });
}

function bind() {
  void hydrateDetailTimeline();
  normalizeProgressForAssignedWorkflow();
  if (document.body.dataset.amcottsRole === 'Supervisor/Manager') {
    document.querySelectorAll<HTMLButtonElement>('.detail-head button.bp-primary').forEach(button => {
      if (button.textContent?.includes('Perform Inspection')) bindApprovalAction(button);
    });
    guardSupervisorInspection();
  } else {
    inspectionGuardShown = false;
  }
}

// Disabled while the React screens own their rendered DOM. Supervisor workflow
// actions are handled by the React view/backend so this legacy DOM rewriter
// must not mutate React-owned children.

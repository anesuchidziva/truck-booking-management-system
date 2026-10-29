function updateInspectionActionLabels() {
  const role = document.body?.dataset.amcottsRole;
  const buttons = document.querySelectorAll<HTMLButtonElement>('.detail-title button.bp-primary');

  buttons.forEach(button => {
    const text = button.textContent?.trim() || '';
    if (role === 'Mechanic' && (text === 'Complete & Send' || text === 'Complete Inspection & Start Repair')) {
      button.dataset.workflowLabel = 'Complete Inspection & Start Repair';
      button.title = 'Complete the inspection and begin the assigned repair.';
    }
    if (role === 'Supervisor/Manager' && text === 'Complete & Send') {
      button.dataset.workflowLabel = 'Mechanic completion required';
      button.disabled = true;
      button.title = 'The assigned mechanic must complete the inspection.';
    }
  });
}

function alignAssignedJobProgress() {
  const progressByStatus: Record<string, string> = {
    Pending: '0',
    Inspection: '20',
    'Ready for allocation': '40',
    Assigned: '60',
    'In Progress': '80',
    Completed: '100'
  };

  const session = document.querySelector<HTMLElement>('.active-session');
  const sessionStatus = session?.querySelector('small b')?.textContent?.trim();
  if (session && sessionStatus && progressByStatus[sessionStatus]) {
    const progress = progressByStatus[sessionStatus];
    session.dataset.workflowProgress = progress;
    session.style.setProperty('--workflow-progress', `${progress}%`);
  }

  document.querySelectorAll<HTMLElement>('.work-card').forEach(card => {
    const status = card.querySelector('.bp-status')?.textContent?.trim();
    if (status && progressByStatus[status]) {
      card.dataset.workflowProgress = progressByStatus[status];
      card.style.setProperty('--workflow-progress', `${progressByStatus[status]}%`);
    }
  });
}

function guardTerminalBookingAction() {
  const modal = document.querySelector<HTMLElement>('.detail-modal');
  const status = modal?.querySelector('.detail-head .bp-status')?.textContent?.trim();
  const action = modal?.querySelector<HTMLButtonElement>('.detail-head > button.bp-primary');
  if (!action || status !== 'Completed') return;
  action.dataset.workflowLabel = 'Completed record';
  action.disabled = true;
  action.title = 'Completed bookings are read-only.';
}

const allocationActions = new WeakSet<HTMLButtonElement>();
function guardReadyForAllocationAction() {
  if (document.body?.dataset.amcottsRole !== 'Supervisor/Manager') return;
  const modal = document.querySelector<HTMLElement>('.detail-modal');
  const status = modal?.querySelector('.detail-head .bp-status')?.textContent?.trim();
  const action = modal?.querySelector<HTMLButtonElement>('.detail-head > button.bp-primary');
  if (!action || status !== 'Ready for allocation') return;
  action.dataset.workflowLabel = 'Open Mechanic Allocation';
  action.title = 'Open the mechanic allocation board for this booking.';
  if (allocationActions.has(action)) return;
  allocationActions.add(action);
  action.addEventListener('click', event => {
    event.preventDefault();
    event.stopImmediatePropagation();
    document.querySelector<HTMLButtonElement>('.detail-head > button:first-child')?.click();
    Array.from(document.querySelectorAll<HTMLButtonElement>('aside button')).find(button => button.textContent?.includes('Mechanic Allocation'))?.click();
  }, true);
}

new MutationObserver(updateInspectionActionLabels).observe(document.documentElement, { childList: true, subtree: true });
updateInspectionActionLabels();
new MutationObserver(alignAssignedJobProgress).observe(document.documentElement, { childList: true, subtree: true });
alignAssignedJobProgress();
new MutationObserver(guardTerminalBookingAction).observe(document.documentElement, { childList: true, subtree: true });
guardTerminalBookingAction();
new MutationObserver(guardReadyForAllocationAction).observe(document.documentElement, { childList: true, subtree: true });
guardReadyForAllocationAction();

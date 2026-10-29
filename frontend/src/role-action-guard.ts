type GuardedAction = { roles: string[]; match: (text: string) => boolean; message: string };

const guardedActions: GuardedAction[] = [
  {
    roles: ['Mechanic'],
    match: text => text === 'New Booking' || text.includes('Schedule Inspection'),
    message: 'Booking creation is limited to clerks and supervisors.'
  },
  {
    roles: ['Mechanic'],
    match: text => text.includes('Manage Mechanics'),
    message: 'Mechanic allocation is managed by a supervisor.'
  },
  {
    roles: ['Booking Clerk'],
    match: text => text.includes('Manage Mechanics'),
    message: 'Mechanic allocation is restricted to supervisors.'
  }
];

function applyRoleActionGuard() {
  const role = document.body.dataset.amcottsRole || '';
  document.querySelectorAll<HTMLButtonElement>('button').forEach(button => {
    const text = button.textContent?.replace(/\s+/g, ' ').trim() || '';
    const rule = guardedActions.find(candidate => candidate.roles.includes(role) && candidate.match(text));
    if (!rule) return;
    button.disabled = true;
    button.dataset.roleRestricted = 'true';
    button.setAttribute('aria-disabled', 'true');
    button.title = rule.message;
    button.style.cursor = 'not-allowed';
    button.style.opacity = '0.58';
  });
}

window.addEventListener('click', event => {
  const target = (event.target as Element | null)?.closest<HTMLButtonElement>('button[data-role-restricted]');
  if (!target) return;
  event.preventDefault();
  event.stopImmediatePropagation();
  const role = document.body.dataset.amcottsRole || '';
  const text = target.textContent?.replace(/\s+/g, ' ').trim() || '';
  const rule = guardedActions.find(candidate => candidate.roles.includes(role) && candidate.match(text));
  if (rule) window.dispatchEvent(new CustomEvent('amcotts:notice', { detail: { message: rule.message, tone: 'error' } }));
}, true);

new MutationObserver(applyRoleActionGuard).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-amcotts-role'] });
applyRoleActionGuard();

const apiBase = import.meta.env.VITE_API_BASE_URL as string | undefined;
const bound = new WeakSet<Element>();
const draftKey = 'amcotts.booking.draft.v1';

function notice(message: string, tone = 'success') {
  const host = document.querySelector('.bp-content') || document.body;
  const existing = host.querySelector('.workflow-inline-notice');
  existing?.remove();
  const node = document.createElement('div');
  node.className = `form-message ${tone} workflow-inline-notice`;
  node.textContent = message;
  host.prepend(node);
  window.setTimeout(() => node.remove(), 3000);
}

function setReactValue(element: HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement, value: string) {
  const prototype = Object.getPrototypeOf(element);
  const descriptor = Object.getOwnPropertyDescriptor(prototype, 'value');
  descriptor?.set?.call(element, value);
  element.dispatchEvent(new Event('input', { bubbles: true }));
  element.dispatchEvent(new Event('change', { bubbles: true }));
}

function bookingIdFromPage() {
  const match = document.body.innerText.match(/Job #BK-(\d{1,8})/i) || document.body.innerText.match(/BK-(\d{1,8})/i);
  return match ? Number(match[1]) : null;
}

async function exportCsv() {
  if (!apiBase) return notice('The live API is not configured.', 'error');
  try {
    const response = await fetch(`${apiBase}/api/bookings?page=1&pageSize=1000`);
    if (!response.ok) throw new Error('Live bookings could not be loaded.');
    const data = await response.json();
    const headers = ['Booking ID', 'Registration', 'Customer', 'Make', 'Model', 'Status', 'Priority', 'Complaint'];
    const rows = (data.items || []).map((x: any) => [x.id, x.registration, x.customer, x.make, x.model, x.status, x.priority, x.complaint]);
    const csv = [headers, ...rows].map(row => row.map((value: unknown) => `"${String(value ?? '').replaceAll('"', '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `amcotts-bookings-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
    notice(`Exported ${rows.length} live booking${rows.length === 1 ? '' : 's'}.`);
  } catch (error) {
    notice(error instanceof Error ? error.message : 'Export failed.', 'error');
  }
}

function saveDraft() {
  const form = document.querySelector('.new-layout');
  if (!form) return;
  const inputs = form.querySelectorAll<HTMLInputElement>('input');
  const selects = form.querySelectorAll<HTMLSelectElement>('select');
  const textarea = form.querySelector<HTMLTextAreaElement>('textarea');
  const draft = {
    registration: inputs[0]?.value || '', customer: inputs[1]?.value || '',
    make: selects[0]?.value || '', model: inputs[2]?.value || '', vin: inputs[3]?.value || '',
    complaint: textarea?.value || '', priority: form.querySelector('.priority-toggle .selected')?.textContent || 'URGENT'
  };
  localStorage.setItem(draftKey, JSON.stringify(draft));
  notice('Draft saved locally on this workstation.');
}

function restoreDraft() {
  const form = document.querySelector('.new-layout');
  if (!form || form.getAttribute('data-draft-restored')) return;
  form.setAttribute('data-draft-restored', 'true');
  const raw = localStorage.getItem(draftKey);
  if (!raw) return;
  try {
    const draft = JSON.parse(raw);
    const inputs = form.querySelectorAll<HTMLInputElement>('input');
    const selects = form.querySelectorAll<HTMLSelectElement>('select');
    const textarea = form.querySelector<HTMLTextAreaElement>('textarea');
    [[inputs[0], draft.registration], [inputs[1], draft.customer], [inputs[2], draft.model], [inputs[3], draft.vin]].forEach(([element, value]) => { if (element && value) setReactValue(element, value); });
    if (selects[0] && draft.make) setReactValue(selects[0], draft.make);
    if (textarea && draft.complaint) setReactValue(textarea, draft.complaint);
    if (draft.priority) form.querySelectorAll<HTMLButtonElement>('.priority-toggle button').forEach(button => button.classList.toggle('selected', button.textContent === draft.priority));
    notice('Saved draft restored.');
  } catch { localStorage.removeItem(draftKey); }
}

function closeModal() { document.querySelector('.workflow-modal')?.remove(); }

async function showHistory() {
  const id = bookingIdFromPage();
  if (!apiBase || !id) return notice('Open a live booking before viewing service history.', 'error');
  try {
    const bookingResponse = await fetch(`${apiBase}/api/bookings/${id}`);
    if (!bookingResponse.ok) throw new Error('Booking history could not be loaded.');
    const booking = await bookingResponse.json();
    const response = await fetch(`${apiBase}/api/trucks/${booking.truckId}/history`);
    if (!response.ok) throw new Error('Vehicle history could not be loaded.');
    const data = await response.json();
    const modal = document.createElement('div');
    modal.className = 'workflow-modal';
    modal.innerHTML = `<div class="workflow-modal-card"><header><div><small>SERVICE HISTORY</small><h2>${data.truck.registrationNumber}</h2></div><button aria-label="Close history">×</button></header><p>${data.truck.make || ''} ${data.truck.model || ''} · ${data.bookings.length} recorded booking${data.bookings.length === 1 ? '' : 's'}</p><div class="workflow-history-list">${data.bookings.map((item: any) => `<div><b>BK-${String(item.id).padStart(4, '0')}</b><span>${item.status}</span><small>${item.serviceDate} · ${item.complaint}</small></div>`).join('') || '<div class="workflow-history-empty">No previous service records.</div>'}</div></div>`;
    modal.addEventListener('click', event => { if (event.target === modal || (event.target as HTMLElement).closest('button')) closeModal(); });
    document.body.appendChild(modal);
  } catch (error) { notice(error instanceof Error ? error.message : 'History could not be loaded.', 'error'); }
}

function bindQueueFilter(button: HTMLButtonElement) {
  let popover: HTMLDivElement | null = null;
  button.addEventListener('click', () => {
    popover?.remove();
    popover = document.createElement('div');
    popover.className = 'queue-filter-popover';
    ['All', 'Awaiting entry', 'In workshop', 'Quality check', 'Ready / completed'].forEach(label => {
      const option = document.createElement('button');
      option.textContent = label;
      option.onclick = () => {
        document.querySelectorAll<HTMLElement>('.kan-col').forEach(column => { column.style.display = label === 'All' || column.querySelector('.kan-head')?.textContent?.toLowerCase().includes(label.toLowerCase()) ? '' : 'none'; });
        button.innerHTML = `Filter: ${label}`;
        popover?.remove();
      };
      popover!.appendChild(option);
    });
    button.parentElement?.appendChild(popover);
  });
}

function bind() {
  document.querySelectorAll<HTMLButtonElement>('button').forEach(button => {
    if (bound.has(button)) return;
    const text = button.textContent?.trim() || '';
    if (text.includes('Export CSV')) { button.addEventListener('click', exportCsv); bound.add(button); }
    else if (text.includes('Save as Draft') || text.includes('Save Draft Locally')) { button.addEventListener('click', saveDraft); bound.add(button); }
    else if (text === 'Service History') { button.addEventListener('click', showHistory); bound.add(button); }
  });
  restoreDraft();
}

// The helper now only binds listeners and adds separate modal elements. It does
// not rewrite React-owned button children.
new MutationObserver(bind).observe(document.documentElement, { childList: true, subtree: true });
bind();

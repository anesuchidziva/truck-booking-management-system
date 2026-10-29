const apiBase = import.meta.env.VITE_API_BASE_URL as string | undefined;

async function guardCompletedInspection() {
  const content = document.querySelector<HTMLElement>('.bp-content');
  if (!content) return;
  if (document.body?.dataset.amcottsRole !== 'Supervisor/Manager') {
    delete content.dataset.terminalInspection;
    delete content.dataset.inspectionGuardId;
    return;
  }
  const jobText = content.querySelector('.detail-title p')?.textContent || '';
  const match = jobText.match(/Job #BK-(\d+)/i);
  if (!apiBase || !match) {
    delete content.dataset.terminalInspection;
    delete content.dataset.inspectionGuardId;
    return;
  }
  const bookingId = Number(match[1]);
  if (!Number.isFinite(bookingId) || content.dataset.inspectionGuardId === String(bookingId)) return;
  content.dataset.inspectionGuardId = String(bookingId);
  delete content.dataset.terminalInspection;

  try {
    const response = await fetch(`${apiBase}/api/bookings/${bookingId}`);
    if (!response.ok) return;
    const booking = await response.json() as { status?: string };
    const currentJobText = content.querySelector('.detail-title p')?.textContent || '';
    const currentMatch = currentJobText.match(/Job #BK-(\d+)/i);
    if (!currentMatch || Number(currentMatch[1]) !== bookingId) return;
    if (booking.status === 'Completed' || booking.status === 'Cancelled') {
      content.dataset.terminalInspection = booking.status;
    } else {
      delete content.dataset.terminalInspection;
    }
  } catch {
    delete content.dataset.terminalInspection;
  }
}

new MutationObserver(() => void guardCompletedInspection()).observe(document.documentElement, { childList: true, subtree: true });
window.addEventListener('amcotts:authenticated', () => void guardCompletedInspection());
guardCompletedInspection();

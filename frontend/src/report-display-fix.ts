const apiBase = import.meta.env.VITE_API_BASE_URL;

async function repairReportSummary() {
  if (!apiBase) return;
  const summary = document.querySelector('.live-report-summary');
  if (!summary || summary.getAttribute('data-summary-fixed') === 'true') return;
  const text = summary.textContent || '';
  if (!text.includes('undefined/undefined eligible assignments completed')) return;

  try {
    const response = await fetch(`${apiBase}/api/reports/mechanic-completion`);
    if (!response.ok) return;
    const data = await response.json();
    const rows = Array.isArray(data.rows) ? data.rows : [];
    const completed = rows.reduce((sum: number, row: any) => sum + Number(row.numerator || 0), 0);
    const eligible = rows.reduce((sum: number, row: any) => sum + Number(row.denominator || 0), 0);
    const walker = document.createTreeWalker(summary, NodeFilter.SHOW_TEXT);
    let node: Node | null;
    while ((node = walker.nextNode())) {
      if (node.textContent?.includes('undefined/undefined eligible assignments completed')) {
        node.textContent = node.textContent.replace('undefined/undefined eligible assignments completed', `${completed}/${eligible} eligible assignments completed`);
      }
    }
    summary.setAttribute('data-summary-fixed', 'true');
  } catch {
    // The report page already handles unavailable live data.
  }
}

new MutationObserver(() => void repairReportSummary()).observe(document.body, { childList: true, subtree: true });
void repairReportSummary();

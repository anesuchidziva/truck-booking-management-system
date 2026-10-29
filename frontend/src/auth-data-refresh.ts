// The first booking query happens while the login screen is visible. Refresh
// once authentication succeeds so dashboards never remain on the pre-login
// empty state.
function refreshAuthenticatedData() {
  window.setTimeout(() => window.dispatchEvent(new Event('amcotts:data-changed')), 250);
}

window.addEventListener('amcotts:authenticated', refreshAuthenticatedData);
new MutationObserver(() => {
  if (document.body.dataset.amcottsRole) refreshAuthenticatedData();
}).observe(document.body, { attributes: true, attributeFilter: ['data-amcotts-role'] });

// Also cover a refresh where the existing auth cookie is restored by /me
// rather than producing a new login event.
window.setTimeout(() => window.dispatchEvent(new Event('amcotts:data-changed')), 1000);

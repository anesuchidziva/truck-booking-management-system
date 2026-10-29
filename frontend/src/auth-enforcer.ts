const apiBase = import.meta.env.VITE_API_BASE_URL as string | undefined;
let authenticated = false;

// All API calls made by the React screens share the Identity cookie. This keeps
// the domain endpoints protected without requiring every legacy fetch call to
// repeat credentials: 'include'.
if (apiBase) {
  const nativeFetch = window.fetch.bind(window);
  window.fetch = (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    if (url.startsWith(apiBase)) return nativeFetch(input, { ...init, credentials: 'include' });
    return nativeFetch(input, init);
  };
}

window.addEventListener('amcotts:logout', () => {
  authenticated = false;
});

function showError(message: string) {
  document.querySelector('.login-error')?.remove();
  const card = document.querySelector('.login-card');
  if (!card) return;
  const error = document.createElement('div');
  error.className = 'login-error';
  error.setAttribute('role', 'alert');
  error.textContent = message;
  card.insertBefore(error, card.querySelector('.login-foot'));
}

function showLoading() {
  const overlay = document.createElement('div');
  overlay.className = 'signin-loading';
  overlay.innerHTML = '<div class="signin-loading-card"><span class="loading-logo">AM</span><h1>Amcotts</h1><p>Workshop Control Center</p><div class="loading-track"><i></i></div><strong>Preparing your workspace</strong><small>Loading secure workshop data...</small><div class="loading-dots"><i></i><i></i><i></i></div></div>';
  document.body.appendChild(overlay);
  return overlay;
}

window.addEventListener('click', async (event) => {
  const button = (event.target as HTMLElement).closest('button') as HTMLButtonElement | null;
  if (!button || (!button.textContent?.includes('AUTHORIZED ENTRY') && !button.textContent?.includes('ADMINISTRATOR ENTRY')) || authenticated) return;
  event.preventDefault();
  event.stopImmediatePropagation();
  const inputs = Array.from(document.querySelectorAll<HTMLInputElement>('.login-card input'));
  const email = inputs[0]?.value.trim() ?? '';
  const password = inputs[1]?.value ?? '';
  if (!email || !password) {
    showError('Enter your work email and system password.');
    return;
  }
  if (!apiBase) {
    showError('Authentication service is not configured.');
    return;
  }
  button.disabled = true;
  const overlay = showLoading();
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), 8000);
  try {
    const response = await fetch(apiBase + '/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email, password }),
      signal: controller.signal
    });
    if (!response.ok) throw new Error(response.status === 401 ? 'Invalid email or password.' : 'Authentication service is unavailable.');
    const account = await response.json() as { displayName?: string; roles?: string[] };
    const selected = document.querySelector<HTMLButtonElement>('.login-role.chosen');
    const expected = document.querySelector('.login-card.admin-mode') ? 'Administrator' : selected?.textContent?.includes('Mechanic') ? 'Mechanic' : selected?.textContent?.includes('Service Clerk') ? 'Booking Clerk' : 'Supervisor/Manager';
    if (!account.roles?.includes(expected)) throw new Error('This account is not authorised for the selected operational role.');
    authenticated = true;
    window.setTimeout(() => {
      overlay.remove();
      window.dispatchEvent(new CustomEvent('amcotts:authenticated', { detail: account }));
      // The legacy visual-selection listener only watches .login-role.
      // Remove that marker for this trusted transition so React's role
      // handler can perform the actual navigation.
      selected?.classList.remove('login-role');
      selected?.click();
    }, 150);
  } catch (error) {
    overlay.remove();
    button.disabled = false;
    showError(error instanceof DOMException && error.name === 'AbortError'
      ? 'The database did not respond in time. Check SQL Server and try again.'
      : error instanceof TypeError
      ? 'Workshop services are offline. Start the Amcotts backend on port 5000 and try again.'
      : error instanceof Error ? error.message : 'Sign in failed.');
  } finally {
    window.clearTimeout(timeout);
  }
}, true);

window.addEventListener('click', (event) => {
  const button = (event.target as HTMLElement).closest('button');
  if (!button || !button.textContent?.includes('Sign Out')) return;
  authenticated = false;
  window.dispatchEvent(new Event('amcotts:logout'));
  if (apiBase) {
    void fetch(apiBase + '/api/auth/logout', { method: 'POST', credentials: 'include' });
    window.setTimeout(() => window.location.reload(), 250);
  }
}, true);

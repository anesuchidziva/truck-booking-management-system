import './profile-menu.css';

const apiBase = import.meta.env.VITE_API_BASE_URL as string | undefined;
const bound = new WeakSet<HTMLElement>();

function closeProfileMenu() {
  document.querySelector('[data-profile-menu]')?.remove();
}

function openProfileEditor() {
  closeProfileMenu();
  const current = document.querySelector('.top-user b')?.textContent?.trim() || '';
  const backdrop = document.createElement('div');
  backdrop.className = 'profile-dialog-backdrop';
  backdrop.innerHTML = `<form class="profile-dialog" data-profile-form><button type="button" class="profile-dialog-close" aria-label="Close">×</button><span class="profile-dialog-kicker">ACCOUNT PROFILE</span><h2>Edit display name</h2><p>Change the name shown in the workshop header and activity records.</p><label>DISPLAY NAME<input name="displayName" required maxlength="80" value="${current.replaceAll('&', '&amp;').replaceAll('"', '&quot;')}</label><div class="profile-dialog-actions"><button type="button" class="bp-outline" data-profile-cancel>Cancel</button><button class="bp-primary" data-profile-save>Save profile</button></div><div class="profile-dialog-message" role="status"></div></form>`;
  const form = backdrop.querySelector<HTMLFormElement>('[data-profile-form]')!;
  const input = form.elements.namedItem('displayName') as HTMLInputElement;
  const message = form.querySelector<HTMLElement>('.profile-dialog-message')!;
  const close = () => backdrop.remove();
  backdrop.addEventListener('click', event => { if (event.target === backdrop) close(); });
  form.querySelector<HTMLButtonElement>('.profile-dialog-close')?.addEventListener('click', close);
  form.querySelector<HTMLButtonElement>('[data-profile-cancel]')?.addEventListener('click', close);
  form.addEventListener('submit', async event => {
    event.preventDefault();
    const name = input.value.trim();
    if (!name) { message.textContent = 'Display name is required.'; return; }
    if (!apiBase) { message.textContent = 'The workshop service is offline.'; return; }
    const save = form.querySelector<HTMLButtonElement>('[data-profile-save]')!;
    save.disabled = true;
    message.textContent = 'Saving…';
    try {
      const response = await fetch(`${apiBase}/api/auth/me`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ displayName: name }) });
      const data = await response.json().catch(() => null);
      if (!response.ok) throw new Error(data?.message || 'Profile could not be updated.');
      const user = document.querySelector('.top-user');
      const nameNode = user?.querySelector('b');
      const avatar = user?.querySelector('span');
      if (nameNode) nameNode.textContent = data.displayName || name;
      if (avatar) avatar.textContent = (data.displayName || name).charAt(0).toUpperCase();
      message.textContent = 'Profile updated successfully.';
      window.setTimeout(close, 700);
    } catch (error) {
      save.disabled = false;
      message.textContent = error instanceof Error ? error.message : 'Profile could not be updated.';
    }
  });
  document.body.appendChild(backdrop);
  input.focus();
  input.select();
}

function bind() {
  document.querySelectorAll<HTMLElement>('.top-user').forEach(user => {
    if (bound.has(user)) return;
    bound.add(user);
    user.setAttribute('role', 'button');
    user.setAttribute('tabindex', '0');
    user.setAttribute('aria-label', 'Open profile menu');
    const toggle = () => {
      const existing = document.querySelector('[data-profile-menu]');
      if (existing) { existing.remove(); return; }
      const menu = document.createElement('div');
      menu.dataset.profileMenu = 'true';
      menu.className = 'profile-menu';
      menu.innerHTML = `<small>ACCOUNT PROFILE</small><b>${user.querySelector('b')?.textContent || 'User'}</b><button type="button" data-profile-edit>Edit display name</button><button type="button" data-profile-signout>Sign Out</button>`;
      menu.querySelector<HTMLButtonElement>('[data-profile-edit]')?.addEventListener('click', openProfileEditor);
      user.parentElement?.appendChild(menu);
    };
    user.addEventListener('click', toggle);
    user.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); toggle(); } });
  });
}

new MutationObserver(bind).observe(document.documentElement, { childList: true, subtree: true });
bind();

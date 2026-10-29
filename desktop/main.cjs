const { app, BrowserWindow, shell } = require('electron');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const http = require('node:http');
const os = require('node:os');
const path = require('node:path');

const PRODUCT_NAME = 'Amcotts Workshop Control';
const FRONTEND_PORT = 5173;
const API_PORT = 5000;
const DIAGNOSTIC_PORT = 8001;
const DEFAULT_CONNECTION = 'Server=localhost\\SQLEXPRESS;Database=AmcottsWorkshop;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true;Connect Timeout=5';

let mainWindow;
let staticServer;
const childProcesses = new Set();

function projectRoot() {
  return path.resolve(__dirname, '..');
}

function resourceRoot() {
  return app.isPackaged ? process.resourcesPath : path.join(__dirname, 'resources');
}

function logDirectory() {
  const directory = path.join(app.getPath('userData'), 'logs');
  fs.mkdirSync(directory, { recursive: true });
  return directory;
}

function readSettings() {
  const settingsPath = path.join(app.getPath('userData'), 'settings.json');
  try {
    return JSON.parse(fs.readFileSync(settingsPath, 'utf8'));
  } catch {
    return {};
  }
}

function executablePath(relativePath) {
  return path.join(resourceRoot(), relativePath);
}

function applicationIconPath() {
  return path.join(__dirname, 'assets', 'icon.ico');
}

function startProcess(label, command, args, options = {}) {
  const logPath = path.join(logDirectory(), `${label}.log`);
  const output = fs.createWriteStream(logPath, { flags: 'a' });
  const child = spawn(command, args, {
    cwd: options.cwd || projectRoot(),
    env: { ...process.env, ...options.env },
    windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe']
  });
  child.stdout.pipe(output);
  child.stderr.pipe(output);
  child.on('error', error => console.error(`${label} failed to start`, error));
  child.on('exit', (code, signal) => console.log(`${label} exited`, { code, signal }));
  childProcesses.add(child);
  return child;
}

function startBackend(settings, frontendPort) {
  const published = executablePath(path.join('backend', 'Amcotts.Workshop.Api.exe'));
  const env = {
    ASPNETCORE_ENVIRONMENT: 'Production',
    FrontendOrigin: `http://localhost:${frontendPort}`,
    ConnectionStrings__DefaultConnection: settings.sqlConnection || DEFAULT_CONNECTION,
    DiagnosticService__BaseUrl: `http://127.0.0.1:${DIAGNOSTIC_PORT}`,
    DemoData__Enabled: 'true'
  };
  if (app.isPackaged) {
    return startProcess('backend', published, ['--urls', `http://localhost:${API_PORT}`], { cwd: path.dirname(published), env });
  }
  return startProcess('backend', 'dotnet', ['run', '--project', path.join(projectRoot(), 'backend', 'Amcotts.Workshop.Api.csproj'), '--', '--urls', `http://localhost:${API_PORT}`], { env });
}

function startDiagnostics() {
  const packaged = executablePath(path.join('diagnosis-service', 'diagnosis-service.exe'));
  if (app.isPackaged) {
    return startProcess('diagnosis', packaged, ['--host', '127.0.0.1', '--port', String(DIAGNOSTIC_PORT)], { cwd: path.dirname(packaged) });
  }
  return startProcess('diagnosis', 'python', ['-m', 'uvicorn', 'app.main:app', '--host', '127.0.0.1', '--port', String(DIAGNOSTIC_PORT)], { cwd: path.join(projectRoot(), 'diagnosis-service') });
}

function contentType(filePath) {
  return ({ '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.png': 'image/png', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml', '.woff': 'font/woff', '.woff2': 'font/woff2' })[path.extname(filePath).toLowerCase()] || 'application/octet-stream';
}

function startFrontend() {
  const root = app.isPackaged ? executablePath('frontend') : path.join(projectRoot(), 'frontend', 'dist');
  staticServer = http.createServer((request, response) => {
    const requestPath = decodeURIComponent((request.url || '/').split('?')[0]);
    const relative = requestPath === '/' ? 'index.html' : requestPath.replace(/^\/+/, '');
    const candidate = path.resolve(root, relative);
    const safeRoot = path.resolve(root);
    const filePath = candidate.startsWith(safeRoot) && fs.existsSync(candidate) && fs.statSync(candidate).isFile() ? candidate : path.join(root, 'index.html');
    response.writeHead(200, { 'Content-Type': contentType(filePath), 'Cache-Control': requestPath === '/' ? 'no-cache' : 'public, max-age=31536000, immutable' });
    fs.createReadStream(filePath).pipe(response);
  });
  return new Promise((resolve, reject) => {
    const tryPort = port => {
      staticServer.once('error', error => {
        if (error.code === 'EADDRINUSE' && port === FRONTEND_PORT) return tryPort(FRONTEND_PORT + 1);
        reject(error);
      });
      staticServer.listen(port, '127.0.0.1', () => resolve(port));
    };
    tryPort(FRONTEND_PORT);
  });
}

function waitFor(url, timeoutMs = 30000) {
  const started = Date.now();
  return new Promise((resolve, reject) => {
    const check = () => {
      const request = http.get(url, response => {
        response.resume();
        if (response.statusCode && response.statusCode < 500) return resolve();
        retry();
      });
      request.on('error', retry);
      request.setTimeout(1500, () => { request.destroy(); retry(); });
    };
    const retry = () => {
      if (Date.now() - started > timeoutMs) return reject(new Error(`Timed out waiting for ${url}`));
      setTimeout(check, 250);
    };
    check();
  });
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1440,
    height: 900,
    minWidth: 1100,
    minHeight: 700,
    show: false,
    backgroundColor: '#f4f6f8',
    icon: applicationIconPath(),
    title: PRODUCT_NAME,
    webPreferences: { contextIsolation: true, sandbox: true, preload: path.join(__dirname, 'preload.cjs') }
  });
  mainWindow.removeMenu();
  mainWindow.webContents.setWindowOpenHandler(({ url }) => { void shell.openExternal(url); return { action: 'deny' }; });
  mainWindow.webContents.on('will-navigate', (event, url) => { if (!url.startsWith('http://localhost:')) { event.preventDefault(); void shell.openExternal(url); } });
  mainWindow.once('ready-to-show', () => mainWindow.show());
  return mainWindow;
}

async function launch() {
  const settings = readSettings();
  const window = createWindow();
  window.maximize();
  await window.loadFile(path.join(__dirname, 'startup.html'));
  window.show();
  try {
    const frontendPort = await startFrontend();
    startDiagnostics();
    startBackend(settings, frontendPort);
    await waitFor(`http://127.0.0.1:${DIAGNOSTIC_PORT}/health`);
    await waitFor(`http://localhost:${API_PORT}/api/health`);
    await window.loadURL(`http://localhost:${frontendPort}`);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The workshop services could not be started.';
    const detail = `${message} Check SQL Server and review the logs in ${logDirectory()}.`;
    const encodedMessage = JSON.stringify('Workshop services could not be started.');
    const encodedDetail = JSON.stringify(detail);
    await window.webContents.executeJavaScript(`window.showStartupError(${encodedMessage}, ${encodedDetail})`).catch(() => undefined);
  }
}

function stopServices() {
  for (const child of childProcesses) {
    try { child.kill(); } catch { /* process already stopped */ }
  }
  childProcesses.clear();
  if (staticServer) staticServer.close();
}

app.whenReady().then(launch);
app.on('before-quit', stopServices);
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });

import { existsSync } from 'node:fs';
import { homedir } from 'node:os';
import { resolve } from 'node:path';
import { spawn } from 'node:child_process';

const projectRoot = resolve(import.meta.dirname, '..');
const configuredCertDir = process.env.HH_SALON_CERT_DIR;
const defaultCertDir = process.platform === 'win32'
  ? resolve(process.env.LOCALAPPDATA ?? homedir(), 'hhSalon', 'https')
  : resolve(homedir(), '.hhsalon', 'https');
const certDir = resolve(configuredCertDir ?? defaultCertDir);
const certPath = resolve(certDir, 'localhost.pem');
const keyPath = resolve(certDir, 'localhost.key');

if (!existsSync(certPath) || !existsSync(keyPath)) {
  console.error(`Trusted localhost certificate files were not found in: ${certDir}`);
  console.error('On Windows, run "npm run setup:https" once, then run "npm start" again.');
  process.exit(1);
}

const ngExecutable = resolve(
  projectRoot,
  'node_modules',
  '.bin',
  process.platform === 'win32' ? 'ng.cmd' : 'ng',
);

const child = spawn(
  ngExecutable,
  [
    'serve',
    '--host', 'localhost',
    '--port', '4200',
    '--ssl',
    '--ssl-cert', certPath,
    '--ssl-key', keyPath,
  ],
  {
    cwd: projectRoot,
    stdio: 'inherit',
    shell: process.platform === 'win32',
  },
);

child.on('error', (error) => {
  console.error('Unable to start the Angular development server.', error);
  process.exit(1);
});

child.on('exit', (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }

  process.exit(code ?? 0);
});

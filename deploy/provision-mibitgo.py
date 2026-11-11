"""Run as root on the existing Linux host. Never restore or copy customer data.

prepare: create isolated resources and start the reviewed bootstrap artifact.
web: verify the new database, apply branding, and enable HTTPS.
register: register SiteLogs after the production Jenkins release is verified.
"""
import fcntl
import json
import secrets
import shlex
import shutil
import socket
import sys
import urllib.request
from pathlib import Path

sys.path.insert(0, '/opt/sitetools-provision')
import worker as w

SITE, DOMAIN, DB, PORT = 'mibitgo', 'mibitgo.com', 'MibitgoDB', 5017
STATE = Path('/var/lib/sitetools-provision/mibitgo-progress.json')
REQUEST = dict(family='pasakay', domain=DOMAIN, site=SITE, database=DB,
               brand='Mibitgo', source='yapasakay', includeWww=True,
               inheritGoogle=True, googleClientId='', googleSecret='', googleKey='')


def save(state):
    w.atomic(STATE, state, mode=0o600)


def write_env(path, values):
    path.write_text(''.join('export '+k+'='+shlex.quote(v)+'\n' for k, v in values.items()))
    path.chmod(0o600)


def job_for(state):
    job = w.Job(state['payload'])
    job.port = PORT
    job.source = json.loads((w.ROOT/'sites.json').read_text())['yapasakay']
    return job


def prepare():
    assert not STATE.exists(), 'Preparation already started; inspect progress instead of recreating resources.'
    w.validate(REQUEST)
    registry = json.loads((w.ROOT/'sites.json').read_text())
    assert SITE not in registry
    assert not any(s['domain'] == DOMAIN or s['database'].lower() == DB.lower() for s in registry.values())
    assert w.available_port() == PORT, 'Port reservation changed; review before continuing.'
    for path in [f'/var/www/{SITE}', f'/var/lib/{SITE}', f'/etc/{SITE}',
                 f'/etc/nginx/sites-available/{SITE}', f'/etc/nginx/sites-enabled/{SITE}',
                 f'/etc/systemd/system/{SITE}.service']:
        assert not Path(path).exists() and not Path(path).is_symlink(), path
    assert w.sql(f"SELECT COUNT(*) FROM sys.databases WHERE name=N'{DB}'") == '0'
    assert w.sql("SELECT COUNT(*) FROM sys.server_principals WHERE name=N'mibitgo_linux'") == '0'
    assert shutil.disk_usage('/var').free > 10*1024**3
    for host in [DOMAIN, 'www.'+DOMAIN]:
        assert set(socket.gethostbyname_ex(host)[2]) == {'162.35.171.92'}, host
    payload = dict(id=secrets.token_hex(16), request=REQUEST, requestedBy='root-ssh')
    state = dict(payload=payload, port=PORT, phase='creating')
    save(state)
    job = job_for(state)
    password = secrets.token_urlsafe(40)
    w.sql(f'CREATE DATABASE [{DB}];')
    w.sql(f"CREATE LOGIN [mibitgo_linux] WITH PASSWORD=N'{password}';")
    w.sql('CREATE USER [mibitgo_linux] FOR LOGIN [mibitgo_linux]; ALTER ROLE db_owner ADD MEMBER [mibitgo_linux];', DB)
    state['phase'] = 'database-created'
    save(state)
    for path in [f'/var/www/{SITE}', f'/var/www/releases/{SITE}', f'/var/lib/{SITE}/uploads/brand',
                 f'/var/lib/{SITE}/keys', f'/var/log/{SITE}', f'/etc/{SITE}']:
        Path(path).mkdir(parents=True, exist_ok=True)
    app = Path('/var/www')/SITE
    w.command(['tar', '-xzf', '/tmp/mibitgo-bootstrap.tar.gz', '-C', str(app)])
    (app/'YaPasakay.Api').chmod(0o755)
    # Reuse the current main build's static UI; Jenkins rebuilds both UIs for the final release.
    w.command(['rsync', '-a', '--exclude', 'uploads', '/var/www/pricebadz/wwwroot/', str(app/'wwwroot')+'/'])
    for name in ['appsettings.json', 'appsettings.Production.json']:
        source = Path('/var/www/yapasakay')/name
        if source.exists():
            shutil.copy2(source, app/name)
    job.configure(password, 'mibitgo_linux')
    path = Path(f'/etc/{SITE}/{SITE}-api.env')
    env = w.shell_env(path)
    for key in list(env):
        if key.startswith('CorsOrigins__'):
            del env[key]
    env.update(CorsOrigins__0='https://'+DOMAIN, CorsOrigins__1='https://www.'+DOMAIN,
               YP_HEALTH_URL=f'http://127.0.0.1:{PORT}/health', Seed__DemoData='false',
               Seed__AdminPassword=secrets.token_urlsafe(32), Database__SkipMigrate='false',
               AllowedHosts=DOMAIN+';www.'+DOMAIN+';localhost;127.0.0.1')
    write_env(path, env)
    for name in ['appsettings.json', 'appsettings.Production.json']:
        path = app/name
        if path.exists():
            cfg = json.loads(path.read_text())
            cfg['CorsOrigins'] = ['https://'+DOMAIN, 'https://www.'+DOMAIN]
            path.write_text(json.dumps(cfg, indent=2))
    w.command(['systemctl', 'daemon-reload'])
    w.command(['systemctl', 'enable', '--now', SITE])
    state['phase'] = 'started'
    save(state)
    job.health()
    print('Mibitgo started on loopback; public routing not enabled yet.', flush=True)


def verify_database():
    assert w.sql('SELECT COUNT(*) FROM Users', DB) == '1', 'Expected only the new administrator.'
    assert w.sql('SELECT COUNT(*) FROM Operators', DB) == '0'
    assert int(w.sql('SELECT COUNT(*) FROM Barangays', DB)) >= 40000
    assert w.sql('SELECT COUNT(*) FROM PlatformBrandSettings', DB) == '1'
    assert int(w.sql('SELECT COUNT(*) FROM __EFMigrationsHistory', DB)) > 0
    w.sql(f'DBCC CHECKDB([{DB}]) WITH NO_INFOMSGS;', DB)


def web():
    state = json.loads(STATE.read_text())
    assert state['phase'] == 'started'
    job_for(state).health()
    verify_database()
    logo = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><rect width="128" height="128" rx="32" fill="#e53935"/><path d="M27 91V37h14l23 28 23-28h14v54H84V62L64 86 44 62v29z" fill="white"/></svg>'''
    Path('/var/lib/mibitgo/uploads/brand/mibitgo.svg').write_text(logo)
    w.sql("UPDATE PlatformBrandSettings SET BrandName=N'Mibitgo',ShortName=N'Mibitgo',LogoPath=N'/uploads/brand/mibitgo.svg',FaviconPath=N'/uploads/brand/mibitgo.svg',UpdatedAtUtc=SYSUTCDATETIME();", DB)
    nginx = '''server {
    listen 80;
    server_name mibitgo.com www.mibitgo.com;
    client_max_body_size 120m;
    location / {
        proxy_pass http://127.0.0.1:5017;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }
}
'''
    path = Path('/etc/nginx/sites-available/mibitgo')
    assert not path.exists()
    path.write_text(nginx)
    Path('/etc/nginx/sites-enabled/mibitgo').symlink_to(path)
    w.command(['nginx', '-t'])
    w.command(['systemctl', 'reload', 'nginx'])
    state['phase'] = 'http-ready'
    save(state)
    w.command(['certbot', '--nginx', '--non-interactive', '--redirect', '-d', DOMAIN, '-d', 'www.'+DOMAIN])
    for host in [DOMAIN, 'www.'+DOMAIN]:
        with urllib.request.urlopen('https://'+host+'/health', timeout=30) as response:
            assert response.status == 200
    state['phase'] = 'https-ready'
    save(state)
    print('Mibitgo fresh database, branding, and both HTTPS names verified.', flush=True)


def register():
    state = json.loads(STATE.read_text())
    assert state['phase'] == 'https-ready'
    expected = sys.argv[2]
    release = json.loads(Path('/var/lib/mibitgo/release.json').read_text())
    assert release['commit'] == expected
    assert release['buildNumber'].isdigit()
    job_for(state).health()
    verify_database()
    registry = json.loads((w.ROOT/'sites.json').read_text())
    assert SITE not in registry
    registry[SITE] = dict(domain=DOMAIN, family='pasakay', database=DB, job='Yapasakay')
    w.atomic(w.ROOT/'sites.json', registry)
    cfgpath = Path('/var/www/sitelogs/appsettings.json')
    shutil.copy2(cfgpath, '/var/backups/sitelogs-appsettings-before-mibitgo.json')
    cfg = json.loads(cfgpath.read_text())
    cfg.setdefault('Sites', {})[SITE] = DOMAIN
    cfg.setdefault('DeploymentJobs', {})[SITE] = 'Yapasakay'
    cfgpath.write_text(json.dumps(cfg, indent=2))
    state.update(phase='complete', release=release)
    save(state)
    print(json.dumps(dict(status='complete', site=SITE, release=release)), flush=True)


if __name__ == '__main__':
    # The daemon holds worker.lock even while idle. Pause it only if no creation is pending.
    assert not list((w.ROOT/'inbox').glob('*.json')), 'Provisioning queue is not empty.'
    assert not any(json.loads(p.read_text()).get('state') == 'running'
                   for p in (w.ROOT/'status').glob('*.json')), 'Another provisioning job is active.'
    w.command(['systemctl', 'stop', 'sitetools-provision'])
    try:
        with open('/var/lib/sitetools-provision/worker.lock', 'a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            {'prepare': prepare, 'web': web, 'register': register}[sys.argv[1]]()
    finally:
        w.command(['systemctl', 'start', 'sitetools-provision'])

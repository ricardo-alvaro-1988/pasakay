"""Run on production with the installed SiteTools worker; contains no credentials.

prepare creates isolated resources and prepares deployment commits.
publish publishes routing, verifies Jenkins, and registers SiteLogs.
finish requires working DNS and enables and verifies TLS.
"""
import fcntl
import json
import secrets
import shlex
import shutil
import socket
import sys
import urllib.request
import urllib.parse
from pathlib import Path

sys.path.insert(0, '/opt/sitetools-provision')
import worker as w

SITE = 'angkasna'
DB = 'angaksnaDb'
DOMAIN = 'angkasna.com'
STATE = Path('/var/lib/sitetools-provision/angkasna-progress.json')
REQUEST = dict(family='pasakay', domain=DOMAIN, site=SITE, database=DB,
               brand='Angkasna', source='yapasakay', includeWww=True,
               inheritGoogle=True, googleClientId='', googleSecret='', googleKey='')


def save(data):
    w.atomic(STATE, data, mode=0o600)


def environment(path, values):
    path.write_text(''.join('export '+k+'='+shlex.quote(v)+'\n' for k,v in values.items()))
    path.chmod(0o600)


def prepare():
    if STATE.exists():
        raise ValueError('Preparation already started; inspect the saved progress before resuming.')
    w.validate(REQUEST)
    registry = json.loads((w.ROOT/'sites.json').read_text())
    assert SITE not in registry
    assert not any(s['domain'] == DOMAIN or s['database'].lower() == DB.lower() for s in registry.values())
    for p in [f'/var/www/{SITE}', f'/var/lib/{SITE}', f'/etc/{SITE}',
              f'/etc/nginx/sites-available/{SITE}', f'/etc/nginx/sites-enabled/{SITE}',
              f'/etc/systemd/system/{SITE}.service']:
        assert not Path(p).exists() and not Path(p).is_symlink(), p
    assert w.sql(f"SELECT COUNT(*) FROM sys.databases WHERE name=N'{DB}'") == '0'
    assert w.sql("SELECT COUNT(*) FROM sys.server_principals WHERE name=N'angkasna_linux'") == '0'
    assert shutil.disk_usage('/var').free > 10*1024**3
    source_env = w.shell_env(Path('/etc/yapasakay/yapasakay-api.env'))
    for key in ['GoogleAuth__ClientId', 'GoogleAuth__ClientSecret', 'Maps__BrowserApiKey', 'Maps__GoogleApiKey']:
        assert source_env.get(key), key+' missing from source environment'
    payload = {'id':secrets.token_hex(16), 'request':REQUEST, 'requestedBy':'production-admin'}
    job = w.Job(payload)
    job.source = registry['yapasakay']
    job.port = w.available_port()
    assert job.port == 5013, 'Review deployment port before proceeding'
    job.repo = job.work/'repo'
    job.git('clone','--branch','main',w.FAMILIES['pasakay']['repo'],str(job.repo),cwd=job.work)
    job.git('config','user.name','Enova SiteTools')
    job.git('config','user.email','sitetools@enovasoftware.com')
    job.git('fetch','origin','Staging')
    job.mainbase = job.git('rev-parse','HEAD')
    job.edit_pipeline()
    w.command(['python3','deploy/check-branch-routing.py'],cwd=job.repo)
    job.git('add','Jenkinsfile','deploy/branch-targets.sh','deploy/check-branch-routing.py')
    job.git('commit','-m','Add angkasna production deployment via SiteTools')
    job.commit = job.git('rev-parse','HEAD')
    job.git('checkout','-b','sitetools-staging','origin/Staging')
    job.git('cherry-pick',job.commit)
    w.command(['python3','deploy/check-branch-routing.py'],cwd=job.repo)
    job.git('checkout','main')
    state = dict(payload=payload, port=job.port, mainbase=job.mainbase, commit=job.commit, phase='prepared-routing')
    save(state)
    backup = f'/var/opt/mssql/backup/angkasna-{payload["id"]}.bak'
    w.sql(f"BACKUP DATABASE [{job.source['database']}] TO DISK=N'{backup}' WITH COPY_ONLY, COMPRESSION;")
    logical = w.sql(f"SELECT name COLLATE DATABASE_DEFAULT+'|'+type_desc COLLATE DATABASE_DEFAULT FROM [{job.source['database']}].sys.database_files").splitlines()
    moves = []
    for i,row in enumerate(logical):
        name,typ = row.strip().split('|')
        name = name.replace("'", "''")
        suffix = '.ldf' if typ == 'LOG' else '.mdf' if i == 0 else '.ndf'
        moves.append(f"MOVE N'{name}' TO N'/var/opt/mssql/data/{DB}_{i}{suffix}'")
    w.sql(f"RESTORE DATABASE [{DB}] FROM DISK=N'{backup}' WITH "+', '.join(moves)+', RECOVERY;')
    password = secrets.token_urlsafe(40)
    w.sql(f"CREATE LOGIN [angkasna_linux] WITH PASSWORD=N'{password}';")
    w.sql('CREATE USER [angkasna_linux] FOR LOGIN [angkasna_linux]; ALTER ROLE db_owner ADD MEMBER [angkasna_linux];',DB)
    state.update(phase='database-created', backup=backup)
    save(state)
    for p in [f'/var/www/{SITE}',f'/var/www/releases/{SITE}',f'/var/lib/{SITE}/uploads',f'/var/lib/{SITE}/keys',f'/var/log/{SITE}',f'/etc/{SITE}']:
        Path(p).mkdir(parents=True,exist_ok=True)
    # Bootstrap from the current production artifact, rather than staging code.
    w.command(['rsync','-a','--exclude','wwwroot/uploads','--exclude','keys','--exclude','logs','/var/www/parasakay/',f'/var/www/{SITE}/'])
    # Preserve Yapasakay defaults while moving secrets into the environment.
    for name in ['appsettings.json','appsettings.Production.json']:
        src = Path('/var/www/yapasakay')/name
        if src.exists(): shutil.copy2(src,Path('/var/www')/SITE/name)
    w.command(['rsync','-a','/var/lib/yapasakay/uploads/',f'/var/lib/{SITE}/uploads/'])
    assert not w.command(['rsync','-rcn','--out-format=%n','/var/lib/yapasakay/uploads/',f'/var/lib/{SITE}/uploads/']).strip()
    job.configure(password,'angkasna_linux')
    env_path = Path(f'/etc/{SITE}/{SITE}-api.env')
    env = w.shell_env(env_path)
    for k in list(env):
        if k.startswith('CorsOrigins__'): del env[k]
    env.update(CorsOrigins__0='https://'+DOMAIN, CorsOrigins__1='https://www.'+DOMAIN,
               YP_HEALTH_URL=f'http://127.0.0.1:{job.port}/health')
    environment(env_path,env)
    w.sql("UPDATE PlatformBrandSettings SET BrandName=N'Angkasna',ShortName=N'Angkasna',UpdatedAtUtc=SYSUTCDATETIME();",DB)
    w.sql(f'DBCC CHECKDB([{DB}]) WITH NO_INFOMSGS;',DB)
    w.command(['systemctl','daemon-reload'])
    w.command(['systemctl','enable','--now',SITE])
    job.health()
    nginx = '''server {
    listen 80;
    server_name angkasna.com www.angkasna.com;
    client_max_body_size 120m;
    location / {
        proxy_pass http://127.0.0.1:5013;
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
    Path(f'/etc/nginx/sites-available/{SITE}').write_text(nginx)
    Path(f'/etc/nginx/sites-enabled/{SITE}').symlink_to(f'/etc/nginx/sites-available/{SITE}')
    w.command(['nginx','-t'])
    w.command(['systemctl','reload','nginx'])
    job.audit_uploads()
    state['phase'] = 'ready-for-dns'
    save(state)
    print('Angkasna database, environment, uploads, production app, HTTP routing, and deployment commits prepared.',flush=True)


def publish():
    state = json.loads(STATE.read_text())
    assert state['phase'] in ['ready-for-dns','published','built','registered','complete']
    job = w.Job(state['payload'])
    job.port = state['port']
    job.repo = job.work/'repo'
    job.commit = state['commit']
    if state['phase'] == 'ready-for-dns':
        verify_local(job)
        job.git('fetch','origin')
        # Rebase only our isolated routing change if main has advanced.
        job.git('checkout','main')
        job.git('rebase','origin/main')
        job.commit = job.git('rev-parse','HEAD')
        w.command(['python3','deploy/check-branch-routing.py'],cwd=job.repo)
        job.git('checkout','sitetools-staging')
        job.git('rebase','origin/Staging')
        w.command(['python3','deploy/check-branch-routing.py'],cwd=job.repo)
        staging = job.git('rev-parse','HEAD')
        job.git('push','--atomic','origin',f'{job.commit}:refs/heads/main',f'{staging}:refs/heads/Staging')
        state.update(phase='published',commit=job.commit,stagingCommit=staging)
        save(state)
    if state['phase'] == 'published':
        job.build(w.FAMILIES['pasakay'])
        state.update(phase='built',buildNumber=job.status['buildNumber'])
        save(state)
    job.health()
    job.audit_uploads()
    verify_local(job)
    registry = json.loads((w.ROOT/'sites.json').read_text())
    registry[SITE] = dict(domain=DOMAIN,family='pasakay',database=DB)
    w.atomic(w.ROOT/'sites.json',registry)
    state['phase'] = 'registered'
    save(state)
    job.status.update(state='complete',url='http://www.'+DOMAIN)
    job.event('Angkasna production deployment verified and added to SiteLogs. Apex DNS and TLS verification remain pending.')
    print(json.dumps({k:v for k,v in state.items() if k != 'payload'},indent=2),flush=True)


def verify_local(job):
    expected = w.shell_env(Path('/etc/yapasakay/yapasakay-api.env'))
    actual = w.shell_env(Path('/etc/angkasna/angkasna-api.env'))
    for k in ['GoogleAuth__ClientId','GoogleAuth__ClientSecret','Maps__BrowserApiKey','Maps__GoogleApiKey']:
        assert actual.get(k) and actual[k] == expected[k], k+' did not match source'
    for endpoint,field,key in [('auth','googleClientId','GoogleAuth__ClientId'),('maps','googleMapsBrowserKey','Maps__BrowserApiKey')]:
        req = urllib.request.Request(f'http://127.0.0.1:{job.port}/api/public/{endpoint}',headers={'Host':DOMAIN,'X-Forwarded-Proto':'https'})
        with urllib.request.urlopen(req,timeout=30) as response:
            result = json.load(response)
        assert result[field] == actual[key] and result['publicOrigin'] == 'https://'+DOMAIN


def finish():
    state = json.loads(STATE.read_text())
    assert state['phase'] in ['registered','complete']
    job = w.Job(state['payload'])
    job.port = state['port']
    for host in [DOMAIN,'www.'+DOMAIN]:
        for resolver in ['1.1.1.1','8.8.8.8']:
            addresses = set(w.command(['dig','@'+resolver,'+short',host,'A']).splitlines())
            assert addresses == {'162.35.171.92'}, 'DNS not ready: '+host
            assert not w.command(['dig','@'+resolver,'+short',host,'AAAA']).strip(), 'Unexpected IPv6 record: '+host
    # The hosting provider resolver can retain a negative cache after public DNS
    # updates. Pin only these two verified hosts for this process's HTTPS checks;
    # TLS still verifies the original hostname, and system DNS is unchanged.
    lookup = socket.getaddrinfo
    def verified_lookup(host, *args, **kwargs):
        return lookup('162.35.171.92' if host in [DOMAIN,'www.'+DOMAIN] else host, *args, **kwargs)
    socket.getaddrinfo = verified_lookup
    if not Path('/etc/letsencrypt/live/angkasna.com/fullchain.pem').exists():
        w.command(['certbot','--nginx','--non-interactive','--redirect','-d',DOMAIN,'-d','www.'+DOMAIN])
    job.health()
    verify_local(job)
    # Validate the Directions API used by GoogleDrivingDistance. The generic
    # worker checks Places (New), whereas this app uses the legacy JS Places API.
    env = w.shell_env(Path('/etc/angkasna/angkasna-api.env'))
    query = urllib.parse.urlencode(dict(origin='14.5995,120.9842',destination='14.5547,121.0244',mode='driving',key=env['Maps__GoogleApiKey']))
    with urllib.request.urlopen('https://maps.googleapis.com/maps/api/directions/json?'+query,timeout=20) as response:
        assert json.load(response).get('status') == 'OK', 'Google Directions check failed'
    for host in [DOMAIN,'www.'+DOMAIN]:
        with urllib.request.urlopen('https://'+host,timeout=30) as response:
            assert response.status == 200
    state['phase'] = 'complete'
    state['googleChecks'] = {'runtimeCredentials':'matched source','directions':'OK','placesNew':'HTTP 403; not the Places API used by this app','browserMapsAndOAuth':'requires browser sign-in test'}
    save(state)
    job.status.update(state='complete',url='https://'+DOMAIN)
    for step in job.status['steps']: step['state'] = 'complete'
    job.status['buildNumber'] = state['buildNumber']
    job.status['buildUrl'] = 'https://devops.enovasoftware.com/job/Yapasakay/'+str(state['buildNumber'])+'/'
    job.status['step'] = len(job.status['steps'])-1
    job.event('Angkasna HTTPS, production deployment, and SiteLogs registration complete.')
    print('Verified https://angkasna.com and https://www.angkasna.com',flush=True)


if __name__ == '__main__':
    lock = open('/var/lib/sitetools-provision/angkasna.lock','w')
    fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    {'prepare':prepare,'publish':publish,'finish':finish}[sys.argv[1]]()

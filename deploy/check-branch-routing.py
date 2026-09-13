"""Validate deployment routing and Bash syntax without deploying."""
import os
from pathlib import Path
import re
import subprocess
root = Path(__file__).resolve().parent.parent
for name in ['branch-targets.sh', 'jenkins-deploy.sh', 'sync-wwwroot.sh']:
    subprocess.run(['bash', '-n', str(root / 'deploy' / name)], check=True)
for script in re.findall(r"sh '''(.*?)'''", (root / 'Jenkinsfile').read_text(), re.S):
    subprocess.run(['bash', '-n'], input=script.replace(chr(92)*2, chr(92)), text=True, check=True)
for branch, expected in {
    'Staging': ['yapasakay'], 'origin/Staging': ['yapasakay'],
    'main': ['pricebadz','pasakyaman','trygoride'],
    'origin/main': ['pricebadz','pasakyaman','trygoride'],
    'staging': None, 'master': None, 'feature/test': None, 'PR-1': None, '': None
}.items():
    result = subprocess.run(['bash', str(root / 'deploy/branch-targets.sh'), branch],
        text=True, capture_output=True, env={**os.environ, 'DEPLOY_HOST':'yapasakay.com'})
    if expected is None:
        assert result.returncode != 0, branch
    else:
        assert result.returncode == 0, branch
        rows = [line.split('|') for line in result.stdout.splitlines()]
        assert [row[0] for row in rows] == expected, branch
        for site, host, app, service, env, health, releases in rows:
            assert host == 'yapasakay.com' and app == '/var/www/' + site
            assert service == site + '.service' and env == '/etc/' + site + '/' + site + '-api.env'
            assert releases == '/var/www/releases/' + site
print('Shell syntax and nine branch routing cases passed.')

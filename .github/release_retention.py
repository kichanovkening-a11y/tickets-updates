"""Keep the latest two complete stable application releases; preserve Git tags."""
import json
import os
import re
import sys
import urllib.request

REQUIRED_ASSETS = {
    'Tickets-application.zip', 'Tickets-database-engine.zip',
    'Tickets-runtime.zip', 'Tickets-Windows-x64.zip', 'update-manifest.json',
}


def retention_plan(releases):
    stable = []
    for release in releases:
        match = re.fullmatch(r'v(\d+)\.(\d+)\.(\d+)', release.get('tag_name', ''))
        if match and release.get('published_at') and not release.get('draft') and not release.get('prerelease'):
            stable.append((tuple(map(int, match.groups())), release))
    stable.sort(key=lambda item: item[0], reverse=True)
    kept = [item[1] for item in stable[:2]]
    removed = [item[1] for item in stable[2:]]
    if removed:
        for release in kept:
            uploaded = {asset['name'] for asset in release.get('assets', []) if asset.get('state') == 'uploaded'}
            if not REQUIRED_ASSETS <= uploaded:
                raise ValueError('Retained release is incomplete: ' + release['tag_name'])
    return kept, removed


def main():
    repo = os.environ['GITHUB_REPOSITORY']
    token = os.environ['GH_TOKEN']
    base = 'https://api.github.com/repos/' + repo

    def api(path, method='GET'):
        request = urllib.request.Request(base + path, method=method, headers={
            'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
            'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'Tickets-release-retention',
        })
        with urllib.request.urlopen(request, timeout=30) as response:
            body = response.read()
            return json.loads(body) if body else None

    releases = []
    page = 1
    while True:
        batch = api('/releases?per_page=100&page=' + str(page))
        releases.extend(batch)
        if len(batch) < 100:
            break
        page += 1
    kept, removed = retention_plan(releases)
    print('Keep: ' + ', '.join(item['tag_name'] for item in kept))
    print('Remove old archives: ' + ', '.join(item['tag_name'] for item in removed))
    if '--apply' not in sys.argv:
        print('Dry run; no releases deleted.')
        return
    for release in removed:
        api('/releases/' + str(int(release['id'])), method='DELETE')
        print('Deleted release and assets: ' + release['tag_name'])
    print('Retention complete. Git tags and source history are unchanged.')


if __name__ == '__main__':
    main()

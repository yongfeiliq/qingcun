"""Read-only macOS runtime diagnostics; never launches payloads or changes security policy."""
import argparse, datetime, hashlib, json, platform, subprocess, sys
from pathlib import Path


def command(args):
    try:
        p = subprocess.run(args, capture_output=True, text=True, errors='replace', timeout=25)
        return {'command': args, 'exit': p.returncode, 'stdout': p.stdout[-24000:], 'stderr': p.stderr[-12000:]}
    except (OSError, subprocess.TimeoutExpired) as exc:
        return {'command': args, 'error': str(exc)}


def inspect(engine):
    runtime = engine / 'runtime'
    files = [runtime / 'dotnet']
    for pattern in ('host/fxr/*/libhostfxr.dylib', 'shared/Microsoft.NETCore.App/*/libhostpolicy.dylib', 'shared/Microsoft.NETCore.App/*/libcoreclr.dylib'):
        files.extend(sorted(runtime.glob(pattern)))
    result = []
    for p in files:
        item = {'path': str(p), 'exists': p.is_file()}
        if p.is_file():
            item['sha256'] = hashlib.sha256(p.read_bytes()).hexdigest()
            item['mode'] = oct(p.stat().st_mode & 0o777)
            item['checks'] = [command(a) for a in (
                ['/usr/bin/file', str(p)],
                ['/usr/bin/codesign', '--verify', '--strict', '--verbose=4', str(p)],
                ['/usr/bin/codesign', '--display', '--verbose=4', str(p)],
                ['/usr/bin/xattr', '-p', 'com.apple.quarantine', str(p)])]
        result.append(item)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--engine', type=Path, help='Exact engine directory containing QingcunHost.dll and runtime/')
    parser.add_argument('--output', type=Path, required=True, help='Write this new diagnostic JSON file')
    args = parser.parse_args()
    if sys.platform != 'darwin':
        parser.error('Run this diagnostic on macOS; Windows results cannot diagnose Gatekeeper.')
    engine = (args.engine or Path.home() / 'Library/Application Support/QingcunVideo/native').expanduser().resolve()
    report = {'created': datetime.datetime.now().isoformat(), 'readOnly': True, 'os': platform.platform(), 'engine': str(engine)}
    report['system'] = [command(a) for a in (['/usr/bin/sw_vers'], ['/usr/bin/uname', '-m'], ['/usr/sbin/sysctl', '-n', 'hw.optional.arm64'])]
    report['files'] = inspect(engine)
    host = engine / 'runtime/dotnet'
    if host.is_file():
        report['assessment'] = command(['/usr/sbin/spctl', '--assess', '--type', 'execute', '--verbose=4', str(host)])
    logs = Path.home() / 'Library/Logs/Qingcun'
    report['installerLogs'] = []
    if logs.is_dir():
        for p in sorted(logs.glob('*.log'), key=lambda p: p.stat().st_mtime, reverse=True)[:5]:
            with p.open('rb') as f:
                f.seek(max(0, p.stat().st_size - 32000))
                report['installerLogs'].append({'name': p.name, 'tail': f.read().decode('utf-8', 'replace')})
    predicate = '(process == "syspolicyd" OR process == "amfid" OR process == "taskgated-helper") AND (eventMessage CONTAINS[c] "Qingcun" OR eventMessage CONTAINS[c] "dotnet" OR eventMessage CONTAINS[c] "NETCore" OR eventMessage CONTAINS[c] "libhost")'
    report['policyLog'] = command(['/usr/bin/log', 'show', '--last', '20m', '--style', 'compact', '--predicate', predicate])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    # Do not overwrite an existing report or application file.
    with args.output.open('x', encoding='utf-8') as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print(str(args.output.resolve()))


if __name__ == '__main__':
    main()

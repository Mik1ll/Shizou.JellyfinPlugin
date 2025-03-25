import urllib.parse
import yaml
import subprocess
import re
import pathlib
import multiprocessing
import argparse
import urllib

parser = argparse.ArgumentParser()
parser.add_argument("GITHUB_REPO", help="The github repo the release will be hosted on")
parser.add_argument("RELEASE_NOTES", help="The release notes to include in the metadata")
args = parser.parse_args()

if str(args.GITHUB_REPO).startswith("https://"):
    repo_url = args.GITHUB_REPO
elif str(args.GITHUB_REPO).startswith("github.com"):
    repo_url = f"https://{args.GITHUB_REPO}"
else:
    repo_url = urllib.parse.urljoin("https://github.com", args.GITHUB_REPO)


git_ver_p = subprocess.run(
    [
        "git",
        "describe",
        "--match=v[0-9]*.[0-9]*.[0-9]*",
        "--tags",
        "--abbrev=0",
    ],
    capture_output=True,
    text=True,
    encoding="utf-8",
)
if git_ver_p.returncode:
    exit(f"Git describe returned error code: {git_ver_p.returncode} {git_ver_p.stderr}")
git_tag = git_ver_p.stdout.strip()
git_ver = git_tag[1:]

artifact_dir = pathlib.Path("artifacts")
artifact_dir.mkdir(exist_ok=True)
if not artifact_dir.is_dir():
    exit(f'"{artifact_dir.absolute()}" is not a directory')

with open("Shizou.JellyfinPlugin.csproj", "r") as csproj:
    csproj_content = csproj.read()
    frameworks_match = re.compile(
        r"<TargetFrameworks?>(.*?)<\/TargetFrameworks?>", re.IGNORECASE
    ).search(csproj_content)
    if not frameworks_match:
        exit("Failed to get .net framework version form csproj")
    framework = frameworks_match.group(1).split(";")[0]
    target_abi_match = re.compile(
        r"^.*<PackageReference (?=.*Include=\"Jellyfin\.Controller\").*Version=\"(.*?)\".*?\/>.*$",
        re.IGNORECASE | re.MULTILINE,
    ).search(csproj_content)
    if not target_abi_match:
        exit("Failed to get Jellyfin package reference version in csproj")
    target_abi = target_abi_match.group(1) + ".0"


metadata_file = "jprm.yaml"
with open(metadata_file, "r") as metadata:
    cfg = yaml.safe_load(metadata)

if cfg is None:
    exit(f'Failed to load any metadata from "{metadata_file}"')

cfg["targetAbi"] = target_abi
cfg["changelog"] = args.RELEASE_NOTES

with open(metadata_file, "w") as metadata:
    yaml.safe_dump(cfg, metadata, sort_keys=False)

try:
    cpu_count = str(multiprocessing.cpu_count())
except:
    cpu_count = "1"

jprm_p = subprocess.run(
    [
        "jprm",
        "-v",
        "debug",
        "plugin",
        "build",
        "--dotnet-framework",
        framework,
        "--max-cpu-count",
        cpu_count,
        "--version",
        git_ver,
        "--output",
        artifact_dir,
    ],
    stdout=subprocess.PIPE,
    text=True,
    encoding="utf-8",
)

if jprm_p.returncode:
    exit(f"Jprm build returned error code: {jprm_p.returncode}")

del cfg["targetAbi"]
del cfg["changelog"]

with open(metadata_file, "w") as metadata:
    yaml.safe_dump(cfg, metadata, sort_keys=False)

package_zip = jprm_p.stdout.strip()

package_filename = pathlib.Path(package_zip).name

print("Package path: " + package_zip)

package_url = f"{repo_url.rstrip('/')}/releases/download/{git_tag}/{package_filename}"

print("Package url: " + package_url)

manifest_file = "Repository/manifest.json"

jprm_repo_p = subprocess.run(
    [
        "jprm",
        "-v",
        "debug",
        "repo",
        "add",
        "-U",
        package_url,
        manifest_file,
        package_zip,
    ]
)

if jprm_repo_p.returncode:
    exit(f"Jprm repo add returned error code: {jprm_repo_p.returncode}")

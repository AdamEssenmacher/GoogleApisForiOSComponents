#!/usr/bin/env bash

set -euo pipefail

script_dir="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(CDPATH= cd -- "${script_dir}/.." && pwd)"

output_dir="${repo_root}/output/firebase-binding-audit"
generator_version="0.7.0"
sharpie_version="26.3.0.11"
sharpie_path=""
disable_sharpie="false"
disable_suppressions="false"
targets=""
keep_temp="false"

usage() {
  cat <<EOF
Usage: $(basename "$0") [--targets target1,target2] [--output-dir path] [--generator-version version] [--sharpie-version version] [--sharpie-path path] [--disable-sharpie] [--disable-suppressions] [--keep-temp]
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --targets)
      [[ $# -ge 2 ]] || { echo "Missing value for --targets" >&2; usage; exit 2; }
      targets="$2"
      shift 2
      ;;
    --output-dir)
      [[ $# -ge 2 ]] || { echo "Missing value for --output-dir" >&2; usage; exit 2; }
      output_dir="$2"
      shift 2
      ;;
    --generator-version)
      [[ $# -ge 2 ]] || { echo "Missing value for --generator-version" >&2; usage; exit 2; }
      generator_version="$2"
      shift 2
      ;;
    --sharpie-version)
      [[ $# -ge 2 ]] || { echo "Missing value for --sharpie-version" >&2; usage; exit 2; }
      sharpie_version="$2"
      shift 2
      ;;
    --sharpie-path)
      [[ $# -ge 2 ]] || { echo "Missing value for --sharpie-path" >&2; usage; exit 2; }
      sharpie_path="$2"
      shift 2
      ;;
    --disable-sharpie)
      disable_sharpie="true"
      shift
      ;;
    --disable-suppressions)
      disable_suppressions="true"
      shift
      ;;
    --keep-temp)
      keep_temp="true"
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 2
      ;;
  esac
done

mkdir -p "$output_dir"

args=(
  --repo-root "$repo_root"
  --output-dir "$output_dir"
  --generator-version "$generator_version"
  --sharpie-version "$sharpie_version"
)

if [[ -n "$targets" ]]; then
  args+=(--targets "$targets")
fi

if [[ -n "$sharpie_path" ]]; then
  args+=(--sharpie-path "$sharpie_path")
fi

if [[ "$disable_sharpie" == "true" ]]; then
  args+=(--disable-sharpie)
fi

if [[ "$disable_suppressions" == "true" ]]; then
  args+=(--disable-suppressions)
fi

if [[ "$keep_temp" == "true" ]]; then
  args+=(--keep-temp)
fi

dotnet run --project "${repo_root}/scripts/FirebaseBindingAudit/FirebaseBindingAudit.csproj" -- "${args[@]}"

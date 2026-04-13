#!/usr/bin/env bash

set -euo pipefail

expected_version="26.3.0.11"
tool_command="${HOME}/.dotnet/tools/sharpie"
wrapper_dir="${HOME}/.local/bin"
wrapper_path="${wrapper_dir}/sharpie"

if dotnet tool update -g Sharpie.Bind.Tool --version "${expected_version}" >/dev/null 2>&1; then
  :
else
  dotnet tool install -g Sharpie.Bind.Tool --version "${expected_version}"
fi

mkdir -p "${wrapper_dir}"

cat > "${wrapper_path}" <<'EOF'
#!/usr/bin/env bash
exec "${HOME}/.dotnet/tools/sharpie" "$@"
EOF

chmod +x "${wrapper_path}"

resolved_path="$(command -v sharpie || true)"
resolved_version="$(sharpie --version 2>&1 || true)"

if [[ "${resolved_path}" != "${wrapper_path}" ]]; then
  echo "Expected sharpie to resolve to ${wrapper_path}, but found ${resolved_path:-<missing>}." >&2
  exit 1
fi

if [[ "${resolved_version}" != *"dotnet-sharpie ${expected_version}"* ]]; then
  echo "Expected sharpie --version to contain 'dotnet-sharpie ${expected_version}', but got: ${resolved_version:-<empty>}." >&2
  exit 1
fi

printf 'sharpie path: %s\n' "${resolved_path}"
printf 'sharpie version: %s\n' "${resolved_version}"

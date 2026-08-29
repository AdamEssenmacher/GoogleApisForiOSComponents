#!/usr/bin/env python3
"""Validate Google Maps BundleResource declarations against the pinned SDK archive."""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
from pathlib import Path, PurePosixPath
import shutil
import sys
import tarfile
import tempfile
import time
from urllib.error import URLError
from urllib.request import Request, urlopen
import xml.etree.ElementTree as ET


EXPECTED_ARCHIVE_SHA256 = (
    "81bbd92c2d627087ae222ae955e5f746590812d7389b9d800add15e4004b6431"
)
RESOURCE_PROPERTY = "_GoogleMapsResourcesBaseFolder"
RESOURCE_TOKEN = f"$({RESOURCE_PROPERTY})"
LOGICAL_ROOT = "GoogleMaps.bundle"
RESTORE_TARGET = "_GMpsDownloadedItems"
EXPECTED_APP_ITEM_CONDITION = "('$(OutputType)'!='Library' OR '$(IsAppExtension)'=='True')"


def repository_root() -> Path:
    return Path(__file__).resolve().parent.parent


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--targets",
        type=Path,
        default=repository_root() / "source/Google/Maps/Maps.targets",
        help="Maps.targets to inspect (default: repository source file)",
    )
    parser.add_argument(
        "--archive",
        type=Path,
        help="Use an existing Google Maps .tar.gz instead of downloading the declared URL",
    )
    return parser.parse_args()


def elements(parent: ET.Element, name: str) -> list[ET.Element]:
    return [element for element in parent.iter() if element.tag.rsplit("}", 1)[-1] == name]


def direct_children(parent: ET.Element, name: str) -> list[ET.Element]:
    return [child for child in list(parent) if child.tag.rsplit("}", 1)[-1] == name]


def normalize_relative_path(value: str, label: str, errors: list[str]) -> str:
    normalized = value.replace("\\", "/")
    path = PurePosixPath(normalized)
    if not normalized or normalized.startswith("/") or any(part in ("", ".", "..") for part in path.parts):
        errors.append(f"{label} is not a normalized relative path: {value!r}")
    return normalized


def one_text(parent: ET.Element, name: str, errors: list[str]) -> str:
    matches = elements(parent, name)
    if len(matches) != 1 or not (matches[0].text or "").strip():
        errors.append(f"expected exactly one non-empty {name}, found {len(matches)}")
        return ""
    return (matches[0].text or "").strip()


def parse_targets(targets_path: Path) -> tuple[str, str, list[str], list[str], list[str]]:
    errors: list[str] = []
    try:
        root = ET.parse(targets_path).getroot()
    except (OSError, ET.ParseError) as exc:
        raise RuntimeError(f"could not parse {targets_path}: {exc}") from exc

    downloads = elements(root, "XamarinBuildDownload")
    if len(downloads) != 1:
        errors.append(f"expected exactly one XamarinBuildDownload item, found {len(downloads)}")
    download = downloads[0] if downloads else root
    archive_url = one_text(download, "Url", errors)
    archive_kind = one_text(download, "Kind", errors)
    if archive_kind and archive_kind.lower() != "tgz":
        errors.append(f"XamarinBuildDownload Kind is {archive_kind!r}, expected 'Tgz'")
    if archive_url and not archive_url.startswith("https://"):
        errors.append(f"archive URL must use HTTPS: {archive_url}")

    properties = elements(root, RESOURCE_PROPERTY)
    if len(properties) != 1 or not (properties[0].text or "").strip():
        errors.append(f"expected exactly one non-empty {RESOURCE_PROPERTY}, found {len(properties)}")
        archive_resource_root = ""
    else:
        resource_base = (properties[0].text or "").strip().replace("\\", "/")
        prefix = "$(XamarinBuildDownloadDir)$(_GoogleMapsItemsFolder)/"
        if not resource_base.startswith(prefix):
            errors.append(f"{RESOURCE_PROPERTY} must start with {prefix!r}: {resource_base}")
            archive_resource_root = ""
        else:
            archive_resource_root = resource_base[len(prefix) :].rstrip("/")
            if not archive_resource_root.endswith(f"/{LOGICAL_ROOT}"):
                errors.append(
                    f"{RESOURCE_PROPERTY} must resolve to {LOGICAL_ROOT}: {archive_resource_root}"
                )

    restore_targets = [
        target for target in direct_children(root, "Target") if target.get("Name") == RESTORE_TARGET
    ]
    if len(restore_targets) != 1:
        errors.append(
            f"expected exactly one project-level Target named {RESTORE_TARGET}, "
            f"found {len(restore_targets)}"
        )
    restore_target = restore_targets[0] if restore_targets else root
    if restore_targets and restore_target.get("Condition", "").strip():
        errors.append(f"Target {RESTORE_TARGET} must not have a Condition")

    all_restore_hooks = [
        item
        for item in elements(root, "XamarinBuildRestoreResources")
        if item.get("Include") == RESTORE_TARGET
    ]
    if len(all_restore_hooks) != 1:
        errors.append(
            f"expected exactly one XamarinBuildRestoreResources hook for {RESTORE_TARGET}, "
            f"found {len(all_restore_hooks)}"
        )

    hook_groups: list[tuple[ET.Element, ET.Element]] = []
    for item_group in direct_children(root, "ItemGroup"):
        for item in direct_children(item_group, "XamarinBuildRestoreResources"):
            if item.get("Include") == RESTORE_TARGET:
                hook_groups.append((item_group, item))

    if len(hook_groups) != 1:
        errors.append(
            f"expected one project-level ItemGroup to schedule {RESTORE_TARGET}, "
            f"found {len(hook_groups)}"
        )
    else:
        hook_group, restore_hook = hook_groups[0]
        if hook_group.get("Condition", "").strip() != EXPECTED_APP_ITEM_CONDITION:
            errors.append(
                f"{RESTORE_TARGET} ItemGroup Condition is {hook_group.get('Condition', '')!r}; "
                f"expected {EXPECTED_APP_ITEM_CONDITION!r}"
            )
        if restore_hook.get("Condition", "").strip():
            errors.append(f"XamarinBuildRestoreResources hook for {RESTORE_TARGET} must not have a Condition")
        if downloads and download not in list(hook_group):
            errors.append("XamarinBuildDownload and its restore hook must share the same ItemGroup")

    includes: list[str] = []
    logical_names: list[str] = []
    bundle_resources = elements(restore_target, "BundleResource")
    all_bundle_resources = elements(root, "BundleResource")
    if len(bundle_resources) != len(all_bundle_resources):
        errors.append(
            f"all BundleResource items must be declared by {RESTORE_TARGET}; "
            f"found {len(all_bundle_resources) - len(bundle_resources)} elsewhere"
        )
    if not bundle_resources:
        errors.append(f"Target {RESTORE_TARGET} declares no BundleResource items")

    for index, resource in enumerate(bundle_resources, start=1):
        include = resource.get("Include", "")
        if not include.startswith(RESOURCE_TOKEN):
            errors.append(
                f"BundleResource #{index} Include must start with {RESOURCE_TOKEN}: {include!r}"
            )
            include_suffix = include
        else:
            include_suffix = include[len(RESOURCE_TOKEN) :]
        include_suffix = normalize_relative_path(
            include_suffix, f"BundleResource #{index} Include", errors
        )
        includes.append(include_suffix)

        if resource.get("Visible", "").lower() != "false":
            errors.append(f"BundleResource #{index} must set Visible=\"False\"")

        logical_elements = [
            child for child in list(resource) if child.tag.rsplit("}", 1)[-1] == "LogicalName"
        ]
        if len(logical_elements) != 1 or not (logical_elements[0].text or "").strip():
            errors.append(
                f"BundleResource #{index} must contain exactly one non-empty LogicalName"
            )
            logical_name = ""
        else:
            logical_name = normalize_relative_path(
                (logical_elements[0].text or "").strip(),
                f"BundleResource #{index} LogicalName",
                errors,
            )
        logical_names.append(logical_name)

        expected_logical_name = f"{LOGICAL_ROOT}/{include_suffix}"
        if logical_name and logical_name != expected_logical_name:
            errors.append(
                f"BundleResource #{index} maps {include_suffix!r} to {logical_name!r}; "
                f"expected {expected_logical_name!r}"
            )

    return archive_url, archive_resource_root, includes, logical_names, errors


def download_archive(url: str, destination: Path) -> None:
    last_error: Exception | None = None
    for attempt in range(1, 4):
        try:
            request = Request(url, headers={"User-Agent": "GoogleApisForiOSComponents-resource-audit"})
            with urlopen(request, timeout=120) as response, destination.open("wb") as output:
                shutil.copyfileobj(response, output)
            return
        except (OSError, URLError) as exc:
            last_error = exc
            destination.unlink(missing_ok=True)
            if attempt < 3:
                time.sleep(attempt * 2)
    raise RuntimeError(f"could not download {url} after 3 attempts: {last_error}")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def archive_resource_files(archive_path: Path, resource_root: str) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    files: list[str] = []
    normalized_root = resource_root.strip("/")
    prefix = f"{normalized_root}/"

    try:
        with tarfile.open(archive_path, "r:gz") as archive:
            for member in archive.getmembers():
                name = member.name
                while name.startswith("./"):
                    name = name[2:]
                if not name.startswith(prefix):
                    continue
                relative = name[len(prefix) :]
                if not relative:
                    continue
                if member.isfile():
                    files.append(normalize_relative_path(relative, "archive member", errors))
                elif not member.isdir():
                    errors.append(f"archive contains a non-file resource member: {name}")
    except (OSError, tarfile.TarError) as exc:
        raise RuntimeError(f"could not inspect {archive_path}: {exc}") from exc

    if not files:
        errors.append(f"archive contains no regular files below {normalized_root}")
    return files, errors


def duplicate_messages(values: list[str], label: str) -> list[str]:
    return [
        f"duplicate {label} ({count} occurrences): {value}"
        for value, count in sorted(Counter(values).items())
        if count > 1
    ]


def compare_sets(actual: set[str], expected: set[str], actual_label: str) -> list[str]:
    errors: list[str] = []
    for value in sorted(expected - actual):
        errors.append(f"missing {actual_label}: {value}")
    for value in sorted(actual - expected):
        errors.append(f"stale {actual_label}: {value}")
    return errors


def main() -> int:
    args = parse_arguments()
    targets_path = args.targets.resolve()

    try:
        archive_url, resource_root, includes, logical_names, errors = parse_targets(targets_path)
        with tempfile.TemporaryDirectory(prefix="maps-resource-manifest-") as temp_dir:
            if args.archive:
                archive_path = args.archive.resolve()
            else:
                if not archive_url:
                    raise RuntimeError("cannot download archive because Maps.targets has no valid URL")
                archive_path = Path(temp_dir) / "GoogleMaps.tar.gz"
                download_archive(archive_url, archive_path)

            actual_sha256 = sha256(archive_path)
            if actual_sha256 != EXPECTED_ARCHIVE_SHA256:
                errors.append(
                    f"archive SHA-256 is {actual_sha256}, expected {EXPECTED_ARCHIVE_SHA256}"
                )

            archive_files, archive_errors = archive_resource_files(archive_path, resource_root)
            errors.extend(archive_errors)
    except (OSError, RuntimeError) as exc:
        print(f"Maps resource manifest check failed: {exc}", file=sys.stderr)
        return 1

    errors.extend(duplicate_messages(includes, "BundleResource Include"))
    errors.extend(duplicate_messages(logical_names, "LogicalName"))
    errors.extend(duplicate_messages(archive_files, "archive resource path"))

    include_set = set(includes)
    archive_set = set(archive_files)
    errors.extend(compare_sets(include_set, archive_set, "BundleResource Include"))
    expected_logical_names = {f"{LOGICAL_ROOT}/{path}" for path in archive_set}
    errors.extend(
        compare_sets(set(logical_names), expected_logical_names, "LogicalName")
    )

    print(f"Targets: {targets_path}")
    print(f"Archive: {archive_url}")
    print(f"SHA-256: {actual_sha256}")
    print(
        f"Resources: {len(includes)} declarations, {len(include_set)} unique target paths, "
        f"{len(archive_files)} archive files"
    )

    if errors:
        print(f"Maps resource manifest check failed with {len(errors)} error(s):", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("Maps resource manifest check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

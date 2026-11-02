#!/usr/bin/env python3
"""Validate Google Maps resources against the pinned SDK archive."""

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
ARCHIVE_URL = "https://dl.google.com/dl/cpdc/33a7ac549361ab23/GoogleMaps-9.2.0.tar.gz"
ARCHIVE_RESOURCE_ROOT = "Maps/Resources/GoogleMapsResources/GoogleMaps.bundle"
EXPECTED_RESOURCE_FILE_COUNT = 190
RESOURCE_PROPERTY = "_GoogleMapsResourcesBaseFolder"
RESOURCE_TOKEN = f"$({RESOURCE_PROPERTY})"
LOGICAL_ROOT = "GoogleMaps.bundle"


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
        help="Use an existing Google Maps .tar.gz instead of downloading the pinned URL",
    )
    parser.add_argument(
        "--copy-bundle-to",
        type=Path,
        metavar="DIRECTORY",
        help=(
            "After all checks pass, copy the verified bundle to "
            "DIRECTORY/GoogleMaps.bundle; the destination must not already exist"
        ),
    )
    return parser.parse_args()


def elements(parent: ET.Element, name: str) -> list[ET.Element]:
    return [element for element in parent.iter() if element.tag.rsplit("}", 1)[-1] == name]


def normalize_relative_path(value: str, label: str, errors: list[str]) -> str:
    normalized = value.replace("\\", "/")
    path = PurePosixPath(normalized)
    if (
        not normalized
        or normalized.startswith("/")
        or path.as_posix() != normalized
        or any(part in ("", ".", "..") for part in path.parts)
    ):
        errors.append(f"{label} is not a normalized relative path: {value!r}")
    return normalized


def parse_targets(targets_path: Path) -> tuple[list[str], list[str], list[str]]:
    errors: list[str] = []
    try:
        root = ET.parse(targets_path).getroot()
    except (OSError, ET.ParseError) as exc:
        raise RuntimeError(f"could not parse {targets_path}: {exc}") from exc

    includes: list[str] = []
    logical_names: list[str] = []
    bundle_resources = elements(root, "BundleResource")
    if not bundle_resources:
        errors.append("Maps.targets declares no BundleResource items")

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

    return includes, logical_names, errors


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


def copy_verified_bundle(
    archive_path: Path,
    resource_root: str,
    relative_files: list[str],
    destination_parent: Path,
) -> Path:
    """Copy regular bundle files from a verified archive without trusting tar paths."""

    destination_parent = destination_parent.expanduser().resolve()
    if destination_parent.exists() and not destination_parent.is_dir():
        raise RuntimeError(f"bundle destination parent is not a directory: {destination_parent}")
    destination_parent.mkdir(parents=True, exist_ok=True)

    destination_bundle = destination_parent / LOGICAL_ROOT
    if destination_bundle.exists() or destination_bundle.is_symlink():
        raise RuntimeError(f"bundle destination already exists: {destination_bundle}")

    normalized_root = resource_root.strip("/")
    prefix = f"{normalized_root}/"
    expected_files = sorted(relative_files)

    try:
        with tempfile.TemporaryDirectory(
            prefix=".googlemaps-bundle-", dir=destination_parent
        ) as staging_directory, tarfile.open(archive_path, "r:gz") as archive:
            staging_bundle = Path(staging_directory) / LOGICAL_ROOT

            for member in archive.getmembers():
                member_name = member.name
                while member_name.startswith("./"):
                    member_name = member_name[2:]
                if not member_name.startswith(prefix):
                    continue

                relative_name = member_name[len(prefix) :]
                if not relative_name:
                    continue
                if member.isdir():
                    relative_name = relative_name.rstrip("/")
                relative_path = PurePosixPath(relative_name)
                if (
                    relative_path.is_absolute()
                    or relative_path.as_posix() != relative_name
                    or any(part in ("", ".", "..") for part in relative_path.parts)
                ):
                    raise RuntimeError(f"unsafe archive resource path: {member.name!r}")

                output_path = staging_bundle.joinpath(*relative_path.parts)
                if member.isdir():
                    output_path.mkdir(parents=True, exist_ok=True)
                    continue
                if not member.isfile():
                    raise RuntimeError(f"archive resource is not a regular file: {member.name}")

                output_path.parent.mkdir(parents=True, exist_ok=True)
                source = archive.extractfile(member)
                if source is None:
                    raise RuntimeError(f"could not read archive resource: {member.name}")
                with source, output_path.open("xb") as destination:
                    shutil.copyfileobj(source, destination)

            copied_files = sorted(
                path.relative_to(staging_bundle).as_posix()
                for path in staging_bundle.rglob("*")
                if path.is_file()
            )
            if copied_files != expected_files:
                raise RuntimeError("copied bundle file set differs from the verified archive manifest")

            staging_bundle.rename(destination_bundle)
    except (OSError, tarfile.TarError) as exc:
        raise RuntimeError(f"could not copy verified Google Maps resources: {exc}") from exc

    return destination_bundle


def main() -> int:
    args = parse_arguments()
    targets_path = args.targets.resolve()

    try:
        includes, logical_names, errors = parse_targets(targets_path)
        with tempfile.TemporaryDirectory(prefix="maps-resource-manifest-") as temp_dir:
            if args.archive:
                archive_path = args.archive.resolve()
            else:
                archive_path = Path(temp_dir) / "GoogleMaps.tar.gz"
                download_archive(ARCHIVE_URL, archive_path)

            actual_sha256 = sha256(archive_path)
            if actual_sha256 != EXPECTED_ARCHIVE_SHA256:
                errors.append(
                    f"archive SHA-256 is {actual_sha256}, expected {EXPECTED_ARCHIVE_SHA256}"
                )

            archive_files, archive_errors = archive_resource_files(
                archive_path, ARCHIVE_RESOURCE_ROOT
            )
            errors.extend(archive_errors)

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

            if len(includes) != EXPECTED_RESOURCE_FILE_COUNT:
                errors.append(
                    f"expected {EXPECTED_RESOURCE_FILE_COUNT} BundleResource declarations, "
                    f"found {len(includes)}"
                )
            if len(include_set) != EXPECTED_RESOURCE_FILE_COUNT:
                errors.append(
                    f"expected {EXPECTED_RESOURCE_FILE_COUNT} unique target paths, "
                    f"found {len(include_set)}"
                )
            if len(archive_files) != EXPECTED_RESOURCE_FILE_COUNT:
                errors.append(
                    f"expected {EXPECTED_RESOURCE_FILE_COUNT} archive files, "
                    f"found {len(archive_files)}"
                )

            print(f"Targets: {targets_path}")
            print(f"Archive URL: {ARCHIVE_URL}")
            print(f"Archive resource root: {ARCHIVE_RESOURCE_ROOT}")
            print(f"SHA-256: {actual_sha256}")
            print(
                f"Resources: {len(includes)} declarations, {len(include_set)} unique target paths, "
                f"{len(archive_files)} archive files"
            )

            if errors:
                print(
                    f"Maps resource manifest check failed with {len(errors)} error(s):",
                    file=sys.stderr,
                )
                for error in errors:
                    print(f"  - {error}", file=sys.stderr)
                return 1

            copied_bundle = None
            if args.copy_bundle_to:
                copied_bundle = copy_verified_bundle(
                    archive_path,
                    ARCHIVE_RESOURCE_ROOT,
                    archive_files,
                    args.copy_bundle_to,
                )

            print("Maps resource manifest check passed.")
            if copied_bundle:
                print(f"Verified bundle: {copied_bundle}")
            return 0
    except (OSError, RuntimeError) as exc:
        print(f"Maps resource manifest check failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""
OpenPerpetuum Asset Integrity & Presence Checker
------------------------------------------------
Verifies that all client and server assets listed in the resource manifest
(resource_0.dat / resource_*.dat) are actually present on disk and match
expected specifications (sizes, line endings, layer binary sources).

Usage:
    python3 script/check_assets.py [OPTIONS]

Options:
    --manifest PATH       Path to manifest file (default: asset/resource_0.dat)
    --asset-dir PATH      Path to asset root directory (default: asset)
    --layers-dir PATH     Additional directory to search for raw layer .bin files
                          (e.g., custom-layers, perpetuum-data/layers)
    --check-sizes         Validate file sizes against sizes recorded in manifest
    --check-definitions   Cross-reference definitionProperties against asset list
    --missing-only        Only display missing or mismatched assets
    --json                Output results as JSON
    --no-color            Disable ANSI color codes in output
    --help, -h            Show this help message
"""

import argparse
import json
import os
import re
import sys


class Colors:
    def __init__(self, enabled=True):
        self.enabled = enabled

    def _wrap(self, code, text):
        if not self.enabled or not sys.stdout.isatty():
            return text
        return f"\033[{code}m{text}\033[0m"

    def green(self, text):
        return self._wrap("32", text)

    def red(self, text):
        return self._wrap("31", text)

    def yellow(self, text):
        return self._wrap("33", text)

    def cyan(self, text):
        return self._wrap("36", text)

    def bold(self, text):
        return self._wrap("1", text)

    def gray(self, text):
        return self._wrap("90", text)


def parse_genxy_dict(text):
    """
    Parses Genxy formatted dictionary / table into Python dict.
    Supports nested brackets and key=value pairs.
    """
    pos = 0
    length = len(text)

    def skip_ws():
        nonlocal pos
        while pos < length and text[pos] in " \t\r\n":
            pos += 1

    def parse_dict():
        nonlocal pos
        res = {}
        while pos < length:
            skip_ws()
            if pos >= length:
                break
            if text[pos] == "]":
                pos += 1
                break
            if text[pos] in "#|":
                pos += 1
                skip_ws()
                key_start = pos
                while pos < length and text[pos] not in "= \t\r\n]":
                    pos += 1
                key = text[key_start:pos].strip()
                skip_ws()
                if pos < length and text[pos] == "=":
                    pos += 1
                    skip_ws()
                    if pos < length and text[pos] == "[":
                        pos += 1
                        val = parse_dict()
                    else:
                        val_start = pos
                        while pos < length and text[pos] not in "|\r\n]":
                            pos += 1
                        val = text[val_start:pos].strip()
                    if key:
                        res[key] = val
            else:
                pos += 1
        return res

    return parse_dict()


def parse_manifest(manifest_path):
    """Loads and parses resource_0.dat manifest."""
    if not os.path.isfile(manifest_path):
        raise FileNotFoundError(f"Manifest file not found: {manifest_path}")

    with open(manifest_path, "r", encoding="utf-8", errors="ignore") as f:
        content = f.read()

    parsed = parse_genxy_dict(content)
    resources = parsed.get("resources", parsed)
    return resources


def parse_manifest_size(size_str):
    """Parses manifest size string ('i48d8' -> hex, 'n100' -> decimal, '100' -> decimal)."""
    if not size_str:
        return None
    size_str = size_str.strip()
    try:
        if size_str.startswith("i"):
            return int(size_str[1:], 16)
        elif size_str.startswith("n"):
            return int(size_str[1:], 10)
        else:
            return int(size_str, 10)
    except ValueError:
        return None


def find_layer_bin(layer_rel_path, search_dirs):
    """
    Given a layer resource path (e.g. lang0000/layers/0070/altitude0070/00000000.dat),
    checks if a corresponding raw bin file (e.g. altitude.0070.bin) exists.
    """
    m = re.match(r".*layers/(\d{4})/([a-z]+)\1/.*\.dat", layer_rel_path)
    if not m:
        return None, None
    zone, layer = m.group(1), m.group(2)
    bin_name = f"{layer}.{zone}.bin"

    for directory in search_dirs:
        if not directory or not os.path.isdir(directory):
            continue
        candidate = os.path.join(directory, bin_name)
        if os.path.isfile(candidate):
            return candidate, bin_name

    return None, bin_name


def check_definitions_cross_reference(def_path, known_assets, asset_dir):
    """
    Inspects definitionProperties/000000XX.txt to ensure all referenced
    icons, models (gfx), and audio (sfx) exist.
    """
    if not os.path.isfile(def_path):
        return None

    with open(def_path, "r", encoding="utf-8", errors="ignore") as f:
        content = f.read()

    referenced_icons = set()
    referenced_gfx = set()
    referenced_sfx = set()

    for m in re.finditer(r"\|icon=\$?([a-zA-Z0-9_\-]+)", content):
        referenced_icons.add(m.group(1))

    for m in re.finditer(r"\|miniicon=\$?([a-zA-Z0-9_\-]+)", content):
        referenced_icons.add(m.group(1))

    for m in re.finditer(r"\|(?:gfxresource|resource)=\$?([a-zA-Z0-9_\-]+)", content):
        referenced_gfx.add(m.group(1))

    for m in re.finditer(r"\|(?:basesound|terrainsound|sound)=\$?([a-zA-Z0-9_\-]+)", content):
        referenced_sfx.add(m.group(1))

    missing_defs = {
        "icons": [icon for icon in sorted(referenced_icons) if icon not in known_assets],
        "gfx": [gfx for gfx in sorted(referenced_gfx) if gfx not in known_assets],
        "sfx": [sfx for sfx in sorted(referenced_sfx) if sfx not in known_assets],
    }
    return missing_defs


def run_checks(manifest_path, asset_dir, layer_search_dirs, check_sizes=False, check_defs=False):
    resources = parse_manifest(manifest_path)

    results = {
        "manifest_path": os.path.abspath(manifest_path),
        "total_entries": len(resources),
        "present": [],
        "missing": [],
        "layer_bins": [],
        "size_mismatches": [],
        "categories": {},
        "definition_cross_ref": None,
    }

    known_asset_names = set(resources.keys())

    for name, item in resources.items():
        if not isinstance(item, dict):
            continue

        raw_path = item.get("path", "")
        rel_path = raw_path[1:] if raw_path.startswith("$") else raw_path
        version = item.get("version", "").lstrip("n")
        manifest_size = parse_manifest_size(item.get("size"))
        hash_val = item.get("hash", "").lstrip("i")

        # Determine category from path
        parts = rel_path.split("/")
        cat = parts[1] if len(parts) > 1 else (parts[0] if parts else "other")
        results["categories"].setdefault(cat, {"total": 0, "present": 0, "missing": 0})
        results["categories"][cat]["total"] += 1

        direct_path = os.path.join(asset_dir, rel_path)

        if os.path.isfile(direct_path):
            actual_size = os.path.getsize(direct_path)
            status = "present"
            size_ok = True
            crlf_match = False

            if check_sizes and manifest_size is not None:
                if actual_size != manifest_size:
                    # Check if CRLF line ending difference
                    if direct_path.endswith((".txt", ".json", ".dat", ".xml")):
                        try:
                            with open(direct_path, "rb") as tf:
                                raw_bytes = tf.read()
                            crlf_size = len(
                                raw_bytes.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
                            )
                            if crlf_size == manifest_size:
                                crlf_match = True
                            else:
                                size_ok = False
                        except Exception:
                            size_ok = False
                    else:
                        size_ok = False

            item_info = {
                "name": name,
                "path": rel_path,
                "disk_path": direct_path,
                "category": cat,
                "version": version,
                "actual_size": actual_size,
                "manifest_size": manifest_size,
                "crlf_match": crlf_match,
                "hash": hash_val,
            }

            results["present"].append(item_info)
            results["categories"][cat]["present"] += 1

            if not size_ok:
                results["size_mismatches"].append(item_info)

        elif "layers/" in rel_path:
            # Check if raw .bin source exists for on-the-fly streaming
            bin_path, bin_name = find_layer_bin(rel_path, layer_search_dirs)
            if bin_path:
                item_info = {
                    "name": name,
                    "path": rel_path,
                    "bin_source": bin_path,
                    "bin_name": bin_name,
                    "category": cat,
                    "version": version,
                }
                results["layer_bins"].append(item_info)
                results["categories"][cat]["present"] += 1
            else:
                item_info = {
                    "name": name,
                    "path": rel_path,
                    "category": cat,
                    "version": version,
                    "expected_bin": bin_name,
                    "reason": f"Neither static .dat nor raw layer bin ({bin_name}) found",
                }
                results["missing"].append(item_info)
                results["categories"][cat]["missing"] += 1
        else:
            item_info = {
                "name": name,
                "path": rel_path,
                "disk_path": direct_path,
                "category": cat,
                "version": version,
                "manifest_size": manifest_size,
                "reason": "File not found on disk",
            }
            results["missing"].append(item_info)
            results["categories"][cat]["missing"] += 1

    if check_defs:
        # Look for the definitionProperties file referenced in manifest or latest in folder
        def_file = os.path.join(asset_dir, "lang0000/definitionProperties/00000031.txt")
        if not os.path.isfile(def_file):
            # Find highest numbered file
            dp_dir = os.path.join(asset_dir, "lang0000/definitionProperties")
            if os.path.isdir(dp_dir):
                files = sorted(os.listdir(dp_dir))
                if files:
                    def_file = os.path.join(dp_dir, files[-1])

        results["definition_cross_ref"] = check_definitions_cross_reference(
            def_file, known_asset_names, asset_dir
        )

    return results


def print_report(results, colors, missing_only=False, check_sizes=False, check_defs=False):
    c = colors
    print(f"\n{c.bold('=== OpenPerpetuum Asset Integrity & Presence Report ===')}\n")
    print(f"Manifest: {c.cyan(results['manifest_path'])}")
    print(f"Total entries in manifest: {c.bold(str(results['total_entries']))}\n")

    # Category Summary Table
    print(f"{'Category':<24} | {'Total':<8} | {'Present / Bin':<15} | {'Missing':<8}")
    print("-" * 62)
    for cat, stats in sorted(results["categories"].items()):
        total = stats["total"]
        pres = stats["present"]
        miss = stats["missing"]
        miss_str = c.red(str(miss)) if miss > 0 else c.green("0")
        pres_str = c.green(str(pres))
        print(f"{cat:<24} | {total:<8} | {pres_str:<24} | {miss_str:<8}")
    print("-" * 62)

    total_pres = len(results["present"]) + len(results["layer_bins"])
    total_miss = len(results["missing"])
    print(
        f"{c.bold('SUMMARY')}: {c.green(f'{total_pres} available')} "
        f"({len(results['present'])} direct files, {len(results['layer_bins'])} layer bin sources), "
        f"{c.red(f'{total_miss} missing') if total_miss > 0 else c.green('0 missing')}"
    )

    if check_sizes:
        mismatches = len(results["size_mismatches"])
        crlf_matches = sum(1 for m in results["present"] if m.get("crlf_match"))
        if mismatches > 0:
            print(f"Size validation: {c.yellow(f'{mismatches} mismatches detected')}")
        else:
            print(f"Size validation: {c.green('All checked file sizes match manifest perfectly')}")
        if crlf_matches > 0:
            print(
                f"  {c.gray(f'({crlf_matches} text files matched after CRLF/LF line-ending normalization)')}"
            )

    # Missing items breakdown
    if results["missing"]:
        print(f"\n{c.bold(c.red('Missing Assets:'))}")
        by_cat = {}
        for item in results["missing"]:
            by_cat.setdefault(item["category"], []).append(item)

        for cat, items in sorted(by_cat.items()):
            print(f"\n  {c.bold(c.yellow(f'[{cat.upper()}] ({len(items)} missing)'))}")
            for item in items[:15]:
                name = item["name"]
                path = item["path"]
                reason = item.get("reason", "")
                print(f"    - {c.bold(name)} ({c.gray(path)}) -> {reason}")
            if len(items) > 15:
                print(f"      {c.gray(f'... and {len(items) - 15} more {cat} entries')}")

    # Size mismatches breakdown
    if check_sizes and results["size_mismatches"]:
        print(f"\n{c.bold(c.yellow('Size Mismatches:'))}")
        for item in results["size_mismatches"][:15]:
            print(
                f"    - {item['name']} ({item['path']}): "
                f"actual {item['actual_size']} bytes vs manifest {item['manifest_size']} bytes"
            )

    # Definition cross reference report
    if check_defs and results.get("definition_cross_ref"):
        dx = results["definition_cross_ref"]
        missing_dx_count = len(dx["icons"]) + len(dx["gfx"]) + len(dx["sfx"])
        print(f"\n{c.bold('Definition Properties Cross-Check:')}")
        if missing_dx_count == 0:
            print(f"  {c.green('All referenced icons, gfx models, and sfx are declared in manifest.')}")
        else:
            if dx["icons"]:
                count = len(dx['icons'])
                sample = ', '.join(dx['icons'][:10])
                print(
                    f"  - {c.yellow(f'Referenced icons missing in manifest ({count}):')} {sample}..."
                )
            if dx["gfx"]:
                count = len(dx['gfx'])
                sample = ', '.join(dx['gfx'][:10])
                print(
                    f"  - {c.yellow(f'Referenced gfx missing in manifest ({count}):')} {sample}..."
                )
            if dx["sfx"]:
                count = len(dx['sfx'])
                sample = ', '.join(dx['sfx'][:10])
                print(
                    f"  - {c.yellow(f'Referenced sfx missing in manifest ({count}):')} {sample}..."
                )

    # Remediation recommendations
    if total_miss > 0:
        print(f"\n{c.bold(c.cyan('Remediation Advice:'))}")
        has_missing_layers = any(item["category"] == "layers" for item in results["missing"])
        has_missing_icons = any(item["category"] == "icons" for item in results["missing"])
        has_missing_gfx_sfx = any(item["category"] in ["gfx", "sfx", "textures"] for item in results["missing"])

        if has_missing_layers:
            print(
                f"  * {c.bold('Gamma Layers Missing')}: Download the latest gamma island layers zip and unpack `.bin` files into:"
            )
            print("      - `asset/lang0000/layers/GAMMA_LAYERS_NEW/`")
            print("      - `custom-layers/`")
        if has_missing_icons or has_missing_gfx_sfx:
            print(
                f"  * {c.bold('Asset Resource Pack Missing')}: Download the asset resource pack and extract `gfx`, `sfx`, `textures`, and `icons` into:"
            )
            print("      - `asset/lang0000/`")
    else:
        print(f"\n{c.bold(c.green('All assets listed in manifest are valid and present on disk!'))}")

    print()


def main():
    parser = argparse.ArgumentParser(
        description="Verify asset presence and integrity against Perpetuum resource_0.dat manifest."
    )
    parser.add_argument(
        "--manifest",
        default="asset/resource_0.dat",
        help="Path to manifest file (default: asset/resource_0.dat)",
    )
    parser.add_argument(
        "--asset-dir",
        default="asset",
        help="Path to asset root directory (default: asset)",
    )
    parser.add_argument(
        "--layers-dir",
        action="append",
        default=[],
        help="Additional search directories for raw layer .bin files (can be specified multiple times)",
    )
    parser.add_argument(
        "--check-sizes",
        action="store_true",
        help="Validate actual file sizes against sizes recorded in manifest",
    )
    parser.add_argument(
        "--check-definitions",
        action="store_true",
        help="Cross-reference definitionProperties against asset list",
    )
    parser.add_argument(
        "--missing-only",
        action="store_true",
        help="Only display missing or mismatched assets",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Output results as JSON",
    )
    parser.add_argument(
        "--no-color",
        action="store_true",
        help="Disable ANSI color output",
    )

    args = parser.parse_args()

    colors = Colors(enabled=not args.no_color)

    # Build layer search directories
    search_dirs = [
        os.path.join(args.asset_dir, "lang0000/layers/GAMMA_LAYERS_NEW"),
        "custom-layers",
        "perpetuum-data/layers",
        "src/Perpetuum.ServerService2/data/layers",
    ] + args.layers_dir

    try:
        results = run_checks(
            manifest_path=args.manifest,
            asset_dir=args.asset_dir,
            layer_search_dirs=search_dirs,
            check_sizes=args.check_sizes,
            check_defs=args.check_definitions,
        )
    except Exception as e:
        print(colors.red(f"Error: {e}"), file=sys.stderr)
        sys.exit(1)

    if args.json:
        print(json.dumps(results, indent=2))
    else:
        print_report(
            results,
            colors=colors,
            missing_only=args.missing_only,
            check_sizes=args.check_sizes,
            check_defs=args.check_definitions,
        )

    if results["missing"]:
        sys.exit(2)
    sys.exit(0)


if __name__ == "__main__":
    main()

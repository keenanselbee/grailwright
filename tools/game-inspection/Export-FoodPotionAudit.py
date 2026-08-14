#!/usr/bin/env python3
"""Export Tainted Grail food and potion text/effect metadata from game assets."""

from __future__ import annotations

import argparse
import base64
import csv
import json
import re
import struct
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

import UnityPy


UNITY_VERSION = "6000.0.64f1"
ITEM_BUNDLE = "templates.items_assets_all.bundle"
LANGUAGE_ARCHIVE = Path("Fall of Avalon_Data/StreamingAssets/Languages/languages.arch")
ADDRESSABLE_ROOT = Path("Fall of Avalon_Data/StreamingAssets/aa")
GUID_PATTERN = re.compile(r"^[0-9a-f]{32}$", re.IGNORECASE)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    return parser.parse_args()


def int32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<i", data, offset)[0]


def catalog_object(data: bytes, offset: int) -> Any:
    object_type = data[offset]
    offset += 1
    if object_type in (0, 1):
        byte_count = int32(data, offset)
        offset += 4
        encoding = "ascii" if object_type == 0 else "utf-16-le"
        return data[offset : offset + byte_count].decode(encoding)
    if object_type == 2:
        return struct.unpack_from("<H", data, offset)[0]
    if object_type == 3:
        return struct.unpack_from("<I", data, offset)[0]
    if object_type == 4:
        return int32(data, offset)
    if object_type == 5:
        byte_count = data[offset]
        return data[offset + 1 : offset + 1 + byte_count].decode("ascii")
    return None


def read_catalog(catalog_path: Path) -> tuple[dict[str, str], dict[str, list[str]]]:
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    bucket_data = base64.b64decode(catalog["m_BucketDataString"])
    key_data = base64.b64decode(catalog["m_KeyDataString"])
    entry_data = base64.b64decode(catalog["m_EntryDataString"])

    bucket_count = int32(bucket_data, 0)
    bucket_offset = 4
    buckets: list[tuple[int, list[int]]] = []
    for _ in range(bucket_count):
        data_offset = int32(bucket_data, bucket_offset)
        entry_count = int32(bucket_data, bucket_offset + 4)
        bucket_offset += 8
        entries = [int32(bucket_data, bucket_offset + index * 4) for index in range(entry_count)]
        bucket_offset += entry_count * 4
        buckets.append((data_offset, entries))

    keys = [catalog_object(key_data, data_offset) for data_offset, _ in buckets]
    location_count = int32(entry_data, 0)
    internal_ids = catalog["m_InternalIds"]
    locations: list[str] = []
    for index in range(location_count):
        entry_offset = 4 + index * 28
        internal_id_index = int32(entry_data, entry_offset)
        locations.append(internal_ids[internal_id_index])

    key_locations: dict[str, list[str]] = defaultdict(list)
    for key, (_, location_indexes) in zip(keys, buckets):
        if not isinstance(key, str):
            continue
        for location_index in location_indexes:
            internal_id = locations[location_index]
            if internal_id not in key_locations[key]:
                key_locations[key].append(internal_id)

    guid_to_asset: dict[str, str] = {}
    for key, internal_id_list in key_locations.items():
        if not GUID_PATTERN.fullmatch(key):
            continue
        asset_paths = [value for value in internal_id_list if value.startswith("Assets/")]
        if asset_paths:
            guid_to_asset[key.lower()] = asset_paths[0]
    return guid_to_asset, dict(key_locations)


def raw_bundle_file(bundle: Any, name: str) -> bytes:
    value = bundle.files[name]
    if hasattr(value, "bytes"):
        return value.bytes
    reader = getattr(value, "reader", None)
    if reader is not None and hasattr(reader, "bytes"):
        return reader.bytes
    raise TypeError(f"Unsupported archive member {name}: {type(value).__name__}")


def decode_indexed_strings(data: bytes, positions: bytes) -> list[str]:
    if len(positions) % 8:
        raise ValueError(f"Position blob length {len(positions)} is not divisible by 8")
    text = data.decode("utf-16-le")
    output: list[str] = []
    for offset in range(0, len(positions), 8):
        char_start, char_length = struct.unpack_from("<II", positions, offset)
        output.append(text[char_start : char_start + char_length])
    return output


def read_english_localization(language_archive: Path) -> dict[str, str]:
    environment = UnityPy.load(str(language_archive))
    bundle = next(iter(environment.files.values()))
    keys = decode_indexed_strings(
        raw_bundle_file(bundle, "keys_data.blob"),
        raw_bundle_file(bundle, "keys_positions.blob"),
    )
    values = decode_indexed_strings(
        raw_bundle_file(bundle, "en/strings.blob"),
        raw_bundle_file(bundle, "en/positions.blob"),
    )
    if len(keys) != len(values):
        raise ValueError(f"Localization key/value count mismatch: {len(keys)} != {len(values)}")
    return dict(zip(keys, values))


def enum_name(value: Any) -> str:
    if not isinstance(value, dict):
        return ""
    enum_ref = value.get("_enumRef", "")
    return enum_ref.rsplit(":", 1)[-1] if ":" in enum_ref else enum_ref


def localized_field(value: Any, localization: dict[str, str]) -> dict[str, str]:
    if not isinstance(value, dict):
        return {"key": "", "english": "", "id_override": "", "generated_id": ""}
    loc_string = value.get("locString", value)
    if not isinstance(loc_string, dict):
        return {"key": "", "english": "", "id_override": "", "generated_id": ""}
    id_override = loc_string.get("IdOverride", "") or ""
    generated_id = loc_string.get("ID", "") or ""
    key = id_override or generated_id
    return {
        "key": key,
        "english": localization.get(key, "") if key else "",
        "id_override": id_override,
        "generated_id": generated_id,
    }


def component_trees(game_object: Any) -> list[dict[str, Any]]:
    output: list[dict[str, Any]] = []
    for component_reference in game_object.m_Component:
        pointer = getattr(component_reference, "component", component_reference)
        object_reader = pointer.assetsfile.objects.get(pointer.path_id)
        if object_reader is None or object_reader.type.name != "MonoBehaviour":
            continue
        try:
            tree = object_reader.read_typetree()
        except Exception:
            continue
        if isinstance(tree, dict):
            output.append(tree)
    return output


def parse_item_prefabs(item_bundle: Path) -> tuple[dict[str, dict[str, Any]], list[dict[str, str]]]:
    environment = UnityPy.load(str(item_bundle))
    prefabs: dict[str, dict[str, Any]] = {}
    failures: list[dict[str, str]] = []
    for asset_path, object_reader in environment.container.items():
        if object_reader.type.name != "GameObject" or not asset_path.lower().endswith(".prefab"):
            continue
        try:
            game_object = object_reader.read()
            trees = component_trees(game_object)
            metadata = next((tree for tree in trees if "itemName" in tree and "tags" in tree), None)
            if metadata is None:
                continue
            action_trees = [tree for tree in trees if "actionType" in tree and "skills" in tree]
            prefabs[asset_path] = {
                "template_name": game_object.m_Name,
                "metadata": metadata,
                "actions": action_trees,
            }
        except Exception as exc:
            failures.append({"asset_path": asset_path, "error": repr(exc)})
    return prefabs, failures


def abstract_ancestors(
    asset_path: str,
    prefabs: dict[str, dict[str, Any]],
    guid_to_asset: dict[str, str],
) -> list[str]:
    found: list[str] = []
    pending = [asset_path]
    visited = {asset_path}
    while pending:
        current_path = pending.pop()
        current = prefabs.get(current_path)
        if current is None:
            continue
        for reference in current["metadata"].get("_abstractTypes", []):
            guid = reference.get("_guid", "").lower() if isinstance(reference, dict) else ""
            parent_path = guid_to_asset.get(guid, "")
            if not parent_path or parent_path in visited:
                continue
            visited.add(parent_path)
            found.append(parent_path)
            pending.append(parent_path)
    return found


def skill_data(skill: dict[str, Any], guid_to_asset: dict[str, str]) -> dict[str, Any]:
    graph_ref = skill.get("skillGraphRef", {})
    guid = graph_ref.get("_guid", "").lower() if isinstance(graph_ref, dict) else ""
    variables = {
        variable.get("name", ""): variable.get("value")
        for variable in skill.get("variables", [])
        if isinstance(variable, dict) and variable.get("name")
    }
    enums = {
        enum.get("name", ""): enum_name(enum.get("enumReference"))
        for enum in skill.get("enums", [])
        if isinstance(enum, dict) and enum.get("name")
    }
    add_value = variables.get("AddValue")
    duration = variables.get("Duration")
    total = add_value * duration if isinstance(add_value, (int, float)) and isinstance(duration, (int, float)) else None
    return {
        "guid": guid,
        "asset_path": guid_to_asset.get(guid, ""),
        "variables": variables,
        "enums": enums,
        "add_value_times_duration": total,
    }


def build_inventory(
    prefabs: dict[str, dict[str, Any]],
    guid_to_asset: dict[str, str],
    localization: dict[str, str],
) -> list[dict[str, Any]]:
    inventory: list[dict[str, Any]] = []
    for asset_path, prefab in prefabs.items():
        metadata = prefab["metadata"]
        if metadata.get("_isAbstract"):
            continue
        actions = prefab["actions"]
        action_types = [enum_name(action.get("actionType")) for action in actions]
        ancestors = abstract_ancestors(asset_path, prefabs, guid_to_asset)
        is_food = "Eat" in action_types
        is_potion = any(
            Path(parent).stem.startswith("Abstract_ItemTemplate_Potion")
            for parent in ancestors
        )
        if not is_food and not is_potion:
            continue

        all_skills = [
            skill_data(skill, guid_to_asset)
            for action in actions
            for skill in action.get("skills", [])
            if isinstance(skill, dict)
        ]
        lowered_path = asset_path.lower()
        availability = "not-in-use" if "/notinuse/" in lowered_path or "/zzz" in lowered_path else "normal"
        inventory.append(
            {
                "category": "potion" if is_potion else "food",
                "availability_hint": availability,
                "asset_path": asset_path,
                "template_name": prefab["template_name"],
                "tags": metadata.get("tags", []),
                "abstract_types": ancestors,
                "action_types": action_types,
                "name": localized_field(metadata.get("itemName"), localization),
                "flavor": localized_field(metadata.get("flavor"), localization),
                "description": localized_field(metadata.get("description"), localization),
                "skills": all_skills,
            }
        )
    return sorted(inventory, key=lambda item: (item["category"], item["template_name"].lower()))


def join_values(values: list[Any]) -> str:
    return " | ".join(str(value) for value in values if value not in (None, ""))


def write_inventory_csv(path: Path, inventory: list[dict[str, Any]]) -> None:
    columns = [
        "Category",
        "Availability",
        "TemplateName",
        "DisplayNameKey",
        "DisplayNameEnglish",
        "FlavorKey",
        "FlavorEnglish",
        "DescriptionKey",
        "DescriptionEnglish",
        "Tags",
        "AbstractTypes",
        "ActionTypes",
        "SkillGraphGuids",
        "SkillGraphPaths",
        "AddValue",
        "Duration",
        "AddValueTimesDuration",
        "StatEnums",
        "AssetPath",
    ]
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns)
        writer.writeheader()
        for item in inventory:
            skills = item["skills"]
            writer.writerow(
                {
                    "Category": item["category"],
                    "Availability": item["availability_hint"],
                    "TemplateName": item["template_name"],
                    "DisplayNameKey": item["name"]["key"],
                    "DisplayNameEnglish": item["name"]["english"],
                    "FlavorKey": item["flavor"]["key"],
                    "FlavorEnglish": item["flavor"]["english"],
                    "DescriptionKey": item["description"]["key"],
                    "DescriptionEnglish": item["description"]["english"],
                    "Tags": join_values(item["tags"]),
                    "AbstractTypes": join_values(item["abstract_types"]),
                    "ActionTypes": join_values(item["action_types"]),
                    "SkillGraphGuids": join_values([skill["guid"] for skill in skills]),
                    "SkillGraphPaths": join_values([skill["asset_path"] for skill in skills]),
                    "AddValue": join_values([skill["variables"].get("AddValue") for skill in skills]),
                    "Duration": join_values([skill["variables"].get("Duration") for skill in skills]),
                    "AddValueTimesDuration": join_values([skill["add_value_times_duration"] for skill in skills]),
                    "StatEnums": join_values([join_values(list(skill["enums"].values())) for skill in skills]),
                    "AssetPath": item["asset_path"],
                }
            )


def write_unique_strings(path: Path, inventory: list[dict[str, Any]]) -> None:
    usage: dict[tuple[str, str, str], dict[str, set[str]]] = defaultdict(
        lambda: {"categories": set(), "templates": set()}
    )
    for item in inventory:
        for field in ("name", "flavor", "description"):
            value = item[field]
            if not value["key"] and not value["english"]:
                continue
            record = usage[(field, value["key"], value["english"])]
            record["categories"].add(item["category"])
            record["templates"].add(item["template_name"])

    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(["Field", "LocalizationKey", "English", "TemplateCount", "Categories", "Templates"])
        for (field, key, english), record in sorted(usage.items()):
            templates = sorted(record["templates"])
            writer.writerow(
                [
                    field,
                    key,
                    english,
                    len(templates),
                    join_values(sorted(record["categories"])),
                    join_values(templates),
                ]
            )


def unique_string_count(inventory: list[dict[str, Any]]) -> int:
    return len({
        (field, item[field]["key"], item[field]["english"])
        for item in inventory
        for field in ("name", "flavor", "description")
        if item[field]["key"] or item[field]["english"]
    })


def main() -> None:
    args = parse_args()
    game_root = args.game_root.resolve()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    UnityPy.config.FALLBACK_UNITY_VERSION = UNITY_VERSION
    addressable_root = game_root / ADDRESSABLE_ROOT
    catalog_path = addressable_root / "catalog.json"
    item_bundle = addressable_root / "StandaloneWindows64" / ITEM_BUNDLE
    language_archive = game_root / LANGUAGE_ARCHIVE

    guid_to_asset, key_locations = read_catalog(catalog_path)
    localization = read_english_localization(language_archive)
    prefabs, failures = parse_item_prefabs(item_bundle)
    inventory = build_inventory(prefabs, guid_to_asset, localization)
    skill_references = [skill for item in inventory for skill in item["skills"]]

    (output_dir / "food-potion-template-audit.json").write_text(
        json.dumps(
            {
                "sources": {
                    "catalog": str(ADDRESSABLE_ROOT / "catalog.json"),
                    "catalog_build_hash": json.loads(catalog_path.read_text(encoding="utf-8"))["m_BuildResultHash"],
                    "item_bundle": str(ADDRESSABLE_ROOT / "StandaloneWindows64" / ITEM_BUNDLE),
                    "language_archive": str(LANGUAGE_ARCHIVE),
                    "unity_version": UNITY_VERSION,
                },
                "counts": {
                    "localization_entries": len(localization),
                    "catalog_keys": len(key_locations),
                    "item_prefabs": len(prefabs),
                    "food_and_potion_templates": len(inventory),
                    "unique_localized_strings": unique_string_count(inventory),
                    "skill_references": len(skill_references),
                    "unmapped_skill_references": sum(
                        bool(skill["guid"]) and not skill["asset_path"] for skill in skill_references
                    ),
                    "missing_english_names": sum(
                        bool(item["name"]["key"]) and not item["name"]["english"] for item in inventory
                    ),
                    "missing_english_descriptions": sum(
                        bool(item["description"]["key"]) and not item["description"]["english"]
                        for item in inventory
                    ),
                    "categories": Counter(item["category"] for item in inventory),
                    "availability": Counter(item["availability_hint"] for item in inventory),
                    "prefab_parse_failures": len(failures),
                },
                "prefab_parse_failures": failures,
                "items": inventory,
            },
            indent=2,
            ensure_ascii=False,
        )
        + "\n",
        encoding="utf-8",
    )
    write_inventory_csv(output_dir / "food-potion-template-audit.csv", inventory)
    write_unique_strings(output_dir / "food-potion-tooltip-strings.csv", inventory)

    print(json.dumps({
        "output_dir": str(output_dir),
        "items": len(inventory),
        "categories": Counter(item["category"] for item in inventory),
        "availability": Counter(item["availability_hint"] for item in inventory),
        "localization_entries": len(localization),
        "prefab_parse_failures": len(failures),
    }, indent=2))


if __name__ == "__main__":
    main()

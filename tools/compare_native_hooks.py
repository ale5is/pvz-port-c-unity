import argparse
import json
from pathlib import Path


def load_catalog(path):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    hooks = data["hooks"]
    result = {}
    for hook in hooks:
        name = hook["name"]
        if name in result:
            raise ValueError(f"Duplicate hook: {name}")
        result[name] = hook
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("baseline")
    parser.add_argument("target")
    args = parser.parse_args()

    baseline = load_catalog(args.baseline)
    target = load_catalog(args.target)

    old_names = set(baseline)
    new_names = set(target)
    added = sorted(new_names - old_names)
    removed = sorted(old_names - new_names)
    shared = sorted(old_names & new_names)
    changed = [
        name for name in shared
        if baseline[name].get("version") != target[name].get("version")
        or baseline[name].get("target") != target[name].get("target")
    ]

    print(f"Baseline hooks: {len(baseline)}")
    print(f"Target hooks: {len(target)}")
    print(f"Shared hooks: {len(shared)}")
    print(f"Added hooks: {len(added)}")
    print(f"Removed hooks: {len(removed)}")
    print(f"Changed version/target: {len(changed)}")

    if added:
        print("\nADDED")
        print("\n".join(added))
    if removed:
        print("\nREMOVED")
        print("\n".join(removed))
    if changed:
        print("\nCHANGED")
        for name in changed:
            print(f"{name}: {baseline[name]} -> {target[name]}")

    if removed or changed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()

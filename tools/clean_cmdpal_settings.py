import json
import os
import sys

path = os.path.join(
    os.environ["LOCALAPPDATA"],
    r"Packages\Microsoft.CommandPalette_8wekyb3d8bbwe\LocalState\settings.json",
)

with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)

prefixes = (
    "Goucey.EverythingExtensionforCommandPalette",
    "VictorLin.EverythingCP",
)

removed = []

def walk(node):
    if isinstance(node, dict):
        for key in list(node.keys()):
            if any(key.startswith(p) for p in prefixes):
                removed.append(key)
                del node[key]
            else:
                walk(node[key])
    elif isinstance(node, list):
        for item in node:
            walk(item)

walk(data)

with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)

print("removed:", len(removed))
for key in removed:
    print(" -", key)

with open(path, "r", encoding="utf-8") as f:
    check = f.read()
print("everything mentions left:", check.count("Everything"))

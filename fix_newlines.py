import os


def fix_file(path):
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    # Only fix if we see literal \n
    if "\\n" in content:
        new_content = content.replace("\\n", "\n")
        with open(path, "w", encoding="utf-8") as f:
            f.write(new_content)
        print(f"Fixed {path}")


for root, _, files in os.walk("frontend/src"):
    for file in files:
        if file.endswith(".ts") or file.endswith(".tsx"):
            fix_file(os.path.join(root, file))

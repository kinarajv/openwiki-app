import os
import re


def add_type_to_imports(filepath):
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    # Look for imports from '../types' or '../../types'
    # e.g. import { RepoSearchResult } from '../types';
    # Replace with: import type { RepoSearchResult } from '../types';

    new_content = re.sub(
        r'import\s+\{([^}]+)\}\s+from\s+[\'"](.*types.*)[\'"];',
        r'import type { \1 } from "\2";',
        content,
    )

    # Also remove unused variables mentioned in TS errors:
    # src/hooks/useApi.ts: Diagram, Relation, RepoMetadata
    if "useApi.ts" in filepath:
        new_content = new_content.replace("Diagram, Relation, RepoMetadata, ", "")
        new_content = new_content.replace("Diagram, ", "")
        new_content = new_content.replace("Relation, ", "")
        new_content = new_content.replace("RepoMetadata, ", "")

    if "DocumentationPage.tsx" in filepath:
        new_content = new_content.replace("onSend=", "onSendQuestion=")

    with open(filepath, "w", encoding="utf-8") as f:
        f.write(new_content)


for root, _, files in os.walk("frontend/src"):
    for file in files:
        if file.endswith(".tsx") or file.endswith(".ts"):
            add_type_to_imports(os.path.join(root, file))

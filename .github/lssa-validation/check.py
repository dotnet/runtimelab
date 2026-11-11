"""Require a PHI store in the LSSA section and a matching block-entry dump."""

import re
import sys
from pathlib import Path

lines = Path(sys.argv[1]).read_text(errors="replace").splitlines()
block = ""
definitions = []
first_dump = False
in_lssa = False
matches = 0
failures = 0
for i, line in enumerate(lines):
    if "In Lssa::SpillValuesLiveAcrossSafePoints" in line:
        in_lssa = True
        block, definitions, first_dump = "", [], False
    if not in_lssa:
        continue
    if re.search(r"====\s+BB\d+", line):
        block, definitions, first_dump = line.strip(), [], False
    if not block:
        continue
    store = re.search(r"\bSTORE_LCL_VAR\b.*?\b(V\d+)(?:\s+\S+)*?\s*d:(\d+)\b", line)
    if store and i + 1 < len(lines) and re.search(r"\bPHI\b", lines[i + 1]):
        if first_dump:
            print(f"{block}: block-entry dump appeared before PHI at line {i + 1}")
            failures += 1
        definitions.append((f"{store[1]}/{store[2]}", i + 1, line.strip()))
    if "Liveness: {" in line and not first_dump:
        first_dump = True
        entries = set(re.findall(r"\bV\d+/\d+\b", line))
        for expected, n, store_line in definitions:
            if expected in entries:
                matches += 1
                print(f"{block}\n  {n}: {store_line}\n  {i + 1}: {line.strip()}")
            else:
                local = expected.split("/")[0]
                if any(entry.startswith(local + "/") for entry in entries):
                    failures += 1
                    print(f"{block}: wrong SSA version for {local}; expected {expected}, got {line.strip()}")

if failures or not matches:
    print(f"No passing verdict: {matches} matching PHI definition(s), {failures} ordering/version failure(s).")
    sys.exit(1)
print(f"PASS: {matches} PHI definition(s) matched the first liveness dump in their block.")

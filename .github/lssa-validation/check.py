"""Match LIR PHI stores to their first block-entry liveness dump."""

import re
import sys
from pathlib import Path

lines = Path(sys.argv[1]).read_text(errors="replace").splitlines()
store_pattern = re.compile(r"\[(\d+)\].*\bSTORE_LCL_VAR\b.*?\b(V\d+).*?\bd:(\d+)\b")
phi_pattern = re.compile(r"\b(t\d+)\s*=\s*\*?\s*PHI\s")
phi_stores = {}
block = None
in_lir = False

# LIR prints operands first. A PHI store consumes the PHI's temporary;
# the tree ID connects that store to the candidate line in the LSSA trace.
for i, line in enumerate(lines):
    if "Trees after Allocate shadow stack slots" in line:
        in_lir = True
    elif in_lir and "Starting PHASE" in line:
        break
    if not in_lir:
        continue
    if header := re.search(r"^\s*-{4,}\s+(BB\d+)\b", line):
        block = header[1]
    if not block or not (phi := phi_pattern.search(line)) or i + 2 >= len(lines):
        continue
    operand = re.search(r"\*\s+(t\d+)\b", lines[i + 1])
    store = store_pattern.search(lines[i + 2])
    if operand and operand[1] == phi[1] and store:
        phi_stores[block, store[1]] = (f"{store[2]}/{store[3]}", line.strip())

matches = 0
failures = 0
active = False
block = None
seen = []
dumped = False
for i, line in enumerate(lines, 1):
    if "In Lssa::SpillValuesLiveAcrossSafePoints" in line:
        active = True
    elif active and "In Lssa::AllocateAndInitializeLocals" in line:
        break
    if not active:
        continue
    if header := re.search(r"====\s+(BB\d+)\b", line):
        if seen and not dumped:
            print(f"{block}: missing block-entry liveness dump")
            failures += 1
        block, seen, dumped = header[1], [], False
    if (store := store_pattern.search(line)) and (phi := phi_stores.get((block, store[1]))):
        if dumped:
            print(f"{block}: block-entry dump appeared before PHI store at line {i}")
            failures += 1
        seen.append((*phi, i, line.strip()))
    if "Liveness: {" in line and not dumped:
        dumped = True
        entries = set(re.findall(r"\bV\d+/\d+\b", line))
        for expected, phi_line, n, store_line in seen:
            if expected in entries:
                matches += 1
                print(f"{block}\n  {phi_line}\n  {n}: {store_line}\n  {i}: {line.strip()}")
            elif any(entry.startswith(expected.split("/")[0] + "/") for entry in entries):
                failures += 1
                print(f"{block}: expected {expected}, got {line.strip()}")

if seen and not dumped:
    print(f"{block}: missing block-entry liveness dump")
    failures += 1
if failures or not matches:
    print(f"No passing verdict: {matches} matching PHI definition(s), {failures} ordering/version failure(s).")
    sys.exit(1)
print(f"PASS: {matches} PHI definition(s) matched the first liveness dump in their block.")

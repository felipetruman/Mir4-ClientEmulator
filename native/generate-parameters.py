"""Generate build inputs from a locally verified profile; do not invent current keys."""
import hashlib
import json
from pathlib import Path
import sys

if len(sys.argv) != 3:
    raise SystemExit("Usage: python native/generate-parameters.py <verified-profile.json> <Mir4G.exe>")
profile_path = Path(sys.argv[1])
profile = json.loads(profile_path.read_text(encoding="utf-8-sig"))
client = Path(sys.argv[2])
if hashlib.sha256(client.read_bytes()).hexdigest() != profile["client_sha256"]:
    raise SystemExit("Profile does not match the installed executable SHA-256.")
parameters = [("sbox", profile["sbox"], 256), ("rcon", profile["rcon"], 16),
              ("current_key_variant0", profile["key_variant0"], 16)]
parts = ["/* Generated from a locally verified client profile. */"]
for name, value, expected in parameters:
    data = bytes.fromhex(value)
    if len(data) != expected:
        raise SystemExit("Invalid parameter length: " + name)
    parts.append("static const uint8_t " + name + "[] = {" + ",".join("0x%02x" % b for b in data) + "};")
flag = int(profile["header_flag"])
if not 0 <= flag <= 255:
    raise SystemExit("Invalid header flag.")
parts.append("static const uint8_t current_header_flag = %d;" % flag)
root = Path(__file__).resolve().parent
(root / "current_parameters.h").write_text("\n".join(parts), encoding="utf-8")
if profile_path.resolve() != (root / "current-profile.json").resolve():
    (root / "current-profile.json").write_text(json.dumps(profile, indent=2), encoding="utf-8")
print("Generated native parameters for verified client " + profile["client_sha256"])

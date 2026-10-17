"""Print request schemas of chosen PixelLab endpoints, refs resolved one level deep."""
import json
import sys

spec = json.load(open(sys.argv[1], encoding="utf-8"))
schemas = spec["components"]["schemas"]


def resolve(s, depth=0):
    if depth > 4:
        return s
    if isinstance(s, dict):
        if "$ref" in s:
            return resolve(schemas[s["$ref"].split("/")[-1]], depth + 1)
        return {k: resolve(v, depth + 1) for k, v in s.items() if k not in ("examples",)}
    if isinstance(s, list):
        return [resolve(v, depth + 1) for v in s]
    return s


for path in sys.argv[2:]:
    op = spec["paths"][path]
    for method, body in op.items():
        print("=" * 20, method.upper(), path)
        print((body.get("description") or body.get("summary") or "")[:600])
        req = body.get("requestBody", {}).get("content", {})
        for ct, c in req.items():
            sch = resolve(c["schema"])
            props = sch.get("properties", {})
            print("required:", sch.get("required"))
            for k, v in props.items():
                v2 = {kk: vv for kk, vv in v.items() if kk in ("type", "enum", "default", "minimum", "maximum", "description", "anyOf", "properties")}
                print(" -", k, json.dumps(v2, ensure_ascii=False)[:400])
        for code, r in body.get("responses", {}).items():
            if code.startswith("2"):
                for ct, c in r.get("content", {}).items():
                    print(" response", code, json.dumps(resolve(c["schema"]), ensure_ascii=False)[:500])

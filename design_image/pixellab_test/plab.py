"""Minimal PixelLab v2 client. Reads the API key from the project's key file; never prints it."""
import base64
import io
import json
import time
import urllib.error
import urllib.request
from pathlib import Path

from PIL import Image

KEY_FILE = Path(r"D:\terraria_mod\sword_master\design_image\pixel_draft\APIKEY.txt")
BASE = "https://api.pixellab.ai/v2"


def _key():
    return KEY_FILE.read_text(encoding="utf-8").strip()


def request(method, path, payload=None, retries=6):
    data = json.dumps(payload).encode() if payload is not None else None
    for attempt in range(retries + 1):
        req = urllib.request.Request(BASE + path, data=data, method=method)
        req.add_header("Authorization", "Bearer " + _key())
        req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=300) as r:
                return json.loads(r.read())
        except urllib.error.HTTPError as e:
            body = e.read().decode(errors="replace")[:800]
            # Free plan allows one job at a time; the previous one may still be settling.
            if e.code == 429 and attempt < retries:
                time.sleep(10)
                continue
            raise RuntimeError(f"{method} {path} -> {e.code}: {body}") from None


def wait_job(job_id, every=4):
    while True:
        j = request("GET", f"/background-jobs/{job_id}")
        if j["status"] != "processing":
            return j
        time.sleep(every)


def b64img(img: Image.Image):
    buf = io.BytesIO(); img.save(buf, "PNG")
    return {"type": "base64", "base64": base64.b64encode(buf.getvalue()).decode(), "format": "png"}


def decode(obj) -> Image.Image:
    return Image.open(io.BytesIO(base64.b64decode(obj["base64"]))).convert("RGBA")


def balance():
    return request("GET", "/balance")

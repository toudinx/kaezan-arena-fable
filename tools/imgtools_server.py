#!/usr/bin/env python3
"""
imgtools_server.py — Kaezan Image Tools
Interface web localhost para upscaling IA e remoção de fundo.

PORTA: 7878

DEPENDÊNCIAS MÍNIMAS (para o servidor rodar):
    pip install fastapi uvicorn python-multipart Pillow numpy

UPSCALING (aba Upscale AI):
    pip install realesrgan basicsr opencv-python-headless

REMOÇÃO DE FUNDO (aba Remove BG):
    pip install rembg onnxruntime            (CPU)
    pip install "rembg[gpu]" onnxruntime-gpu (GPU — muito mais rápido)

USO:
    python tools/imgtools_server.py
    Abra: http://localhost:7878
"""
from __future__ import annotations
import io, sys, threading, traceback, urllib.request
from pathlib import Path

import numpy as np
from PIL import Image

# ── Feature detection ──────────────────────────────────────────────────────
CUDA_OK   = False
CUDA_NAME = "N/A"
UPSCALE_OK  = False
REMOVEBG_OK = False

try:
    import torch
    CUDA_OK = torch.cuda.is_available()
    if CUDA_OK:
        CUDA_NAME = torch.cuda.get_device_name(0)
except ImportError:
    pass

try:
    from basicsr.archs.rrdbnet_arch import RRDBNet
    from realesrgan import RealESRGANer
    import cv2 as _cv2
    UPSCALE_OK = True
except ImportError:
    pass

try:
    from rembg import new_session as _rembg_new_session, remove as _rembg_remove
    REMOVEBG_OK = True
except ImportError:
    pass

# ── Constants ──────────────────────────────────────────────────────────────
PORT        = 7878
WEIGHTS_DIR = Path(__file__).parent / "weights"
MODEL_NAME  = "RealESRGAN_x4plus_anime_6B"
MODEL_URL   = (
    "https://github.com/xinntao/Real-ESRGAN/releases/download/"
    "v0.2.2.4/RealESRGAN_x4plus_anime_6B.pth"
)

# ── Model caching (lazy, thread-safe) ─────────────────────────────────────
_upscaler      = None
_upscaler_tile = None
_upscaler_lock = threading.Lock()
_rembg_sessions: dict = {}
_rembg_lock = threading.Lock()

def _ensure_weights() -> Path:
    WEIGHTS_DIR.mkdir(parents=True, exist_ok=True)
    path = WEIGHTS_DIR / f"{MODEL_NAME}.pth"
    if not path.exists():
        print(f"[upscale] Baixando {MODEL_NAME}.pth (~17 MB)…")
        urllib.request.urlretrieve(MODEL_URL, path)
        print("[upscale] Download concluído.")
    return path

def _get_upscaler(tile: int):
    global _upscaler, _upscaler_tile
    if _upscaler is not None and _upscaler_tile == tile:
        return _upscaler
    with _upscaler_lock:
        if _upscaler is not None and _upscaler_tile == tile:
            return _upscaler
        print("[upscale] Carregando modelo…")
        weights = _ensure_weights()
        model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64,
                        num_block=6, num_grow_ch=32, scale=4)
        _upscaler = RealESRGANer(
            scale=4, model_path=str(weights), model=model,
            tile=tile, tile_pad=10, pre_pad=0,
            half=CUDA_OK, device="cuda" if CUDA_OK else "cpu",
        )
        _upscaler_tile = tile
        print("[upscale] Modelo pronto.")
        return _upscaler

def _get_rembg(model: str):
    with _rembg_lock:
        if model not in _rembg_sessions:
            print(f"[rembg] Carregando modelo '{model}'…")
            _rembg_sessions[model] = _rembg_new_session(model)
            print(f"[rembg] '{model}' pronto.")
        return _rembg_sessions[model]

# ── Processing ─────────────────────────────────────────────────────────────
def _do_upscale(data: bytes, scale: int, tile: int) -> bytes:
    import cv2
    up = _get_upscaler(tile)
    arr = np.frombuffer(data, dtype=np.uint8)
    img = cv2.imdecode(arr, cv2.IMREAD_UNCHANGED)
    if img is None:
        raise ValueError("Não foi possível decodificar a imagem.")
    has_alpha = img.ndim == 3 and img.shape[2] == 4
    if has_alpha:
        alpha = img[:, :, 3]
        img   = img[:, :, :3]
    out, _ = up.enhance(img, outscale=scale)
    if has_alpha:
        h, w = out.shape[:2]
        alpha_up = cv2.resize(alpha, (w, h), interpolation=cv2.INTER_LANCZOS4)
        out = cv2.merge([*cv2.split(out), alpha_up])
    ok, buf = cv2.imencode(".png", out)
    if not ok:
        raise RuntimeError("cv2.imencode falhou.")
    return buf.tobytes()

def _do_removebg(data: bytes, model: str) -> bytes:
    return _rembg_remove(data, session=_get_rembg(model))

# ── FastAPI ────────────────────────────────────────────────────────────────
try:
    from fastapi import FastAPI, File, UploadFile, Form, HTTPException
    from fastapi.responses import Response, HTMLResponse
    from fastapi.middleware.cors import CORSMiddleware
    import uvicorn
except ImportError:
    print("ERRO: fastapi/uvicorn não instalados.")
    print("  pip install fastapi uvicorn python-multipart")
    sys.exit(1)

app = FastAPI(title="Kaezan Image Tools")
app.add_middleware(CORSMiddleware, allow_origins=["*"],
                   allow_methods=["*"], allow_headers=["*"])

@app.get("/", response_class=HTMLResponse)
async def index():
    return _HTML

@app.get("/api/status")
async def api_status():
    return {
        "upscale":   UPSCALE_OK,
        "removebg":  REMOVEBG_OK,
        "cuda":      CUDA_OK,
        "cuda_name": CUDA_NAME,
    }

@app.post("/api/upscale")
async def api_upscale(
    file:  UploadFile = File(...),
    scale: int        = Form(2),
    tile:  int        = Form(512),
):
    if not UPSCALE_OK:
        raise HTTPException(503, "realesrgan não está instalado.")
    data = await file.read()
    try:
        result = _do_upscale(data, scale=scale, tile=tile)
        return Response(content=result, media_type="image/png",
                        headers={"X-Filename": file.filename or "result.png"})
    except Exception as e:
        traceback.print_exc()
        raise HTTPException(500, str(e))

@app.post("/api/removebg")
async def api_removebg(
    file:  UploadFile = File(...),
    model: str        = Form("isnet-anime"),
):
    if not REMOVEBG_OK:
        raise HTTPException(503, "rembg não está instalado.")
    data = await file.read()
    try:
        result = _do_removebg(data, model=model)
        return Response(content=result, media_type="image/png",
                        headers={"X-Filename": file.filename or "result.png"})
    except Exception as e:
        traceback.print_exc()
        raise HTTPException(500, str(e))

# ── HTML ───────────────────────────────────────────────────────────────────
_HTML = r"""<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Kaezan Image Tools</title>
<style>
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
:root {
  --bg:       #0c0c11;
  --surface:  #131318;
  --card:     #1a1a22;
  --border:   #26263a;
  --accent:   #8b5cf6;
  --accent2:  #ec4899;
  --txt:      #ddddf0;
  --muted:    #6464848;
  --muted:    #646484;
  --ok:       #22c55e;
  --err:      #ef4444;
  --warn:     #f59e0b;
  --radius:   10px;
  font-family: Inter, system-ui, sans-serif;
}
body { background: var(--bg); color: var(--txt); min-height: 100vh; }

/* ── Header ── */
header {
  display: flex; align-items: center; gap: 14px;
  padding: 16px 24px; border-bottom: 1px solid var(--border);
  background: var(--surface);
}
.logo {
  font-size: 15px; font-weight: 700; letter-spacing: .03em;
  background: linear-gradient(90deg, var(--accent), var(--accent2));
  -webkit-background-clip: text; -webkit-text-fill-color: transparent;
}
.status-bar {
  display: flex; gap: 16px; margin-left: auto; font-size: 12px; color: var(--muted);
}
.status-item { display: flex; align-items: center; gap: 5px; }
.dot { width: 7px; height: 7px; border-radius: 50%; flex-shrink: 0; }
.dot-ok   { background: var(--ok); box-shadow: 0 0 6px var(--ok); }
.dot-err  { background: var(--err); }
.dot-warn { background: var(--warn); }

/* ── Layout ── */
main { max-width: 860px; margin: 0 auto; padding: 28px 20px; }

/* ── Tabs ── */
.tabs { display: flex; gap: 2px; margin-bottom: 24px; }
.tab {
  padding: 9px 22px; border-radius: 8px 8px 0 0; cursor: pointer;
  font-size: 13.5px; font-weight: 500; color: var(--muted);
  border-bottom: 2px solid transparent; transition: all .15s;
  background: var(--surface); border: 1px solid var(--border);
  border-bottom: none;
}
.tab:hover { color: var(--txt); }
.tab.active { color: var(--txt); background: var(--card); border-color: var(--border); position: relative; }
.tab.active::after {
  content: ''; position: absolute; bottom: -1px; left: 0; right: 0; height: 2px;
  background: linear-gradient(90deg, var(--accent), var(--accent2));
}
.tab-content { display: none; }
.tab-content.active { display: block; }

/* ── Drop zone ── */
.dropzone {
  border: 2px dashed var(--border); border-radius: var(--radius);
  padding: 52px 24px; text-align: center; cursor: pointer;
  transition: all .2s; background: var(--card); user-select: none;
}
.dropzone:hover, .dropzone.over {
  border-color: var(--accent); background: rgba(139,92,246,.06);
}
.drop-icon { font-size: 36px; margin-bottom: 12px; opacity: .5; }
.drop-title { font-size: 15px; font-weight: 500; margin-bottom: 6px; }
.drop-sub { font-size: 12px; color: var(--muted); }
.drop-input { display: none; }

/* ── Settings row ── */
.settings-row {
  display: flex; align-items: center; gap: 12px;
  margin: 14px 0; flex-wrap: wrap;
}
.settings-row label { font-size: 12px; color: var(--muted); display: flex; align-items: center; gap: 6px; }
select {
  background: var(--surface); color: var(--txt); border: 1px solid var(--border);
  border-radius: 6px; padding: 5px 10px; font-size: 12.5px; cursor: pointer;
  outline: none;
}
select:focus { border-color: var(--accent); }
.btn {
  padding: 7px 18px; border-radius: 7px; border: none; cursor: pointer;
  font-size: 13px; font-weight: 600; transition: all .15s;
}
.btn-primary {
  background: linear-gradient(90deg, var(--accent), var(--accent2));
  color: #fff;
}
.btn-primary:hover { opacity: .88; transform: translateY(-1px); }
.btn-primary:active { transform: none; }
.btn-primary:disabled { opacity: .35; cursor: default; transform: none; }
.btn-ghost {
  background: var(--card); color: var(--txt); border: 1px solid var(--border);
}
.btn-ghost:hover { border-color: var(--accent); color: var(--accent); }
.btn-ghost:disabled { opacity: .35; cursor: default; }
.spacer { flex: 1; }

/* ── Install banner ── */
.install-banner {
  background: rgba(245,158,11,.07); border: 1px solid rgba(245,158,11,.3);
  border-radius: var(--radius); padding: 14px 18px; font-size: 12.5px;
  color: var(--warn); margin-bottom: 16px; line-height: 1.7;
}
.install-banner code {
  font-family: 'JetBrains Mono', 'Consolas', monospace;
  background: rgba(0,0,0,.3); padding: 2px 6px; border-radius: 4px;
  font-size: 11.5px; color: #fbbf24;
}

/* ── Queue ── */
.queue { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
.qi {
  background: var(--card); border: 1px solid var(--border); border-radius: var(--radius);
  display: flex; align-items: center; gap: 12px; padding: 10px 14px;
  transition: border-color .15s; position: relative; overflow: hidden;
}
.qi.done   { border-color: rgba(34,197,94,.25); cursor: pointer; }
.qi.done:hover { border-color: rgba(34,197,94,.5); }
.qi.error  { border-color: rgba(239,68,68,.3); }
.qi.processing { border-color: rgba(139,92,246,.4); }
.qi-thumb {
  width: 52px; height: 52px; border-radius: 6px; overflow: hidden;
  background: #111; flex-shrink: 0; position: relative;
}
.qi-thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
.qi-thumb .qi-img-result { position: absolute; inset: 0; }
.qi-body { flex: 1; min-width: 0; }
.qi-name {
  font-size: 13px; font-weight: 500; white-space: nowrap;
  overflow: hidden; text-overflow: ellipsis;
  font-family: 'Consolas', monospace;
}
.qi-info { font-size: 11px; color: var(--muted); margin-top: 3px; }
.qi-side { display: flex; align-items: center; gap: 10px; flex-shrink: 0; }
.badge {
  font-size: 10.5px; font-weight: 600; padding: 3px 9px; border-radius: 20px;
  letter-spacing: .03em; text-transform: uppercase;
}
.badge-wait     { background: rgba(100,100,132,.15); color: var(--muted); }
.badge-proc     { background: rgba(139,92,246,.15); color: var(--accent); }
.badge-done     { background: rgba(34,197,94,.15);  color: var(--ok); }
.badge-err      { background: rgba(239,68,68,.15);  color: var(--err); }
.qi-dl {
  width: 30px; height: 30px; border-radius: 6px; background: rgba(34,197,94,.12);
  color: var(--ok); border: 1px solid rgba(34,197,94,.2); display: flex;
  align-items: center; justify-content: center; font-size: 14px; cursor: pointer;
  text-decoration: none; transition: all .15s; flex-shrink: 0;
}
.qi-dl:hover { background: rgba(34,197,94,.25); }
.qi-progress {
  position: absolute; bottom: 0; left: 0; height: 2px;
  background: linear-gradient(90deg, var(--accent), var(--accent2));
  transition: width .3s; border-radius: 0 2px 0 0;
}
@keyframes pulse-bar { 0%,100%{opacity:.5} 50%{opacity:1} }
.qi-progress.indeterminate {
  animation: pulse-bar 1.2s infinite;
  width: 100% !important;
}
.spinner {
  width: 16px; height: 16px; border-radius: 50%;
  border: 2px solid rgba(139,92,246,.2); border-top-color: var(--accent);
  animation: spin .7s linear infinite; flex-shrink: 0;
}
@keyframes spin { to { transform: rotate(360deg); } }
.qi-err-msg { font-size: 11px; color: var(--err); margin-top: 4px; }
.compare-hint {
  font-size: 10px; color: var(--muted); margin-top: 3px; display: none;
}
.qi.done .compare-hint { display: block; }

/* ── Empty queue ── */
.queue-empty { font-size: 13px; color: var(--muted); text-align: center; padding: 24px; }

/* ── Footer row ── */
.footer-row {
  display: flex; align-items: center; justify-content: flex-end;
  margin-top: 12px; gap: 10px;
}
.count-label { font-size: 12px; color: var(--muted); margin-right: auto; }

/* ── Compare Modal ── */
#cmp-modal {
  display: none; position: fixed; inset: 0;
  background: rgba(0,0,0,.85); z-index: 100;
  align-items: center; justify-content: center;
}
#cmp-modal.open { display: flex; }
.cmp-box {
  background: var(--surface); border: 1px solid var(--border);
  border-radius: 14px; padding: 20px; max-width: 90vw; max-height: 90vh;
  display: flex; flex-direction: column; gap: 14px;
}
.cmp-header { display: flex; justify-content: space-between; align-items: center; }
.cmp-title { font-size: 13px; font-weight: 600; color: var(--txt); }
.cmp-close {
  width: 28px; height: 28px; border-radius: 6px; background: var(--card);
  border: 1px solid var(--border); color: var(--muted); cursor: pointer;
  font-size: 14px; display: flex; align-items: center; justify-content: center;
}
.cmp-close:hover { color: var(--txt); border-color: var(--accent); }
.cmp-container {
  position: relative; overflow: hidden; border-radius: 8px;
  max-width: 700px; max-height: 70vh; user-select: none; cursor: ew-resize;
}
.cmp-container img {
  display: block; max-width: 700px; max-height: 70vh;
  object-fit: contain;
}
.cmp-after  { display: block; }
.cmp-before {
  position: absolute; inset: 0; width: 100%; height: 100%;
  object-fit: contain;
  clip-path: inset(0 50% 0 0);
}
.cmp-handle {
  position: absolute; top: 0; bottom: 0; left: 50%; width: 2px;
  background: #fff; transform: translateX(-50%); pointer-events: none;
}
.cmp-handle::after {
  content: '◀ ▶'; position: absolute; top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  background: #fff; color: #111; border-radius: 20px;
  padding: 4px 8px; font-size: 10px; font-weight: 700;
  white-space: nowrap; pointer-events: none;
}
.cmp-labels { display: flex; justify-content: space-between; font-size: 11px; color: var(--muted); }
.lbl-orig  { color: var(--warn); font-weight: 600; }
.lbl-proc  { color: var(--ok);   font-weight: 600; }
</style>
</head>
<body>

<header>
  <div class="logo">◆ Kaezan Image Tools</div>
  <div class="status-bar" id="status-bar">
    <div class="status-item"><div class="dot dot-warn"></div><span>verificando…</span></div>
  </div>
</header>

<main>
  <div class="tabs">
    <div class="tab active" onclick="switchTab('upscale')">Upscale AI</div>
    <div class="tab" onclick="switchTab('removebg')">Remover Fundo</div>
  </div>

  <!-- ── Tab: Upscale ── -->
  <div class="tab-content active" id="tab-upscale">
    <div id="banner-upscale" class="install-banner" style="display:none">
      ⚠️ Pacote <strong>realesrgan</strong> não está instalado. Para habilitar esta aba:<br>
      <code>pip install realesrgan basicsr opencv-python-headless</code>
    </div>
    <div class="dropzone" id="dz-upscale"
         onclick="document.getElementById('fi-upscale').click()"
         ondragover="onDragOver(event,'upscale')"
         ondragleave="onDragLeave('upscale')"
         ondrop="onDrop(event,'upscale')">
      <div class="drop-icon">🖼️</div>
      <div class="drop-title">Solte as imagens aqui ou clique para selecionar</div>
      <div class="drop-sub">PNG · JPG · WEBP — múltiplos arquivos</div>
    </div>
    <input type="file" id="fi-upscale" class="drop-input" multiple accept="image/*"
           onchange="onFileInput(event,'upscale')">

    <div class="settings-row">
      <label>Escala
        <select id="sel-scale">
          <option value="2" selected>2× (recomendado)</option>
          <option value="4">4× (só para print)</option>
        </select>
      </label>
      <label>Tile GPU
        <select id="sel-tile">
          <option value="512" selected>512 px</option>
          <option value="256">256 px (VRAM baixa)</option>
          <option value="1024">1024 px (VRAM alta)</option>
          <option value="0">Sem tile (cuidado)</option>
        </select>
      </label>
      <div class="spacer"></div>
      <button class="btn btn-ghost" onclick="clearQueue('upscale')" id="btn-clear-upscale" disabled>
        Limpar
      </button>
      <button class="btn btn-primary" onclick="processQueue('upscale')" id="btn-proc-upscale" disabled>
        ▶ Processar
      </button>
    </div>

    <div class="queue" id="queue-upscale">
      <div class="queue-empty">Nenhuma imagem na fila</div>
    </div>
    <div class="footer-row" id="footer-upscale" style="display:none">
      <span class="count-label" id="count-upscale"></span>
      <button class="btn btn-ghost" onclick="downloadAll('upscale')">↓ Baixar todos</button>
    </div>
  </div>

  <!-- ── Tab: Remove BG ── -->
  <div class="tab-content" id="tab-removebg">
    <div id="banner-removebg" class="install-banner" style="display:none">
      ⚠️ Pacote <strong>rembg</strong> não está instalado. Para habilitar esta aba:<br>
      CPU: <code>pip install rembg onnxruntime</code><br>
      GPU: <code>pip install "rembg[gpu]" onnxruntime-gpu</code>
    </div>
    <div class="dropzone" id="dz-removebg"
         onclick="document.getElementById('fi-removebg').click()"
         ondragover="onDragOver(event,'removebg')"
         ondragleave="onDragLeave('removebg')"
         ondrop="onDrop(event,'removebg')">
      <div class="drop-icon">✂️</div>
      <div class="drop-title">Solte as imagens aqui ou clique para selecionar</div>
      <div class="drop-sub">PNG · JPG · WEBP — múltiplos arquivos — saída em PNG transparente</div>
    </div>
    <input type="file" id="fi-removebg" class="drop-input" multiple accept="image/*"
           onchange="onFileInput(event,'removebg')">

    <div class="settings-row">
      <label>Modelo
        <select id="sel-model">
          <option value="isnet-anime" selected>isnet-anime (arte anime — recomendado)</option>
          <option value="u2net_human_seg">u2net_human_seg (silhueta humana)</option>
          <option value="u2net">u2net (propósito geral)</option>
        </select>
      </label>
      <div class="spacer"></div>
      <button class="btn btn-ghost" onclick="clearQueue('removebg')" id="btn-clear-removebg" disabled>
        Limpar
      </button>
      <button class="btn btn-primary" onclick="processQueue('removebg')" id="btn-proc-removebg" disabled>
        ▶ Processar
      </button>
    </div>

    <div class="queue" id="queue-removebg">
      <div class="queue-empty">Nenhuma imagem na fila</div>
    </div>
    <div class="footer-row" id="footer-removebg" style="display:none">
      <span class="count-label" id="count-removebg"></span>
      <button class="btn btn-ghost" onclick="downloadAll('removebg')">↓ Baixar todos</button>
    </div>
  </div>
</main>

<!-- ── Compare Modal ── -->
<div id="cmp-modal" onclick="if(event.target===this)closeCompare()">
  <div class="cmp-box">
    <div class="cmp-header">
      <span class="cmp-title" id="cmp-title">Comparação</span>
      <button class="cmp-close" onclick="closeCompare()">✕</button>
    </div>
    <div class="cmp-container" id="cmp-container"
         onmousemove="cmpDrag(event)" onclick="cmpDrag(event)">
      <img class="cmp-after"  id="cmp-after"  src="" alt="">
      <img class="cmp-before" id="cmp-before" src="" alt="">
      <div class="cmp-handle" id="cmp-handle"></div>
    </div>
    <div class="cmp-labels">
      <span class="lbl-orig">◀ ORIGINAL</span>
      <span class="lbl-proc">PROCESSADO ▶</span>
    </div>
  </div>
</div>

<script>
// ── State ─────────────────────────────────────────────────────────────────
const queues     = { upscale: [], removebg: [] };
const processing = { upscale: false, removebg: false };
const caps       = { upscale: false, removebg: false };
let   nextId     = 0;
let   activeTab  = 'upscale';

// ── Init ──────────────────────────────────────────────────────────────────
(async () => {
  try {
    const s = await fetch('/api/status').then(r => r.json());
    caps.upscale  = s.upscale;
    caps.removebg = s.removebg;

    const bar = document.getElementById('status-bar');
    bar.innerHTML = `
      <div class="status-item">
        <div class="dot ${s.cuda ? 'dot-ok' : 'dot-warn'}"></div>
        <span>${s.cuda ? 'GPU: ' + s.cuda_name : 'CPU'}</span>
      </div>
      <div class="status-item">
        <div class="dot ${s.upscale ? 'dot-ok' : 'dot-err'}"></div>
        <span>Upscale${s.upscale ? '' : ' (não instalado)'}</span>
      </div>
      <div class="status-item">
        <div class="dot ${s.removebg ? 'dot-ok' : 'dot-err'}"></div>
        <span>Remove BG${s.removebg ? '' : ' (não instalado)'}</span>
      </div>`;

    if (!s.upscale)  document.getElementById('banner-upscale').style.display  = '';
    if (!s.removebg) document.getElementById('banner-removebg').style.display = '';

    updateButtons('upscale');
    updateButtons('removebg');
  } catch (e) {
    document.getElementById('status-bar').innerHTML =
      `<div class="status-item"><div class="dot dot-err"></div><span>servidor offline</span></div>`;
  }
})();

// ── Tabs ──────────────────────────────────────────────────────────────────
function switchTab(name) {
  activeTab = name;
  document.querySelectorAll('.tab').forEach((t, i) => {
    const tabs = ['upscale', 'removebg'];
    t.classList.toggle('active', tabs[i] === name);
  });
  document.querySelectorAll('.tab-content').forEach(c =>
    c.classList.toggle('active', c.id === 'tab-' + name));
}

// ── Drag & Drop ───────────────────────────────────────────────────────────
function onDragOver(e, tab) {
  e.preventDefault();
  document.getElementById('dz-' + tab).classList.add('over');
}
function onDragLeave(tab) {
  document.getElementById('dz-' + tab).classList.remove('over');
}
function onDrop(e, tab) {
  e.preventDefault();
  document.getElementById('dz-' + tab).classList.remove('over');
  addFiles([...e.dataTransfer.files], tab);
}
function onFileInput(e, tab) {
  addFiles([...e.target.files], tab);
  e.target.value = '';
}

// ── Queue management ──────────────────────────────────────────────────────
function addFiles(files, tab) {
  const imgs = files.filter(f => f.type.startsWith('image/'));
  if (!imgs.length) return;
  for (const f of imgs) {
    const item = {
      id: ++nextId, file: f, tab,
      status: 'waiting',
      origUrl: URL.createObjectURL(f),
      resultUrl: null, error: null, info: '',
    };
    queues[tab].push(item);
    appendQueueItem(item, tab);
  }
  updateButtons(tab);
  updateFooter(tab);
}

function clearQueue(tab) {
  queues[tab] = [];
  const el = document.getElementById('queue-' + tab);
  el.innerHTML = '<div class="queue-empty">Nenhuma imagem na fila</div>';
  updateButtons(tab);
  updateFooter(tab);
}

// ── Render queue item ─────────────────────────────────────────────────────
function appendQueueItem(item, tab) {
  const container = document.getElementById('queue-' + tab);
  const empty = container.querySelector('.queue-empty');
  if (empty) empty.remove();

  const div = document.createElement('div');
  div.className = 'qi';
  div.id = 'qi-' + item.id;
  div.innerHTML = `
    <div class="qi-thumb">
      <img class="qi-img-orig" src="${item.origUrl}" alt="">
      <img class="qi-img-result" id="qir-${item.id}" src="" alt="" style="display:none">
    </div>
    <div class="qi-body">
      <div class="qi-name">${escHtml(item.file.name)}</div>
      <div class="qi-info" id="qii-${item.id}">aguardando…</div>
      <div class="compare-hint">Clique para comparar antes/depois</div>
    </div>
    <div class="qi-side">
      <span class="badge badge-wait" id="qib-${item.id}">aguardando</span>
      <a class="qi-dl" id="qid-${item.id}" style="display:none" title="Baixar"></a>
    </div>
    <div class="qi-progress" id="qip-${item.id}" style="width:0"></div>`;
  div.addEventListener('click', () => {
    if (item.status === 'done') openCompare(item);
  });
  container.appendChild(div);
}

function updateQueueItem(item) {
  const el = document.getElementById('qi-' + item.id);
  if (!el) return;
  el.className = 'qi ' + item.status;

  const badge    = document.getElementById('qib-' + item.id);
  const info     = document.getElementById('qii-' + item.id);
  const dl       = document.getElementById('qid-' + item.id);
  const progress = document.getElementById('qip-' + item.id);
  const resultImg = document.getElementById('qir-' + item.id);

  const badges = { waiting: ['badge-wait','aguardando'], processing: ['badge-proc','processando'],
                   done: ['badge-done','pronto'], error: ['badge-err','erro'] };
  badge.className = 'badge ' + badges[item.status][0];
  badge.textContent = badges[item.status][1];

  // Spinner
  const existingSpinner = el.querySelector('.spinner');
  if (existingSpinner) existingSpinner.remove();
  if (item.status === 'processing') {
    const sp = document.createElement('div');
    sp.className = 'spinner';
    el.querySelector('.qi-side').prepend(sp);
    progress.className = 'qi-progress indeterminate';
  } else {
    progress.className = 'qi-progress';
    progress.style.width = item.status === 'done' ? '100%' : '0';
  }

  if (item.status === 'done') {
    info.textContent = item.info || 'concluído';
    dl.style.display = '';
    dl.href = item.resultUrl;
    dl.download = item.file.name.replace(/\.(jpe?g|webp)$/i, '.png');
    dl.textContent = '↓';
    dl.title = 'Baixar ' + dl.download;
    // Stop click-to-compare propagating to download
    dl.addEventListener('click', e => e.stopPropagation());
    if (resultImg && item.resultUrl) {
      resultImg.src = item.resultUrl;
      resultImg.style.display = 'block';
    }
  } else if (item.status === 'error') {
    info.innerHTML = `<span class="qi-err-msg">${escHtml(item.error || 'erro desconhecido')}</span>`;
  }
}

// ── Process ───────────────────────────────────────────────────────────────
async function processQueue(tab) {
  if (processing[tab]) return;
  if (!caps[tab]) {
    alert('Pacote não instalado. Veja o banner de instalação acima.');
    return;
  }
  processing[tab] = true;
  updateButtons(tab);

  const endpoint = tab === 'upscale' ? '/api/upscale' : '/api/removebg';

  for (const item of queues[tab]) {
    if (item.status !== 'waiting') continue;
    item.status = 'processing';
    updateQueueItem(item);

    const fd = new FormData();
    fd.append('file', item.file);
    if (tab === 'upscale') {
      fd.append('scale', document.getElementById('sel-scale').value);
      fd.append('tile',  document.getElementById('sel-tile').value);
    } else {
      fd.append('model', document.getElementById('sel-model').value);
    }

    try {
      const t0  = Date.now();
      const res = await fetch(endpoint, { method: 'POST', body: fd });
      if (!res.ok) {
        const msg = await res.text();
        throw new Error(msg);
      }
      const blob = await res.blob();
      item.resultUrl = URL.createObjectURL(blob);
      item.status = 'done';
      const elapsed = ((Date.now() - t0) / 1000).toFixed(1);
      // Get dimensions from the blob
      item.info = await getImgDims(item.origUrl, item.resultUrl, elapsed);
    } catch (e) {
      item.status = 'error';
      item.error  = e.message;
    }
    updateQueueItem(item);
    updateFooter(tab);
  }

  processing[tab] = false;
  updateButtons(tab);
}

async function getImgDims(origUrl, resultUrl, elapsed) {
  try {
    const [o, r] = await Promise.all([loadImgSize(origUrl), loadImgSize(resultUrl)]);
    return `${o.w}×${o.h} → ${r.w}×${r.h}  (${elapsed}s)`;
  } catch { return `concluído em ${elapsed}s`; }
}
function loadImgSize(url) {
  return new Promise((res, rej) => {
    const i = new Image();
    i.onload  = () => res({ w: i.naturalWidth, h: i.naturalHeight });
    i.onerror = rej;
    i.src = url;
  });
}

// ── Buttons / footer ──────────────────────────────────────────────────────
function updateButtons(tab) {
  const hasWaiting = queues[tab].some(i => i.status === 'waiting');
  const hasAny     = queues[tab].length > 0;
  const isProc     = processing[tab];

  const proc  = document.getElementById('btn-proc-' + tab);
  const clear = document.getElementById('btn-clear-' + tab);
  if (proc)  proc.disabled  = isProc || !hasWaiting || !caps[tab];
  if (clear) clear.disabled = isProc || !hasAny;
}
function updateFooter(tab) {
  const done  = queues[tab].filter(i => i.status === 'done').length;
  const total = queues[tab].length;
  const footer = document.getElementById('footer-' + tab);
  const count  = document.getElementById('count-'  + tab);
  if (footer) footer.style.display = total ? '' : 'none';
  if (count)  count.textContent = `${done} de ${total} concluídos`;
}

// ── Download all ──────────────────────────────────────────────────────────
async function downloadAll(tab) {
  const done = queues[tab].filter(i => i.status === 'done');
  for (const item of done) {
    const a = document.createElement('a');
    a.href     = item.resultUrl;
    a.download = item.file.name.replace(/\.(jpe?g|webp)$/i, '.png');
    a.click();
    await new Promise(r => setTimeout(r, 250));
  }
}

// ── Compare Modal ─────────────────────────────────────────────────────────
let cmpPct = 50;
function openCompare(item) {
  document.getElementById('cmp-before').src = item.origUrl;
  document.getElementById('cmp-after').src  = item.resultUrl;
  document.getElementById('cmp-title').textContent = item.file.name;
  setCmpPct(50);
  document.getElementById('cmp-modal').classList.add('open');
}
function closeCompare() {
  document.getElementById('cmp-modal').classList.remove('open');
}
function setCmpPct(pct) {
  cmpPct = Math.max(0, Math.min(100, pct));
  document.getElementById('cmp-handle').style.left = cmpPct + '%';
  document.getElementById('cmp-before').style.clipPath =
    `inset(0 ${100 - cmpPct}% 0 0)`;
}
let cmpDragging = false;
document.getElementById('cmp-handle').addEventListener('mousedown', e => {
  cmpDragging = true; e.stopPropagation();
});
document.addEventListener('mouseup', () => cmpDragging = false);
document.addEventListener('mousemove', e => {
  if (!cmpDragging) return;
  const r = document.getElementById('cmp-container').getBoundingClientRect();
  setCmpPct((e.clientX - r.left) / r.width * 100);
});
function cmpDrag(e) {
  const r = e.currentTarget.getBoundingClientRect();
  setCmpPct((e.clientX - r.left) / r.width * 100);
}
document.addEventListener('keydown', e => {
  if (e.key === 'Escape') closeCompare();
});

// ── Util ──────────────────────────────────────────────────────────────────
function escHtml(s) {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}
</script>
</body>
</html>"""

# ── Entry point ────────────────────────────────────────────────────────────
if __name__ == "__main__":
    device = f"CUDA ({CUDA_NAME})" if CUDA_OK else "CPU"
    print(f"\n  ◆ Kaezan Image Tools")
    print(f"  ─────────────────────────────────────")
    print(f"  Porta     : {PORT}")
    print(f"  Device    : {device}")
    print(f"  Upscale   : {'pronto' if UPSCALE_OK else 'NÃO INSTALADO — pip install realesrgan basicsr opencv-python-headless'}")
    print(f"  Remove BG : {'pronto' if REMOVEBG_OK else 'NÃO INSTALADO — pip install rembg onnxruntime'}")
    print(f"\n  Abra: http://localhost:{PORT}\n")
    uvicorn.run(app, host="0.0.0.0", port=PORT, log_level="warning")

#!/usr/bin/env python3
"""
kaezan_tools_ui.py — Interface de upscale batch via ComfyUI
Porta 7879 · Abre o browser automaticamente · Sem pip install

Uso direto:  python tools/kaezan_tools_ui.py
Ou clique:   kaezan-tools.bat (na raiz do projeto)
"""
from __future__ import annotations
import json, os, queue, socketserver, subprocess, sys, threading, time, webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

PORT            = 7879
PROJECT_ROOT    = Path(__file__).parent.parent.resolve()
BATCH_SCRIPT    = PROJECT_ROOT / "tools" / "comfyui_batch.py"
VALIDATE_SCRIPT = PROJECT_ROOT / "tools" / "validate_assets.py"

# ── Job state ──────────────────────────────────────────────────────────────
class Job:
    def __init__(self, job_id: str, cmd: list[str], script: Path = None):
        self.id         = job_id
        self.cmd        = cmd
        self.script     = script
        self.q: queue.Queue = queue.Queue()
        self.proc       = None
        self.done       = False
        self.returncode = None

_jobs: dict[str, Job] = {}
_lock = threading.Lock()

def _run_job(job: Job):
    script = job.script if job.script else BATCH_SCRIPT
    try:
        job.proc = subprocess.Popen(
            [sys.executable, "-u", str(script)] + job.cmd,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, bufsize=1, cwd=str(PROJECT_ROOT),
            encoding="utf-8", errors="replace",
        )
        for line in job.proc.stdout:
            job.q.put(line.rstrip("\n"))
        job.proc.wait()
        job.returncode = job.proc.returncode
    except Exception as e:
        job.q.put(f"  ERRO ao iniciar processo: {e}")
    finally:
        job.done = True
        job.q.put(None)

def start_job(args: list[str], script: Path = None) -> str:
    job_id = str(int(time.time() * 1000))
    job = Job(job_id, args, script)
    with _lock:
        _jobs[job_id] = job
    threading.Thread(target=_run_job, args=(job,), daemon=True).start()
    return job_id

# ── HTTP Handler ───────────────────────────────────────────────────────────
class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_): pass

    def _json(self, data: dict, status: int = 200):
        body = json.dumps(data).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        if self.path in ("/", "/index.html"):
            body = _HTML.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        elif self.path.startswith("/stream/"):
            job_id = self.path.split("/stream/", 1)[1]
            with _lock:
                job = _jobs.get(job_id)
            if not job:
                self.send_error(404, "Job nao encontrado")
                return
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Cache-Control", "no-cache")
            self.send_header("X-Accel-Buffering", "no")
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            try:
                while True:
                    try:
                        line = job.q.get(timeout=30)
                    except queue.Empty:
                        self.wfile.write(b": keep-alive\n\n")
                        self.wfile.flush()
                        continue
                    if line is None:
                        rc = job.returncode or 0
                        self.wfile.write(
                            f'event: done\ndata: {json.dumps({"rc": rc})}\n\n'.encode()
                        )
                        self.wfile.flush()
                        break
                    self.wfile.write(f"data: {json.dumps(line)}\n\n".encode())
                    self.wfile.flush()
            except (BrokenPipeError, ConnectionResetError):
                pass

        elif self.path == "/cancel":
            cancelled = False
            for job in list(_jobs.values()):
                if not job.done and job.proc:
                    job.proc.terminate()
                    cancelled = True
            self._json({"cancelled": cancelled})

        elif self.path.startswith("/browse"):
            from urllib.parse import parse_qs, urlparse as _up
            qs    = parse_qs(_up(self.path).query)
            start = qs.get("start", [""])[0]
            if not start or not Path(start).exists():
                start = str(PROJECT_ROOT)
            try:
                import tkinter as tk
                from tkinter import filedialog
                root = tk.Tk()
                root.withdraw()
                root.attributes("-topmost", True)
                folder = filedialog.askdirectory(initialdir=start, title="Selecionar pasta")
                root.destroy()
                self._json({"path": folder or ""})
            except Exception as exc:
                self._json({"path": "", "error": str(exc)})

        else:
            self.send_error(404)

    def do_POST(self):
        length  = int(self.headers.get("Content-Length", 0))
        payload = json.loads(self.rfile.read(length)) if length else {}
        if self.path == "/run":
            args        = payload.get("args", [])
            script_key  = payload.get("script", "batch")
            script_path = VALIDATE_SCRIPT if script_key == "validate" else None
            job_id = start_job([str(a) for a in args], script_path)
            self._json({"job_id": job_id})
        else:
            self.send_error(404)


class _ThreadedHTTPServer(socketserver.ThreadingMixIn, HTTPServer):
    daemon_threads = True

# ── HTML UI ────────────────────────────────────────────────────────────────
_HTML = r"""<!DOCTYPE html>
<html lang="pt-BR">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Kaezan Tools</title>
<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
:root{
  --bg:#09090f;--surface:#0f0e18;--card:#14121f;--border:#1f1c30;
  --accent:#7c6cfc;--gold:#e8b84b;--txt:#d8d5f0;--muted:#6b6685;
  --ok:#4ade80;--err:#fb7185;--warn:#fbbf24;--info:#60a5fa;--green:#22c55e;
  font-family:'Inter',system-ui,sans-serif;font-size:13px;color:var(--txt);
}
body{background:var(--bg);min-height:100vh;display:flex;flex-direction:column}

/* header */
header{
  display:flex;align-items:center;gap:10px;padding:9px 16px;
  background:var(--surface);border-bottom:1px solid var(--border);flex-shrink:0;
}
.logo{font-size:12px;font-weight:700;letter-spacing:.06em;color:var(--accent)}
.logo-gem{color:var(--gold);margin-right:5px}
.hdr-right{margin-left:auto;display:flex;align-items:center;gap:14px}
.status-pill{display:flex;align-items:center;gap:6px;font-size:11px;color:var(--muted)}
.dot{width:6px;height:6px;border-radius:50%;background:var(--muted);flex-shrink:0;transition:.3s}
.dot.running{background:var(--ok);animation:pulse 1.2s infinite}
.dot.error{background:var(--err)}
@keyframes pulse{0%,100%{opacity:.45}50%{opacity:1}}
.port-tag{font-size:10px;color:var(--border);font-family:'Consolas',monospace}

/* main layout */
main{flex:1;display:grid;grid-template-columns:288px 1fr;overflow:hidden}

/* ── left panel ── */
.left{
  background:var(--surface);border-right:1px solid var(--border);
  display:flex;flex-direction:column;overflow:hidden;
}
/* tabs */
.tabbar{display:flex;flex-shrink:0;background:var(--surface);border-bottom:1px solid var(--border)}
.tab{
  flex:1;padding:9px 4px;background:transparent;border:none;cursor:pointer;
  border-bottom:2px solid transparent;color:var(--muted);font-family:inherit;
  font-size:10px;font-weight:700;letter-spacing:.03em;transition:.15s;white-space:nowrap;
}
.tab:hover{color:var(--txt)}
.tab.active{color:var(--accent);border-bottom-color:var(--accent)}
.tabpane{display:none;flex:1;min-height:0;overflow-y:auto;flex-direction:column}
.tabpane.active{display:flex}
.info-body{font-size:11px;color:var(--txt);line-height:1.65;opacity:.9}
.info-body b{color:var(--txt);opacity:1}
.info-body code{font-family:'Consolas',monospace;font-size:10.5px;color:var(--gold);
  background:rgba(232,184,75,.08);padding:1px 4px;border-radius:3px}
.info-body ul{margin:5px 0 5px 16px;display:flex;flex-direction:column;gap:3px}
.info-body ul ul{margin:3px 0 3px 14px}
.info-body li{color:var(--muted)}
.info-body li b{color:var(--accent);opacity:1}
.section{padding:13px 14px;border-bottom:1px solid var(--border)}
.sec-label{
  font-size:9.5px;font-weight:700;letter-spacing:.13em;text-transform:uppercase;
  color:var(--muted);margin-bottom:9px;display:flex;align-items:center;gap:7px;
}
.sec-label::before{
  content:'';display:inline-block;width:2px;height:11px;
  background:var(--accent);border-radius:1px;flex-shrink:0;
}
.sec-label.gold::before{background:var(--gold)}
.field{display:flex;flex-direction:column;gap:3px;margin-bottom:8px}
.field:last-child{margin-bottom:0}
.field>label{font-size:10.5px;color:var(--muted)}
.path-row{display:flex;gap:4px}
.path-row input{flex:1;min-width:0}
input[type=text],input[type=number],select{
  background:var(--card);color:var(--txt);border:1px solid var(--border);
  border-radius:5px;padding:6px 8px;font-size:11.5px;outline:none;width:100%;
  font-family:'Cascadia Code','JetBrains Mono','Consolas',monospace;
}
input:focus,select:focus{border-color:var(--accent)}
select{cursor:pointer}
.btn-browse{
  flex-shrink:0;padding:5px 7px;border-radius:5px;border:1px solid var(--border);
  background:var(--card);color:var(--muted);cursor:pointer;font-size:12px;
  line-height:1;transition:.15s;
}
.btn-browse:hover{border-color:var(--accent);color:var(--accent)}

/* type info */
.type-meta{
  display:flex;gap:6px;flex-wrap:wrap;margin-top:5px;
}
.type-badge{
  padding:2px 7px;border-radius:3px;font-size:10px;font-weight:700;
  font-family:'Consolas',monospace;letter-spacing:.04em;
}
.type-badge.on {background:rgba(34,197,94,.15);color:var(--ok);border:1px solid rgba(34,197,94,.25)}
.type-badge.off{background:rgba(107,102,133,.1);color:var(--muted);border:1px solid var(--border)}
#type-notes{font-size:10px;color:var(--muted);line-height:1.5;margin-top:4px;opacity:.75}

/* chips */
.chips{display:flex;flex-wrap:wrap;gap:3px;margin-top:4px}
.chip{
  padding:3px 7px;border-radius:4px;font-size:10.5px;cursor:pointer;
  background:transparent;border:1px solid var(--border);color:var(--muted);
  transition:.15s;font-family:'Consolas',monospace;user-select:none;
}
.chip:hover{border-color:var(--accent);color:var(--txt)}
.chip.active{background:rgba(124,108,252,.14);border-color:var(--accent);
  color:var(--accent);font-weight:600}
.chip-del{margin-left:3px;opacity:.4;font-size:9px;vertical-align:middle}
.chip-del:hover{opacity:1;color:var(--err)}
.chip-add-row{display:flex;gap:4px;margin-top:5px}
.chip-add-row input{
  flex:1;padding:3px 7px;border-radius:4px;font-size:10.5px;
  background:var(--card);border:1px solid var(--border);color:var(--txt);
  font-family:'Consolas',monospace;outline:none;
}
.chip-add-row input:focus{border-color:var(--accent)}
.chip-add-btn{
  padding:3px 8px;border-radius:4px;font-size:10.5px;cursor:pointer;white-space:nowrap;
  background:rgba(124,108,252,.1);border:1px solid rgba(124,108,252,.28);color:var(--accent);
}
.chip-add-btn:hover{background:rgba(124,108,252,.2)}

/* toggle */
.toggle-row{display:flex;align-items:center;gap:7px;font-size:11.5px;cursor:pointer}
.toggle-row input[type=checkbox]{accent-color:var(--accent);width:13px;height:13px;flex-shrink:0}

/* actions */
.actions{padding:13px 14px;margin-top:auto}
.btn-batch{
  width:100%;padding:11px 14px;border-radius:6px;border:none;cursor:pointer;
  background:var(--green);color:#021206;font-size:13px;font-weight:700;
  letter-spacing:.05em;font-family:inherit;transition:.15s;
  display:flex;align-items:center;justify-content:center;gap:7px;
}
.btn-batch:not(:disabled):hover{filter:brightness(1.1);transform:translateY(-1px)}
.btn-batch:disabled{opacity:.3;cursor:default}
.btn-upscale{
  width:100%;padding:11px 14px;border-radius:6px;border:none;cursor:pointer;
  background:var(--gold);color:#0d0900;font-size:13px;font-weight:700;
  letter-spacing:.05em;font-family:inherit;transition:.15s;
  display:flex;align-items:center;justify-content:center;gap:7px;margin-top:7px;
}
.btn-upscale:not(:disabled):hover{filter:brightness(1.1);transform:translateY(-1px)}
.btn-upscale:disabled{opacity:.3;cursor:default}
.btn-removebg{
  width:100%;padding:11px 14px;border-radius:6px;border:none;cursor:pointer;
  background:var(--accent);color:#fff;font-size:13px;font-weight:700;
  letter-spacing:.05em;font-family:inherit;transition:.15s;
  display:flex;align-items:center;justify-content:center;gap:7px;margin-top:7px;
}
.btn-removebg:not(:disabled):hover{filter:brightness(1.15);transform:translateY(-1px)}
.btn-removebg:disabled{opacity:.3;cursor:default}
.btn-row{display:flex;gap:5px;margin-top:7px}
.btn-ghost{
  flex:1;padding:7px 10px;border-radius:5px;border:1px solid var(--border);
  background:transparent;color:var(--muted);cursor:pointer;
  font-size:11.5px;font-family:inherit;transition:.15s;
}
.btn-ghost:not(:disabled):hover{border-color:var(--accent);color:var(--accent)}
.btn-ghost:disabled{opacity:.25;cursor:default}
.btn-stop{
  flex:1;padding:7px 10px;border-radius:5px;cursor:pointer;font-size:11.5px;
  font-family:inherit;transition:.15s;border:1px solid rgba(251,113,133,.2);
  background:rgba(251,113,133,.05);color:var(--err);
}
.btn-stop:not(:disabled):hover{background:rgba(251,113,133,.12)}
.btn-stop:disabled{opacity:.2;cursor:default}

/* restore */
.restore{padding:12px 14px;border-top:1px solid var(--border)}
.restore-grid{display:grid;grid-template-columns:auto 1fr auto;gap:5px;align-items:center;margin-bottom:5px}
.restore-grid label{font-size:10.5px;color:var(--muted);white-space:nowrap}
.restore-actions{display:flex;gap:5px;margin-top:6px}

/* ── right panel ── */
.right{display:flex;flex-direction:column;overflow:hidden}
.term-head{
  display:flex;align-items:center;gap:10px;padding:8px 14px;
  background:var(--surface);border-bottom:1px solid var(--border);flex-shrink:0;
}
.term-cmd{
  flex:1;font-size:10.5px;color:var(--muted);
  font-family:'Consolas',monospace;
  white-space:nowrap;overflow:hidden;text-overflow:ellipsis;
}
.prog-badge{
  font-family:'Cascadia Code','Consolas',monospace;font-size:12px;
  font-weight:700;color:var(--gold);white-space:nowrap;display:none;
  letter-spacing:.02em;
}
.prog-track{height:3px;background:var(--border);flex-shrink:0;display:none}
.prog-fill{height:100%;background:var(--gold);width:0%;transition:width .35s ease}
#terminal{
  flex:1;overflow-y:auto;padding:12px 16px;background:var(--bg);
  font-family:'Cascadia Code','JetBrains Mono','Consolas',monospace;
  font-size:12px;line-height:1.85;
}
.l-ok  {color:var(--ok)}
.l-err {color:var(--err)}
.l-warn{color:var(--warn)}
.l-info{color:var(--info)}
.l-prog{color:var(--txt)}
.l-cmd {color:var(--accent);opacity:.55;font-size:11px}
.l-head{color:var(--muted)}
.l-bkp {color:var(--info);opacity:.65}
.l-skip{color:var(--muted);opacity:.5}
.term-foot{
  padding:7px 14px;background:var(--surface);border-top:1px solid var(--border);
  display:flex;align-items:center;gap:10px;flex-shrink:0;
}
#rc-badge{font-size:11px;font-family:'Consolas',monospace;color:var(--muted);flex:1}
</style>
</head>
<body>

<header>
  <span class="logo"><span class="logo-gem">&#9670;</span>KAEZAN TOOLS</span>
  <div class="hdr-right">
    <div class="status-pill">
      <div class="dot" id="sdot"></div>
      <span id="stxt">pronto</span>
    </div>
    <span class="port-tag">:7879</span>
  </div>
</header>

<main>
<!-- ── LEFT ── -->
<div class="left">

  <div class="tabbar">
    <button class="tab active" data-tab="process"  onclick="showTab('process')">Pós-processo</button>
    <button class="tab"        data-tab="skin"     onclick="showTab('skin')">Skin Variant</button>
    <button class="tab"        data-tab="validate" onclick="showTab('validate')">Validar</button>
    <button class="tab"        data-tab="info"     onclick="showTab('info')">&#9432; Info</button>
  </div>

  <div class="tabpane active" id="tab-process">

  <!-- ── TIPO DE ASSET ── -->
  <div class="section">
    <div class="sec-label gold">Tipo de Asset</div>
    <div class="field">
      <label>Tipo</label>
      <select id="asset-type" onchange="onTypeChange()">
        <option value="">— manual (upscale/removebg avulso) —</option>
        <option value="kaeli">kaeli</option>
        <option value="item">item</option>
        <option value="mob">mob</option>
        <option value="background">background</option>
        <option value="logo">logo</option>
        <option value="motion">motion</option>
      </select>
    </div>
    <div class="type-meta" id="type-meta" style="display:none">
      <span class="type-badge" id="badge-up">upscale</span>
      <span class="type-badge" id="badge-rb">removebg</span>
      <span class="type-badge" id="badge-fr">face restore</span>
    </div>
    <div id="type-notes"></div>
    <div class="field" style="margin-top:8px">
      <label class="toggle-row">
        <input type="checkbox" id="force-flag">
        Force (reprocessar arquivos já existentes)
      </label>
    </div>
  </div>

  <div class="section">
    <div class="sec-label">Entrada</div>
    <div class="field">
      <label>Pasta</label>
      <div class="path-row">
        <input type="text" id="input-dir" value="frontend/public/assets/kaelis">
        <button class="btn-browse" onclick="browse('input-dir')" title="Selecionar">&#128193;</button>
      </div>
    </div>
    <div class="field">
      <label>Filtro de arquivo</label>
      <input type="text" id="glob" value="idle-*.png">
      <div class="chips" id="glob-chips">
        <span class="chip" onclick="setGlob('idle-1.png')">idle-1.png</span>
        <span class="chip active" onclick="setGlob('idle-*.png')">idle-*.png</span>
        <span class="chip" onclick="setGlob('*.png')">*.png</span>
        <span class="chip" onclick="setGlob('banner.png')">banner.png</span>
        <span class="chip" onclick="setGlob('thumb.png')">thumb.png</span>
      </div>
      <div class="chip-add-row">
        <input type="text" id="glob-new" placeholder="novo filtro..."
               onkeydown="if(event.key==='Enter')addChip()">
        <button class="chip-add-btn" onclick="addChip()">+ Adicionar</button>
      </div>
    </div>
  </div>

  <div class="section">
    <div class="sec-label">Upscale</div>
    <div class="field">
      <label>Upscale model</label>
      <input type="text" id="upscale-model" value="RealESRGAN_x4plus_anime_6B.pth">
      <div class="chips" id="up-model-chips">
        <span class="chip active" data-val="RealESRGAN_x4plus_anime_6B.pth"
              onclick="setUpModel('RealESRGAN_x4plus_anime_6B.pth')">anime_6B</span>
        <span class="chip" data-val="4xNomos2_hq_dat2.safetensors"
              onclick="setUpModel('4xNomos2_hq_dat2.safetensors')">4xNomos2</span>
        <span class="chip" data-val="RealESRGAN_x4plus.pth"
              onclick="setUpModel('RealESRGAN_x4plus.pth')">x4plus</span>
        <span class="chip" data-val="ESRGAN.pth"
              onclick="setUpModel('ESRGAN.pth')">ESRGAN</span>
      </div>
    </div>
    <div class="field">
      <label>Escala</label>
      <select id="scale">
        <option value="0.5" selected>2x net (recomendado)</option>
        <option value="1.0">4x net</option>
      </select>
    </div>
  </div>

  <div class="section">
    <div class="sec-label">Remove BG</div>
    <div class="field">
      <label>Modelo ISNet</label>
      <select id="removebg-model">
        <option value="isnet-anime" selected>isnet-anime (anime, padrão)</option>
        <option value="isnet_is">isnet_is</option>
        <option value="u2net">u2net (geral)</option>
        <option value="u2netp">u2netp (leve)</option>
      </select>
    </div>
    <div class="field" style="font-size:10.5px;color:var(--muted);line-height:1.5">
      Pré-req: ComfyUI Manager → Install Custom Nodes<br>→ <b>comfyui-art-venture</b> (sipherxyz)
    </div>
  </div>

  <div class="section">
    <div class="sec-label">Face Restore (IMG-06)</div>
    <div class="field">
      <label class="toggle-row">
        <input type="checkbox" id="face-restore-flag" onchange="onFaceRestoreToggle()">
        Ativar face detailer (rosto nítido — opcional)
      </label>
    </div>
    <div id="face-restore-params" style="display:none">
      <div class="field">
        <label>Checkpoint (anime recomendado para Kaelis)</label>
        <input type="text" id="face-restore-checkpoint" value="v1-5-pruned-emaonly.ckpt">
        <div class="chips" id="fr-ckpt-chips" style="margin-top:4px">
          <span class="chip" onclick="setFrCkpt('v1-5-pruned-emaonly.ckpt')">SD 1.5</span>
          <span class="chip" onclick="setFrCkpt('CounterfeitV30_v30.safetensors')">Counterfeit</span>
          <span class="chip" onclick="setFrCkpt('AnyLoRA_noVae_bf16.safetensors')">AnyLoRA</span>
        </div>
      </div>
      <div class="field">
        <label>Detector de rosto (bbox)</label>
        <input type="text" id="face-restore-bbox-model" value="face_yolov8m.pt">
      </div>
      <div class="field">
        <label>Denoise (0.2 sutil → 0.5 forte)</label>
        <input type="number" id="face-restore-denoise" value="0.35" min="0.1" max="1.0" step="0.05">
      </div>
      <div class="field">
        <label>Steps</label>
        <input type="number" id="face-restore-steps" value="20" min="10" max="50">
      </div>
      <div class="field">
        <label>CFG Scale</label>
        <input type="number" id="face-restore-cfg" value="7.0" min="1" max="15" step="0.5">
      </div>
      <div class="field" style="font-size:10px;color:var(--muted);line-height:1.5">
        Pré-req: Impact Pack (ltdrdata) · face_yolov8m.pt<br>
        em models/ultralytics/bbox/ · checkpoint anime.<br>
        Roda após upscale, antes do removebg.
      </div>
    </div>
  </div>

  <div class="section">
    <div class="sec-label">Saida</div>
    <div class="field">
      <label>Pasta</label>
      <div class="path-row">
        <input type="text" id="output-dir" value="output/upscaled" oninput="syncRestore()">
        <button class="btn-browse" onclick="browse('output-dir',syncRestore)" title="Selecionar">&#128193;</button>
      </div>
    </div>
    <div class="field">
      <label class="toggle-row">
        <input type="checkbox" id="backup" checked>
        Backup automatico dos originais
      </label>
    </div>
  </div>

  <div class="actions">
    <button class="btn-batch" id="btn-batch" onclick="runBatch()" disabled>
      &#9654; Processar por Tipo
    </button>
    <button class="btn-upscale" id="btn-up" onclick="run('upscale')">
      &#8679; Upscale
    </button>
    <button class="btn-removebg" id="btn-rbg" onclick="run('removebg')">
      &#9680; Remove BG (ISNet)
    </button>
    <div class="btn-row">
      <button class="btn-ghost" id="btn-dr" onclick="runDryBatch()">&#128065; Dry Run</button>
      <button class="btn-stop" id="btn-cancel" onclick="cancelJob()" disabled>&#9209; Parar</button>
    </div>
  </div>

  <div class="restore">
    <div class="sec-label" style="margin-bottom:8px">Restore</div>
    <div class="restore-grid">
      <label>backup</label>
      <input type="text" id="backup-dir" value="output/upscaled/_originais">
      <button class="btn-browse" onclick="browse('backup-dir')" title="Selecionar">&#128193;</button>
      <label>para</label>
      <input type="text" id="restore-to" value="frontend/public/assets/kaelis">
      <button class="btn-browse" onclick="browse('restore-to')" title="Selecionar">&#128193;</button>
      <label>versao</label>
      <input type="number" id="restore-version" value="0" min="0">
      <span></span>
    </div>
    <div class="restore-actions">
      <button class="btn-ghost" onclick="runRestore(true)">&#128203; Ver versoes</button>
      <button class="btn-ghost" onclick="runRestore(false)">&#8617; Restaurar</button>
    </div>
  </div>

  </div><!-- /tab-process -->

  <div class="tabpane" id="tab-validate">

  <!-- ── VALIDAR / CROP (IMG-04) ── -->
  <div class="section">
    <div class="sec-label">Validar / Crop</div>
    <div class="field">
      <label>Tipo</label>
      <select id="val-type" onchange="onValTypeChange()">
        <option value="kaeli">kaeli</option>
        <option value="item">item</option>
        <option value="mob">mob</option>
        <option value="background">background</option>
        <option value="logo">logo</option>
        <option value="motion">motion</option>
      </select>
    </div>
    <div class="field">
      <label>Pasta</label>
      <div class="path-row">
        <input type="text" id="val-dir" value="output/upscaled/kaeli">
        <button class="btn-browse" onclick="browse('val-dir')" title="Selecionar">&#128193;</button>
      </div>
    </div>
    <div class="btn-row" style="margin-top:8px">
      <button class="btn-ghost" id="btn-val"      onclick="runValidate()">&#10003; Validar</button>
      <button class="btn-ghost" id="btn-crop"     onclick="runCrop(false)">&#9986; Crop</button>
      <button class="btn-ghost" id="btn-crop-dry" onclick="runCrop(true)">&#128065; Dry</button>
    </div>
    <div style="font-size:10px;color:var(--muted);line-height:1.5;margin-top:6px">
      Validar: proporção, alpha e set completo (kaelis).<br>
      Crop: recorta ao centro p/ a proporção-alvo do tipo.
    </div>
  </div>

  </div><!-- /tab-validate -->

  <div class="tabpane" id="tab-skin">

  <!-- ── SKIN VARIANT (IMG-07, experimental) ── -->
  <div class="section">
    <div class="sec-label gold">Skin Variant (IMG-07) &middot; exp.</div>
    <div class="field">
      <label>Prompt (nova roupa/cenário)</label>
      <input type="text" id="skin-prompt"
             placeholder="elegant red winter dress, snowy castle">
    </div>
    <div class="field">
      <label>Base (pasta)</label>
      <div class="path-row">
        <input type="text" id="skin-input" value="output/upscaled/kaeli">
        <button class="btn-browse" onclick="browse('skin-input')" title="Selecionar">&#128193;</button>
      </div>
    </div>
    <div class="field">
      <label>Imagem base (glob)</label>
      <input type="text" id="skin-glob" value="idle-1.png">
    </div>
    <div class="field">
      <label>Saída</label>
      <div class="path-row">
        <input type="text" id="skin-output" value="output/skins">
        <button class="btn-browse" onclick="browse('skin-output')" title="Selecionar">&#128193;</button>
      </div>
    </div>
    <div class="field">
      <label>Preservar estrutura (ControlNet)</label>
      <select id="skin-control-type">
        <option value="canny" selected>canny (core — mantém contorno)</option>
        <option value="openpose">openpose (só pose — troca roupa) [aux]</option>
        <option value="lineart">lineart (mantém contorno) [aux]</option>
        <option value="depth">depth (volume 3D) [aux]</option>
      </select>
    </div>
    <div class="field">
      <label class="toggle-row">
        <input type="checkbox" id="skin-no-ip">
        Sem IPAdapter (rig sem CLIP-Vision / smoke test)
      </label>
    </div>
    <div class="field">
      <label>Rótulo</label>
      <input type="text" id="skin-name" value="skin">
    </div>
    <div class="restore-grid" style="grid-template-columns:auto 1fr;margin-top:2px">
      <label>denoise</label>
      <input type="number" id="skin-denoise" value="0.6" min="0.2" max="1.0" step="0.05">
      <label>strength</label>
      <input type="number" id="skin-strength" value="0.7" min="0.1" max="1.5" step="0.05">
      <label>ip-weight</label>
      <input type="number" id="skin-ipw" value="0.8" min="0.1" max="1.0" step="0.05">
      <label>max-mp</label>
      <input type="number" id="skin-maxmp" value="1.0" min="0" max="8" step="0.25">
      <label>hires ×</label>
      <input type="number" id="skin-hires" value="0" min="0" max="4" step="0.5">
      <label>count</label>
      <input type="number" id="skin-count" value="2" min="1" max="8">
    </div>
    <div class="btn-row" style="margin-top:8px">
      <button class="btn-ghost" id="btn-skin"     onclick="runSkinvar(false)">&#9670; Gerar Skin</button>
      <button class="btn-ghost" id="btn-skin-dry" onclick="runSkinvar(true)">&#128065; Dry</button>
    </div>
    <div style="font-size:10px;color:var(--muted);line-height:1.5;margin-top:6px">
      Experimental: img2img + ControlNet (estrutura) + IPAdapter (rosto).<br>
      <b>canny</b> roda sem instalar nada. <b>openpose/lineart/depth</b> exigem o node
      <b>comfyui_controlnet_aux</b>. O IPAdapter exige <b>CLIP-Vision ViT-H</b> em
      models/clip_vision. Sem ele, marque "Sem IPAdapter". Veja a aba <b>Info</b>.
    </div>
  </div>

  </div><!-- /tab-skin -->

  <div class="tabpane" id="tab-info">
    <div class="section">
      <div class="sec-label gold">Como funciona</div>
      <div class="info-body">
        Fluxo do projeto: <b>ChatGPT (celular)</b> gera a imagem-base →
        cai em <code>output/inbox/&lt;tipo&gt;/&lt;slug&gt;/</code> →
        este painel faz o <b>pós-processo local grátis</b> via ComfyUI
        (<code>localhost:8188</code>) → resultado em <code>output/upscaled/</code>.
        O ComfyUI precisa estar rodando.
      </div>
    </div>

    <div class="section">
      <div class="sec-label">Aba “Pós-processo”</div>
      <div class="info-body">
        Pipeline por <b>tipo de asset</b> (kaeli/item/mob/background/logo/motion).
        Cada tipo tem um tratamento próprio (ver badges):
        <ul>
          <li><b>Upscale</b> — Real-ESRGAN 2x/4x. Aumenta a resolução; sempre primeiro.</li>
          <li><b>Face Restore</b> — FaceDetailer (Impact Pack): redesenha o rosto p/ ficar
              nítido/consistente. Opcional. Requer checkpoint + <code>face_yolov8m.pt</code>.</li>
          <li><b>Remove BG</b> — ISNet-anime (art-venture): deixa o fundo transparente.
              Roda por último.</li>
        </ul>
        “Processar por Tipo” roda a sequência certa. Botões <b>Upscale</b>/<b>Remove BG</b>
        são avulsos. <b>Force</b> reprocessa o que já existe; sem ele é idempotente.
        <b>Restore</b> recupera os originais salvos com “Backup”.
      </div>
    </div>

    <div class="section">
      <div class="sec-label">Aba “Skin Variant” (experimental)</div>
      <div class="info-body">
        Cria <b>variações de skin</b> de uma Kaeli (troca roupa/cenário) a partir do
        <code>idle-1</code>, sem pagar o GPT. Combina:
        <ul>
          <li><b>ControlNet</b> — preserva a <b>estrutura</b> da base. O tipo decide o quê:
            <ul>
              <li><b>canny</b> — copia as <b>bordas</b> (contorno). Tende a <b>manter a roupa</b>;
                  bom p/ variar só cenário/cor. Roda sem instalar nada.</li>
              <li><b>openpose</b> — copia só o <b>esqueleto/pose</b>. Libera a roupa →
                  ideal p/ <b>trocar de roupa</b>. Requer <code>comfyui_controlnet_aux</code>.</li>
              <li><b>lineart</b> — contorno fino (parecido c/ canny).</li>
              <li><b>depth</b> — mapa de <b>profundidade</b> (mantém o volume 3D do corpo).</li>
            </ul>
          </li>
          <li><b>IPAdapter</b> — injeta o <b>rosto/identidade</b> da base, p/ a personagem
              continuar reconhecível mesmo soltando a estrutura. Exige <b>CLIP-Vision ViT-H</b>.</li>
          <li><b>img2img + denoise</b> — quanto a imagem muda: <b>0.5</b> sutil, <b>0.75</b> agressivo.</li>
          <li><b>strength</b> — força do ControlNet (quão preso à estrutura).
              <b>ip-weight</b> — fidelidade ao rosto (0.5 solto, 0.9 fiel).</li>
        </ul>
        Regra de bolso p/ trocar roupa: <b>openpose</b> + denoise ~0.7 + IPAdapter.
        Sem CLIP-Vision, marque “Sem IPAdapter” (só estrutura).
      </div>
    </div>

    <div class="section">
      <div class="sec-label">Aba “Validar”</div>
      <div class="info-body">
        <b>Validar</b>: confere proporção, canal alpha (onde devia) e o set completo
        (ex. os 8 arquivos de uma Kaeli). <b>Crop</b>: recorta ao centro p/ a proporção-alvo
        do tipo. <b>Dry</b>: simula sem alterar arquivos.
      </div>
    </div>

    <div class="section">
      <div class="sec-label">Pré-requisitos</div>
      <div class="info-body">
        ComfyUI em <code>localhost:8188</code>. Modelos de upscale em
        <code>models/upscale_models</code>. Para Skin Variant: checkpoint SDXL,
        ControlNet, CLIP-Vision ViT-H e IPAdapter. Detalhes e CLI em
        <code>tools/README.md</code>.
      </div>
    </div>
  </div><!-- /tab-info -->

</div>

<!-- ── RIGHT: TERMINAL ── -->
<div class="right">
  <div class="term-head">
    <div class="term-cmd" id="term-title">pronto</div>
    <div class="prog-badge" id="prog-badge"></div>
  </div>
  <div class="prog-track" id="prog-track">
    <div class="prog-fill" id="prog-fill"></div>
  </div>
  <div id="terminal">
    <span class="l-head">Selecione um tipo de asset e clique em "Processar por Tipo", ou use os botões Upscale / Remove BG para operações manuais.</span>
  </div>
  <div class="term-foot">
    <span id="rc-badge"></span>
    <button class="btn-ghost" style="width:auto;padding:4px 12px"
            onclick="clearTerm()">Limpar</button>
  </div>
</div>
</main>

<script>
let es = null, running = false, _total = 0, _done = 0;

// ── Tipo de asset ──────────────────────────────────────────────────────────
const TYPE_CONFIGS = {
  kaeli:      { upscale: true,  removebg: true,  face_restore: true,  notes: 'idle-1/2/3 (transparente) + wallpaper/banner/thumb' },
  item:       { upscale: true,  removebg: true,  face_restore: false, notes: 'ícones 1:1 transparentes' },
  mob:        { upscale: true,  removebg: true,  face_restore: true,  notes: 'arte card/bestiary — não é sprite in-game' },
  background: { upscale: true,  removebg: false, face_restore: false, notes: 'backgrounds 16:9 / 9:16' },
  logo:       { upscale: true,  removebg: true,  face_restore: false, notes: 'logos e badges transparentes' },
  motion:     { upscale: false, removebg: false, face_restore: false, notes: 'refs/frames para cutscenes — sem pós-processo' },
};

function onTypeChange() {
  const type = document.getElementById('asset-type').value;
  const meta  = document.getElementById('type-meta');
  const notes = document.getElementById('type-notes');
  const btn   = document.getElementById('btn-batch');
  const dr    = document.getElementById('btn-dr');

  if (!type) {
    meta.style.display = 'none';
    notes.textContent  = '';
    btn.disabled = true;
    dr.textContent = '👁 Dry Run';
    return;
  }

  const cfg = TYPE_CONFIGS[type];
  meta.style.display = 'flex';

  const bup = document.getElementById('badge-up');
  const brb = document.getElementById('badge-rb');
  const bfr = document.getElementById('badge-fr');
  bup.textContent = 'upscale: ' + (cfg.upscale       ? 'sim' : 'não');
  bup.className   = 'type-badge ' + (cfg.upscale       ? 'on' : 'off');
  brb.textContent = 'removebg: ' + (cfg.removebg      ? 'sim' : 'não');
  brb.className   = 'type-badge ' + (cfg.removebg      ? 'on' : 'off');
  bfr.textContent = 'face restore: ' + (cfg.face_restore ? 'opcional' : 'n/a');
  bfr.className   = 'type-badge ' + (cfg.face_restore ? 'on' : 'off');
  notes.textContent = cfg.notes;

  // Auto-fill paths
  document.getElementById('input-dir').value  = 'output/inbox/' + type;
  document.getElementById('output-dir').value = 'output/upscaled/' + type;
  syncRestore();

  btn.disabled = false;
  dr.textContent = '👁 Dry Run (' + type + ')';
}

function onFaceRestoreToggle() {
  const on = document.getElementById('face-restore-flag').checked;
  document.getElementById('face-restore-params').style.display = on ? 'block' : 'none';
}

function setFrCkpt(val) {
  document.getElementById('face-restore-checkpoint').value = val;
  document.querySelectorAll('#fr-ckpt-chips .chip').forEach(c =>
    c.classList.toggle('active', c.textContent.trim() === val ||
      c.getAttribute('onclick') === "setFrCkpt('" + val + "')"));
}

function getBatchArgs(dryRun) {
  const type = document.getElementById('asset-type').value;
  if (!type) return null;

  const args = ['batch', '--type', type,
    '--input',  document.getElementById('input-dir').value.trim(),
    '--output', document.getElementById('output-dir').value.trim(),
    '--scale',  document.getElementById('scale').value,
  ];

  const upModel = document.getElementById('upscale-model').value.trim();
  if (upModel) args.push('--upscale-model', upModel);

  const rbgModel = document.getElementById('removebg-model').value.trim();
  if (rbgModel) args.push('--removebg-model', rbgModel);

  if (document.getElementById('face-restore-flag').checked) {
    args.push('--face-restore');
    const ckpt = document.getElementById('face-restore-checkpoint').value.trim();
    if (ckpt) args.push('--face-restore-checkpoint', ckpt);
    const bbox = document.getElementById('face-restore-bbox-model').value.trim();
    if (bbox) args.push('--face-restore-bbox-model', bbox);
    const den = document.getElementById('face-restore-denoise').value.trim();
    if (den) args.push('--face-restore-denoise', den);
    const steps = document.getElementById('face-restore-steps').value.trim();
    if (steps) args.push('--face-restore-steps', steps);
    const cfg = document.getElementById('face-restore-cfg').value.trim();
    if (cfg) args.push('--face-restore-cfg', cfg);
  }

  if (document.getElementById('backup').checked)  args.push('--backup');
  if (document.getElementById('force-flag').checked) args.push('--force');
  if (dryRun) args.push('--dry-run');

  return args;
}

function runBatch()    { const a = getBatchArgs(false); if (a && !running) startStream(a); }
function runDryBatch() {
  const type = document.getElementById('asset-type').value;
  if (!type) { run('upscale', true); return; }
  const a = getBatchArgs(true); if (a && !running) startStream(a);
}

// ── Utilitários de UI ──────────────────────────────────────────────────────
async function browse(fieldId, cb) {
  const cur = document.getElementById(fieldId).value.trim();
  const qs  = cur ? '?start=' + encodeURIComponent(cur) : '';
  try {
    const r = await fetch('/browse' + qs);
    const { path, error } = await r.json();
    if (error) { appendLine('Erro ao abrir dialog: ' + error, 'err'); return; }
    if (path) { document.getElementById(fieldId).value = path; if (typeof cb==='function') cb(); }
  } catch(e) { appendLine('Erro no seletor: ' + e, 'err'); }
}

function setGlob(val) {
  document.getElementById('glob').value = val;
  document.querySelectorAll('#glob-chips .chip').forEach(c => {
    const lbl = c.querySelector('.chip-label') || c;
    c.classList.toggle('active', lbl.textContent.trim() === val);
  });
}
function addChip() {
  const inp = document.getElementById('glob-new'), val = inp.value.trim();
  if (!val) return;
  const box = document.getElementById('glob-chips');
  for (const c of box.querySelectorAll('.chip')) {
    const lbl = c.querySelector('.chip-label');
    if ((lbl ? lbl.textContent : c.textContent).trim() === val) { setGlob(val); inp.value=''; return; }
  }
  const span = document.createElement('span'); span.className = 'chip';
  const lbl  = document.createElement('span'); lbl.className  = 'chip-label'; lbl.textContent = val;
  const del  = document.createElement('span'); del.className  = 'chip-del';   del.textContent = 'x';
  del.title='Remover'; del.onclick = e => { e.stopPropagation(); span.remove(); };
  span.onclick = () => setGlob(val);
  span.appendChild(lbl); span.appendChild(del); box.appendChild(span);
  setGlob(val); inp.value='';
}

function setUpModel(val) {
  document.getElementById('upscale-model').value = val;
  document.querySelectorAll('#up-model-chips .chip').forEach(c =>
    c.classList.toggle('active', c.dataset.val === val));
}

function syncRestore() {
  document.getElementById('backup-dir').value =
    document.getElementById('output-dir').value.trim() + '/_originais';
}

function getArgs(mode, dryRun=false) {
  const args = [mode,
    '--input',  document.getElementById('input-dir').value.trim(),
    '--glob',   document.getElementById('glob').value.trim(),
    '--output', document.getElementById('output-dir').value.trim(),
  ];
  if (mode === 'upscale') {
    args.push('--scale', document.getElementById('scale').value);
    const m = document.getElementById('upscale-model').value.trim();
    if (m) args.push('--upscale-model', m);
  } else if (mode === 'removebg') {
    const m = document.getElementById('removebg-model').value.trim();
    if (m) args.push('--model', m);
  }
  if (document.getElementById('backup').checked) args.push('--backup');
  if (dryRun) args.push('--dry-run');
  return args;
}

function run(mode, dryRun=false) { if (!running) startStream(getArgs(mode, dryRun)); }

function runRestore(listOnly) {
  if (running) return;
  const bdir = document.getElementById('backup-dir').value.trim();
  const rto  = document.getElementById('restore-to').value.trim();
  const ver  = parseInt(document.getElementById('restore-version').value) || 0;
  const args = ['restore','--backup-dir',bdir,'--restore-to',rto];
  if (listOnly)    args.push('--list');
  else if (ver)    args.push('--version', String(ver));
  startStream(args);
}

// ── Validar / Crop (IMG-04) ────────────────────────────────────────────────
function onValTypeChange() {
  const t = document.getElementById('val-type').value;
  document.getElementById('val-dir').value = 'output/upscaled/' + t;
}

function runValidate() {
  if (running) return;
  const type = document.getElementById('val-type').value;
  const dir  = document.getElementById('val-dir').value.trim();
  startStream(['validate', '--type', type, '--dir', dir], 'validate');
}

function runCrop(dryRun) {
  if (running) return;
  const type = document.getElementById('val-type').value;
  const dir  = document.getElementById('val-dir').value.trim();
  const args = ['crop', '--type', type, '--input', dir];
  if (dryRun) args.push('--dry-run');
  startStream(args, 'validate');
}

// ── Tabs ────────────────────────────────────────────────────────────────────
function showTab(name) {
  document.querySelectorAll('.tabpane').forEach(p =>
    p.classList.toggle('active', p.id === 'tab-' + name));
  document.querySelectorAll('.tabbar .tab').forEach(t =>
    t.classList.toggle('active', t.dataset.tab === name));
}

// ── Skin Variant (IMG-07) ──────────────────────────────────────────────────
function runSkinvar(dryRun) {
  if (running) return;
  const prompt = document.getElementById('skin-prompt').value.trim();
  if (!prompt) { appendLine('Informe um prompt para a skin (roupa/cenário).', 'err'); return; }
  const args = ['skinvar',
    '--prompt',        prompt,
    '--input',         document.getElementById('skin-input').value.trim(),
    '--glob',          document.getElementById('skin-glob').value.trim(),
    '--output',        document.getElementById('skin-output').value.trim(),
    '--name',          document.getElementById('skin-name').value.trim() || 'skin',
    '--control-type',  document.getElementById('skin-control-type').value,
    '--control-strength', document.getElementById('skin-strength').value,
    '--denoise',       document.getElementById('skin-denoise').value,
    '--ipadapter-weight', document.getElementById('skin-ipw').value,
    '--max-mp',        document.getElementById('skin-maxmp').value,
    '--hires',         document.getElementById('skin-hires').value,
    '--count',         document.getElementById('skin-count').value,
  ];
  if (document.getElementById('skin-no-ip').checked) args.push('--no-ipadapter');
  if (dryRun) args.push('--dry-run');
  startStream(args, 'batch');
}

async function startStream(args, script = 'batch') {
  if (running) return;
  if (es) { es.close(); es=null; }
  clearTerm(); setRunning(true); _total=0; _done=0; setProgress(0,0);
  const scriptName = script === 'validate' ? 'validate_assets.py' : 'comfyui_batch.py';
  document.getElementById('term-title').textContent = scriptName + ' ' + args.join(' ');
  appendLine('$ python tools/' + scriptName + ' ' + args.join(' '), 'cmd');

  const resp = await fetch('/run',{
    method:'POST', headers:{'Content-Type':'application/json'},
    body: JSON.stringify({args, script}),
  });
  const { job_id } = await resp.json();

  es = new EventSource('/stream/' + job_id);
  es.addEventListener('message', e => {
    const line = JSON.parse(e.data);
    appendLine(line);
    const m = line.match(/\[\s*(\d+)\/(\d+)\]/);
    if (m) { _done=+m[1]; _total=+m[2]; setProgress(_done,_total); }
  });
  es.addEventListener('done', e => {
    const { rc } = JSON.parse(e.data);
    const b = document.getElementById('rc-badge');
    b.textContent = rc===0 ? '✓ concluido' : '✗ codigo '+rc;
    b.style.color = rc===0 ? 'var(--ok)' : 'var(--err)';
    es.close(); es=null; setRunning(false, rc!==0);
    document.getElementById('term-title').textContent = rc===0 ? 'concluido' : 'erro';
    if (rc===0 && _total>0) setProgress(_total,_total);
  });
  es.addEventListener('error', () => {
    appendLine('Conexao perdida.','err'); es.close(); es=null; setRunning(false,true);
  });
}

async function cancelJob() {
  await fetch('/cancel'); appendLine('⏹ Cancelado.','warn');
}

function setProgress(done, total) {
  const badge = document.getElementById('prog-badge');
  const fill  = document.getElementById('prog-fill');
  const track = document.getElementById('prog-track');
  if (total>0) {
    badge.style.display='block'; badge.textContent=done+' / '+total;
    track.style.display='block'; fill.style.width=(done/total*100)+'%';
  } else {
    badge.style.display='none'; track.style.display='none';
  }
}

function appendLine(text, hint='') {
  const term = document.getElementById('terminal');
  const div  = document.createElement('div');
  let cls = hint;
  if (!cls) {
    if (/✓/.test(text))                        cls='ok';
    else if (/✗|ERRO|error|AUSENTE/i.test(text)) cls='err';
    else if (/SKIP|CROP\s/i.test(text))         cls='skip';
    else if (/backup|Backup/.test(text))        cls='bkp';
    else if (/\[\s*\d+\/\d+\]/.test(text))     cls='prog';
    else if (/^──/.test(text.trim()))           cls='info';
    else if (/^--/.test(text.trim()))           cls='head';
    else if (/DRY RUN/i.test(text))            cls='warn';
  }
  div.className = cls ? 'l-'+cls : '';
  div.textContent = text;
  term.appendChild(div);
  term.scrollTop = term.scrollHeight;
}

function clearTerm() {
  document.getElementById('terminal').innerHTML =
    '<span class="l-head">Execute um upscale para ver o output aqui.</span>';
  document.getElementById('rc-badge').textContent='';
  setProgress(0,0);
}

function setRunning(val, isErr=false) {
  running=val;
  const dot=document.getElementById('sdot');
  dot.className='dot'+(val?' running':isErr?' error':'');
  document.getElementById('stxt').textContent=val?'processando...':isErr?'erro':'pronto';
  document.getElementById('btn-cancel').disabled=!val;
  const type = document.getElementById('asset-type').value;
  ['btn-up','btn-rbg'].forEach(id=>{const b=document.getElementById(id);if(b)b.disabled=val;});
  const bb = document.getElementById('btn-batch');
  if (bb) bb.disabled = val || !type;
  document.getElementById('btn-dr').disabled = val;
  document.querySelectorAll('.restore-actions button').forEach(b=>b.disabled=val);
  ['btn-val','btn-crop','btn-crop-dry'].forEach(id=>{const b=document.getElementById(id);if(b)b.disabled=val;});
  ['btn-skin','btn-skin-dry'].forEach(id=>{const b=document.getElementById(id);if(b)b.disabled=val;});
  const frFlag = document.getElementById('face-restore-flag');
  if (frFlag) frFlag.disabled = val;
  ['face-restore-checkpoint','face-restore-bbox-model','face-restore-denoise',
   'face-restore-steps','face-restore-cfg'].forEach(id=>{const e=document.getElementById(id);if(e)e.disabled=val;});
}
</script>
</body>
</html>"""

# ── Entry point ────────────────────────────────────────────────────────────
def main():
    if not BATCH_SCRIPT.exists():
        print(f"ERRO: {BATCH_SCRIPT} nao encontrado.")
        sys.exit(1)

    server = _ThreadedHTTPServer(("0.0.0.0", PORT), Handler)
    url    = f"http://localhost:{PORT}"

    print(f"\n  Kaezan Tools")
    print(f"  URL    : {url}")
    print(f"  Script : {BATCH_SCRIPT.relative_to(PROJECT_ROOT)}")
    print(f"  Ctrl+C para encerrar\n")

    threading.Timer(1.2, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n  Encerrado.")

if __name__ == "__main__":
    main()

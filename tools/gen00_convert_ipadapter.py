#!/usr/bin/env python3
"""
gen00_convert_ipadapter.py — GEN-00: ip-adapter-plus_sdxl_vit-h.bin → .safetensors

Requer torch + safetensors (venv do ComfyUI/StabilityMatrix tem ambos).
Execute com o Python do ComfyUI — NÃO com o Python do sistema:

  "C:\\Kaezan\\StabilityMatrix\\Data\\Packages\\ComfyUI\\venv\\Scripts\\python.exe" ^
      tools/gen00_convert_ipadapter.py

O .safetensors é gerado NA MESMA PASTA do .bin.
O .bin original NÃO é apagado.

Por que .safetensors? O IPAdapterUnifiedLoader (preset "PLUS high strength") procura
o arquivo por extensão. Só o .safetensors é aceito — o .bin é ignorado pelo preset.

Estrutura do arquivo convertido:
  Chaves com prefixo dotted: "image_proj.proj.weight", "ip_adapter.1.to_k_ip.weight" ...
  Formato idêntico aos arquivos oficiais de hf.co/h94/IP-Adapter (sdxl_models/).
"""
from __future__ import annotations
import pathlib, sys

SRC = pathlib.Path(
    "C:/Kaezan/StabilityMatrix/Data/Models/IpAdapter"
    "/ip-adapter-plus_sdxl_vit-h.bin"
)


def _flatten(data: dict, prefix: str = "") -> dict:
    """Achata dicts aninhados para dotted-key flat (formato HuggingFace safetensors)."""
    import torch
    result: dict = {}
    for k, v in data.items():
        full = f"{prefix}.{k}" if prefix else k
        if isinstance(v, torch.Tensor):
            result[full] = v.cpu().contiguous()
        elif isinstance(v, dict):
            result.update(_flatten(v, full))
    return result


def main() -> None:
    if not SRC.exists():
        print(f"ERRO: {SRC} não encontrado.")
        print("Ajuste a constante SRC neste script se o caminho for diferente.")
        sys.exit(1)

    dst = SRC.with_suffix(".safetensors")
    if dst.exists():
        size_mb = dst.stat().st_size / 1e6
        print(f"Já existe: {dst}  ({size_mb:.1f} MB)")
        print("Apague o .safetensors se quiser regenerar.")
        sys.exit(0)

    try:
        import torch
    except ImportError:
        print("ERRO: 'torch' não disponível.")
        print("Execute com o Python do ComfyUI (que tem torch instalado):")
        print('  "venv\\Scripts\\python.exe" tools/gen00_convert_ipadapter.py')
        sys.exit(1)

    try:
        from safetensors.torch import save_file
    except ImportError:
        print("ERRO: 'safetensors' não disponível.")
        print("Execute com o Python do ComfyUI.")
        sys.exit(1)

    src_mb = SRC.stat().st_size / 1e6
    print(f"Carregando {SRC.name}  ({src_mb:.1f} MB) …")
    data = torch.load(str(SRC), map_location="cpu", weights_only=False)

    if not isinstance(data, dict):
        print(f"ERRO: formato inesperado ({type(data).__name__}). Esperado dict.")
        sys.exit(1)

    # Os .bin do IP-Adapter têm estrutura aninhada: {"image_proj": {...}, "ip_adapter": {...}}
    # Achatamos para o formato dotted que o ComfyUI_IPAdapter_plus espera no .safetensors.
    state_dict = _flatten(data)

    if not state_dict:
        print("ERRO: nenhum tensor encontrado no arquivo.")
        sys.exit(1)

    n = len(state_dict)
    print(f"  {n} tensor(s) achados  →  {dst.name}")
    save_file(state_dict, str(dst))
    dst_mb = dst.stat().st_size / 1e6
    print(f"  ✓ {dst}  ({dst_mb:.1f} MB)\n")
    print("Próximos passos:")
    print("  1. Reinicie o ComfyUI (recarrega a lista de modelos)")
    print("  2. python tools/comfyui_batch.py audit-rig")


if __name__ == "__main__":
    main()

"""
打包 mc_artmesh add-on 为 mc_artmesh.zip
在此文件所在目录运行：python build_zip.py
"""
import zipfile
import pathlib

ROOT  = pathlib.Path(__file__).parent
SRC   = ROOT / "mc_artmesh"
OUT   = ROOT / "mc_artmesh.zip"

FILES = ["__init__.py", "cube_table.py"]

with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as zf:
    for name in FILES:
        zf.write(SRC / name, f"mc_artmesh/{name}")

print(f"Done → {OUT}")

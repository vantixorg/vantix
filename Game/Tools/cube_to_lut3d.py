import sys
from pathlib import Path
import numpy as np
from PIL import Image

SCIFI = Path(r"D:/Godot/Game/assets/lut/Scifi")
RES_DIR = "res://assets/lut/Scifi"

IMPORT_TEMPLATE = """[remap]

importer="3d_texture"
type="CompressedTexture3D"

[deps]

source_file="{res_path}"

[params]

compress/mode=3
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
slices/horizontal={size}
slices/vertical=1
"""


def parse_cube(path):
    size = None
    data = []
    for line in path.read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = line.split()
        key = parts[0].upper()
        if key == "LUT_3D_SIZE":
            size = int(parts[1])
            continue
        if key in ("DOMAIN_MIN", "DOMAIN_MAX", "TITLE", "LUT_1D_SIZE"):
            continue
        if len(parts) == 3:
            try:
                data.append((float(parts[0]), float(parts[1]), float(parts[2])))
            except ValueError:
                continue
    if size is None:
        raise ValueError(f"{path.name}: no LUT_3D_SIZE")
    if len(data) != size ** 3:
        raise ValueError(f"{path.name}: expected {size**3} entries, got {len(data)}")
    return size, np.array(data, dtype=np.float32)


def build_strip(size, data):
    # .cube order: red fastest, then green, then blue -> index = r + g*size + b*size^2
    rgb = data.reshape(size, size, size, 3)  # [b][g][r]
    strip = np.zeros((size, size * size, 4), dtype=np.uint8)
    px = np.clip(np.round(rgb * 255.0), 0, 255).astype(np.uint8)
    for b in range(size):
        for g in range(size):
            for r in range(size):
                strip[g, b * size + r, 0:3] = px[b, g, r]
    strip[:, :, 3] = 255
    return strip


def main():
    cubes = sorted(SCIFI.glob("*.cube"))
    if not cubes:
        print("no .cube files found")
        return
    for cube in cubes:
        size, data = parse_cube(cube)
        strip = build_strip(size, data)
        png_path = cube.with_suffix(".png")
        Image.fromarray(strip, "RGBA").save(png_path)
        res_path = f"{RES_DIR}/{png_path.name}"
        png_path.with_suffix(".png.import").write_text(
            IMPORT_TEMPLATE.format(res_path=res_path, size=size)
        )
        print(f"{cube.name} -> {png_path.name} ({size}^3, {strip.shape[1]}x{strip.shape[0]})")
    print(f"done: {len(cubes)} LUTs")


if __name__ == "__main__":
    main()

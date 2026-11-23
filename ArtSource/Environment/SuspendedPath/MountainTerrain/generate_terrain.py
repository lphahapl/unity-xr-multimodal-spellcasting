"""Fit a Copernicus DEM to the chapter and bake the Unity terrain textures."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates, shift


TILE_URL = ("https://copernicus-dem-30m.s3.amazonaws.com/"
            "Copernicus_DSM_COG_10_N27_00_E100_00_DEM/"
            "Copernicus_DSM_COG_10_N27_00_E100_00_DEM.tif")
WEST_TILE_URL = TILE_URL.replace("E100", "E099")
CENTER = (35.6, 24.0)
EXTENT = 610
MIST = np.array([0.61, 0.69, 0.72], dtype=np.float32)


def smooth(value):
    u = np.clip(value, 0, 1)
    return u * u * (3 - 2 * u)


def mix(a, b, weight):
    return a + (b - a) * weight


def read_tile(path, longitude):
    with Image.open(path) as image:
        assert image.size == (3600, 3600) and image.mode == "F"
        assert image.tag_v2[33922][3:5] == (longitude, 28)
        return np.asarray(image).copy()


def slice_tile(tile, args, radius):
    resolution = 10
    axis = np.arange(-radius, radius + 1, resolution, dtype=np.float32)
    x, z = np.meshgrid(axis, axis)
    angle = np.radians(args.bearing)
    east = (x * np.cos(angle) + z * np.sin(angle)) / args.horizontal_scale
    north = (-x * np.sin(angle) + z * np.cos(angle)) / args.horizontal_scale
    lon = args.longitude + east / (111320 * np.cos(np.radians(args.latitude)))
    lat = args.latitude + north / 110820
    rows, cols = (28 - lat) * 3600 - .5, (lon - 99) * 3600 - .5
    if not (rows.min() >= 0 and cols.min() >= 0
            and rows.max() < tile.shape[0]-1 and cols.max() < tile.shape[1]-1):
        raise ValueError("DEM crop crosses its source tile. Adjust center, scale or crop radius.")
    source = map_coordinates(tile, (rows, cols), order=1)
    return axis, source


def hydraulic(height, args):
    """Conservative local water/sediment transport on a closed four-neighbor grid."""
    bed = height.copy()
    water = np.zeros_like(bed)
    sediment = np.zeros_like(bed)
    directions = ((-1, 0), (1, 0), (0, -1), (0, 1))
    opposite = (1, 0, 3, 2)
    for _ in range(args.steps):
        water += args.rain
        surface = bed + water
        downhill = np.stack([
            np.maximum(surface - shift(surface, d, order=0, mode="nearest"), 0)
            for d in directions
        ])
        slope = np.max(downhill, axis=0) / 10
        outgoing = downhill * np.minimum(.14, water / np.maximum(downhill.sum(axis=0), 1e-6))
        speed = outgoing.sum(axis=0)
        capacity = args.capacity * speed * np.minimum(slope, 3)
        cut = np.minimum(np.maximum(capacity - sediment, 0) * args.erodibility,
                         np.maximum(bed - height + args.max_cut, 0))
        settle = np.maximum(sediment - capacity, 0) * args.deposition
        bed += settle - cut
        sediment += cut - settle
        payload = outgoing * (sediment / np.maximum(water, 1e-6))
        for i, d in enumerate(directions):
            water += shift(outgoing[opposite[i]], d, order=0, mode="constant") - outgoing[i]
            sediment += shift(payload[opposite[i]], d, order=0, mode="constant") - payload[i]
        water = np.maximum(water, 0) * (1 - args.evaporation)
        sediment = np.maximum(sediment, 0)
    return bed + sediment


def protected_height(x, z, raw):
    # The authored cliffs, bridge and platforms occupy the central footprint.
    distance = ((x / 105)**4 + ((z - 5) / 98)**4)**.25
    underfill = -112 + 8 * np.sin(x * .018 + z * .023)
    return mix(np.minimum(raw, underfill), raw, smooth((distance - 1) / .64))


def sample(grid, axis, x, z, order=1):
    return map_coordinates(grid, ((z - axis[0]) / 10, (x - axis[0]) / 10),
                           order=order, mode="nearest")


def ground_noise(x, z, spacing, seed):
    u, v = x / spacing, z / spacing
    a, b = np.floor(u), np.floor(v)
    i, j = smooth(u-a), smooth(v-b)

    def lattice(x_cell, z_cell):
        h = np.sin(x_cell*127.1 + z_cell*311.7 + seed) * 43758.5453
        return 2*(h-np.floor(h))-1

    return mix(mix(lattice(a, b), lattice(a+1, b), i),
               mix(lattice(a, b+1), lattice(a+1, b+1), i), j)


def colorize(x, z, h, dx, dz):
    slope = np.hypot(dx, dz)
    macro = ground_noise(x, z, 71, 4.2)
    detail = macro + .46*ground_noise(x, z, 16, 61.1) + .28*ground_noise(x, z, 3.6, 0.8)
    strata = np.sin(h*.41 + x*.066 - z*.046 + 2.2*macro)
    rock = smooth((slope-.47+.26*detail) / .82)
    green = np.array([.245, .30, .215])
    stone = np.array([.49, .459, .405])
    surface = mix(green, stone, rock[..., None])
    surface *= (1 + .145 * detail + .052 * strata * rock)[..., None]
    light = np.clip((1 - .62*dx + .45*dz) / np.sqrt(1 + slope*slope), .12, 1.26)
    return surface * (.57 + .43*light[..., None])


def save_rgb(path, rgb):
    Image.fromarray(np.uint8(np.clip(rgb, 0, 1) * 255 + .5)).save(path)


def bake_panorama(output, bed, axis, dx, dz):
    width, height = 4096, 2048
    lon = (.5 - (np.arange(width, dtype=np.float32) + .5)/width) * 2*np.pi
    lat = (.5 - (np.arange(height, dtype=np.float32) + .5)/height) * np.pi
    xx, zz = np.cos(lon), np.sin(lon)
    up = np.sin(lat)[:, None]
    sky = mix(MIST, np.array([.38, .55, .67]), smooth(up*1.32)[..., None])
    # Layered, periodic cloud structure in spherical coordinates.
    band = (np.sin(2*lon+9*up) + .54*np.sin(5*lon-23*up) +
            .31*np.sin(11*lon+41*up) + .14*np.sin(19*lon-64*up))
    clouds = smooth((band-.09)*.66) * np.exp(-((up-.27)/.34)**2) * smooth(up*9)
    canvas = mix(sky, np.array([.82, .84, .82]), .56 * clouds[..., None]).astype(np.float32)
    previous = np.full(width, -1, dtype=np.int32)
    first = previous.copy()
    covered = np.zeros((height, width), dtype=bool)
    # Project successive radial segments into angular intervals, front to back.
    # Angles below the live-mesh seam are carried by haze, not a stretched sample.
    for radius in np.arange(620, 2475, 10):
        x, z = radius * xx, radius * zz
        h = sample(bed, axis, x, z)
        limit = np.clip(np.floor((.5 - np.arctan2(h-5, radius)/np.pi)*height),
                        0, height-1).astype(np.int32)
        valid = np.maximum(abs(x), abs(z)) >= 557
        entering = valid & (first < 0)
        first[entering] = limit[entering]
        active = valid & (previous >= 0)
        start = np.minimum(limit, previous)
        stop = np.maximum(limit, previous)
        surface = colorize(x, z, h, sample(dx, axis, x, z), sample(dz, axis, x, z))
        tint = MIST + np.array([-.047, -.023, .001])
        surface = mix(surface, tint, 1 - np.exp(-radius / 670))
        if np.any(active):
            used = np.flatnonzero(active)
            rows = np.arange(start[used].min(), stop[used].max()+1)[:, None]
            selected = ((rows >= start[used]) & (rows <= stop[used]) & ~covered[rows, used])
            select_y, select_x = np.where(selected)
            yi, xi = rows[select_y, 0], used[select_x]
            alpha = 1 - smooth((yi-first[xi]+15) / 87)
            canvas[yi, xi] = mix(canvas[yi, xi], surface[xi], alpha[:, None])
            covered[yi, xi] = True
        previous[valid] = limit[valid]
    save_rgb(output, canvas)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tile", type=Path)
    parser.add_argument("west_tile", type=Path)
    parser.add_argument("data", type=Path)
    parser.add_argument("assets", type=Path)
    parser.add_argument("--longitude", type=float, default=100.132)
    parser.add_argument("--latitude", type=float, default=27.199)
    parser.add_argument("--bearing", type=float, default=35)
    parser.add_argument("--horizontal-scale", type=float, default=.225)
    parser.add_argument("--vertical-scale", type=float, default=.16)
    parser.add_argument("--datum", type=float, default=1730)
    parser.add_argument("--steps", type=int, default=85)
    parser.add_argument("--rain", type=float, default=.07)
    parser.add_argument("--capacity", type=float, default=1.35)
    parser.add_argument("--erodibility", type=float, default=.38)
    parser.add_argument("--deposition", type=float, default=.27)
    parser.add_argument("--evaporation", type=float, default=.055)
    parser.add_argument("--max-cut", type=float, default=13)
    args = parser.parse_args()
    if args.horizontal_scale <= 0 or args.vertical_scale <= 0 or args.steps < 0:
        parser.error("Scales must be positive and steps nonnegative")
    if any(not 0 <= value <= 1 for value in (args.erodibility, args.deposition, args.evaporation)):
        parser.error("Erodibility, deposition and evaporation must be between 0 and 1")
    if min(args.rain, args.capacity, args.max_cut) < 0:
        parser.error("Rain, capacity and max cut must be nonnegative")
    args.data.mkdir(parents=True, exist_ok=True)
    args.assets.mkdir(parents=True, exist_ok=True)
    tiles = np.concatenate((read_tile(args.west_tile, 99), read_tile(args.tile, 100)), axis=1)
    axis, source = slice_tile(tiles, args, 2490)
    raw = (source - args.datum) * args.vertical_scale - 96
    eroded = hydraulic(raw, args)
    z, x = np.meshgrid(axis, axis, indexing="ij")
    bed = protected_height(x, z, eroded)
    dz, dx = np.gradient(bed, 10)
    np.savez_compressed(args.data / "GorgeHeights.npz", axis=axis,
                        source_elevation_m=source, before_m=raw, after_m=bed,
                        hydraulic_delta_m=eroded-raw)

    raw_axis = np.linspace(-EXTENT, EXTENT, 513, dtype=np.float32)
    rz, rx = np.meshgrid(raw_axis, raw_axis, indexing="ij")
    height = protected_height(rx, rz, sample(eroded, axis, rx, rz, order=3))
    minimum, maximum = float(height.min()), float(height.max())
    np.rint((height-minimum)/(maximum-minimum)*65535).astype("<u2").tofile(
        args.data / "GorgeCenter_513.r16")
    # Keep the inner relief detailed and reduce geometric density with distance.
    positive = np.r_[np.arange(0, 211, 5), np.arange(222, 431, 12),
                     np.arange(454, 609, 26), EXTENT].astype(np.float32)
    mesh_axis = np.r_[-positive[:0:-1], positive]
    mz, mx = np.meshgrid(mesh_axis, mesh_axis, indexing="ij")
    mesh_y = protected_height(mx, mz, sample(eroded, axis, mx, mz, order=3))
    np.savez(args.data / "MeshSampling.npz", axis=mesh_axis, height=mesh_y)
    corner_indices = ((0, 0), (0, -1), (-1, 0), (-1, -1))
    (args.data / "corners.txt").write_text(
        "".join(f"{mesh_y[row, col]}\n" for row, col in corner_indices),
        encoding="ascii")
    texture_axis = (np.arange(2048, dtype=np.float32) + .5) / 2048 * (2*EXTENT) - EXTENT
    tz, tx = np.meshgrid(texture_axis[::-1], texture_axis, indexing="ij")
    near = colorize(tx, tz, sample(bed, axis, tx, tz),
                    sample(dx, axis, tx, tz), sample(dz, axis, tx, tz))
    save_rgb(args.assets / "GorgeRelief_BaseColor.png", near)
    bake_panorama(args.assets / "GorgeSkyline.png", bed, axis, dx, dz)
    assert np.all(np.isfinite(bed))
    assert np.max(bed[(abs(x)<95) & (abs(z-5)<83) &
                      (((x/105)**4 + ((z-5)/98)**4) <= 1)]) <= -104
    config = {k: v for k, v in vars(args).items() if not isinstance(v, Path)}
    sources = {}
    for url, path in ((TILE_URL, args.tile), (WEST_TILE_URL, args.west_tile)):
        with path.open("rb") as stream:
            sources[url] = hashlib.file_digest(stream, "sha256").hexdigest()
    config.update(sources=sources, center_xz_m=CENTER,
        raw_layout="uint16 little endian, 513x513, columns +X, rows +Z from south",
        raw_size_xz_m=[2*EXTENT, 2*EXTENT], raw_height_min_m=minimum,
        raw_height_max_m=maximum, source_extent_game_m=[-2490,2490],
        delta_m=[float((eroded-raw).min()), float((eroded-raw).max())],
        delta_abs_mean_m=float(np.abs(eroded-raw).mean()),
        grid_samples_per_axis=len(mesh_axis), triangles=2*(len(mesh_axis)-1)**2)
    (args.data / "terrain.json").write_text(json.dumps(config, indent=2)+"\n", encoding="utf-8")
    print(json.dumps(config, indent=2))


if __name__ == "__main__":
    main()

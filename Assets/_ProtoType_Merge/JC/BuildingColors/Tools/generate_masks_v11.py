"""High001: reserve wall paint for facade; protect antenna and entrance props.

Run after v10 with Python -B. Changes face roles only, keeping palette masks,
source references and user target colors intact. Repeated runs are harmless.
"""
from pathlib import Path
import json, sys
import numpy as np
from PIL import Image
import generate_masks_v8 as v8

ROOT = v8.ROOT


def main():
    out = Path(sys.argv[1])
    row = next(r for r in json.loads((ROOT / 'Data/inventory_v10.json').read_text(encoding='utf8'))['bindings'] if r['name'] == 'BGHigh001')
    key = row['key']
    p, u, t = v8.v3.geo.load(key)[0]
    roles = v8.v7.roles_file(key)[0]
    before = roles.copy()
    groups = v8.v3.geo.components(p, t)
    src = np.asarray(Image.open(v8.v3.v2.PROJECT / row['source']).convert('RGB'))
    uv = u[t].mean(1)
    xy = np.clip(np.c_[uv[:, 0], 1-uv[:, 1]] * 256, 0, 255).astype(int)
    rgb = src[xy[:, 1], xy[:, 0]]
    # Group 35 is a detached beige facade sliver beside the upper glazing band.
    shell = np.isin(np.arange(len(t)), np.concatenate([groups[0], groups[1], groups[35]]))
    facade = shell & (np.all(rgb == [227, 212, 189], axis=1) | np.all(rgb == [228, 200, 155], axis=1))
    # Entrance assembly, white window frames and antenna are not facade paint.
    roles[(roles == 0) & ~facade] = 15
    v = p[t]
    # Keep the low olive mounting plate (24 triangles) as roof trim. Everything
    # raised above it is the gray plinth, shaft or yellow antenna cap.
    antenna = (v[:, :, 1].min(1) > 3.5033) & (v[:, :, 1].max(1) > 3.504)
    roles[antenna] = 15
    assert int((roles == 0).sum()) == 598
    assert np.all(roles[groups[3]] == 15)
    assert np.all(roles[antenna] == 15)
    v8.v7.write_roles(key, [roles])
    report = dict(wallFaces=int((roles == 0).sum()), removedWallFaces=int(((before == 0) & (roles == 15)).sum()), removedRoofFaces=int(((before == 1) & (roles == 15)).sum()), antennaFaces=int(antenna.sum()), changedFaces=int((before != roles).sum()))
    (out / 'high1_wall_report.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()

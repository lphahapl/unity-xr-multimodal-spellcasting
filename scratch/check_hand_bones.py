import struct, zlib

def parse_fbx_74(filename):
    with open(filename, 'rb') as f:
        buf = f.read()
    
    def read_node(p):
        if p + 25 > len(buf): return None, len(buf)
        end_offset, num_props, prop_len, name_len = struct.unpack('<QQQB', buf[p:p+25])
        if end_offset == 0: return None, p + 25
        p += 25
        name = buf[p:p+name_len].decode('ascii', 'ignore')
        p += name_len
        props = []
        for _ in range(num_props):
            ptype = chr(buf[p]); p += 1
            if ptype == 'Y': val = struct.unpack('<h', buf[p:p+2])[0]; p += 2
            elif ptype == 'C': val = struct.unpack('<?', buf[p:p+1])[0]; p += 1
            elif ptype == 'I': val = struct.unpack('<i', buf[p:p+4])[0]; p += 4
            elif ptype == 'F': val = struct.unpack('<f', buf[p:p+4])[0]; p += 4
            elif ptype == 'D': val = struct.unpack('<d', buf[p:p+8])[0]; p += 8
            elif ptype == 'L': val = struct.unpack('<q', buf[p:p+8])[0]; p += 8
            elif ptype in ('f', 'd', 'l', 'i', 'b'):
                arr_len, enc, comp_len = struct.unpack('<III', buf[p:p+12]); p += 12
                raw = buf[p:p+comp_len]; p += comp_len
                if enc == 1: raw = zlib.decompress(raw)
                val = (ptype, arr_len, raw)
            elif ptype in ('S', 'R'):
                slen = struct.unpack('<I', buf[p:p+4])[0]; p += 4
                val = buf[p:p+slen]; p += slen
            else: break
            props.append(val)
        
        children = []
        while p < end_offset:
            child, p = read_node(p)
            if child: children.append(child)
            else: break
        return (name, props, children), end_offset

    root_nodes = []
    p = 27
    while p < len(buf) - 50:
        n, p = read_node(p)
        if n: root_nodes.append(n)
        else: break
    return root_nodes

def analyze_fbx(path):
    print("="*60)
    print("Analyzing:", path)
    nodes = parse_fbx_74(path)
    
    models = {}
    connections = []
    
    for n in nodes:
        if n[0] == 'Objects':
            for c in n[2]:
                if c[0] == 'Model':
                    mid = c[1][0]
                    mname = c[1][1].decode('ascii', 'ignore')
                    trans = [0.0, 0.0, 0.0]
                    for mc in c[2]:
                        if mc[0] == 'Properties70':
                            for p70 in mc[2]:
                                if p70[1] and p70[1][0] == b'Lcl Translation':
                                    trans = [p70[1][4], p70[1][5], p70[1][6]]
                    models[mid] = {'name': mname, 'trans': trans, 'children': []}
                elif c[0] == 'Geometry':
                    for gc in c[2]:
                        if gc[0] == 'Vertices':
                            raw = gc[1][0][2]
                            coords = struct.unpack(f'<{len(raw)//8}d', raw)
                            xs, ys, zs = coords[0::3], coords[1::3], coords[2::3]
                            print(f"Mesh Vertices: {len(xs)}")
                            print(f"X bounds: min={min(xs):.2f}, max={max(xs):.2f}, size={max(xs)-min(xs):.2f}")
                            print(f"Y bounds: min={min(ys):.2f}, max={max(ys):.2f}, size={max(ys)-min(ys):.2f}")
                            print(f"Z bounds: min={min(zs):.2f}, max={max(zs):.2f}, size={max(zs)-min(zs):.2f}")
        elif n[0] == 'Connections':
            for c in n[2]:
                # OO: Object to Object connection (child, parent)
                if c[1] and c[1][0] == b'OO':
                    child_id = c[1][1]
                    parent_id = c[1][2]
                    connections.append((child_id, parent_id))
                    
    # build hierarchy
    for child_id, parent_id in connections:
        if child_id in models and parent_id in models:
            models[parent_id]['children'].append(child_id)
            
    print(f"Total models found: {len(models)}")
    for mid, m in sorted(models.items(), key=lambda x: x[1]['name']):
        t = m['trans']
        print(f"  {m['name']:30s} trans=[{t[0]:.2f}, {t[1]:.2f}, {t[2]:.2f}]")

analyze_fbx(r'Library\PackageCache\com.meta.xr.sdk.core@4c2eac90db2d\Meshes\HandTracking\OculusHand_L.fbx')
analyze_fbx(r'Library\PackageCache\com.meta.xr.sdk.core@4c2eac90db2d\Meshes\HandTracking\OpenXRLeftHand.fbx')

"""Capture the procedural facts actually needed to position a source with an unknown registration."""
import numpy as np
from math import pi

def horizontal_registration(verts):
    # Fit a stable 2D minimum box; percentiles reject stray submillimetre scan grit.
    subset=verts[::max(1,len(verts)//25000),:2]
    best=None
    for angle in np.linspace(0,np.pi*.5,181,endpoint=True):
        c,s=np.cos(angle),np.sin(angle);R=np.array([[c,-s],[s,c]])
        q=subset@R
        size=np.percentile(q,99.8,axis=0)-np.percentile(q,.2,axis=0)
        area=float(np.prod(size))
        if best is None or area<best[0]:best=(area,angle,size)
    _,angle,size=best
    if size[0]<size[1]:angle+=np.pi/2
    return float(angle)

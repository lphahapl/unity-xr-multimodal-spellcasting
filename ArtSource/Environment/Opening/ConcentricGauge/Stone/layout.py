"""Stone UV0 atlas charts. Rects are pixels from the top left, with external gutters."""
from pathlib import Path

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[4]
EXPORT = PROJECT / "Assets/Art/Environment/Opening/ConcentricGauge/Stone"
SIZE = 2048
# x, y, width, height, represented width and height in metres
CHARTS = {
    "stele": (8, 8, 842, 1508, .734, 1.33),
    "tabletop": (874, 8, 1166, 724, 1.25, .78),
    "apron": (874, 752, 1166, 202, 1.01, .202),
    "post": (874, 974, 328, 694, .289, .611),
    "field": (1222, 974, 818, 1066, .84, 1.10),
    "socle": (8, 1536, 842, 244, .82, .223),
    "fascia": (8, 1800, 842, 240, 1.25, .35),
    "arris": (874, 1688, 328, 352, .32, .35),
}


# -*- mode: python ; coding: utf-8 -*-
import os

_PROJECT = os.path.dirname(os.path.abspath(SPEC))
_PACKAGE_TOOL = os.path.join(_PROJECT, "packageTool")
distpath = os.path.join(_PACKAGE_TOOL, "dist")
workpath = os.path.join(_PACKAGE_TOOL, "build")

a = Analysis(
    [os.path.join(_PROJECT, "packageTool", "sync_packages.py")],
    pathex=[_PROJECT],
    binaries=[],
    datas=[(os.path.join(_PROJECT, "packageTool", "required-packages.json"), ".")],
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name="sync_packages",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

"""Binary worker contract checks. No COM registration or Explorer changes."""
import pathlib
import struct
import subprocess
import sys
import tempfile
import zipfile

repo = pathlib.Path(__file__).resolve().parents[1]
worker = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else repo / "artifacts/isolated/worker/ApkShellext2.Worker.exe"


def run(*args):
    return subprocess.run([str(worker), *map(str, args)], capture_output=True, timeout=8)


def image(result, size):
    assert result.returncode == 0, result.stderr
    width, height = struct.unpack_from("<II", result.stdout)
    assert 1 <= width <= size and 1 <= height <= size
    assert len(result.stdout) == 8 + width * height * 4
    pixels = result.stdout[8:]
    for offset in range(0, len(pixels), 4):
        b, g, r, a = pixels[offset:offset + 4]
        assert max(b, g, r) <= a, "Pixels are not premultiplied BGRA"
    assert any(pixels[3::4]), "Default icon is completely transparent"


with tempfile.TemporaryDirectory(prefix="apkshellext-worker-") as directory:
    root = pathlib.Path(directory)
    broken = root / "broken space 中文.apk"
    broken.write_bytes(b"invalid archive")
    empty = root / "empty.apk"
    with zipfile.ZipFile(empty, "w"):
        pass
    for path in (broken, empty):
        for size in (1, 16, 64, 256, 1024):
            image(run("--icon", size, path), size)
        info = run("--info", path)
        assert info.returncode == 0, info.stderr
        count, = struct.unpack_from("<I", info.stdout)
        assert 0 < count <= 32768
        assert len(info.stdout) == 4 + count * 2
        assert info.stdout[4:].decode("utf-16-le")
        thumbnail = run("--thumbnail", 64, path)
        assert thumbnail.returncode != 0 and thumbnail.stdout == b""
    for args in ((), ("--icon", "0", broken), ("--icon", "1025", broken),
                 ("--icon", "-1", broken), ("--icon", "abc", broken),
                 ("--unknown",), ("--associations", "invalid")):
        result = run(*args)
        assert result.returncode != 0 and result.stdout == b"", args
    appx = root / "valid.appx"
    with zipfile.ZipFile(appx, "w") as archive:
        archive.writestr("AppxManifest.xml", '<Package><Identity Name="Worker.Fixture" Version="1.2.3.4" Publisher="CN=Fixture"/><Properties><DisplayName>Worker Fixture</DisplayName><Logo>logo.png</Logo></Properties></Package>')
    info = run("--info", appx)
    assert info.returncode == 0, info.stderr
    count, = struct.unpack_from("<I", info.stdout)
    assert len(info.stdout) == 4 + count * 2
    assert "Worker.Fixture" in info.stdout[4:].decode("utf-16-le")
print("PASS worker: bounded image protocol, PArgb, invalid/empty APK fallback, UTF-16 info, valid APPX metadata, thumbnail failure, invalid arguments")

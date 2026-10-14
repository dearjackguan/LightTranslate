"""Verify real offline models, preserved lines, cold latency and child memory."""

import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import subprocess
import time


ENGINE = Path(r"A:\Agent\Codex\Translation\app\engine")
REPORT = Path(r"A:\Agent\Codex\Translation\verification\offline-runtime-results.json")
NETWORK_DENIED = """import sys,runpy
def deny_network(event,args):
    if event.startswith('socket.') or event.startswith('urllib.'):
        raise RuntimeError('NETWORK_DISABLED_IN_VERIFICATION')
sys.addaudithook(deny_network)
runpy.run_path(sys.argv[1],run_name='__main__')
"""


class MemoryCounters(ctypes.Structure):
    _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD),
                ("PeakWorkingSetSize", ctypes.c_size_t), ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t), ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t), ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t), ("PeakPagefileUsage", ctypes.c_size_t)]


def run_case(name, text, source, target, denied=False):
    arguments = [str(ENGINE / "python" / "python.exe"), "-I", "-B"]
    if denied:
        arguments += ["-c", NETWORK_DENIED]
    arguments.append(str(ENGINE / "translation-worker.py"))
    environment = dict(os.environ)
    environment.update(TEMP=str(REPORT.parent / "runtime-temp"), TMP=str(REPORT.parent / "runtime-temp"))
    start = time.perf_counter()
    with subprocess.Popen(arguments, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, env=environment) as child:
        child.stdin.write(json.dumps(dict(text=text, source=source, target=target)).encode("ascii"))
        child.stdin.close()
        peak = 0
        memory_info = ctypes.WinDLL("psapi").GetProcessMemoryInfo
        memory_info.argtypes = [wintypes.HANDLE, ctypes.POINTER(MemoryCounters), wintypes.DWORD]
        while child.poll() is None:
            counters = MemoryCounters()
            counters.cb = ctypes.sizeof(counters)
            if memory_info(int(child._handle), ctypes.byref(counters), counters.cb):
                peak = max(peak, counters.PeakWorkingSetSize)
            if time.perf_counter() - start > 120:
                child.kill()
                raise TimeoutError(name)
            time.sleep(0.01)
        response = json.loads(child.stdout.read().decode("utf-8"))
        child.stderr.read()
    assert response["ok"], response
    output = response["text"]
    assert output.count("\n") == text.count("\n"), "Paragraph boundaries changed."
    if source == "en":
        assert "世界" in output and "123" in output, "Meaning or number changed."
    else:
        assert "window" in output.lower(), "Expected meaning is absent."
    return dict(name=name, source=source, target=target, passed=True, output=output,
                seconds=round(time.perf_counter() - start, 3), peakChildWorkingSetBytes=peak,
                pythonNetworkApisDenied=denied)


def main():
    cases = [run_case("english-chinese-network-denied", "Hello world 123", "en", "zh", True),
             run_case("chinese-english-network-denied", "请打开翻译窗口。", "zh", "en", True),
             run_case("long-multiline-english-chinese", "\n".join(["Hello world 123."] * 100), "en", "zh"),
             run_case("long-multiline-chinese-english", "\n".join(["请打开翻译窗口。"] * 100), "zh", "en")]
    report = dict(results=cases, scope="Actual CPU inference. Python socket/urllib events denied for network cases; native sockets are not intercepted; global connectivity is unchanged.")
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    for case in cases:
        print("PASS", case["name"], "seconds=" + str(case["seconds"]),
              "peakMiB=" + str(round(case["peakChildWorkingSetBytes"] / 1048576, 1)))


if __name__ == "__main__":
    main()

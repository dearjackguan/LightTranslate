param([string]$SourceDirectory = 'A:\Agent\Codex\Translation\app')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = [IO.Path]::GetFullPath($SourceDirectory)
foreach ($relative in @('engine\python\python.exe', 'engine\models\en_zh\model\model.bin', 'engine\models\zh_en\model\model.bin',
    'ocr\RapidOCR-json.exe', 'ocr\models\ch_PP-OCRv4_det_infer.onnx', 'ocr\models\rec_ch_PP-OCRv4_infer.onnx',
    'ocr\models\ch_ppocr_mobile_v2.0_cls_infer.onnx', 'ocr\models\dict_chinese.txt', 'ocr\provenance.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $relative))) { throw "Incomplete offline package: $relative" }
}
foreach ($name in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $source -Force
}
$env:LIGHTTRANSLATE_PACKAGE_SOURCE = $source
@'
from pathlib import Path
import hashlib,json,os,zipfile
source=Path(os.environ['LIGHTTRANSLATE_PACKAGE_SOURCE'])
release=Path(r'A:\Agent\Codex\Translation\release')
release.mkdir(parents=True,exist_ok=True)
archive=release/'LightTranslate-1.2.1-win-x64.zip'
files=[]
for path in sorted(source.rglob('*')):
    if not path.is_file(): continue
    relative=path.relative_to(source)
    if any(part in {'temp','__pycache__','stanza'} for part in relative.parts) or path.suffix=='.pyc': continue
    files.append((path,relative))
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as package:
    for path,relative in files:
        package.write(path,Path('LightTranslate')/relative)
with zipfile.ZipFile(archive,'r') as package:
    bad=package.testzip()
    if bad: raise RuntimeError('Archive CRC validation failed: '+bad)
with archive.open('rb') as stream:
    digest=hashlib.file_digest(stream,'sha256').hexdigest()
report=dict(archive=str(archive),sha256=digest,files=len(files),archiveBytes=archive.stat().st_size,
            extractedBytes=sum(p.stat().st_size for p,_ in files))
(release/'package-results.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
'@ | python -I -B -
if ($LASTEXITCODE -ne 0) { throw 'Portable package creation failed.' }

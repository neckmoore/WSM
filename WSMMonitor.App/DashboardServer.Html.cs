namespace WSMMonitor;

public sealed partial class DashboardServer
{
    private const string HtmlPage = """
<!DOCTYPE html>
<html lang="{{WSM_UI_LANG}}">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<meta http-equiv="Cache-Control" content="no-store"/>
<title>WSM Monitor</title>
<link rel="icon" href="/favicon.ico" type="image/x-icon"/>
<style>
/* Dark dashboard — compact, high contrast */
:root{
  --bg:#0c0f14;
  --surface:#141922;
  --surface2:#1c2430;
  --border:rgba(255,255,255,.08);
  --border2:rgba(255,255,255,.12);
  --text:#e8eaef;
  --muted:#8b95a5;
  --accent:#3b82f6;
  --accent-dim:rgba(59,130,246,.35);
  --ok:#34d399;
  --warn:#fbbf24;
  --crit:#f87171;
  --chart-bg:#0f141c;
  --radius:12px;
  --radius-sm:8px;
  --shadow:0 8px 32px rgba(0,0,0,.45);
}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--text);font-family:system-ui,"Segoe UI",Roboto,sans-serif;font-size:13px;line-height:1.45;-webkit-font-smoothing:antialiased}
.zb-header{background:linear-gradient(180deg,#151b26 0%,#0f131a 100%);border-bottom:1px solid var(--border);color:var(--text)}
.zb-header-inner{max-width:1600px;margin:0 auto;padding:14px 20px;display:flex;align-items:center;gap:12px;flex-wrap:wrap}
.zb-logo{font-size:17px;font-weight:650;letter-spacing:-.02em}
.zb-breadcrumb{font-size:12px;color:var(--muted);border-left:1px solid var(--border2);padding-left:14px}
.zb-toolbar{background:var(--surface);border-bottom:1px solid var(--border);padding:10px 20px;display:flex;flex-wrap:wrap;align-items:center;gap:8px}
.zb-page{max-width:1600px;margin:0 auto;padding:16px 20px 28px}
.card--interactive{cursor:pointer;transition:transform .12s,box-shadow .15s,border-color .15s;border-color:var(--border2)!important}
.card--interactive:hover{box-shadow:0 0 0 1px var(--accent-dim),var(--shadow);transform:translateY(-1px)}
.card--interactive:active{transform:translateY(0)}
.hs-hint{font-size:12px;color:var(--muted);margin:0 0 14px;line-height:1.55}
.hs-section{margin:16px 0 10px;font-size:11px;font-weight:650;text-transform:uppercase;letter-spacing:.06em}
.hs-section.bad{color:var(--crit)}
.hs-section.warn{color:var(--warn)}
.hs-section.good{color:var(--ok)}
.hs-row{border:1px solid var(--border);border-radius:var(--radius-sm);padding:10px 12px;margin-bottom:8px;border-left-width:3px;background:var(--surface2)}
.hs-row .hs-cat{font-weight:650;font-size:13px;margin-bottom:4px}
.hs-row .hs-detail{font-size:12px;color:var(--text);line-height:1.5;opacity:.92}
.hs-bad{border-left-color:var(--crit);background:rgba(248,113,113,.08)}
.hs-warning{border-left-color:var(--warn);background:rgba(251,191,36,.08)}
.hs-good{border-left-color:var(--ok);background:rgba(52,211,153,.08)}
.tmb,.fbtn{padding:6px 14px;border:1px solid var(--border2);background:var(--surface2);border-radius:999px;color:var(--text);cursor:pointer;font-size:12px;font-weight:500;font-family:inherit;transition:background .12s,border-color .12s}
.tmb:hover,.fbtn:hover{background:#252f3d;border-color:rgba(255,255,255,.18)}
.tmb.on,.fbtn.on{background:var(--accent);border-color:var(--accent);color:#fff}
.tmb.on:hover,.fbtn.on:hover{filter:brightness(1.08)}
.badge{padding:4px 10px;border:1px solid var(--border);background:var(--surface2);border-radius:999px;font-size:11px;color:var(--muted);font-family:ui-monospace,Consolas,monospace}
.kpi{display:grid;gap:12px;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));margin-bottom:14px}
.card{background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);padding:14px 16px;box-shadow:0 2px 12px rgba(0,0,0,.2)}
.card h3{margin:0 0 10px;color:var(--muted);font-weight:600;font-size:11px;text-transform:uppercase;letter-spacing:.07em;border-bottom:none;padding-bottom:0}
.metric{font-size:28px;font-weight:700;line-height:1.1;font-family:ui-monospace,Consolas,monospace;letter-spacing:-.02em}
.sub{margin-top:8px;color:var(--muted);font-size:12px}
.ok{color:var(--ok)}.warn{color:var(--warn)}.crit{color:var(--crit)}
.grid{display:grid;gap:12px;grid-template-columns:repeat(3,minmax(240px,1fr));margin-bottom:14px}
.chartWrap{height:180px;position:relative;background:var(--chart-bg);border:1px solid var(--border);border-radius:var(--radius-sm);margin-top:6px;overflow:hidden;cursor:crosshair}
.chartLbl{position:absolute;right:8px;bottom:6px;font-size:10px;color:var(--muted);z-index:2;font-family:ui-monospace,Consolas,monospace;opacity:.85}
.chartTooltip{position:fixed;z-index:200;display:none;padding:7px 11px;font-size:11px;line-height:1.4;background:var(--surface2);border:1px solid var(--border2);border-radius:8px;color:var(--text);pointer-events:none;box-shadow:var(--shadow);max-width:min(320px,90vw);font-family:ui-monospace,Consolas,monospace}
canvas{width:100%;height:100%}
table.data-like{width:100%;border-collapse:separate;border-spacing:0;font-size:12px;border:1px solid var(--border);border-radius:var(--radius-sm);overflow:hidden}
table.data-like th,table.data-like td{padding:8px 10px;border-bottom:1px solid var(--border);vertical-align:top;text-align:left}
table.data-like th{background:var(--surface2);color:var(--muted);font-weight:600;font-size:11px;text-transform:uppercase;letter-spacing:.04em}
table.data-like tr:last-child td{border-bottom:none}
table.data-like tbody tr:nth-child(even){background:rgba(255,255,255,.02)}
.mono{font-family:ui-monospace,Consolas,monospace}
.alert{padding:10px 12px;margin-bottom:6px;font-size:12px;cursor:pointer;border:1px solid var(--border);border-left-width:3px;border-radius:var(--radius-sm);background:var(--surface2);transition:background .12s}
.alert:hover{background:#252f3d}
.alert.critical{border-left-color:var(--crit);background:rgba(248,113,113,.06)}
.alert.warning{border-left-color:var(--warn);background:rgba(251,191,36,.06)}
.filters{display:flex;flex-wrap:wrap;gap:10px 14px;align-items:center;margin:0 0 14px;padding:12px 16px;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius)}
.filters .ftitle{font-weight:650;color:var(--text);margin-right:4px;font-size:11px;text-transform:uppercase;letter-spacing:.06em}
.filters label{cursor:pointer;user-select:none;color:var(--text)}
.filters input{accent-color:var(--accent)}
.filters select{min-width:140px;padding:6px 10px;border:1px solid var(--border2);border-radius:var(--radius-sm);background:var(--surface2);color:var(--text);font-family:inherit;font-size:12px}
.modal{position:fixed;inset:0;background:rgba(0,0,0,.65);backdrop-filter:blur(4px);display:none;align-items:center;justify-content:center;z-index:100;padding:20px;cursor:pointer}
.modal.on{display:flex}
.modalBox{max-width:720px;width:100%;max-height:85vh;overflow:auto;background:var(--surface);border:1px solid var(--border2);border-radius:var(--radius);padding:20px;box-shadow:var(--shadow);cursor:default}
.modalHead{display:flex;align-items:flex-start;justify-content:space-between;gap:12px;border-bottom:1px solid var(--border);padding-bottom:10px;margin-bottom:12px}
.modalHead h2{margin:0;padding:0;font-size:17px;font-weight:650;color:var(--text);flex:1;line-height:1.35;border:none}
.modalX{flex-shrink:0;width:38px;height:38px;border-radius:var(--radius-sm);border:1px solid var(--border2);background:var(--surface2);color:var(--text);cursor:pointer;font-size:24px;line-height:1;display:flex;align-items:center;justify-content:center;font-family:inherit;padding:0}
.modalX:hover{background:#252f3d;border-color:rgba(255,255,255,.18)}
.modalActions{margin-top:14px}
.modalBox pre{white-space:pre-wrap;word-break:break-word;font-size:12px;background:var(--chart-bg);padding:12px;border-radius:var(--radius-sm);border:1px solid var(--border);font-family:ui-monospace,Consolas,monospace;color:var(--text)}
.rowBar{display:flex;align-items:center;gap:10px;margin:6px 0}
.rowBar .bar{flex:1;height:8px;background:var(--chart-bg);border-radius:999px;overflow:hidden;border:1px solid var(--border)}
.rowBar .fill{height:100%;background:linear-gradient(90deg,var(--warn),#f59e0b);border-radius:999px}
.build-foot{margin:24px 0 0;padding:14px 0 0;border-top:1px solid var(--border);text-align:center;font-size:11px;color:var(--muted);font-family:ui-monospace,Consolas,monospace;letter-spacing:.02em}
@media(max-width:1200px){.grid{grid-template-columns:1fr}}
@media(max-width:768px){
  .zb-header-inner{padding:10px 12px}
  .zb-toolbar{padding:8px 12px}
  .zb-page{padding:12px 12px 20px}
  .filters{padding:10px 12px;gap:8px}
  .metric{font-size:22px}
}
</style>
</head>
<body>
<div class="zb-header">
  <div class="zb-header-inner">
    <span class="zb-logo">WSM Monitor</span>
    <span class="zb-breadcrumb" data-i18n="hdrCrumb">Monitoring · Latest data · Host: local</span>
  </div>
</div>
<div class="zb-toolbar">
  <span class="sub" style="margin:0;color:var(--muted);font-weight:650" data-i18n="tbRange">Time range</span>
  <button type="button" class="tmb on" data-m="now" data-i18n="tmbNow">Now</button>
  <button type="button" class="tmb" data-m="15m" data-i18n="tmb15m">15m</button>
  <button type="button" class="tmb" data-m="1h" data-i18n="tmb1h">1h</button>
  <button type="button" class="tmb" data-m="24h" data-i18n="tmb24h">24h</button>
  <button type="button" class="tmb" data-m="7d" data-i18n="tmb7d">7d</button>
  <span class="badge mono" id="ts">-</span>
  <span class="badge mono" id="rangeLbl"></span>
</div>
<div class="zb-page" id="zbPage">
<div class="filters">
  <span class="ftitle" data-i18n="fltTitle">Filter</span>
  <span class="sub" style="margin:0;color:var(--muted)" data-i18n="fltSev">Severity:</span>
  <label><input type="checkbox" id="fSevCrit" checked/> <span data-i18n="fltSevCrit">Disaster / High</span></label>
  <label><input type="checkbox" id="fSevWarn" checked/> <span data-i18n="fltSevWarn">Average / Warning</span></label>
  <label><input type="checkbox" id="fSevInfo" checked/> <span data-i18n="fltSevInfo">Information</span></label>
  <span class="sub" style="margin:0 0 0 8px;color:var(--muted)" data-i18n="fltCodeLbl">Trigger / code:</span>
  <select id="fCode">
    <option value="" data-i18n="fltAll">All</option>
  </select>
  <button type="button" class="fbtn on" id="applyF" data-i18n="fltApply">Apply</button>
  <span class="sub" style="margin:0;color:var(--muted)" data-i18n="fltAuto">Auto on change</span>
</div>

<div class="kpi" id="kpi"></div>

<div class="grid" id="healthBreak"></div>

<div class="grid">
  <div class="card"><h3 data-i18n="chartCpu">CPU %</h3><div class="chartWrap"><canvas id="chCpu"></canvas><span class="chartLbl" id="lblCpu"></span></div></div>
  <div class="card"><h3 data-i18n="chartMem">Memory %</h3><div class="chartWrap"><canvas id="chMem"></canvas><span class="chartLbl" id="lblMem"></span></div></div>
  <div class="card"><h3 data-i18n="chartLat">Disk latency (avg ms)</h3><div class="chartWrap"><canvas id="chLat"></canvas><span class="chartLbl" id="lblLat"></span></div></div>
</div>
<div class="grid">
  <div class="card"><h3 data-i18n="chartNet">Network MiB/s</h3><div class="chartWrap"><canvas id="chNet"></canvas><span class="chartLbl" id="lblNet"></span></div></div>
  <div class="card"><h3 data-i18n="chartHs">Health score</h3><div class="chartWrap"><canvas id="chScore"></canvas><span class="chartLbl" id="lblScore"></span></div></div>
  <div class="card"><h3 data-i18n="chartTopCpu">Top CPU processes</h3><table id="pcpu" class="data-like"></table></div>
</div>
<div class="grid">
  <div class="card"><h3 data-i18n="gridProblems">Problems · triggers</h3><div id="alerts"></div></div>
  <div class="card"><h3 data-i18n="gridDisks">Disks</h3><table id="disk" class="data-like"></table></div>
  <div class="card"><h3 data-i18n="gridDiskIo">Disk I/O</h3><table id="diskio" class="data-like"></table></div>
</div>
<div class="grid">
  <div class="card"><h3 data-i18n="gridNet">Network interfaces</h3><table id="net" class="data-like"></table></div>
  <div class="card"><h3 data-i18n="gridSvc">Services</h3><table id="svc" class="data-like"></table></div>
  <div class="card"><h3 data-i18n="gridErr">Recent errors</h3><table id="ev" class="data-like"></table></div>
</div>
<div class="grid">
  <div class="card"><h3 data-i18n="gridMemDet">Memory details</h3><table id="memct" class="data-like"></table></div>
  <div class="card"><h3 data-i18n="gridSecDet">Security detections</h3><table id="det" class="data-like"></table></div>
  <div class="card"><h3 data-i18n="gridPluginHealth">Plugin health</h3><table id="plh" class="data-like"></table></div>
</div>
<div class="grid">
  <div class="card" style="grid-column:1/-1"><h3 data-i18n="thermalSectionTitle">Температуры и датчики</h3><p class="sub" id="thermalSub" style="margin:0 0 8px"></p><table id="thermal" class="data-like"></table></div>
</div>
<div class="grid">
  <div class="card" style="grid-column:1/-1"><h3 data-i18n="gridSecEvents">Recent security events</h3><table id="sev" class="data-like"></table></div>
</div>
<footer class="build-foot" id="buildFoot">{{WSM_BUILD_STAMP}}</footer>
</div>
<div id="chartTooltip" class="chartTooltip" role="status" aria-live="polite"></div>

<div class="modal" id="modal" role="dialog" aria-modal="true" aria-hidden="true">
  <div class="modalBox" id="modalBox">
    <div class="modalHead">
      <h2 id="mTitle">Detail</h2>
      <button type="button" class="modalX" id="mX" title="">&times;</button>
    </div>
    <div id="mBody"></div>
    <div class="modalActions"><button type="button" class="fbtn on" id="mClose">Close</button></div>
  </div>
</div>

<script>
const WSM_UI_LANG='{{WSM_UI_LANG}}';
document.documentElement.setAttribute('lang',WSM_UI_LANG);
const L={
ru:{
hdrCrumb:'Мониторинг · Актуальные данные · Хост: local',tbRange:'Диапазон времени',
tmbNow:'Сейчас',tmb15m:'15 мин',tmb1h:'1 ч',tmb24h:'24 ч',tmb7d:'7 дн',rangeLive:'Живой буфер',
fltTitle:'Фильтр',fltSev:'Важность:',fltSevCrit:'Катастрофа / Высокая',fltSevWarn:'Средняя / Предупреждение',fltSevInfo:'Информация',fltCodeLbl:'Триггер / код:',fltAll:'Все',fltApply:'Применить',fltAuto:'Авто при изменении',
chartCpu:'Загрузка CPU %',chartMem:'Память %',chartLat:'Задержка диска (ср. мс)',chartNet:'Сеть MiB/s',chartHs:'Health score',chartTopCpu:'Процессы CPU (топ)',
gridProblems:'Проблемы · триггеры',gridDisks:'Диски',gridDiskIo:'Диск I/O',gridNet:'Сетевые интерфейсы',gridSvc:'Службы',gridErr:'Недавние ошибки',
gridMemDet:'Память — детали',gridSecDet:'Сигналы безопасности',gridPluginHealth:'Здоровье плагинов',thermalSectionTitle:'Температуры и датчики',gridSecEvents:'События безопасности',
ylabelCpu:'CPU %',ylabelMem:'Память %',ylabelLat:'Задержка',ylabelNet:'MiB/s',ylabelScore:'Оценка',
kpiCpuTitle:'Загрузка CPU',kpiMemTitle:'Память',kpiHealthTitle:'Оценка здоровья',kpiThroughputTitle:'Пропускная способность',kpiTempTitle:'Температуры',
kpiCoresFmt:'ядра {0} | очередь {1}',kpiMemFmt:'{0} / {1} MiB | commit {2}%',kpiHealthSub:'100 = норма · нажмите для подробностей',kpiThroughputRxTx:'RX {0} | TX {1}',
kpiTempSensorsFmt:'{0} датчиков{1}',kpiTempLhmFmt:' · LHM {0}',kpiTempNoRows:'нет строк thermal',
healthFactorsTitle:'Health score — факторы влияния',
thermalSubOnFmt:'Строк: {0}{1}. Источник ACPI — WMI; LHM — при включении в настройках.',thermalSubLhmFmt:' · из них LibreHardwareMonitor: {0}',thermalSubOff:'Нет данных температур. После включения LibreHardwareMonitor в настройках перезапустите companion и службу WSMMonitor.',
thermalThSrc:'Источник',thermalThName:'Датчик',thermalThC:'°C',
diskThVol:'Том',diskThFreeGb:'Свободно ГБ',diskThFreePct:'Свободно %',diskIoThDisk:'Диск',diskIoThQ:'Очередь',diskIoThRms:'Чт мс',diskIoThWms:'Зап мс',diskIoThRs:'Чт/с',diskIoThWs:'Зап/с',
netThAd:'Адаптер',netThRx:'RX',netThTx:'TX',svcThName:'Служба',svcThSt:'Состояние',evThTime:'Время',evThLog:'Журнал',evThMsg:'Сообщение',
pcpuThProc:'Процесс',pcpuThCpu:'~CPU%',pcpuThWs:'WS MiB',detThSev:'Важность',detThRule:'Правило',detThWhen:'Когда',detThWhy:'Причина',
plhThPl:'Плагин',plhThHl:'OK',plhThMsg:'Сообщение',sevThTime:'Время',sevThId:'ID',sevThImg:'Образ',sevThCmd:'Команда',
memNonPaged:'Non-paged pool',memAvail:'Доступно',memCache:'Кэш',memStandby:'Standby',memCompressed:'Сжато',memHdrMetric:'Показатель',memHdrValue:'Значение',rowBarCapFmt:'потолок {0}',noProblems:'Нет проблем',liveLbl:'живой',
modalClose:'Закрыть',modalCloseAria:'Закрыть окно',
hsTitleFmt:'Оценка здоровья системы · {0}',hsLegend:'■ Красный — сильный негатив · ■ Жёлтый — осторожность · ■ Зелёный — в норме',
hsNegFmt:'Негатив / повышено ({0})',hsNoneNeg:'Нет измерений в зонах предупреждения или критичности.',hsPosFmt:'В норме ({0})',hsPenalties:'Применённые штрафы к оценке',
hsThArea:'Область',hsThPenalty:'Штраф',hsThCap:'Потолок',hsThDetail:'Деталь',
thermalModalTitle:'Датчики температуры',thermalNoRowsInModal:'Нет строк температуры.',
alertMetricSnap:'Снимок метрик (текущий scrape)',alertLoading:'Загрузка события…',alertLoadErr:'Не удалось загрузить событие.',alertSigma:'Обнаружение Sigma.',detectionHdr:'Обнаружение',
verBannerFmt:'Версия агента ({0}) не совпадает с companion ({1}). Служба часто указывает на старый exe — sc stop WSMMonitor, замените exe из publish, sc start. Либо откройте URL встроенного агента (другой порт в трее).',
scoreCardTitle:'Оценка здоровья системы',healthCardHint:'Нажмите для подробного разбора',tempCardHint:'Полная таблица ниже. Нажмите для списка датчиков',chartMinLbl:'мин {0}',chartMaxLbl:'макс {0}'
},
en:{
hdrCrumb:'Monitoring · Latest data · Host: local',tbRange:'Time range',
tmbNow:'Now',tmb15m:'15m',tmb1h:'1h',tmb24h:'24h',tmb7d:'7d',rangeLive:'Live buffer',
fltTitle:'Filter',fltSev:'Severity:',fltSevCrit:'Disaster / High',fltSevWarn:'Average / Warning',fltSevInfo:'Information',fltCodeLbl:'Trigger / code:',fltAll:'All',fltApply:'Apply',fltAuto:'Auto on change',
chartCpu:'CPU %',chartMem:'Memory %',chartLat:'Disk latency (avg ms)',chartNet:'Network MiB/s',chartHs:'Health score',chartTopCpu:'Top CPU processes',
gridProblems:'Problems · triggers',gridDisks:'Disks',gridDiskIo:'Disk I/O',gridNet:'Network interfaces',gridSvc:'Services',gridErr:'Recent errors',
gridMemDet:'Memory details',gridSecDet:'Security detections',gridPluginHealth:'Plugin health',thermalSectionTitle:'Temperatures & sensors',gridSecEvents:'Recent security events',
ylabelCpu:'CPU %',ylabelMem:'Mem %',ylabelLat:'Latency',ylabelNet:'MiB/s',ylabelScore:'Score',
kpiCpuTitle:'CPU load',kpiMemTitle:'Memory',kpiHealthTitle:'System health score',kpiThroughputTitle:'Throughput',kpiTempTitle:'Temperatures',
kpiCoresFmt:'cores {0} | queue {1}',kpiMemFmt:'{0} / {1} MiB | commit {2}%',kpiHealthSub:'100 = healthy · click for details',kpiThroughputRxTx:'RX {0} | TX {1}',
kpiTempSensorsFmt:'{0} sensors{1}',kpiTempLhmFmt:' · LHM {0}',kpiTempNoRows:'no thermal rows',
healthFactorsTitle:'Health score · contributing factors',
thermalSubOnFmt:'Rows: {0}{1}. ACPI — WMI; LHM when enabled in settings.',thermalSubLhmFmt:' · from LibreHardwareMonitor: {0}',thermalSubOff:'No temperature data. Enable LibreHardwareMonitor in settings, then restart companion and WSMMonitor service.',
thermalThSrc:'Source',thermalThName:'Sensor',thermalThC:'°C',
diskThVol:'Volume',diskThFreeGb:'Free GB',diskThFreePct:'Free %',diskIoThDisk:'Disk',diskIoThQ:'Q',diskIoThRms:'R ms',diskIoThWms:'W ms',diskIoThRs:'R/s',diskIoThWs:'W/s',
netThAd:'Adapter',netThRx:'RX',netThTx:'TX',svcThName:'Service',svcThSt:'Status',evThTime:'Time',evThLog:'Log',evThMsg:'Message',
pcpuThProc:'Process',pcpuThCpu:'~CPU%',pcpuThWs:'WS MiB',detThSev:'Severity',detThRule:'Rule',detThWhen:'When',detThWhy:'Reason',
plhThPl:'Plugin',plhThHl:'Healthy',plhThMsg:'Message',sevThTime:'Time',sevThId:'ID',sevThImg:'Image',sevThCmd:'Cmd',
memNonPaged:'Non-paged pool',memAvail:'Available',memCache:'Cache',memStandby:'Standby',memCompressed:'Compressed',memHdrMetric:'Metric',memHdrValue:'Value',rowBarCapFmt:'cap {0}',noProblems:'No problems',liveLbl:'live',
modalClose:'Close',modalCloseAria:'Close dialog',
hsTitleFmt:'System health score · {0}',hsLegend:'■ Red — strong negative · ■ Yellow — caution · ■ Green — OK',
hsNegFmt:'Negative / elevated ({0})',hsNoneNeg:'No dimension in warning or critical bands.',hsPosFmt:'Positive / within range ({0})',hsPenalties:'Applied score penalties',
hsThArea:'Area',hsThPenalty:'Penalty',hsThCap:'Cap',hsThDetail:'Detail',
thermalModalTitle:'Temperature sensors',thermalNoRowsInModal:'No temperature sensor rows.',
alertMetricSnap:'Metric snapshot (current scrape)',alertLoading:'Loading event…',alertLoadErr:'Could not load event.',alertSigma:'Sigma detection.',detectionHdr:'Detection',
verBannerFmt:'Agent version ({0}) does not match companion ({1}). The service often points at an old exe — sc stop WSMMonitor, replace exe from publish, sc start. Or open the embedded agent URL (different port from tray).',
scoreCardTitle:'System health score',healthCardHint:'Click for score breakdown',tempCardHint:'Full table below. Click for sensor list',chartMinLbl:'min {0}',chartMaxLbl:'max {0}'
}
};
function pickLang(){return (WSM_UI_LANG==='en'?'en':'ru');}
function tr(k){
  const lang=pickLang();
  const t=L[lang]||L.ru;
  if(Object.prototype.hasOwnProperty.call(t,k))return t[k];
  if(Object.prototype.hasOwnProperty.call(L.ru,k))return L.ru[k];
  return k;
}
function trf(k,...parts){
  let s=tr(k);
  parts.forEach((v,i)=>{s=s.split('{'+i+'}').join(String(v));});
  return s;
}
function applyStaticI18n(){
  document.querySelectorAll('[data-i18n]').forEach(el=>{
    const k=el.getAttribute('data-i18n');
    if(k) el.textContent=tr(k);
  });
  const c=document.getElementById('mClose'); if(c) c.textContent=tr('modalClose');
  const x=document.getElementById('mX');
  if(x){ x.setAttribute('title',tr('modalClose')); x.setAttribute('aria-label',tr('modalCloseAria')); }
}
function closeModal(){
  const m=document.getElementById('modal');
  if(!m)return;
  m.classList.remove('on');
  m.setAttribute('aria-hidden','true');
}
function openModal(){
  const m=document.getElementById('modal');
  if(!m)return;
  m.classList.add('on');
  m.setAttribute('aria-hidden','false');
}
(function wireModal(){
  const modal=document.getElementById('modal');
  const box=document.getElementById('modalBox');
  if(!modal||!box)return;
  modal.addEventListener('click',e=>{ if(e.target===modal) closeModal(); });
  box.addEventListener('click',e=>{ e.stopPropagation(); });
  document.getElementById('mClose')?.addEventListener('click',e=>{ e.stopPropagation(); closeModal(); });
  document.getElementById('mX')?.addEventListener('click',e=>{ e.stopPropagation(); closeModal(); });
  document.addEventListener('keydown',e=>{
    if(e.key==='Escape'&&modal.classList.contains('on')){
      e.preventDefault();
      closeModal();
    }
  });
})();

let timeMode='now';
const live={cpu:[],mem:[],lat:[],net:[],score:[]};
const maxPts=120;
let lastMetrics=null;
let histPoints=[];
let histMeta={from:'',to:''};
let histTimeLabels=[];

function pushLive(name,val){
  live[name].push(val);
  if(live[name].length>maxPts)live[name].shift();
}

document.querySelectorAll('.tmb[data-m]').forEach(b=>{
  b.addEventListener('click',()=>{
    timeMode=b.dataset.m;
    document.querySelectorAll('.tmb[data-m]').forEach(x=>x.classList.toggle('on',x.dataset.m===timeMode));
    if(timeMode==='now'){
      live.cpu=[];live.mem=[];live.lat=[];live.net=[];live.score=[];
      document.getElementById('rangeLbl').textContent=tr('rangeLive');
    }
    refreshHistory();
  });
});

function openHealthScoreModal(){
  if(!lastMetrics)return;
  const d=lastMetrics;
  const score=Number(d.healthScore??0);
  const insights=d.healthScoreInsights||[];
  const factors=d.healthBreakdown||[];
  document.getElementById('mTitle').textContent=trf('hsTitleFmt',score);
  const rowHtml=x=>{
    const imp=(x.impact||'good').toLowerCase();
    const cls=imp==='bad'?'hs-bad':imp==='warning'?'hs-warning':'hs-good';
    return '<div class="hs-row '+cls+'"><div class="hs-cat">'+esc(x.category)+'</div><div class="hs-detail">'+esc(x.detail)+'</div></div>';
  };
  const bad=insights.filter(i=>(i.impact||'').toLowerCase()==='bad');
  const warn=insights.filter(i=>(i.impact||'').toLowerCase()==='warning');
  const good=insights.filter(i=>(i.impact||'').toLowerCase()==='good');
  let html='<p class="hs-hint">'+tr('hsLegend')+'</p>';
  html+='<div class="hs-section bad">'+trf('hsNegFmt',bad.length+warn.length)+'</div>';
  if(bad.length+warn.length===0) html+='<div class="sub" style="padding:8px">'+tr('hsNoneNeg')+'</div>';
  else html+=bad.map(rowHtml).join('')+warn.map(rowHtml).join('');
  html+='<div class="hs-section good">'+trf('hsPosFmt',good.length)+'</div>';
  html+=good.map(rowHtml).join('');
  if(factors.length){
    html+='<div class="hs-section" style="color:var(--text);margin-top:16px">'+tr('hsPenalties')+'</div>';
    html+='<table class="data-like" style="margin-top:6px"><tr><th>'+tr('hsThArea')+'</th><th>'+tr('hsThPenalty')+'</th><th>'+tr('hsThCap')+'</th><th>'+tr('hsThDetail')+'</th></tr>';
    html+=factors.map(f=>'<tr><td>'+esc(f.category)+'</td><td class="mono">-'+f.penalty+'</td><td class="mono">'+f.cap+'</td><td>'+esc(f.detail)+'</td></tr>').join('');
    html+='</table>';
  }
  document.getElementById('mBody').innerHTML=html;
  openModal();
}

function esc(s){return String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))}
function openThermalModal(){
  if(!lastMetrics)return;
  const th=thermalList(lastMetrics);
  document.getElementById('mTitle').textContent=tr('thermalModalTitle');
  let html='<table class="data-like"><tr><th>'+tr('thermalThSrc')+'</th><th>'+tr('thermalThName')+'</th><th>'+tr('thermalThC')+'</th></tr>';
  if(!th.length) html+='<tr><td colspan="3" class="sub" style="padding:12px">'+tr('thermalNoRowsInModal')+'</td></tr>';
  else for(const t of th){
    const c=tempC(t);
    const cls=c!=null&&c>=90?'crit':c!=null&&c>=80?'warn':'';
    html+='<tr><td class="mono">'+esc(tempSrc(t))+'</td><td>'+esc(tempName(t))+'</td><td class="mono '+cls+'">'+(c==null?'-':c.toFixed(1))+'</td></tr>';
  }
  html+='</table>';
  document.getElementById('mBody').innerHTML=html;
  openModal();
}
document.getElementById('zbPage').addEventListener('click',e=>{
  if(e.target.closest('#healthScoreCard')) openHealthScoreModal();
  if(e.target.closest('#tempCard')) openThermalModal();
});
function thermalList(d){
  const a=d&&(d.thermal||d.Thermal);
  return Array.isArray(a)?a:[];
}
function tempSrc(t){return String(t&&(t.source||t.Source)||'');}
function tempName(t){return String(t&&(t.name||t.Name)||'');}
function tempC(t){
  const v=t&&(t.celsius!=null?t.celsius:t.Celsius);
  const n=Number(v);
  return Number.isFinite(n)?n:null;
}
function toClass(v,w,c){if(v>=c)return'crit';if(v>=w)return'warn';return'ok'}
function rateToMiB(s){
  const t=String(s||'').trim();
  const m=t.match(/([0-9.]+)\s*(B|KiB|MiB)\/s/i); if(!m)return 0;
  const v=parseFloat(m[1]); const u=m[2].toLowerCase();
  if(u==='mib')return v; if(u==='kib')return v/1024; if(u==='b')return v/1048576; return 0;
}
function avgLatency(d){
  const p=(d.diskPerf||[]).filter(x=>x.readLatencyMs!=null||x.writeLatencyMs!=null);
  if(!p.length)return 0;
  let s=0,c=0; for(const x of p){if(x.readLatencyMs!=null){s+=x.readLatencyMs;c++} if(x.writeLatencyMs!=null){s+=x.writeLatencyMs;c++}}
  return c? s/c : 0;
}

function drawZoneChart(id, series, opt){
  opt=opt||{};
  const cv=document.getElementById(id);
  if(!cv)return;
  const dpr=Math.min(2,window.devicePixelRatio||1);
  const wCss=cv.clientWidth||400, hCss=cv.clientHeight||180;
  cv.width=Math.round(wCss*dpr); cv.height=Math.round(hCss*dpr);
  cv.style.width=wCss+'px'; cv.style.height=hCss+'px';
  const g=cv.getContext('2d');
  g.setTransform(dpr,0,0,dpr,0,0);
  const w=wCss, h=hCss;
  g.clearRect(0,0,w,h);
  cv._wsmChart=null;
  if(!series||!series.length)return;
  const padL=opt.padL??42, padR=10, padT=10, padB=(opt.timeLabels&&opt.timeLabels.length)?26:10;
  const innerW=w-padL-padR, innerH=h-padT-padB;
  const maxY=Math.max(opt.minY||0, opt.maxY||100,...series,1);
  const minY=opt.minY||0;
  const rng=maxY-minY||1;
  const n=series.length;
  const color=opt.lineColor||'#60a5fa';
  const dec=opt.decimals===2?2:1;
  const xAt=i=>padL+(n<=1?innerW/2:(i/Math.max(1,n-1))*innerW);
  const yAt=v=>padT+innerH-((Math.min(maxY,Math.max(minY,v))-minY)/rng)*innerH;
  (opt.zones||[]).forEach(z=>{
    const ya=yAt(z.from), yb=yAt(z.to);
    g.fillStyle=z.color;
    g.fillRect(padL,Math.min(ya,yb),innerW,Math.abs(yb-ya)||1);
  });
  g.strokeStyle='rgba(148,163,184,.14)'; g.lineWidth=1;
  for(let k=0;k<=4;k++){
    const y=padT+(innerH/4)*k+.5;
    g.beginPath(); g.moveTo(padL,y); g.lineTo(padL+innerW,y); g.stroke();
    const val=maxY-(maxY-minY)*k/4;
    g.fillStyle='rgba(148,163,184,.6)';
    g.font='10px ui-monospace,Consolas,sans-serif';
    g.textAlign='right'; g.textBaseline='middle';
    g.fillText(val.toFixed(dec),padL-6,y);
  }
  const pts=[];
  for(let i=0;i<n;i++) pts.push({x:xAt(i),y:yAt(series[i]??0)});
  if(pts.length>=2){
    g.beginPath();
    g.moveTo(pts[0].x,padT+innerH);
    pts.forEach(p=>g.lineTo(p.x,p.y));
    g.lineTo(pts[pts.length-1].x,padT+innerH);
    g.closePath();
    const gr=g.createLinearGradient(0,padT,0,padT+innerH);
    const hex=(typeof color==='string'&&/^#[0-9a-fA-F]{6}$/.test(color))?color+'44':'rgba(56,189,248,.22)';
    gr.addColorStop(0,hex);
    gr.addColorStop(1,'rgba(0,0,0,0)');
    g.fillStyle=gr;
    g.fill();
  }
  g.strokeStyle=color; g.lineWidth=2; g.beginPath();
  for(let i=0;i<n;i++){
    const x=xAt(i), yy=yAt(series[i]??0);
    if(i===0)g.moveTo(x,yy); else g.lineTo(x,yy);
  }
  g.stroke();
  if(opt.timeLabels&&n>2){
    let mn=series[0],mx=series[0];
    for(let i=0;i<n;i++){const v=series[i]??0; if(v<mn)mn=v; if(v>mx)mx=v;}
    g.fillStyle='rgba(148,163,184,.8)'; g.font='10px ui-monospace,Consolas,sans-serif';
    g.textAlign='left'; g.textBaseline='top';
    g.fillText(trf('chartMinLbl',mn.toFixed(dec)),padL,2);
    g.textAlign='right';
    g.fillText(trf('chartMaxLbl',mx.toFixed(dec)),padL+innerW,2);
  }
  cv._wsmChart={padL,padR,padT,padB,w,h,innerW,innerH,n,series,minY,maxY,decimals:dec,timeLabels:opt.timeLabels||null,ylabel:opt.ylabel||''};
}

function wireChartHover(){
  const tip=document.getElementById('chartTooltip');
  const zb=document.getElementById('zbPage');
  if(!tip||!zb)return;
  const hide=()=>{ tip.style.display='none'; };
  zb.addEventListener('mousemove',e=>{
    const wrap=e.target.closest('.chartWrap');
    if(!wrap){ hide(); return; }
    const cv=wrap.querySelector('canvas');
    const meta=cv&&cv._wsmChart;
    if(!meta||!meta.n){ hide(); return; }
    const r=cv.getBoundingClientRect();
    const mx=e.clientX-r.left;
    if(mx<meta.padL||mx>meta.w-meta.padR){ hide(); return; }
    const idx=Math.round(((mx-meta.padL)/meta.innerW)*(meta.n-1));
    const i=Math.max(0,Math.min(meta.n-1,idx));
    const v=meta.series[i]??0;
    let txt=(meta.ylabel?meta.ylabel+': ':'')+v.toFixed(meta.decimals);
    if(meta.timeLabels&&meta.timeLabels[i]) txt=meta.timeLabels[i]+' · '+txt;
    else txt+=' · '+(i+1)+'/'+meta.n;
    tip.textContent=txt;
    tip.style.display='block';
    const tw=tip.offsetWidth||160, th=tip.offsetHeight||40;
    let lx=e.clientX+14, ly=e.clientY+14;
    if(lx+tw>innerWidth-8) lx=e.clientX-tw-14;
    if(ly+th>innerHeight-8) ly=e.clientY-th-14;
    tip.style.left=lx+'px'; tip.style.top=ly+'px';
  });
  zb.addEventListener('mouseleave',hide);
}

function cpuZones(){return[
  {from:0,to:85,color:'rgba(52,211,153,0.12)'},{from:85,to:95,color:'rgba(251,191,36,0.14)'},{from:95,to:100,color:'rgba(248,113,113,0.14)'}
];}
function memZones(){return cpuZones();}
function latZones(){return[
  {from:0,to:40,color:'rgba(52,211,153,0.1)'},{from:40,to:80,color:'rgba(251,191,36,0.12)'},{from:80,to:500,color:'rgba(248,113,113,0.1)'}
];}
function scoreZones(){return[
  {from:0,to:60,color:'rgba(248,113,113,0.12)'},{from:60,to:80,color:'rgba(251,191,36,0.1)'},{from:80,to:100,color:'rgba(52,211,153,0.12)'}
];}

function redrawChartsFromLive(){
  drawZoneChart('chCpu',live.cpu,{maxY:100,zones:cpuZones(),lineColor:'#38bdf8',ylabel:tr('ylabelCpu')});
  drawZoneChart('chMem',live.mem,{maxY:100,zones:memZones(),lineColor:'#a78bfa',ylabel:tr('ylabelMem')});
  const latMax=Math.max(50,...live.lat,1);
  drawZoneChart('chLat',live.lat,{maxY:latMax,minY:0,zones:latZones(),lineColor:'#fbbf24',decimals:2,ylabel:tr('ylabelLat')});
  const nMax=Math.max(10,...live.net,1);
  drawZoneChart('chNet',live.net,{maxY:nMax,minY:0,zones:[],lineColor:'#34d399',ylabel:tr('ylabelNet')});
  drawZoneChart('chScore',live.score,{maxY:100,minY:0,zones:scoreZones(),lineColor:'#94a3b8',ylabel:tr('ylabelScore')});
}

function redrawChartsFromHistory(){
  const cpu=histPoints.map(p=>p.cpu??0);
  const mem=histPoints.map(p=>p.mem??0);
  const lat=histPoints.map(p=>p.lat??0);
  const net=histPoints.map(p=>p.net??0);
  const sc=histPoints.map(p=>p.score??0);
  const tl=histTimeLabels;
  drawZoneChart('chCpu',cpu,{maxY:100,zones:cpuZones(),lineColor:'#38bdf8',timeLabels:tl,ylabel:tr('ylabelCpu')});
  drawZoneChart('chMem',mem,{maxY:100,zones:memZones(),lineColor:'#a78bfa',timeLabels:tl,ylabel:tr('ylabelMem')});
  drawZoneChart('chLat',lat,{maxY:Math.max(50,...lat,1),minY:0,zones:latZones(),lineColor:'#fbbf24',decimals:2,timeLabels:tl,ylabel:tr('ylabelLat')});
  drawZoneChart('chNet',net,{maxY:Math.max(10,...net,1),minY:0,zones:[],lineColor:'#34d399',timeLabels:tl,ylabel:tr('ylabelNet')});
  drawZoneChart('chScore',sc,{maxY:100,minY:0,zones:scoreZones(),lineColor:'#94a3b8',timeLabels:tl,ylabel:tr('ylabelScore')});
  document.getElementById('lblCpu').textContent=histMeta.from.slice(0,16)+' → '+histMeta.to.slice(0,16);
  document.getElementById('lblMem').textContent=document.getElementById('lblCpu').textContent;
  document.getElementById('lblLat').textContent=document.getElementById('lblCpu').textContent;
  document.getElementById('lblNet').textContent=document.getElementById('lblCpu').textContent;
  document.getElementById('lblScore').textContent=document.getElementById('lblCpu').textContent;
}

async function refreshHistory(){
  if(timeMode==='now'){ redrawChartsFromLive(); return; }
  try{
    const r=await fetch('/api/v1/metrics/history?preset='+encodeURIComponent(timeMode),{cache:'no-store'});
    const h=await r.json();
    histPoints=h.points||[];
    histMeta={from:h.fromUtc||'',to:h.toUtc||''};
    histTimeLabels=(histPoints||[]).map(p=>{
      const t=String(p.t||'');
      if(t.length>=19)return t.slice(11,19);
      return t.slice(0,8);
    });
    document.getElementById('rangeLbl').textContent=(histMeta.from||'').slice(5,16)+' — '+(histMeta.to||'').slice(5,16);
    redrawChartsFromHistory();
  }catch(e){console.error(e)}
}

function sevMatch(sev){
  const s=(sev||'').toLowerCase();
  const c=document.getElementById('fSevCrit').checked;
  const w=document.getElementById('fSevWarn').checked;
  const i=document.getElementById('fSevInfo').checked;
  if(s.includes('critical')||s==='critical')return c;
  if(s.includes('high'))return c;
  if(s.includes('error'))return w;
  if(s.includes('warn')||s==='warning')return w;
  return i;
}

function codeMatch(code){
  const sel=document.getElementById('fCode');
  const v=sel.value;
  if(!v)return true;
  return (code||'')===v;
}

function fillCodeFilter(alerts){
  const sel=document.getElementById('fCode');
  const cur=sel.value;
  const codes=[...new Set((alerts||[]).map(a=>a.code).filter(Boolean))].sort();
  sel.innerHTML='<option value="">'+esc(tr('fltAll'))+'</option>'+codes.map(c=>'<option value="'+esc(c)+'">'+esc(c)+'</option>').join('');
  if(codes.includes(cur))sel.value=cur;
}

function drawScoreRing(score){
  const id='scoreRing'; const cv=document.getElementById(id); if(!cv)return;
  const w=cv.clientWidth||120,h=cv.clientHeight||120; cv.width=w; cv.height=h;
  const g=cv.getContext('2d'); const cx=w/2,cy=h/2,r=Math.min(w,h)/2-8;
  g.clearRect(0,0,w,h);
  g.lineWidth=10; g.strokeStyle='#334155'; g.beginPath(); g.arc(cx,cy,r,0,Math.PI*2); g.stroke();
  const p=Math.max(0,Math.min(100,score))/100;
  g.strokeStyle=score>=80?'#34d399':score>=60?'#fbbf24':'#f87171';
  g.beginPath(); g.arc(cx,cy,r,-Math.PI/2,-Math.PI/2+Math.PI*2*p); g.stroke();
  g.fillStyle='#e8eaef'; g.font='700 22px ui-monospace,Consolas,Segoe UI'; g.textAlign='center'; g.textBaseline='middle'; g.fillText(String(score),cx,cy);
}

async function openAlertDetail(a){
  const body=document.getElementById('mBody');
  const title=document.getElementById('mTitle');
  title.textContent=(a.code||'')+' — '+(a.severity||'');
  const ref=a.ref||{};
  if(ref.kind==='metric'&&lastMetrics){
    const m=lastMetrics;
    body.innerHTML='<p>'+esc(tr('alertMetricSnap'))+'</p><pre>'+esc(JSON.stringify({
      cpu:m.cpuTotalPct,memoryUsed:m.memory?.usedPct,health:m.healthScore,queue:m.cpuQueueLength
    },null,2))+'</pre>';
    openModal(); return;
  }
  if(ref.kind==='event'&&ref.log&&ref.recordId!=null){
    body.innerHTML='<p>'+esc(tr('alertLoading'))+'</p>';
    openModal();
    try{
      const r=await fetch('/api/v1/log-event?log='+encodeURIComponent(ref.log)+'&recordId='+encodeURIComponent(ref.recordId),{cache:'no-store'});
      if(!r.ok){ body.innerHTML='<p>'+esc(tr('alertLoadErr'))+'</p>'; return; }
      const j=await r.json();
      body.innerHTML='<pre>'+esc(j.message||'')+'</pre><p class="sub mono">'+esc(j.provider||'')+' | id '+(j.eventId??'')+'</p>';
    }catch(e){ body.innerHTML='<p>'+esc(String(e))+'</p>'; }
    return;
  }
  if(ref.kind==='security'){
    body.innerHTML='<p>'+esc(tr('alertSigma'))+'</p><pre>'+esc(JSON.stringify(ref,null,2))+'</pre>';
    if(lastMetrics&&lastMetrics.detections){
      const hit=(lastMetrics.detections||[]).find(d=>d.ruleId===ref.ruleId&&d.eventTime===ref.eventTime);
      if(hit) body.innerHTML+='<h3>'+esc(tr('detectionHdr'))+'</h3><pre>'+esc(JSON.stringify(hit,null,2))+'</pre>';
    }
    openModal(); return;
  }
  body.innerHTML='<pre>'+esc(JSON.stringify(a,null,2))+'</pre>';
  openModal();
}

function showVersionMismatchIfNeeded(resp){
  try{
    const u=new URLSearchParams(location.search);
    const expect=u.get('wsm_expect');
    if(!expect||document.getElementById('wsmVerBanner'))return;
    const hv=(resp.headers.get('X-WSM-Version')||resp.headers.get('x-wsm-version')||'').trim();
    if(!hv)return;
    if(hv===expect)return;
    const b=document.createElement('div');
    b.id='wsmVerBanner';
    b.style.cssText='background:#7f1d1d;color:#fecaca;padding:12px 16px;text-align:center;font-size:13px;line-height:1.5;margin:0;border-bottom:2px solid #450a0a';
    b.textContent=trf('verBannerFmt',hv,expect);
    document.body.insertBefore(b,document.body.firstChild);
  }catch(e){console.error(e)}
}

async function load(){
  try{
    applyStaticI18n();
    const r=await fetch('/api/v1/metrics',{cache:'no-store'});
    showVersionMismatchIfNeeded(r);
    const d=await r.json();
    lastMetrics=d;
    const th=thermalList(d);
    const lhmCount=th.filter(t=>tempSrc(t).toUpperCase()==='LHM').length;
    let maxTemp=null;
    for(const t of th){ const c=tempC(t); if(c!=null&&(maxTemp==null||c>maxTemp))maxTemp=c; }
    const m=d.memory||d.Memory||{};
    document.getElementById('ts').textContent=d.timestamp||'-';
    const cpu=Number(d.cpuTotalPct??0), mem=Number(m.usedPct??0), score=Number(d.healthScore??75);
    const lat=avgLatency(d);
    const rx=(d.network||[]).reduce((a,n)=>a+rateToMiB(n.rxPerSec),0);
    const tx=(d.network||[]).reduce((a,n)=>a+rateToMiB(n.txPerSec),0);
    if(timeMode==='now'){
      pushLive('cpu',cpu); pushLive('mem',mem); pushLive('lat',lat); pushLive('net',rx+tx); pushLive('score',score);
      redrawChartsFromLive();
      const ll=tr('liveLbl');
      document.getElementById('lblCpu').textContent=ll;
      document.getElementById('lblMem').textContent=ll;
      document.getElementById('lblLat').textContent=ll;
      document.getElementById('lblNet').textContent=ll;
      document.getElementById('lblScore').textContent=ll;
    }

    const kpi=document.getElementById('kpi');
    const tempMetric=maxTemp!=null?`${maxTemp.toFixed(1)} °C`:'—';
    const tempLhm=lhmCount?trf('kpiTempLhmFmt',lhmCount):'';
    const tempSub=th.length?trf('kpiTempSensorsFmt',th.length,tempLhm):tr('kpiTempNoRows');
    const tempCls=maxTemp!=null?toClass(maxTemp,80,90):'';
    kpi.innerHTML=
      '<div class="card"><h3>'+esc(tr('kpiCpuTitle'))+'</h3><div class="metric '+toClass(cpu,85,95)+'">'+cpu.toFixed(1)+'%</div><div class="sub mono">'+esc(trf('kpiCoresFmt',d.cpuLogicalCores??d.CpuLogicalCores,d.cpuQueueLength??d.CpuQueueLength??'-'))+'</div></div>'+
      '<div class="card"><h3>'+esc(tr('kpiMemTitle'))+'</h3><div class="metric '+toClass(mem,85,92)+'">'+mem.toFixed(1)+'%</div><div class="sub mono">'+esc(trf('kpiMemFmt',m.usedMiB??m.UsedMiB??'-',m.totalMiB??m.TotalMiB??'-',m.commitPct??m.CommitPct??'-'))+'</div></div>'+
      '<div class="card card--interactive" id="healthScoreCard" title="'+esc(tr('healthCardHint'))+'"><h3>'+esc(tr('kpiHealthTitle'))+'</h3><div style="display:flex;gap:10px;align-items:center"><canvas id="scoreRing" style="width:96px;height:96px"></canvas><div><div class="metric '+toClass(100-score,20,40)+'">'+score+'</div><div class="sub">'+esc(tr('kpiHealthSub'))+'</div></div></div></div>'+
      '<div class="card"><h3>'+esc(tr('kpiThroughputTitle'))+'</h3><div class="metric">'+(rx+tx).toFixed(2)+' MiB/s</div><div class="sub mono">'+esc(trf('kpiThroughputRxTx',rx.toFixed(2),tx.toFixed(2)))+'</div></div>'+
      '<div class="card card--interactive" id="tempCard" title="'+esc(tr('tempCardHint'))+'"><h3>'+esc(tr('kpiTempTitle'))+'</h3><div class="metric '+tempCls+'">'+esc(tempMetric)+'</div><div class="sub mono">'+esc(tempSub)+'</div></div>';
    drawScoreRing(score);

    const hb=document.getElementById('healthBreak');
    const factors=d.healthBreakdown||[];
    if(factors.length){
      hb.innerHTML='<div class="card" style="grid-column:1/-1"><h3>'+esc(tr('healthFactorsTitle'))+'</h3>'+
        factors.map(f=>{
          const pct=Math.min(100,(f.penalty/Math.max(1,f.cap))*100);
          return '<div class="rowBar"><span style="min-width:140px">'+esc(f.category)+'</span><div class="bar"><div class="fill" style="width:'+pct+'%"></div></div>'+
            '<span class="mono">-'+f.penalty+'</span><span class="sub">'+esc(trf('rowBarCapFmt',f.cap))+'</span></div><div class="sub" style="margin-left:148px;margin-bottom:8px">'+esc(f.detail)+'</div>';
        }).join('')+'</div>';
    }else hb.innerHTML='';

    fillCodeFilter(d.alerts);
    const al=(d.alerts||[]).filter(x=>sevMatch(x.severity)&&codeMatch(x.code));
    document.getElementById('alerts').innerHTML=al.length?al.slice(0,24).map((x,i)=>`<div class="alert ${x.severity==='critical'?'critical':'warning'}" data-idx="${i}"><span class="mono" style="color:var(--muted)">${esc(x.code)}</span> · ${esc(x.text)}</div>`).join(''):'<div class="sub" style="padding:8px;color:var(--muted)">'+esc(tr('noProblems'))+'</div>';
    document.querySelectorAll('#alerts .alert').forEach(el=>{
      el.addEventListener('click',()=>{
        const i=+el.dataset.idx;
        openAlertDetail(al[i]);
      });
    });

    const disk=d.disks||[];
    document.getElementById('disk').innerHTML='<tr><th>'+esc(tr('diskThVol'))+'</th><th>'+esc(tr('diskThFreeGb'))+'</th><th>'+esc(tr('diskThFreePct'))+'</th></tr>'+disk.map(x=>`<tr><td>${esc(x.deviceId)} ${esc(x.label||'')}</td><td>${x.freeGB}</td><td>${x.freePct}</td></tr>`).join('');
    const dio=d.diskPerf||[];
    document.getElementById('diskio').innerHTML='<tr><th>'+esc(tr('diskIoThDisk'))+'</th><th>'+esc(tr('diskIoThQ'))+'</th><th>'+esc(tr('diskIoThRms'))+'</th><th>'+esc(tr('diskIoThWms'))+'</th><th>'+esc(tr('diskIoThRs'))+'</th><th>'+esc(tr('diskIoThWs'))+'</th></tr>'+dio.slice(0,12).map(x=>`<tr><td class="mono">${esc(x.instance)}</td><td>${x.queueLength??'-'}</td><td>${x.readLatencyMs??'-'}</td><td>${x.writeLatencyMs??'-'}</td><td>${x.readsPerSec??'-'}</td><td>${x.writesPerSec??'-'}</td></tr>`).join('');
    const net=d.network||[];
    document.getElementById('net').innerHTML='<tr><th>'+esc(tr('netThAd'))+'</th><th>'+esc(tr('netThRx'))+'</th><th>'+esc(tr('netThTx'))+'</th></tr>'+net.map(n=>`<tr><td>${esc(n.name)}</td><td>${esc(n.rxPerSec)}</td><td>${esc(n.txPerSec)}</td></tr>`).join('');
    const thSub=document.getElementById('thermalSub');
    const thTbl=document.getElementById('thermal');
    if(thSub){
      thSub.textContent=th.length
        ? trf('thermalSubOnFmt',th.length,lhmCount?trf('thermalSubLhmFmt',lhmCount):'')
        : tr('thermalSubOff');
    }
    if(thTbl){
      thTbl.innerHTML='<tr><th>'+esc(tr('thermalThSrc'))+'</th><th>'+esc(tr('thermalThName'))+'</th><th>'+esc(tr('thermalThC'))+'</th></tr>'+
        (th.length?th.slice(0,64).map(t=>{
          const c=tempC(t);
          const cls=c!=null&&c>=90?'crit':c!=null&&c>=80?'warn':'';
          return `<tr><td class="mono">${esc(tempSrc(t))}</td><td>${esc(tempName(t))}</td><td class="mono ${cls}">${c==null?'-':c.toFixed(1)}</td></tr>`;
        }).join(''):'<tr><td colspan="3" class="sub" style="padding:12px">—</td></tr>');
    }

    const mc=d.memoryCounters||{};
    document.getElementById('memct').innerHTML='<tr><th>'+esc(tr('memHdrMetric'))+'</th><th>'+esc(tr('memHdrValue'))+'</th></tr>'+
      '<tr><td>'+esc(tr('memNonPaged'))+'</td><td>'+esc(String(mc.nonPagedPoolMiB??'-'))+' MiB</td></tr>'+
      '<tr><td>'+esc(tr('memAvail'))+'</td><td>'+esc(String(mc.availableBytesMiB??'-'))+' MiB</td></tr>'+
      '<tr><td>'+esc(tr('memCache'))+'</td><td>'+esc(String(mc.cacheResidentMiB??'-'))+' MiB</td></tr>'+
      '<tr><td>'+esc(tr('memStandby'))+'</td><td>'+esc(String(mc.standbyListMiB??'-'))+' MiB</td></tr>'+
      '<tr><td>'+esc(tr('memCompressed'))+'</td><td>'+esc(String(mc.compressedMiB??'-'))+' MiB</td></tr>';

    const sv=d.services||[];
    document.getElementById('svc').innerHTML='<tr><th>'+esc(tr('svcThName'))+'</th><th>'+esc(tr('svcThSt'))+'</th></tr>'+sv.slice(0,24).map(s=>`<tr><td>${esc(s.serviceName)}</td><td>${esc(s.status)}</td></tr>`).join('');
    const ev=(d.events||[]).filter(e=>sevMatch(e.level));
    document.getElementById('ev').innerHTML='<tr><th>'+esc(tr('evThTime'))+'</th><th>'+esc(tr('evThLog'))+'</th><th>'+esc(tr('evThMsg'))+'</th></tr>'+ev.slice(0,12).map(e=>`<tr><td class="mono">${esc(e.time)}</td><td>${esc(e.log)}</td><td>${esc((e.message||'').slice(0,120))}</td></tr>`).join('');
    const tp=d.topCpu||[];
    document.getElementById('pcpu').innerHTML='<tr><th>'+esc(tr('pcpuThProc'))+'</th><th>'+esc(tr('pcpuThCpu'))+'</th><th>'+esc(tr('pcpuThWs'))+'</th></tr>'+tp.slice(0,12).map(p=>`<tr><td>${esc(p.name)} (${p.id})</td><td>${p.cpuPctApprox}</td><td>${p.ws_MB??p.wsMb}</td></tr>`).join('');
    const det=(d.detections||[]).filter(x=>sevMatch(x.severity));
    document.getElementById('det').innerHTML='<tr><th>'+esc(tr('detThSev'))+'</th><th>'+esc(tr('detThRule'))+'</th><th>'+esc(tr('detThWhen'))+'</th><th>'+esc(tr('detThWhy'))+'</th></tr>'+det.slice(0,20).map(x=>`<tr><td>${esc(x.severity)}</td><td>${esc(x.title)}</td><td class="mono">${esc(x.eventTime)}</td><td>${esc(x.matchReason)}</td></tr>`).join('');
    const ph=d.pluginHealth||[];
    document.getElementById('plh').innerHTML='<tr><th>'+esc(tr('plhThPl'))+'</th><th>'+esc(tr('plhThHl'))+'</th><th>'+esc(tr('plhThMsg'))+'</th></tr>'+ph.map(p=>`<tr><td>${esc(p.name)}</td><td>${p.isHealthy?'yes':'no'}</td><td>${esc(p.message)}</td></tr>`).join('');
    const sev=(d.securityEvents||[]).filter(()=>true);
    document.getElementById('sev').innerHTML='<tr><th>'+esc(tr('sevThTime'))+'</th><th>'+esc(tr('sevThId'))+'</th><th>'+esc(tr('sevThImg'))+'</th><th>'+esc(tr('sevThCmd'))+'</th></tr>'+sev.slice(0,20).map(s=>`<tr><td class="mono">${esc(s.time)}</td><td>${s.eventId}</td><td>${esc(s.image||'')}</td><td>${esc((s.commandLine||'').slice(0,90))}</td></tr>`).join('');
  }catch(e){console.error(e)}
}

document.getElementById('applyF').onclick=()=>load();
function wireFilterAutoApply(){
  ['fSevCrit','fSevWarn','fSevInfo'].forEach(id=>{
    const el=document.getElementById(id);
    if(el) el.addEventListener('change',()=>load());
  });
  const sel=document.getElementById('fCode');
  if(sel) sel.addEventListener('change',()=>load());
}
wireFilterAutoApply();
wireChartHover();
let _chartResizeTimer;
window.addEventListener('resize',()=>{
  clearTimeout(_chartResizeTimer);
  _chartResizeTimer=setTimeout(()=>{
    try{
      if(timeMode==='now') redrawChartsFromLive();
      else redrawChartsFromHistory();
    }catch(_){/* */}
  },160);
});
function applyQueryFromUrl(){
  try{
    const p=new URLSearchParams(location.search);
    const pe=p.get('preset');
    if(pe&&['now','15m','1h','24h','7d'].includes(pe)){
      timeMode=pe;
      document.querySelectorAll('.tmb[data-m]').forEach(b=>b.classList.toggle('on',b.dataset.m===timeMode));
      if(timeMode==='now'){
        live.cpu=[];live.mem=[];live.lat=[];live.net=[];live.score=[];
        document.getElementById('rangeLbl').textContent=tr('rangeLive');
      }
      else document.getElementById('rangeLbl').textContent='';
    }
    const tri=p.get('crit'); if(tri==='0'||tri==='1'){const el=document.getElementById('fSevCrit');if(el)el.checked=tri==='1';}
    const tw=p.get('warn'); if(tw==='0'||tw==='1'){const el=document.getElementById('fSevWarn');if(el)el.checked=tw==='1';}
    const ti=p.get('info'); if(ti==='0'||ti==='1'){const el=document.getElementById('fSevInfo');if(el)el.checked=ti==='1';}
    const code=p.get('code');
    if(code!=null&&code!==''){
      const sel=document.getElementById('fCode');
      if(sel){
        let found=false;
        for(let i=0;i<sel.options.length;i++){ if(sel.options[i].value===code){ found=true; sel.value=code; break; } }
        if(!found){ const o=document.createElement('option'); o.value=code; o.textContent=code; sel.appendChild(o); sel.value=code; }
      }
    }
  }catch(e){console.error(e)}
}
setInterval(()=>{ load(); if(timeMode!=='now')refreshHistory(); }, 2500);
(async ()=>{
  await load();
  const hadQs=location.search.length>1;
  applyQueryFromUrl();
  if(hadQs) await load();
  if(timeMode!=='now') await refreshHistory();
})();
</script>
</body>
</html>
""";
}

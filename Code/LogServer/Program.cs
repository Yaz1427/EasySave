using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
if (!Directory.Exists(logDir))
    Directory.CreateDirectory(logDir);

var fileLock = new object();

// ── API: Receive log ──
app.MapPost("/api/logs", async (HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        string body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return Results.BadRequest("Empty body");

        var entry = JsonSerializer.Deserialize<JsonElement>(body);

        var logEntry = new
        {
            MachineName = entry.TryGetProperty("MachineName", out var mn) ? mn.GetString() : "Unknown",
            UserName = entry.TryGetProperty("UserName", out var un) ? un.GetString() : "Unknown",
            Timestamp = entry.TryGetProperty("Timestamp", out var ts) ? ts.GetString() : DateTime.Now.ToString("o"),
            JobName = entry.TryGetProperty("JobName", out var jn) ? jn.GetString() : "",
            SourcePath = entry.TryGetProperty("SourcePath", out var sp) ? sp.GetString() : "",
            TargetPath = entry.TryGetProperty("TargetPath", out var tp) ? tp.GetString() : "",
            FileSize = entry.TryGetProperty("FileSize", out var fs) ? fs.GetInt64() : 0,
            TransferTime = entry.TryGetProperty("TransferTime", out var tt) ? tt.GetInt32() : 0,
            EncryptionTime = entry.TryGetProperty("EncryptionTime", out var et) ? et.GetInt32() : 0
        };

        string fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
        string filePath = Path.Combine(logDir, fileName);

        lock (fileLock)
        {
            List<object> logs;
            if (File.Exists(filePath))
            {
                string existingJson = File.ReadAllText(filePath);
                logs = JsonSerializer.Deserialize<List<object>>(existingJson) ?? new List<object>();
            }
            else
            {
                logs = new List<object>();
            }
            logs.Add(logEntry);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(logs, options);
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, jsonString);
            File.Copy(tempPath, filePath, overwrite: true);
            File.Delete(tempPath);
        }

        return Results.Ok(new { status = "logged" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

// ── API: List log files ──
app.MapGet("/api/logs", () =>
{
    try
    {
        var files = Directory.GetFiles(logDir, "*.json")
            .OrderByDescending(f => f)
            .Select(f => Path.GetFileName(f))
            .ToList();
        return Results.Ok(files);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

// ── API: Get logs for a specific date ──
app.MapGet("/api/logs/{date}", (string date) =>
{
    try
    {
        string filePath = Path.Combine(logDir, $"{date}.json");
        if (!File.Exists(filePath))
            return Results.NotFound($"No logs for {date}");
        string json = File.ReadAllText(filePath);
        return Results.Content(json, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

// ── API: Stats summary ──
app.MapGet("/api/stats", () =>
{
    try
    {
        var files = Directory.GetFiles(logDir, "*.json").OrderByDescending(f => f).ToList();
        int totalFiles = 0;
        long totalSize = 0;
        int totalEntries = 0;
        var machines = new HashSet<string>();
        var users = new HashSet<string>();
        var jobs = new HashSet<string>();

        foreach (var f in files)
        {
            try
            {
                string json = File.ReadAllText(f);
                var entries = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (entries == null) continue;
                totalEntries += entries.Count;
                foreach (var e in entries)
                {
                    if (e.TryGetProperty("FileSize", out var fs)) totalSize += fs.GetInt64();
                    if (e.TryGetProperty("MachineName", out var mn)) machines.Add(mn.GetString() ?? "");
                    if (e.TryGetProperty("UserName", out var un)) users.Add(un.GetString() ?? "");
                    if (e.TryGetProperty("JobName", out var jn)) jobs.Add(jn.GetString() ?? "");
                    totalFiles++;
                }
            }
            catch { }
        }

        return Results.Ok(new
        {
            totalLogDays = files.Count,
            totalEntries,
            totalFilesCopied = totalFiles,
            totalSizeBytes = totalSize,
            uniqueMachines = machines.Count,
            uniqueUsers = users.Count,
            uniqueJobs = jobs.Count,
            machineNames = machines,
            userNames = users
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error: {ex.Message}");
    }
});

// ── Dashboard HTML ──
app.MapGet("/", () => Results.Content(DashboardHtml(), "text/html"));

app.Run();

static string DashboardHtml() => """
<!DOCTYPE html>
<html lang="fr">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>EasySave - Log Server</title>
    <style>
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

        :root {
            --bg: #f4f5f7;
            --surface: #ffffff;
            --surface2: #f0f1f3;
            --border: #d9dce1;
            --accent: #2c5282;
            --accent-light: #3a6ba5;
            --green: #2f855a;
            --orange: #b7791f;
            --red: #c53030;
            --text: #1a202c;
            --text2: #4a5568;
            --text3: #718096;
            --radius: 6px;
        }

        body {
            font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
            background: var(--bg);
            color: var(--text);
            min-height: 100vh;
            font-size: 14px;
        }

        .header {
            background: var(--surface);
            border-bottom: 2px solid var(--accent);
            padding: 16px 32px;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }
        .header-left { display: flex; align-items: center; gap: 14px; }
        .logo {
            width: 36px; height: 36px;
            background: var(--accent);
            border-radius: 6px;
            display: flex; align-items: center; justify-content: center;
            font-size: 15px; font-weight: 700; color: white;
            letter-spacing: -0.5px;
        }
        .header h1 { font-size: 18px; font-weight: 600; color: var(--text); }
        .header-sub { color: var(--text3); font-size: 12px; margin-top: 1px; }
        .status-badge {
            display: flex; align-items: center; gap: 6px;
            padding: 6px 12px; border-radius: 4px; font-size: 12px;
            color: var(--green); background: #f0fff4; border: 1px solid #c6f6d5;
        }
        .status-dot {
            width: 7px; height: 7px; background: var(--green);
            border-radius: 50%;
        }

        .container { max-width: 1360px; margin: 0 auto; padding: 20px 32px; }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 12px;
            margin-bottom: 20px;
        }
        .stat-card {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: var(--radius);
            padding: 16px;
        }
        .stat-label {
            font-size: 11px; text-transform: uppercase; letter-spacing: 0.6px;
            color: var(--text3); margin-bottom: 6px; font-weight: 500;
        }
        .stat-value { font-size: 24px; font-weight: 700; color: var(--text); }
        .stat-sub { font-size: 11px; color: var(--text3); margin-top: 3px; }

        .panel {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: var(--radius);
            margin-bottom: 20px;
            overflow: hidden;
        }
        .panel-header {
            display: flex; align-items: center; justify-content: space-between;
            padding: 12px 16px;
            border-bottom: 1px solid var(--border);
            background: var(--surface2);
        }
        .panel-title { font-size: 14px; font-weight: 600; color: var(--text); }
        .panel-body { padding: 0; }

        .controls { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
        select, button {
            font-family: inherit; font-size: 12px;
            background: var(--surface); color: var(--text);
            border: 1px solid var(--border); border-radius: 4px;
            padding: 6px 10px; cursor: pointer;
        }
        select:focus, button:focus { outline: 2px solid var(--accent); outline-offset: -1px; }
        button.primary {
            background: var(--accent); border-color: var(--accent);
            color: white; font-weight: 500;
        }
        .search-input {
            background: var(--surface); color: var(--text);
            border: 1px solid var(--border); border-radius: 4px;
            padding: 6px 10px; font-size: 12px; font-family: inherit; min-width: 180px;
        }
        .search-input:focus { outline: 2px solid var(--accent); outline-offset: -1px; }
        .search-input::placeholder { color: var(--text3); }

        .log-table { width: 100%; border-collapse: collapse; font-size: 12px; }
        .log-table thead th {
            text-align: left; padding: 10px 14px;
            font-size: 11px; text-transform: uppercase; letter-spacing: 0.4px;
            color: var(--text3); font-weight: 600;
            background: var(--surface2);
            border-bottom: 1px solid var(--border);
            position: sticky; top: 0; cursor: pointer; user-select: none;
        }
        .log-table thead th:hover { color: var(--accent); }
        .log-table tbody tr { border-bottom: 1px solid #edf0f4; }
        .log-table tbody tr:hover { background: #f7f8fa; }
        .log-table td {
            padding: 8px 14px; color: var(--text2);
            white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 260px;
        }
        .log-table td.job { color: var(--accent); font-weight: 600; }
        .log-table td.size { font-family: 'Consolas', 'Courier New', monospace; color: var(--text); font-size: 12px; }
        .log-table td.time { font-family: 'Consolas', 'Courier New', monospace; font-size: 12px; }
        .log-table td.time.fast { color: var(--green); }
        .log-table td.time.slow { color: var(--orange); }
        .log-table td.time.error { color: var(--red); }
        .log-table td.path { font-size: 11px; max-width: 300px; }

        .table-scroll { max-height: 540px; overflow-y: auto; }
        .table-scroll::-webkit-scrollbar { width: 5px; }
        .table-scroll::-webkit-scrollbar-track { background: var(--surface); }
        .table-scroll::-webkit-scrollbar-thumb { background: var(--border); border-radius: 3px; }

        .empty { text-align: center; padding: 48px 20px; color: var(--text3); }
        .empty p { font-size: 14px; }
        .empty .empty-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 6px; }

        .tag {
            display: inline-block; padding: 1px 6px;
            border-radius: 3px; font-size: 11px; font-weight: 500;
        }
        .tag.user { background: #ebf4ff; color: #2b6cb0; }
        .tag.machine { background: #fefcbf; color: #975a16; }

        .footer {
            text-align: center; padding: 16px;
            color: var(--text3); font-size: 11px;
            border-top: 1px solid var(--border); margin-top: 8px;
        }

        @media (max-width: 768px) {
            .header { flex-direction: column; gap: 10px; padding: 14px; }
            .container { padding: 14px; }
            .stats-grid { grid-template-columns: repeat(2, 1fr); }
        }
    </style>
</head>
<body>

<div class="header">
    <div class="header-left">
        <div class="logo">ES</div>
        <div>
            <h1>EasySave Log Server</h1>
            <div class="header-sub">v3.0 &ndash; Centralized daily logs</div>
        </div>
    </div>
    <div class="status-badge">
        <div class="status-dot"></div>
        Online
    </div>
</div>

<div class="container">

    <div class="stats-grid" id="statsGrid">
        <div class="stat-card">
            <div class="stat-label">Log entries</div>
            <div class="stat-value" id="statEntries">&ndash;</div>
        </div>
        <div class="stat-card">
            <div class="stat-label">Files transferred</div>
            <div class="stat-value" id="statFiles">&ndash;</div>
        </div>
        <div class="stat-card">
            <div class="stat-label">Total size</div>
            <div class="stat-value" id="statSize">&ndash;</div>
        </div>
        <div class="stat-card">
            <div class="stat-label">Log days</div>
            <div class="stat-value" id="statDays">&ndash;</div>
        </div>
        <div class="stat-card">
            <div class="stat-label">Machines</div>
            <div class="stat-value" id="statMachines">&ndash;</div>
            <div class="stat-sub" id="statMachineNames"></div>
        </div>
        <div class="stat-card">
            <div class="stat-label">Users</div>
            <div class="stat-value" id="statUsers">&ndash;</div>
            <div class="stat-sub" id="statUserNames"></div>
        </div>
    </div>

    <div class="panel">
        <div class="panel-header">
            <div class="panel-title">Log entries</div>
            <div class="controls">
                <input type="text" class="search-input" id="searchInput" placeholder="Nom d'un document...">
                <select id="dateSelect"></select>
                <button class="primary" onclick="loadLogs()">Refresh</button>
            </div>
        </div>
        <div class="panel-body">
            <div class="table-scroll" id="tableContainer">
                <table class="log-table">
                    <thead>
                        <tr>
                            <th onclick="sortBy('Timestamp')">Timestamp</th>
                            <th onclick="sortBy('MachineName')">Machine</th>
                            <th onclick="sortBy('UserName')">User</th>
                            <th onclick="sortBy('JobName')">Job</th>
                            <th onclick="sortBy('SourcePath')">Source</th>
                            <th onclick="sortBy('TargetPath')">Target</th>
                            <th onclick="sortBy('FileSize')">Size</th>
                            <th onclick="sortBy('TransferTime')">Transfer</th>
                            <th onclick="sortBy('EncryptionTime')">Encrypt</th>
                        </tr>
                    </thead>
                    <tbody id="logBody"></tbody>
                </table>
            </div>
        </div>
    </div>

</div>

<div class="footer">
    EasySave Log Server v3.0 &ndash; CESI &ndash; Module Genie Logiciel
</div>

<script>
let allLogs = [];
let currentSort = { key: 'Timestamp', asc: false };

async function loadStats() {
    try {
        const res = await fetch('/api/stats');
        const s = await res.json();
        document.getElementById('statEntries').textContent = s.totalEntries.toLocaleString();
        document.getElementById('statFiles').textContent = s.totalFilesCopied.toLocaleString();
        document.getElementById('statSize').textContent = formatSize(s.totalSizeBytes);
        document.getElementById('statDays').textContent = s.totalLogDays;
        document.getElementById('statMachines').textContent = s.uniqueMachines;
        document.getElementById('statUsers').textContent = s.uniqueUsers;
        document.getElementById('statMachineNames').textContent = [...s.machineNames].join(', ');
        document.getElementById('statUserNames').textContent = [...s.userNames].join(', ');
    } catch (e) { console.error('Stats error:', e); }
}

async function loadDates() {
    try {
        const res = await fetch('/api/logs');
        const files = await res.json();
        const sel = document.getElementById('dateSelect');
        sel.innerHTML = '';
        if (files.length === 0) {
            sel.innerHTML = '<option>No logs yet</option>';
            return;
        }
        files.forEach(f => {
            const date = f.replace('.json', '');
            const opt = document.createElement('option');
            opt.value = date;
            opt.textContent = date;
            sel.appendChild(opt);
        });
        loadLogs();
    } catch (e) { console.error('Dates error:', e); }
}

async function loadLogs() {
    const date = document.getElementById('dateSelect').value;
    if (!date || date === 'No logs yet') {
        document.getElementById('logBody').innerHTML = '';
        showEmpty();
        return;
    }
    try {
        const res = await fetch(`/api/logs/${date}`);
        if (!res.ok) { showEmpty(); return; }
        allLogs = await res.json();
        renderTable();
    } catch (e) {
        console.error('Logs error:', e);
        showEmpty();
    }
}

function renderTable() {
    const search = document.getElementById('searchInput').value.toLowerCase();
    let logs = allLogs;

    if (search) {
        logs = logs.filter(l =>
            JSON.stringify(l).toLowerCase().includes(search)
        );
    }

    logs.sort((a, b) => {
        let va = a[currentSort.key] ?? '';
        let vb = b[currentSort.key] ?? '';
        if (typeof va === 'number' && typeof vb === 'number')
            return currentSort.asc ? va - vb : vb - va;
        return currentSort.asc
            ? String(va).localeCompare(String(vb))
            : String(vb).localeCompare(String(va));
    });

    const body = document.getElementById('logBody');

    if (logs.length === 0) {
        showEmpty();
        return;
    }

    body.innerHTML = logs.map(l => {
        const tf = transferClass(l.TransferTime);
        const ef = transferClass(l.EncryptionTime);
        return `<tr>
            <td>${formatTime(l.Timestamp)}</td>
            <td><span class="tag machine">${esc(l.MachineName || '-')}</span></td>
            <td><span class="tag user">${esc(l.UserName || '-')}</span></td>
            <td class="job">${esc(l.JobName || '-')}</td>
            <td class="path" title="${esc(l.SourcePath)}">${esc(shortPath(l.SourcePath))}</td>
            <td class="path" title="${esc(l.TargetPath)}">${esc(shortPath(l.TargetPath))}</td>
            <td class="size">${formatSize(l.FileSize || 0)}</td>
            <td class="time ${tf}">${l.TransferTime ?? 0} ms</td>
            <td class="time ${ef}">${l.EncryptionTime ?? 0} ms</td>
        </tr>`;
    }).join('');
}

function showEmpty() {
    document.getElementById('logBody').innerHTML =
        `<tr><td colspan="9"><div class="empty"><div class="empty-label">No data</div><p>No log entries found for this date.</p></div></td></tr>`;
}

function sortBy(key) {
    if (currentSort.key === key) currentSort.asc = !currentSort.asc;
    else { currentSort.key = key; currentSort.asc = true; }
    renderTable();
}

function formatSize(bytes) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return (bytes / Math.pow(k, i)).toFixed(i > 0 ? 1 : 0) + ' ' + sizes[i];
}

function formatTime(ts) {
    if (!ts) return '-';
    try {
        const d = new Date(ts);
        return d.toLocaleString('fr-FR', { hour12: false });
    } catch { return ts; }
}

function shortPath(p) {
    if (!p) return '-';
    const parts = p.replace(/\\\\/g, '/').replace(/\\/g, '/').split('/');
    if (parts.length <= 3) return p;
    return '.../' + parts.slice(-3).join('/');
}

function transferClass(ms) {
    if (ms < 0) return 'error';
    if (ms > 500) return 'slow';
    return 'fast';
}

function esc(s) {
    const d = document.createElement('div');
    d.textContent = s || '';
    return d.innerHTML;
}

document.getElementById('searchInput').addEventListener('input', renderTable);
document.getElementById('dateSelect').addEventListener('change', loadLogs);

setInterval(() => { loadStats(); loadLogs(); }, 10000);

loadStats();
loadDates();
</script>

</body>
</html>
""";


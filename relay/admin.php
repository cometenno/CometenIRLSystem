<?php

declare(strict_types=1);

$secureCookie = !empty($_SERVER['HTTPS']) && strtolower((string)$_SERVER['HTTPS']) !== 'off';
session_name('cometen_irl_admin');
session_set_cookie_params([
    'lifetime' => 0,
    'path' => '/',
    'secure' => $secureCookie,
    'httponly' => true,
    'samesite' => 'Strict',
]);
session_start();

require __DIR__ . '/bootstrap.php';
$config = load_config();
$adminConfig = is_array($config['admin'] ?? null) ? $config['admin'] : [];
$adminUser = trim((string)($adminConfig['username'] ?? 'cometen')) ?: 'cometen';
$adminHash = trim((string)($adminConfig['password_hash'] ?? ''));

function admin_json(int $status, array $payload): never
{
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    http_response_code($status);
    echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function admin_logged_in(): bool
{
    return !empty($_SESSION['cometen_admin_ok']);
}

function admin_require_login(): void
{
    if (!admin_logged_in()) {
        admin_json(401, ['ok' => false, 'error' => 'login_required']);
    }
}

function admin_csrf(): string
{
    if (empty($_SESSION['cometen_admin_csrf'])) {
        $_SESSION['cometen_admin_csrf'] = bin2hex(random_bytes(24));
    }
    return (string)$_SESSION['cometen_admin_csrf'];
}

function admin_require_csrf(): void
{
    $provided = trim((string)($_SERVER['HTTP_X_CSRF_TOKEN'] ?? $_POST['csrf'] ?? ''));
    $expected = admin_csrf();
    if ($provided === '' || !hash_equals($expected, $provided)) {
        admin_json(403, ['ok' => false, 'error' => 'invalid_csrf']);
    }
}

function admin_clean_mac(mixed $value): string
{
    $mac = strtoupper(trim((string)$value));
    if (preg_match('/^(?:[0-9A-F]{2}:){5}[0-9A-F]{2}$/', $mac) !== 1) {
        admin_json(422, ['ok' => false, 'error' => 'invalid_bluetooth_mac']);
    }
    return $mac;
}

function admin_queue_control(array $config, string $message, int $amount = 0): array
{
    $message = clean_text($message, 250);
    if ($message === '') {
        admin_json(422, ['ok' => false, 'error' => 'empty_command']);
    }

    $id = random_event_id();
    $admin = is_array($config['admin'] ?? null) ? $config['admin'] : [];
    $ttl = max(20, min(60, (int)($admin['control_ttl_seconds'] ?? 45)));
    $createdAt = new DateTimeImmutable('now', new DateTimeZone('UTC'));
    $expiresAt = $createdAt->modify('+' . $ttl . ' seconds');

    $sql = <<<'SQL'
INSERT INTO irl_alert_events
    (id, event_type, user_name, amount, message, sound_file, priority, created_at, expires_at)
VALUES
    (:id, 'control', 'web-admin', :amount, :message, 'control.wav', 75, :created_at, :expires_at)
SQL;

    try {
        $statement = database($config)->prepare($sql);
        $statement->execute([
            ':id' => $id,
            ':amount' => max(0, min(100, $amount)),
            ':message' => $message,
            ':created_at' => $createdAt->format('Y-m-d H:i:s.u'),
            ':expires_at' => $expiresAt->format('Y-m-d H:i:s.u'),
        ]);
    } catch (Throwable $exception) {
        error_log('CometenIRL admin queue error: ' . $exception->getMessage());
        admin_json(500, ['ok' => false, 'error' => 'command_not_queued']);
    }

    return [
        'event_id' => $id,
        'expires_at' => $expiresAt->format(DATE_ATOM),
    ];
}

function admin_read_result(array $config, string $id): array
{
    if (preg_match('/^[A-Za-z0-9._:-]{8,100}$/', $id) !== 1) {
        admin_json(422, ['ok' => false, 'error' => 'invalid_event_id']);
    }

    try {
        $statement = database($config)->prepare(
            "SELECT message FROM irl_alert_events WHERE id = :id AND event_type = 'control' LIMIT 1"
        );
        $statement->execute([':id' => $id]);
        $row = $statement->fetch();
    } catch (Throwable $exception) {
        error_log('CometenIRL admin result error: ' . $exception->getMessage());
        admin_json(500, ['ok' => false, 'error' => 'result_read_failed']);
    }

    if (!is_array($row)) {
        return ['ok' => true, 'ready' => false];
    }

    $stored = (string)($row['message'] ?? '');
    if (str_starts_with($stored, 'RESULT_OK:')) {
        return [
            'ok' => true,
            'ready' => true,
            'result_ok' => true,
            'message' => substr($stored, strlen('RESULT_OK:')),
        ];
    }
    if (str_starts_with($stored, 'RESULT_ERR:')) {
        return [
            'ok' => true,
            'ready' => true,
            'result_ok' => false,
            'message' => substr($stored, strlen('RESULT_ERR:')),
        ];
    }
    return ['ok' => true, 'ready' => false];
}

function admin_receiver_status(array $config): array
{
    $offlineSeconds = max(30, min(300, (int)($config['receiver_offline_seconds'] ?? 90)));
    try {
        $statement = database($config)->prepare(
            "SELECT receiver_id, last_seen, version,
                    TIMESTAMPDIFF(MICROSECOND, last_seen, UTC_TIMESTAMP(6)) / 1000000.0 AS age_seconds
             FROM irl_receiver_status
             WHERE receiver_id = 'belabox'
             LIMIT 1"
        );
        $statement->execute();
        $row = $statement->fetch();
    } catch (Throwable $exception) {
        return ['ok' => false, 'online' => false, 'error' => 'receiver_status_failed'];
    }

    if (!is_array($row)) {
        return ['ok' => true, 'online' => false, 'age_seconds' => null];
    }

    $age = max(0.0, (float)$row['age_seconds']);
    return [
        'ok' => true,
        'online' => $age <= $offlineSeconds,
        'age_seconds' => round($age, 1),
        'last_seen_utc' => str_replace(' ', 'T', (string)$row['last_seen']) . 'Z',
        'version' => (string)$row['version'],
    ];
}

// Login/logout are normal form posts; all device actions use the authenticated
// JSON API below.
if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && isset($_POST['login'])) {
    if ($adminHash === '') {
        $_SESSION['login_error'] = 'Admin er ikke konfigurert i config.php.';
    } else {
        $user = trim((string)($_POST['username'] ?? ''));
        $password = (string)($_POST['password'] ?? '');
        if (hash_equals($adminUser, $user) && password_verify($password, $adminHash)) {
            session_regenerate_id(true);
            $_SESSION['cometen_admin_ok'] = true;
            $_SESSION['cometen_admin_csrf'] = bin2hex(random_bytes(24));
            header('Location: admin.php');
            exit;
        }
        usleep(350000);
        $_SESSION['login_error'] = 'Feil brukernavn eller passord.';
    }
    header('Location: admin.php');
    exit;
}

if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && isset($_POST['logout'])) {
    if (admin_logged_in()) {
        admin_require_csrf();
    }
    $_SESSION = [];
    session_destroy();
    header('Location: admin.php');
    exit;
}

$api = trim((string)($_GET['api'] ?? ''));
if ($api !== '') {
    admin_require_login();

    if ($api === 'receiver' && ($_SERVER['REQUEST_METHOD'] ?? '') === 'GET') {
        admin_json(200, admin_receiver_status($config));
    }

    if ($api === 'result' && ($_SERVER['REQUEST_METHOD'] ?? '') === 'GET') {
        $id = trim((string)($_GET['id'] ?? ''));
        admin_json(200, admin_read_result($config, $id));
    }

    if ($api === 'command' && ($_SERVER['REQUEST_METHOD'] ?? '') === 'POST') {
        admin_require_csrf();
        $command = strtolower(trim((string)($_POST['command'] ?? '')));
        $message = '';
        $amount = 0;

        switch ($command) {
            case 'status':
                $message = 'status';
                break;
            case 'browser_get':
                $message = 'admin_browser_audio_get';
                break;
            case 'browser_on':
                $message = 'browser_audio_master_on';
                break;
            case 'browser_off':
                $message = 'browser_audio_master_off';
                break;
            case 'browser_restart':
                $message = 'browser_audio_restart';
                break;
            case 'browser_remove':
                $message = 'browser_audio_remove soundalerts';
                break;
            case 'browser_save':
                $url = trim((string)($_POST['url'] ?? ''));
                if (strlen($url) > 190 || filter_var($url, FILTER_VALIDATE_URL) === false) {
                    admin_json(422, ['ok' => false, 'error' => 'invalid_browser_audio_url']);
                }
                $scheme = strtolower((string)parse_url($url, PHP_URL_SCHEME));
                if (!in_array($scheme, ['http', 'https'], true)) {
                    admin_json(422, ['ok' => false, 'error' => 'invalid_browser_audio_url']);
                }
                $message = 'browser_audio_add soundalerts ' . $url;
                break;
            case 'audio_test':
                $message = 'alert_test';
                break;
            case 'mute':
                $message = 'mute';
                break;
            case 'unmute':
                $message = 'unmute';
                break;
            case 'volume_set':
                $amount = max(0, min(100, (int)($_POST['value'] ?? 0)));
                $message = 'volume_set';
                break;
            case 'bt_status':
                $message = 'bt_status';
                break;
            case 'bt_list':
                $message = 'bt_list';
                break;
            case 'bt_scan':
                $message = 'bt_scan';
                break;
            case 'bt_pair':
            case 'bt_connect':
            case 'bt_disconnect':
            case 'bt_remove':
            case 'bt_default':
                $mac = admin_clean_mac($_POST['mac'] ?? '');
                $message = $command . ' ' . $mac;
                break;
            default:
                admin_json(422, ['ok' => false, 'error' => 'unsupported_admin_command']);
        }

        $queued = admin_queue_control($config, $message, $amount);
        admin_json(202, ['ok' => true] + $queued);
    }

    admin_json(405, ['ok' => false, 'error' => 'method_not_allowed']);
}

header('Content-Type: text/html; charset=utf-8');
header('Cache-Control: no-store');

$loginError = (string)($_SESSION['login_error'] ?? '');
unset($_SESSION['login_error']);
$csrf = admin_csrf();
?>
<!doctype html>
<html lang="no">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Cometen IRL - BELABOX Admin</title>
<style>
:root{color-scheme:dark;--bg:#0f1216;--card:#191f26;--line:#303844;--text:#edf2f7;--muted:#9eabb8;--accent:#7c4dff;--ok:#24b35a;--bad:#e34949;--warn:#d89b2b}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font-family:system-ui,-apple-system,Segoe UI,sans-serif}.wrap{max-width:1080px;margin:auto;padding:20px}.top{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:18px}h1{font-size:1.35rem;margin:0}.badge{padding:6px 10px;border-radius:999px;background:#333;color:var(--muted);font-size:.85rem}.badge.ok{background:#123d24;color:#8af0ad}.badge.bad{background:#4a1b1b;color:#ffaaaa}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(310px,1fr));gap:14px}.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px}.card h2{font-size:1rem;margin:0 0 12px}.row{display:flex;gap:8px;flex-wrap:wrap;align-items:center}.stack{display:grid;gap:9px}input,button{font:inherit;border-radius:8px;border:1px solid var(--line)}input{background:#0e1318;color:var(--text);padding:10px;width:100%}button{background:#28313b;color:var(--text);padding:9px 12px;cursor:pointer}button:hover{border-color:#657181}button.primary{background:var(--accent);border-color:var(--accent)}button.good{background:#176d36}button.danger{background:#7c2525}.result{margin-top:10px;padding:10px;background:#0e1318;border-radius:8px;color:#cbd5df;white-space:pre-wrap;word-break:break-word;min-height:42px}.muted{color:var(--muted);font-size:.87rem}.device{border:1px solid var(--line);border-radius:9px;padding:10px;margin-top:8px}.device strong{display:block}.login{max-width:420px;margin:10vh auto}.error{background:#511f1f;color:#ffc0c0;padding:10px;border-radius:8px;margin-bottom:10px}.setup{background:#4d3c14;color:#ffe6a1;padding:12px;border-radius:8px}.spinner{opacity:.65}.footer{margin:18px 0;color:var(--muted);font-size:.8rem}@media(max-width:550px){.wrap{padding:12px}.top{align-items:flex-start}.top form{margin-left:auto}}
</style>
</head>
<body>
<div class="wrap">
<?php if (!admin_logged_in()): ?>
    <div class="login card">
        <h1>Cometen IRL - BELABOX Admin</h1>
        <p class="muted">Logg inn for å administrere BELABOX.</p>
        <?php if ($adminHash === ''): ?>
            <div class="setup">Admin-passord er ikke satt i <code>relay/config.php</code>. Legg inn <code>admin.password_hash</code> før panelet kan brukes.</div>
        <?php endif; ?>
        <?php if ($loginError !== ''): ?><div class="error"><?=htmlspecialchars($loginError, ENT_QUOTES, 'UTF-8')?></div><?php endif; ?>
        <form method="post" class="stack">
            <input type="hidden" name="login" value="1">
            <label>Brukernavn<input name="username" autocomplete="username" value="<?=htmlspecialchars($adminUser, ENT_QUOTES, 'UTF-8')?>"></label>
            <label>Passord<input name="password" type="password" autocomplete="current-password"></label>
            <button class="primary" type="submit" <?=$adminHash === '' ? 'disabled' : ''?>>Logg inn</button>
        </form>
    </div>
<?php else: ?>
    <div class="top">
        <div><h1>Cometen IRL - BELABOX Admin</h1><div class="muted">Remote administrasjon via eksisterende relay</div></div>
        <div class="row"><span id="onlineBadge" class="badge">Sjekker...</span><form method="post"><input type="hidden" name="logout" value="1"><input type="hidden" name="csrf" value="<?=htmlspecialchars($csrf, ENT_QUOTES, 'UTF-8')?>"><button>Logg ut</button></form></div>
    </div>

    <div class="grid">
        <section class="card">
            <h2>Systemstatus</h2>
            <div class="row"><button class="primary" onclick="refreshStatus()">Oppdater status</button></div>
            <div id="systemResult" class="result">Henter...</div>
        </section>

        <section class="card">
            <h2>Browser Audio / Sound Alerts</h2>
            <div class="stack">
                <label class="muted">Browser Audio URL</label>
                <input id="browserUrl" type="url" placeholder="https://...">
                <div class="row">
                    <button class="primary" onclick="saveBrowserUrl()">Lagre URL</button>
                    <button class="good" onclick="simple('browser_on','browserResult',loadBrowser)">På</button>
                    <button onclick="simple('browser_off','browserResult',loadBrowser)">Av</button>
                    <button onclick="simple('browser_restart','browserResult',loadBrowser)">Restart</button>
                    <button class="danger" onclick="removeBrowser()">Fjern</button>
                </div>
            </div>
            <div id="browserResult" class="result">Henter...</div>
        </section>

        <section class="card">
            <h2>Bluetooth</h2>
            <div id="btDefault" class="muted">Henter standardenhet...</div>
            <div class="row" style="margin-top:10px">
                <button class="primary" onclick="loadBtList(false)">Kjente enheter</button>
                <button onclick="loadBtList(true)">Scan 8 sek</button>
            </div>
            <div id="btDevices"></div>
            <div id="btResult" class="result">Klar.</div>
        </section>

        <section class="card">
            <h2>Lydkontroll</h2>
            <div class="stack">
                <label class="muted">Volum 0-100 %</label>
                <div class="row"><input id="volumeValue" type="number" min="0" max="100" value="75" style="width:110px"><button class="primary" onclick="setVolume()">Sett volum</button></div>
                <div class="row"><button onclick="simple('mute','audioResult')">Mute</button><button onclick="simple('unmute','audioResult')">Unmute</button><button onclick="simple('audio_test','audioResult')">Test høyttaler</button></div>
            </div>
            <div id="audioResult" class="result">Klar.</div>
        </section>
    </div>
    <div class="footer">Endringer sendes som allow-listede kontrollkommandoer. Panelet eksponerer ikke relay-token i nettleseren.</div>

<script>
const csrf = <?=json_encode($csrf, JSON_UNESCAPED_SLASHES)?>;
const $ = id => document.getElementById(id);

function esc(s){return String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));}

async function queueCommand(command, extra={}){
    const body = new URLSearchParams({command, ...extra});
    const r = await fetch('admin.php?api=command',{method:'POST',headers:{'X-CSRF-Token':csrf,'Content-Type':'application/x-www-form-urlencoded'},body});
    const j = await r.json();
    if(!r.ok || !j.ok) throw new Error(j.error || 'command_failed');
    return waitResult(j.event_id);
}

async function waitResult(id){
    for(let i=0;i<76;i++){
        await new Promise(r=>setTimeout(r,500));
        const r=await fetch('admin.php?api=result&id='+encodeURIComponent(id),{cache:'no-store'});
        const j=await r.json();
        if(j.ready){
            if(!j.result_ok) throw new Error(j.message || 'BELABOX command failed');
            return j.message || 'OK';
        }
    }
    throw new Error('Timeout - BELABOX svarte ikke');
}

async function simple(command,resultId,after){
    const box=$(resultId); box.textContent='Jobber...'; box.classList.add('spinner');
    try{const msg=await queueCommand(command);box.textContent=msg;if(after)await after();}
    catch(e){box.textContent='FEIL: '+e.message;}
    finally{box.classList.remove('spinner');}
}

async function refreshReceiver(){
    try{
        const r=await fetch('admin.php?api=receiver',{cache:'no-store'}); const j=await r.json();
        const b=$('onlineBadge');
        if(j.online){b.textContent='BELABOX ONLINE';b.className='badge ok';}
        else{b.textContent='BELABOX OFFLINE';b.className='badge bad';}
    }catch(e){$('onlineBadge').textContent='STATUS ?';}
}

async function refreshStatus(){await simple('status','systemResult');}

async function loadBrowser(){
    const box=$('browserResult');
    try{
        const msg=await queueCommand('browser_get');
        const parts=msg.split('|');
        if(parts[0]==='BROWSER'){
            const enabled=parts[1]==='1';
            const url=parts.slice(2).join('|');
            $('browserUrl').value=url;
            box.textContent=(enabled?'Browser Audio PÅ':'Browser Audio AV')+(url?' | '+url:' | ingen URL');
        }else box.textContent=msg;
    }catch(e){box.textContent='FEIL: '+e.message;}
}

async function saveBrowserUrl(){
    const url=$('browserUrl').value.trim();
    if(!/^https?:\/\//i.test(url)){ $('browserResult').textContent='FEIL: URL må starte med http:// eller https://'; return; }
    const box=$('browserResult');box.textContent='Lagrer...';
    try{box.textContent=await queueCommand('browser_save',{url});await loadBrowser();}
    catch(e){box.textContent='FEIL: '+e.message;}
}

async function removeBrowser(){
    if(!confirm('Fjerne Sound Alerts Browser Audio-kilden?')) return;
    await simple('browser_remove','browserResult',loadBrowser);
}

function parseBtList(msg){
    if(!msg.startsWith('BTLIST|')) return [];
    const raw=msg.substring(7); if(!raw) return [];
    return raw.split(';').map(row=>{
        const p=row.split('~'); return {mac:p[0]||'',name:p[1]||p[0],paired:p[2]==='1',connected:p[3]==='1'};
    }).filter(d=>d.mac);
}

async function loadBtStatus(){
    try{
        const msg=await queueCommand('bt_status');
        const raw=msg.startsWith('BTSTATUS|')?msg.substring(9):'';
        const p=raw.split('~');
        if(p[0]) $('btDefault').textContent='Standard: '+(p[1]||p[0])+' - '+p[0]+' - '+(p[2]==='1'?'tilkoblet':'frakoblet');
        else $('btDefault').textContent='Ingen standard Bluetooth-enhet valgt.';
    }catch(e){$('btDefault').textContent='BT status feil: '+e.message;}
}

function renderBt(devices){
    const root=$('btDevices');
    if(!devices.length){root.innerHTML='<div class="muted" style="margin-top:10px">Ingen enheter funnet.</div>';return;}
    root.innerHTML=devices.map(d=>`<div class="device"><strong>${esc(d.name)}</strong><div class="muted">${esc(d.mac)} | ${d.paired?'paired':'ikke paired'} | ${d.connected?'tilkoblet':'frakoblet'}</div><div class="row" style="margin-top:8px">${!d.paired?`<button onclick="btAction('bt_pair','${d.mac}')">Pair</button>`:''}${d.paired&&!d.connected?`<button class="good" onclick="btAction('bt_connect','${d.mac}')">Connect</button>`:''}${d.connected?`<button onclick="btAction('bt_disconnect','${d.mac}')">Disconnect</button>`:''}${d.paired?`<button class="primary" onclick="btAction('bt_default','${d.mac}')">Sett standard</button><button class="danger" onclick="btRemove('${d.mac}')">Remove</button>`:''}</div></div>`).join('');
}

async function loadBtList(scan){
    const box=$('btResult');box.textContent=scan?'Scanner i 8 sekunder...':'Henter...';
    try{const msg=await queueCommand(scan?'bt_scan':'bt_list');renderBt(parseBtList(msg));box.textContent=msg;await loadBtStatus();}
    catch(e){box.textContent='FEIL: '+e.message;}
}

async function btAction(command,mac){
    const box=$('btResult');box.textContent='Jobber...';
    try{box.textContent=await queueCommand(command,{mac});await loadBtList(false);await loadBtStatus();}
    catch(e){box.textContent='FEIL: '+e.message;}
}

async function btRemove(mac){if(confirm('Fjerne Bluetooth-enheten '+mac+'?')) await btAction('bt_remove',mac);}

async function setVolume(){
    let value=parseInt($('volumeValue').value,10); if(Number.isNaN(value)) value=75; value=Math.max(0,Math.min(100,value));
    await simpleWith('volume_set',{value},'audioResult');
}
async function simpleWith(command,extra,resultId){
    const box=$(resultId);box.textContent='Jobber...';
    try{box.textContent=await queueCommand(command,extra);}catch(e){box.textContent='FEIL: '+e.message;}
}

refreshReceiver();refreshStatus();loadBrowser();loadBtStatus();loadBtList(false);
setInterval(refreshReceiver,30000);
</script>
<?php endif; ?>
</div>
</body>
</html>

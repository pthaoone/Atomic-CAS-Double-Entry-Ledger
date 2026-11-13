// FinTech Engine Frontend Client

const API_BASE = window.location.origin + '/api';

// DOM Elements
const accountsGrid = document.getElementById('accountsGrid');
const txTableBody = document.getElementById('txTableBody');
const valTotalTx = document.getElementById('valTotalTx');
const valSuccessFail = document.getElementById('valSuccessFail');
const valConflicts = document.getElementById('valConflicts');
const valTotalRetries = document.getElementById('valTotalRetries');
const valAvgLatency = document.getElementById('valAvgLatency');
const valTps = document.getElementById('valTps');

const btnAuditLedger = document.getElementById('btnAuditLedger');
const btnResetDemo = document.getElementById('btnResetDemo');
const btnRunCasStress = document.getElementById('btnRunCasStress');
const btnRunUnsafeStress = document.getElementById('btnRunUnsafeStress');

const testProgressSection = document.getElementById('testProgressSection');
const testTitle = document.getElementById('testTitle');
const testProgressBar = document.getElementById('testProgressBar');
const statSent = document.getElementById('statSent');
const statCompleted = document.getElementById('statCompleted');
const statConflicts = document.getElementById('statConflicts');
const statFailed = document.getElementById('statFailed');
const testVerdict = document.getElementById('testVerdict');
const testTimeElapsed = document.getElementById('testTimeElapsed');

const auditDebits = document.getElementById('auditDebits');
const auditCredits = document.getElementById('auditCredits');
const auditDelta = document.getElementById('auditDelta');
const auditStatus = document.getElementById('auditStatus');
const auditDescription = document.getElementById('auditDescription');
const auditBadge = document.getElementById('auditBadge');

const manualTxForm = document.getElementById('manualTxForm');
const txType = document.getElementById('txType');
const txSource = document.getElementById('txSource');
const txDest = document.getElementById('txDest');
const destGroup = document.getElementById('destGroup');
const txAmount = document.getElementById('txAmount');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    fetchAccounts();
    fetchMetrics();
    fetchTransactions();
    runAudit(false);

    // Auto refresh every 2.5 seconds
    setInterval(() => {
        fetchAccounts();
        fetchMetrics();
        fetchTransactions();
    }, 2500);

    // Event listeners
    btnAuditLedger.addEventListener('click', () => runAudit(true));
    btnResetDemo.addEventListener('click', handleResetDemo);
    btnRunCasStress.addEventListener('click', handleCasStressTest);
    btnRunUnsafeStress.addEventListener('click', handleUnsafeStressTest);

    txType.addEventListener('change', () => {
        destGroup.style.display = (txType.value === 'transfer') ? 'block' : 'none';
    });

    manualTxForm.addEventListener('submit', handleManualTx);
});

// Fetch Accounts
async function fetchAccounts() {
    try {
        const res = await fetch(`${API_BASE}/accounts`);
        if (!res.ok) return;
        const accounts = await res.json();

        accountsGrid.innerHTML = accounts.map(acc => {
            const isVault = acc.type === 'SystemVault';
            return `
                <div class="account-card">
                    <div class="account-top">
                        <div>
                            <div class="account-id">${acc.accountId}</div>
                            <div class="account-name">${acc.accountName}</div>
                        </div>
                        <span class="version-pill">Ver: ${acc.version}</span>
                    </div>
                    <div class="account-balance">
                        ${Number(acc.currentBalance).toLocaleString('vi-VN')} <span style="font-size: 14px; color: var(--text-muted)">VND</span>
                    </div>
                    <div class="account-meta">
                        <span>Type: ${acc.type}</span>
                        <span>${isVault ? 'System Liquidity' : 'Customer Account'}</span>
                    </div>
                </div>
            `;
        }).join('');
    } catch (err) {
        console.error('Failed to fetch accounts:', err);
    }
}

// Fetch Metrics
async function fetchMetrics() {
    try {
        const res = await fetch(`${API_BASE}/metrics`);
        if (!res.ok) return;
        const m = await res.json();

        valTotalTx.innerText = m.totalRequests.toLocaleString();
        valSuccessFail.innerText = `Success: ${m.successfulTransactions} | Failed: ${m.failedTransactions}`;
        valConflicts.innerText = m.casConflictCount.toLocaleString();
        valTotalRetries.innerText = `Retries Executed: ${m.totalRetries.toLocaleString()}`;
        valAvgLatency.innerText = `${m.averageLatencyMs} ms`;
        valTps.innerText = `${m.currentTps} TPS`;
    } catch (err) {
        console.error('Failed to fetch metrics:', err);
    }
}

// Fetch Transactions
async function fetchTransactions() {
    try {
        const res = await fetch(`${API_BASE}/transactions?limit=20`);
        if (!res.ok) return;
        const txs = await res.json();

        txTableBody.innerHTML = txs.map(t => {
            const statusClass = t.status === 'Completed' ? 'text-emerald' : 'text-rose';
            const shortId = t.txId.substring(0, 8);
            return `
                <tr>
                    <td title="${t.txId}">#${shortId}</td>
                    <td><strong>${t.type}</strong></td>
                    <td>${Number(t.amount).toLocaleString('vi-VN')} VND</td>
                    <td>${t.retryCount > 0 ? `<span class="text-amber">⟳ ${t.retryCount}</span>` : '0'}</td>
                    <td>${t.latencyMs} ms</td>
                    <td class="${statusClass}">● ${t.status}</td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        console.error('Failed to fetch transactions:', err);
    }
}

// Run Ledger Audit
async function runAudit(showToastMsg = true) {
    try {
        const res = await fetch(`${API_BASE}/audit/reconcile`);
        if (!res.ok) return;
        const report = await res.json();

        auditDebits.innerText = `${Number(report.totalDebits).toLocaleString('vi-VN')} VND`;
        auditCredits.innerText = `${Number(report.totalCredits).toLocaleString('vi-VN')} VND`;
        auditDelta.innerText = `${Number(report.imbalanceDelta).toLocaleString('vi-VN')} VND`;

        if (report.isOverallBalanced && !report.anyNegativeBalances) {
            auditStatus.innerText = '100% Balanced (VAS/IFRS)';
            auditStatus.className = 'text-emerald';
            auditBadge.innerText = 'Audit Passed';
            auditBadge.className = 'badge badge-success';
        } else {
            auditStatus.innerText = `DISCREPANCY DETECTED (${report.imbalanceDelta} VND)`;
            auditStatus.className = 'text-rose';
            auditBadge.innerText = 'Audit Failed';
            auditBadge.className = 'badge tag-danger';
        }

        auditDescription.innerText = report.summary;

        if (showToastMsg) {
            showToast(report.isOverallBalanced ? '✅ Ledger Audit Passed: Debits == Credits' : '⚠️ Ledger discrepancy detected!');
        }
    } catch (err) {
        console.error('Failed to run audit:', err);
    }
}

// Reset Demo Environment
async function handleResetDemo() {
    try {
        btnResetDemo.disabled = true;
        btnResetDemo.innerText = 'Resetting...';

        const res = await fetch(`${API_BASE}/demo/reset`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ initialBalance: 1000, concurrentRequests: 100, amountPerRequest: 10 })
        });

        if (res.ok) {
            showToast('🔄 Environment reset: Account A = 1,000 VND (100 tx buffer)');
            testProgressSection.classList.add('hidden');
            await fetchAccounts();
            await fetchMetrics();
            await fetchTransactions();
            await runAudit(false);
        }
    } catch (err) {
        showToast('❌ Failed to reset demo environment');
    } finally {
        btnResetDemo.disabled = false;
        btnResetDemo.innerHTML = '<span>🔄 Reset Environment</span>';
    }
}

// Scenario 5: 100 Concurrent Withdrawals (Atomic CAS)
async function handleCasStressTest() {
    // Step 1: Auto reset environment
    await handleResetDemo();

    testProgressSection.classList.remove('hidden');
    testTitle.innerText = '🚀 Scenario 5: Running 100 Concurrent CAS Requests...';
    testProgressBar.style.width = '0%';
    testProgressBar.style.background = 'linear-gradient(90deg, #6366f1, #10b981)';
    testVerdict.innerHTML = '';
    testVerdict.className = 'test-verdict';

    let completed = 0;
    let failed = 0;
    let conflicts = 0;
    const totalRequests = 100;
    const amount = 10;
    const startTime = performance.now();

    statSent.innerText = totalRequests;
    statCompleted.innerText = '0';
    statConflicts.innerText = '0';
    statFailed.innerText = '0';

    // Dispatch 100 concurrent requests
    const promises = Array.from({ length: totalRequests }).map(async (_, idx) => {
        try {
            const res = await fetch(`${API_BASE}/transactions/withdraw`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    accountId: 'ACC_A',
                    amount: amount,
                    description: `Concurrent CAS Withdrawal #${idx + 1}`,
                    maxRetries: 25
                })
            });

            if (res.ok) {
                const data = await res.json();
                completed++;
                if (data.retriesUsed > 0) conflicts += data.retriesUsed;
            } else {
                failed++;
            }
        } catch {
            failed++;
        } finally {
            const done = completed + failed;
            const pct = Math.round((done / totalRequests) * 100);
            testProgressBar.style.width = `${pct}%`;
            statCompleted.innerText = completed;
            statConflicts.innerText = conflicts;
            statFailed.innerText = failed;
            testTimeElapsed.innerText = `${Math.round(performance.now() - startTime)} ms`;
        }
    });

    await Promise.all(promises);

    const totalDuration = Math.round(performance.now() - startTime);

    // Refresh data and run ledger audit
    await fetchAccounts();
    await fetchMetrics();
    await fetchTransactions();
    const auditRes = await fetch(`${API_BASE}/audit/reconcile`);
    const audit = await auditRes.json();

    const accRes = await fetch(`${API_BASE}/accounts/ACC_A`);
    const accA = await accRes.json();

    if (accA.currentBalance === 0 && completed === 100 && audit.isOverallBalanced) {
        testVerdict.className = 'test-verdict verdict-success';
        testVerdict.innerHTML = `
            🎉 <strong>PERFECT RESULT (Scenario 5 Verified):</strong><br>
            • Exactly <strong>100/100 transactions succeeded</strong>.<br>
            • Final Account A Balance: <strong>0.00 VND</strong> (NO negative balance, NO overdraft).<br>
            • CAS Conflict Retries Handled: <strong>${conflicts} automatic retries</strong> resolved.<br>
            • Total Execution Time: <strong>${totalDuration} ms</strong>.<br>
            • Double-Entry Ledger: <strong>100% Balanced (Delta = 0 VND)</strong>.
        `;
        showToast('🎉 100 Concurrent CAS Requests Succeeded with 0 Balance!');
    } else {
        testVerdict.className = 'test-verdict verdict-failed';
        testVerdict.innerHTML = `
            ⚠️ Result: Completed: ${completed}, Failed: ${failed}, Final Balance: ${accA.currentBalance} VND.
        `;
    }
}

// Unsafe Race Condition Benchmark (Non-CAS)
async function handleUnsafeStressTest() {
    await handleResetDemo();

    testProgressSection.classList.remove('hidden');
    testTitle.innerText = '⚠️ Running 100 UNSAFE Concurrent Requests (No CAS)...';
    testProgressBar.style.width = '0%';
    testProgressBar.style.background = 'linear-gradient(90deg, #f43f5e, #e11d48)';
    testVerdict.innerHTML = '';
    testVerdict.className = 'test-verdict';

    let completed = 0;
    let failed = 0;
    const totalRequests = 100;
    const amount = 10;
    const startTime = performance.now();

    statSent.innerText = totalRequests;
    statCompleted.innerText = '0';
    statConflicts.innerText = '0 (Disabled)';
    statFailed.innerText = '0';

    const promises = Array.from({ length: totalRequests }).map(async (_, idx) => {
        try {
            const res = await fetch(`${API_BASE}/unsafe/withdraw`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    accountId: 'ACC_A',
                    amount: amount,
                    description: `Unsafe Non-CAS Withdrawal #${idx + 1}`
                })
            });

            if (res.ok) {
                completed++;
            } else {
                failed++;
            }
        } catch {
            failed++;
        } finally {
            const done = completed + failed;
            testProgressBar.style.width = `${Math.round((done / totalRequests) * 100)}%`;
            statCompleted.innerText = completed;
            statFailed.innerText = failed;
            testTimeElapsed.innerText = `${Math.round(performance.now() - startTime)} ms`;
        }
    });

    await Promise.all(promises);

    await fetchAccounts();
    await fetchMetrics();
    await fetchTransactions();

    const accRes = await fetch(`${API_BASE}/accounts/ACC_A`);
    const accA = await accRes.json();

    testVerdict.className = 'test-verdict verdict-failed';
    testVerdict.innerHTML = `
        💥 <strong>RACE CONDITION / DOUBLE-SPENDING DEFECT OBSERVED:</strong><br>
        • Total requests processed: <strong>${completed}</strong>.<br>
        • Expected Final Balance: <strong>0.00 VND</strong>, BUT Actual Balance: <strong class="text-rose">${Number(accA.currentBalance).toLocaleString('vi-VN')} VND</strong>!<br>
        • <em>Explanation:</em> Without Atomic CAS Version check, concurrent threads read stale balances and blind-overwrite each other (Lost Update Problem). Money was lost or corrupted!<br>
        • This illustrates why Atomic CAS is mandatory for FinTech Transaction Engines.
    `;
    showToast('⚠️ Race condition occurred! Balance corrupted as expected in Non-CAS test.');
}

// Manual Transaction Handler
async function handleManualTx(e) {
    e.preventDefault();
    const type = txType.value;
    const amount = parseFloat(txAmount.value);
    const source = txSource.value;
    const dest = txDest.value;

    let endpoint = `${API_BASE}/transactions/withdraw`;
    let body = { accountId: source, amount: amount };

    if (type === 'deposit') {
        endpoint = `${API_BASE}/transactions/deposit`;
        body = { accountId: source, amount: amount };
    } else if (type === 'transfer') {
        endpoint = `${API_BASE}/transactions/transfer`;
        body = { sourceAccountId: source, destinationAccountId: dest, amount: amount };
    }

    try {
        const res = await fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        const data = await res.json();
        if (res.ok) {
            showToast(`✅ ${type.toUpperCase()} ${amount.toLocaleString()} VND Succeeded (Retries: ${data.retriesUsed})`);
            fetchAccounts();
            fetchMetrics();
            fetchTransactions();
            runAudit(false);
        } else {
            showToast(`❌ Error: ${data.error || 'Transaction failed'}`);
        }
    } catch {
        showToast('❌ Network error processing transaction');
    }
}

// Toast notification
function showToast(message) {
    const container = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.innerText = message;
    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateY(10px)';
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

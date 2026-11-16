(() => {
    "use strict";
    const form = document.getElementById("recovery-retry");
    if (!form) return;
    const progress = document.getElementById("recovery-progress");
    const filter = document.getElementById("recovery-filter");
    let busy = false;
    const selections = () => [...form.querySelectorAll(".payment-selection")];
    const controls = () => [...form.querySelectorAll("button,input"), ...filter.querySelectorAll("button,input")];
    const navigation = [...form.querySelectorAll(".recovery-navigation")];
    const originalDisabled = new Map(controls().map(el => [el, el.disabled]));
    function setBusy(value) {
        busy = value;
        controls().forEach(el => { el.disabled = value || originalDisabled.get(el); });
        navigation.forEach(el => el.setAttribute("aria-disabled", String(value)));
    }
    navigation.forEach(link => link.addEventListener("click", e => { if (busy) e.preventDefault(); }));
    filter.addEventListener("submit", e => { if (busy) e.preventDefault(); });
    document.getElementById("select-all")?.addEventListener("change", e => {
        selections().forEach(box => { if (!box.disabled) box.checked = e.target.checked; });
    });
    window.addEventListener("beforeunload", e => {
        if (busy) { e.preventDefault(); e.returnValue = ""; }
    });
    async function retry(rows) {
        if (busy || !rows.length) return;
        if (!window.confirm("Verify and retry " + rows.length + " existing top-up(s)? Wallets are credited only when payment is confirmed. No new checkout will be created.")) return;
        const token = form.querySelector('[name="__RequestVerificationToken"]').value;
        setBusy(true);
        let done = 0;
        let credited = 0;
        let stopped = false;
        try {
            for (const row of rows) {
                progress.textContent = "Checking " + (done + 1) + " of " + rows.length + ". Keep this page open.";
                const cell = row.querySelector(".payment-result");
                cell.textContent = "Checking original payment…";
                let result;
                try {
                    // One request at a time, with no automatic transport retries.
                    const response = await fetch(form.action, {
                        method: "POST",
                        credentials: "same-origin",
                        headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" },
                        body: new URLSearchParams({ id: row.dataset.paymentId, __RequestVerificationToken: token })
                    });
                    if (!response.ok || response.redirected ||
                        !(response.headers.get("content-type") || "").includes("application/json")) {
                        throw new Error("Payment result unavailable");
                    }
                    result = await response.json();
                    if (typeof result.Success !== "boolean" || typeof result.Message !== "string")
                        throw new Error("Payment result unreadable");
                } catch {
                    cell.textContent = "Result unconfirmed. Refresh and check this payment before retrying.";
                    stopped = true;
                    break;
                }
                cell.textContent = result.Message;
                done++;
                if (result.Success) {
                    credited++;
                    const box = row.querySelector(".payment-selection");
                    if (box) { box.checked = false; originalDisabled.set(box, true); }
                    const button = row.querySelector(".retry-one");
                    if (button) originalDisabled.set(button, true);
                }
                if (result.StopBatch) { stopped = true; break; }
            }
        } finally {
            setBusy(false);
            progress.textContent = (stopped ? "Stopped. " : "Finished. ") + done + " checked; " + credited +
                " confirmed complete. Review each result. Unconfirmed results do not establish that a payment is unpaid.";
        }
    }
    form.addEventListener("submit", e => e.preventDefault());
    form.querySelector("#retry-selected")?.addEventListener("click", () =>
        retry(selections().filter(box => box.checked && !box.disabled).map(box => box.closest("tr"))));
    form.querySelectorAll(".retry-one").forEach(button =>
        button.addEventListener("click", () => retry([button.closest("tr")])));
})();

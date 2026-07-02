/**
 * POS HTTP clients: local Bridge and Pos.Web BFF (ETCS.API proxied server-side).
 */
(function (App) {
    const config = App.config;
    const parseApiResponse = App.helpers.parseApiResponse;

    App.api = {
        BridgeClient: {
            async health() {
                const res = await fetch(config.bridgeBaseUrl + '/health');
                return parseApiResponse(res);
            },
            async purchase(amount, transactionId, itemCount) {
                const terminalIp = document.getElementById('txtTerminalIp')?.value?.trim();
                const res = await fetch(config.bridgeBaseUrl + '/ibonus/purchase', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ terminalIp, amount, transactionId, itemCount })
                });
                return parseApiResponse(res);
            },
            async undo(amount, transactionId, itemCount) {
                const terminalIp = document.getElementById('txtTerminalIp')?.value?.trim();
                const res = await fetch(config.bridgeBaseUrl + '/ibonus/undo', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ terminalIp, amount, transactionId, itemCount })
                });
                return parseApiResponse(res);
            },
            async printReceipt(payload) {
                const res = await fetch(config.bridgeBaseUrl + '/print/receipt', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                return parseApiResponse(res);
            },
            async printUndoReceipt(payload) {
                const res = await fetch(config.bridgeBaseUrl + '/print/undo-receipt', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });
                return parseApiResponse(res);
            }
        },

        PosApiClient: {
            jsonHeaders() {
                return { 'Content-Type': 'application/json' };
            },
            async postPurchaseLines(body) {
                const res = await fetch('/Pos/Api/Purchases/PostLines', {
                    method: 'POST',
                    headers: this.jsonHeaders(),
                    body: JSON.stringify(body)
                });
                return parseApiResponse(res);
            },
            async rollbackSpendLimit(body) {
                const res = await fetch('/Pos/Api/SpendLimit/Rollback', {
                    method: 'POST',
                    headers: this.jsonHeaders(),
                    body: JSON.stringify(body)
                });
                return parseApiResponse(res);
            },
            async cashPurchase(body) {
                const res = await fetch('/Pos/Api/Purchases/Cash', {
                    method: 'POST',
                    headers: this.jsonHeaders(),
                    body: JSON.stringify(body)
                });
                return parseApiResponse(res);
            },
            async undoCashPurchase(body) {
                const res = await fetch('/Pos/Api/Purchases/Cash/Undo', {
                    method: 'POST',
                    headers: this.jsonHeaders(),
                    body: JSON.stringify(body)
                });
                return parseApiResponse(res);
            },
            async cardPurchase(body) {
                const res = await fetch('/Pos/Api/Purchases/Card', {
                    method: 'POST',
                    headers: this.jsonHeaders(),
                    body: JSON.stringify(body)
                });
                return parseApiResponse(res);
            },
            async spendInfo(customerId) {
                const res = await fetch('/Pos/Api/Students/' + encodeURIComponent(customerId) + '/SpendInfo');
                return parseApiResponse(res);
            }
        }
    };
})(window.PosApp);
